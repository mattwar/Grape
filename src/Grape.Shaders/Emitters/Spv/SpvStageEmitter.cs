namespace Grape.Shaders.Emitters.Spv;

/// <summary>
/// Lowers one bound <see cref="ShaderStage"/> into a SPIR-V module via an
/// <see cref="SpvStageBuilder"/>. Single tree-walk producing SSA result IDs.
///
/// Storage class mapping:
///   VertexInput / StageInput / Builtin(StageInput) → Input
///   StageOutput / Builtin(StageOutput)             → Output
///   Uniform (non-opaque)                           → Uniform (wrapped in Block struct)
///   Uniform (opaque) / Texture / Sampler           → UniformConstant
///   PushConstant                                   → PushConstant (wrapped in Block struct)
///   DeclareLocal                                   → Function (hoisted to entry block)
/// </summary>
internal sealed class SpvStageEmitter
{
    private readonly SpvStageBuilder _b = new();
    private readonly Dictionary<ShaderType, uint> _types = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ShaderGlobal, GlobalMapping> _globals = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ParameterExpression, LocalMapping> _locals = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ShaderFunction, uint> _functions = new(ReferenceEqualityComparer.Instance);

    private uint _entryFnId;
    private uint _entryBlockId;

    private readonly struct GlobalMapping(uint pointerId, uint pointerTypeId, ShaderType pointee, uint? structMemberIndex)
    {
        /// <summary>The OpVariable's result ID.</summary>
        public uint PointerId => pointerId;
        /// <summary>OpTypePointer ID matching <see cref="PointerId"/>'s storage class and pointee.</summary>
        public uint PointerTypeId => pointerTypeId;
        /// <summary>Pointee shader type.</summary>
        public ShaderType Pointee => pointee;
        /// <summary>If non-null, the global is wrapped in a struct (UBO/PushConstant) and is at this member index.</summary>
        public uint? StructMemberIndex => structMemberIndex;
    }

    private readonly struct LocalMapping(uint pointerId, uint pointerTypeId)
    {
        public uint PointerId => pointerId;
        public uint PointerTypeId => pointerTypeId;
    }

    public byte[] Emit(ShaderStage stage)
    {
        if (stage.IsUnbound)
            throw new InvalidOperationException("Stage is unbound.");

        // ---- Module preamble ----
        _b.AddCapability(SpvCapability.Shader);
        _b.SetMemoryModel(SpvAddressingModel.Logical, SpvMemoryModel.GLSL450);
        // Force GLSL.std.450 import early so its ID is small/stable.
        _b.GlslExtSet();

        // ---- Module-scope variables for every global ----
        foreach (var g in stage.Globals) DeclareGlobal(g);

        // ---- User functions ----
        foreach (var f in stage.Functions) DeclareUserFunction(f);
        foreach (var f in stage.Functions) DefineUserFunction(f);

        // ---- Entry point ----
        var voidId = _b.TypeVoid();
        var fnTypeId = _b.TypeFunction(voidId, ReadOnlySpan<uint>.Empty);
        _entryFnId = _b.BeginFunction(voidId, fnTypeId);
        _entryBlockId = _b.Label();
        EmitStatement(stage.EntryBody);
        _b.Return();
        _b.EndFunction();

        var execModel = stage.Stage switch
        {
            ShaderStageKind.Vertex   => SpvExecutionModel.Vertex,
            ShaderStageKind.Fragment => SpvExecutionModel.Fragment,
            ShaderStageKind.Compute  => SpvExecutionModel.GLCompute,
            _ => throw new InvalidOperationException($"Unknown stage {stage.Stage}."),
        };

        // Interface IDs: every Input/Output module-scope variable used by the entry point.
        var interfaceIds = new List<uint>();
        foreach (var g in stage.Globals)
        {
            if (!_globals.TryGetValue(g, out var m)) continue;
            // Only Input/Output variables go in the interface list; Uniform/PushConstant/UniformConstant don't.
            // We can detect by storage class lookup, but cleaner to recompute here from the kind.
            var sc = StorageClassOf(g);
            if (sc is SpvStorageClass.Input or SpvStorageClass.Output)
                interfaceIds.Add(m.PointerId);
        }
        _b.AddEntryPoint(execModel, _entryFnId, "main", interfaceIds);

        if (stage.Stage == ShaderStageKind.Fragment)
            _b.AddExecutionMode(_entryFnId, SpvExecutionMode.OriginUpperLeft);

        return SpvStageWriter.ToBytes(_b);
    }

    // =========================================================================
    // Globals
    // =========================================================================

    private void DeclareGlobal(ShaderGlobal g)
    {
        var sc = StorageClassOf(g);
        switch (g.GlobalKind)
        {
            case ShaderGlobalKind.VertexInput:
            case ShaderGlobalKind.StageInput:
            case ShaderGlobalKind.StageOutput:
            {
                if (g.Builtin != ShaderBuiltin.None) goto case ShaderGlobalKind.Builtin;
                var pointeeId = TypeOf(g.Type);
                var pointerId = _b.TypePointer(sc, pointeeId);
                var varId = _b.Variable(pointerId, sc);
                _b.AddName(varId, g.Name);
                if (g.Location is int loc) _b.Decorate(varId, SpvDecoration.Location, (uint)loc);
                _globals[g] = new GlobalMapping(varId, pointerId, g.Type, structMemberIndex: null);
                return;
            }

            case ShaderGlobalKind.Builtin:
            {
                var pointeeId = TypeOf(g.Type);
                var pointerId = _b.TypePointer(sc, pointeeId);
                var varId = _b.Variable(pointerId, sc);
                _b.AddName(varId, g.Name);
                _b.Decorate(varId, SpvDecoration.Builtin, (uint)BuiltInOf(g.Builtin));
                _globals[g] = new GlobalMapping(varId, pointerId, g.Type, structMemberIndex: null);
                return;
            }

            case ShaderGlobalKind.Uniform when IsOpaque(g.Type):
            case ShaderGlobalKind.Texture:
            case ShaderGlobalKind.Sampler:
            {
                var pointeeId = TypeOf(g.Type);
                var pointerId = _b.TypePointer(sc, pointeeId);
                var varId = _b.Variable(pointerId, sc);
                _b.AddName(varId, g.Name);
                if (g.BindingSlot is int slot)
                {
                    _b.Decorate(varId, SpvDecoration.Binding, (uint)slot);
                    _b.Decorate(varId, SpvDecoration.DescriptorSet, (uint)(g.BindingSet ?? 0));
                }
                _globals[g] = new GlobalMapping(varId, pointerId, g.Type, structMemberIndex: null);
                return;
            }

            case ShaderGlobalKind.Uniform:
            case ShaderGlobalKind.PushConstant:
            {
                // Wrap in a single-member Block struct. Member 0 is the value.
                var memberId = TypeOf(g.Type);
                var structKey = new BlockStructKey(g);
                var structId = _b.TypeStruct(stackalloc uint[] { memberId }, structKey);
                _b.Decorate(structId, SpvDecoration.Block);
                _b.MemberDecorate(structId, 0, SpvDecoration.Offset, 0);
                if (g.Type is MatrixType m)
                {
                    _b.MemberDecorate(structId, 0, SpvDecoration.ColMajor);
                    _b.MemberDecorate(structId, 0, SpvDecoration.MatrixStride, (uint)(m.Rows * 4));
                }

                var pointerId = _b.TypePointer(sc, structId);
                var varId = _b.Variable(pointerId, sc);
                _b.AddName(varId, g.Name);
                if (g.GlobalKind == ShaderGlobalKind.Uniform && g.BindingSlot is int slot)
                {
                    _b.Decorate(varId, SpvDecoration.Binding, (uint)slot);
                    _b.Decorate(varId, SpvDecoration.DescriptorSet, (uint)(g.BindingSet ?? 0));
                }
                _globals[g] = new GlobalMapping(varId, pointerId, g.Type, structMemberIndex: 0);
                return;
            }

            default:
                throw new InvalidOperationException($"Unknown global kind {g.GlobalKind}.");
        }
    }

    private SpvStorageClass StorageClassOf(ShaderGlobal g) => g.GlobalKind switch
    {
        ShaderGlobalKind.VertexInput => SpvStorageClass.Input,
        ShaderGlobalKind.StageInput  => SpvStorageClass.Input,
        ShaderGlobalKind.StageOutput => SpvStorageClass.Output,
        ShaderGlobalKind.Builtin     => g.Builtin is ShaderBuiltin.Position or ShaderBuiltin.PointSize
            or ShaderBuiltin.FragDepth ? SpvStorageClass.Output : SpvStorageClass.Input,
        ShaderGlobalKind.Texture     => SpvStorageClass.UniformConstant,
        ShaderGlobalKind.Sampler     => SpvStorageClass.UniformConstant,
        ShaderGlobalKind.Uniform     => IsOpaque(g.Type) ? SpvStorageClass.UniformConstant : SpvStorageClass.Uniform,
        ShaderGlobalKind.PushConstant=> SpvStorageClass.PushConstant,
        _ => throw new InvalidOperationException($"Unknown global kind {g.GlobalKind}."),
    };

    private static bool IsOpaque(ShaderType t) => t is SamplerType
        or Texture2DType or Texture3DType or TextureCubeType or Texture2DArrayType;

    private static SpvBuiltIn BuiltInOf(ShaderBuiltin b) => b switch
    {
        ShaderBuiltin.VertexIndex   => SpvBuiltIn.VertexIndex,
        ShaderBuiltin.InstanceIndex => SpvBuiltIn.InstanceIndex,
        ShaderBuiltin.Position      => SpvBuiltIn.Position,
        ShaderBuiltin.PointSize     => SpvBuiltIn.PointSize,
        ShaderBuiltin.FragCoord     => SpvBuiltIn.FragCoord,
        ShaderBuiltin.FrontFacing   => SpvBuiltIn.FrontFacing,
        ShaderBuiltin.FragDepth     => SpvBuiltIn.FragDepth,
        _ => throw new InvalidOperationException($"Unknown built-in {b}."),
    };

    /// <summary>Per-global key for interning a Block struct wrapper. Identity by ShaderGlobal reference.</summary>
    private sealed record BlockStructKey(ShaderGlobal Global);

    // =========================================================================
    // Types
    // =========================================================================

    private uint TypeOf(ShaderType t)
    {
        if (_types.TryGetValue(t, out var id)) return id;

        switch (t)
        {
            case VoidType:   id = _b.TypeVoid(); break;
            case BoolType:   id = _b.TypeBool(); break;
            case IntType:    id = _b.TypeInt32(signed: true); break;
            case UIntType:   id = _b.TypeInt32(signed: false); break;
            case FloatType:  id = _b.TypeFloat32(); break;

            case VectorType v:
                id = _b.TypeVector(TypeOf(v.Component), v.N, t);
                break;

            case MatrixType m:
            {
                var col = _b.TypeVector(TypeOf(m.Component), m.Rows, MatrixColumnKey(m));
                id = _b.TypeMatrix(col, m.Cols, t);
                break;
            }

            case ArrayType a:
            {
                var lenConst = _b.ConstantUInt32((uint)a.Length);
                id = _b.TypeArray(TypeOf(a.Element), lenConst, t);
                break;
            }

            case Texture2DType:
                id = _b.TypeImage(_b.TypeFloat32(), SpvDim.Dim2D, arrayed: false, ms: false, sampled: true,
                    SpvImageFormat.Unknown, t);
                break;
            case Texture3DType:
                id = _b.TypeImage(_b.TypeFloat32(), SpvDim.Dim3D, arrayed: false, ms: false, sampled: true,
                    SpvImageFormat.Unknown, t);
                break;
            case TextureCubeType:
                id = _b.TypeImage(_b.TypeFloat32(), SpvDim.DimCube, arrayed: false, ms: false, sampled: true,
                    SpvImageFormat.Unknown, t);
                break;
            case Texture2DArrayType:
                id = _b.TypeImage(_b.TypeFloat32(), SpvDim.Dim2D, arrayed: true, ms: false, sampled: true,
                    SpvImageFormat.Unknown, t);
                break;

            case SamplerType:
                id = _b.TypeSampler(t);
                break;

            default:
                throw new InvalidOperationException($"Unsupported type {t.GetType().Name}.");
        }

        _types[t] = id;
        return id;
    }

    private static object MatrixColumnKey(MatrixType m) => (m.Component, m.Rows, "col");

    // =========================================================================
    // User functions
    // =========================================================================

    private void DeclareUserFunction(ShaderFunction f)
    {
        // Reserve a function ID. Body emitted in DefineUserFunction once entry-point types are stable.
        _functions[f] = 0;
    }

    private void DefineUserFunction(ShaderFunction f)
    {
        var retId = TypeOf(f.ReturnType);
        Span<uint> paramTypeIds = stackalloc uint[f.Parameters.Length];
        for (int i = 0; i < f.Parameters.Length; i++)
            paramTypeIds[i] = TypeOf(f.Parameters[i].ResultType!);
        var fnTypeId = _b.TypeFunction(retId, paramTypeIds);
        var fnId = _b.BeginFunction(retId, fnTypeId);
        _functions[f] = fnId;

        // OpFunctionParameter for each parameter.
        var paramValueIds = new uint[f.Parameters.Length];
        for (int i = 0; i < f.Parameters.Length; i++)
        {
            var pid = _b.AllocId();
            paramValueIds[i] = pid;
            // Function parameter pseudo-instruction: emit raw via Binary helper-like method.
            // SpvOp.FunctionParameter has form (resultType, resultId).
            // We need a low-level wrapper -- use Unary with an unused operand? Easier: add to current block via a small helper.
            EmitFunctionParameter(paramTypeIds[i], pid);
            _locals[f.Parameters[i]] = new LocalMapping(pid, paramTypeIds[i]); // value, not pointer
        }

        _b.Label();
        EmitStatement(f.Body);
        if (f.ReturnType is VoidType) _b.Return();
        // Non-void: the body should have produced a return; if not, we leave the block unterminated and trust the binder.
        _b.EndFunction();
    }

    private void EmitFunctionParameter(uint typeId, uint id)
    {
        // SpvOp.FunctionParameter: word count 3 (op, type, id). We need a builder hook.
        // Since SpvBuilder only exposes Unary(op, type, a) which has different operand layout,
        // route through a fresh helper: temporarily use Unary(SpvOp.FunctionParameter, typeId, 0) won't work
        // because that allocates a result ID. Use the public Variable-shaped helper? No.
        //
        // Simplest: extend SpvBuilder with a dedicated FunctionParameter primitive. Done inline:
        _b.FunctionParameterRaw(typeId, id);
    }

    // =========================================================================
    // Function-parameter / local handling for ParameterExpression
    // =========================================================================

    private (uint id, bool isPointer) LocalRef(ParameterExpression p)
    {
        if (!_locals.TryGetValue(p, out var m))
            throw new InvalidOperationException($"Unbound parameter '{p.Name}'.");
        // Function parameters are stored in _locals as (id=value, pointerTypeId=value-type, marker via convention?).
        // We distinguish by checking whether PointerTypeId is the type (value param) or a pointer type.
        // Cleaner: add a flag. For now we use a side dictionary.
        return _localKind.TryGetValue(p, out var kind) && kind == LocalKind.Value
            ? (m.PointerId, false)
            : (m.PointerId, true);
    }

    private enum LocalKind { Value, Pointer }
    private readonly Dictionary<ParameterExpression, LocalKind> _localKind = new(ReferenceEqualityComparer.Instance);

    // =========================================================================
    // Statements
    // =========================================================================

    private void EmitStatement(ShaderExpression e)
    {
        switch (e)
        {
            case BlockExpression b:
                foreach (var s in b.Body) EmitStatement(s);
                return;

            case DeclareLocalExpression d:
            {
                var pointeeId = TypeOf(d.Variable.ResultType!);
                var pointerId = _b.TypePointer(SpvStorageClass.Function, pointeeId);
                // Hoist OpVariable to entry block start. SpvBuilder.FunctionVariable always emits at the
                // current insertion point; SPIR-V spec actually requires Function variables to be the first
                // instructions of the entry block. Since our blocks rarely have many vars and we always
                // emit them sequentially before non-trivial control flow, this is fine for the current
                // demo. A future pass can hoist if needed.
                var varId = _b.FunctionVariable(pointerId);
                _b.AddName(varId, d.Variable.Name);
                _locals[d.Variable] = new LocalMapping(varId, pointerId);
                _localKind[d.Variable] = LocalKind.Pointer;
                if (d.Initializer is { } init)
                {
                    var v = EmitValue(init);
                    _b.Store(varId, v);
                }
                return;
            }

            case AssignExpression a:
            {
                var ptr = EmitPointer(a.Target);
                var val = EmitValue(a.Value);
                _b.Store(ptr, val);
                return;
            }

            case IfExpression iff when iff.IfFalse is null:
            {
                var cond = EmitValue(iff.Test);
                var thenLbl  = _b.AllocLabel();
                var mergeLbl = _b.AllocLabel();
                _b.SelectionMerge(mergeLbl, SpvSelectionControl.None);
                _b.BranchConditional(cond, thenLbl, mergeLbl);
                _b.LabelHere(thenLbl);
                EmitStatement(iff.IfTrue);
                _b.Branch(mergeLbl);
                _b.LabelHere(mergeLbl);
                return;
            }

            case IfExpression iff:
            {
                var cond = EmitValue(iff.Test);
                var thenLbl  = _b.AllocLabel();
                var elseLbl  = _b.AllocLabel();
                var mergeLbl = _b.AllocLabel();
                _b.SelectionMerge(mergeLbl, SpvSelectionControl.None);
                _b.BranchConditional(cond, thenLbl, elseLbl);
                _b.LabelHere(thenLbl);
                EmitStatement(iff.IfTrue);
                _b.Branch(mergeLbl);
                _b.LabelHere(elseLbl);
                EmitStatement(iff.IfFalse!);
                _b.Branch(mergeLbl);
                _b.LabelHere(mergeLbl);
                return;
            }

            case ForExpression fe:
                EmitFor(fe);
                return;

            case WhileExpression we:
                EmitWhile(we);
                return;

            case ReturnExpression r:
                if (r.Value is null) _b.Return();
                else _b.ReturnValue(EmitValue(r.Value));
                return;

            case DiscardExpression: _b.Kill(); return;

            // Standalone expression in statement position: evaluate for side effects (e.g. user function call),
            // discard result.
            default:
                _ = EmitValue(e);
                return;
        }
    }

    private void EmitFor(ForExpression f)
    {
        // Locals dictionary captures the loop variable as a Function-storage pointer.
        var pointeeId = TypeOf(f.Variable.ResultType!);
        var pointerId = _b.TypePointer(SpvStorageClass.Function, pointeeId);
        var varId = _b.FunctionVariable(pointerId);
        _b.AddName(varId, f.Variable.Name);
        _locals[f.Variable] = new LocalMapping(varId, pointerId);
        _localKind[f.Variable] = LocalKind.Pointer;

        _b.Store(varId, EmitValue(f.Initial));

        var headerLbl   = _b.AllocLabel();
        var condLbl     = _b.AllocLabel();
        var bodyLbl     = _b.AllocLabel();
        var continueLbl = _b.AllocLabel();
        var mergeLbl    = _b.AllocLabel();

        _b.Branch(headerLbl);
        _b.LabelHere(headerLbl);
        _b.LoopMerge(mergeLbl, continueLbl, SpvLoopControl.None);
        _b.Branch(condLbl);

        _b.LabelHere(condLbl);
        var c = EmitValue(f.Test);
        _b.BranchConditional(c, bodyLbl, mergeLbl);

        _b.LabelHere(bodyLbl);
        EmitStatement(f.Body);
        _b.Branch(continueLbl);

        _b.LabelHere(continueLbl);
        // Step is a value expression, but typically a side-effecting Assign.
        EmitStatement(f.Step);
        _b.Branch(headerLbl);

        _b.LabelHere(mergeLbl);
    }

    private void EmitWhile(WhileExpression w)
    {
        var headerLbl   = _b.AllocLabel();
        var condLbl     = _b.AllocLabel();
        var bodyLbl     = _b.AllocLabel();
        var continueLbl = _b.AllocLabel();
        var mergeLbl    = _b.AllocLabel();

        _b.Branch(headerLbl);
        _b.LabelHere(headerLbl);
        _b.LoopMerge(mergeLbl, continueLbl, SpvLoopControl.None);
        _b.Branch(condLbl);

        _b.LabelHere(condLbl);
        var c = EmitValue(w.Test);
        _b.BranchConditional(c, bodyLbl, mergeLbl);

        _b.LabelHere(bodyLbl);
        EmitStatement(w.Body);
        _b.Branch(continueLbl);

        _b.LabelHere(continueLbl);
        _b.Branch(headerLbl);

        _b.LabelHere(mergeLbl);
    }

    // =========================================================================
    // L-values (pointer-producing expressions)
    // =========================================================================

    private uint EmitPointer(ShaderExpression e)
    {
        switch (e)
        {
            case ParameterExpression p:
                if (!_locals.TryGetValue(p, out var lm))
                    throw new InvalidOperationException($"Unknown parameter '{p.Name}'.");
                if (_localKind[p] != LocalKind.Pointer)
                    throw new InvalidOperationException($"Parameter '{p.Name}' is a value, not assignable.");
                return lm.PointerId;

            case GlobalReferenceExpression g:
            {
                var gm = _globals[g.Global!];
                if (gm.StructMemberIndex is uint mi)
                {
                    // Wrapped (Uniform UBO / PushConstant): need access chain to member.
                    var memberPointerId = _b.TypePointer(StorageClassOf(g.Global!), TypeOf(gm.Pointee));
                    var idxConst = _b.ConstantUInt32(mi);
                    return _b.AccessChain(memberPointerId, gm.PointerId, stackalloc uint[] { idxConst });
                }
                return gm.PointerId;
            }

            case IndexExpression idx:
            {
                var basePtr = EmitPointer(idx.Source);
                var indexId = EmitValue(idx.Index);
                var elemType = idx.ResultType!;
                var sc = StorageClassFromBasePointer(idx.Source);
                var elemPointerId = _b.TypePointer(sc, TypeOf(elemType));
                return _b.AccessChain(elemPointerId, basePtr, stackalloc uint[] { indexId });
            }

            case FieldAccessExpression fa:
            {
                var basePtr = EmitPointer(fa.Source);
                var srcType = (StructType)fa.Source.ResultType!;
                int memberIdx = -1;
                for (int i = 0; i < srcType.Fields.Length; i++)
                    if (srcType.Fields[i].Name == fa.FieldName) { memberIdx = i; break; }
                if (memberIdx < 0) throw new InvalidOperationException($"No field '{fa.FieldName}'.");
                var sc = StorageClassFromBasePointer(fa.Source);
                var memberPointerId = _b.TypePointer(sc, TypeOf(fa.ResultType!));
                var idxConst = _b.ConstantUInt32((uint)memberIdx);
                return _b.AccessChain(memberPointerId, basePtr, stackalloc uint[] { idxConst });
            }

            // Single-component swizzle assignment ('x','y','z','w') is handled via access chain on a vector pointer.
            case SwizzleExpression sw when sw.Components.Length == 1:
            {
                var basePtr = EmitPointer(sw.Source);
                var component = SwizzleIndex(sw.Components[0]);
                var sc = StorageClassFromBasePointer(sw.Source);
                var componentPointerId = _b.TypePointer(sc, TypeOf(sw.ResultType!));
                var idxConst = _b.ConstantUInt32((uint)component);
                return _b.AccessChain(componentPointerId, basePtr, stackalloc uint[] { idxConst });
            }

            default:
                throw new InvalidOperationException(
                    $"Cannot take pointer of {e.GetType().Name} (multi-component swizzle assign not supported in v1).");
        }
    }

    private SpvStorageClass StorageClassFromBasePointer(ShaderExpression baseExpr) => baseExpr switch
    {
        ParameterExpression  => SpvStorageClass.Function,
        GlobalReferenceExpression g => StorageClassOf(g.Global!),
        IndexExpression i      => StorageClassFromBasePointer(i.Source),
        FieldAccessExpression f => StorageClassFromBasePointer(f.Source),
        SwizzleExpression s    => StorageClassFromBasePointer(s.Source),
        _ => SpvStorageClass.Function,
    };

    // =========================================================================
    // R-values
    // =========================================================================

    private uint EmitValue(ShaderExpression e)
    {
        switch (e)
        {
            case LiteralExpression lit:
                return EmitLiteral(lit);

            case ParameterExpression p:
            {
                if (!_locals.TryGetValue(p, out var lm))
                    throw new InvalidOperationException($"Unknown parameter '{p.Name}'.");
                if (_localKind[p] == LocalKind.Value) return lm.PointerId;   // function parameter (already a value)
                return _b.Load(TypeOf(p.ResultType!), lm.PointerId);
            }

            case GlobalReferenceExpression:
            case IndexExpression:
            case FieldAccessExpression:
            {
                var ptr = EmitPointer(e);
                return _b.Load(TypeOf(e.ResultType!), ptr);
            }

            case SwizzleExpression sw:
                return EmitSwizzle(sw);

            case BinaryExpression bin:
                return EmitBinary(bin);

            case UnaryExpression un:
                return EmitUnary(un);

            case ConstructExpression ctor:
                return EmitConstruct(ctor);

            case CallExpression call:
                return EmitCall(call);

            case SampleExpression samp:
                return EmitSample(samp);

            case AssignExpression a:
            {
                var ptr = EmitPointer(a.Target);
                var v = EmitValue(a.Value);
                _b.Store(ptr, v);
                return v; // assignment evaluates to the assigned value
            }

            case IfExpression iff when iff.IfFalse is not null:
                return EmitIfExpression(iff);

            case BlockExpression block:
            {
                if (block.Body.Length == 0) throw new InvalidOperationException("Empty block as value.");
                for (int i = 0; i < block.Body.Length - 1; i++) EmitStatement(block.Body[i]);
                return EmitValue(block.Body[^1]);
            }

            default:
                throw new InvalidOperationException($"Cannot emit value of {e.GetType().Name}.");
        }
    }

    // ---- Literals ----

    private uint EmitLiteral(LiteralExpression lit) => lit.Value switch
    {
        bool b  => _b.ConstantBool(b),
        int i   => _b.ConstantInt32(i),
        uint u  => _b.ConstantUInt32(u),
        float f => _b.ConstantFloat32(f),
        _ => throw new InvalidOperationException($"Unsupported literal {lit.Value?.GetType().Name}."),
    };

    // ---- Swizzle (read) ----

    private uint EmitSwizzle(SwizzleExpression sw)
    {
        var srcVal = EmitValue(sw.Source);
        var srcType = (VectorType)sw.Source.ResultType!;
        if (sw.Components.Length == 1)
        {
            var componentTypeId = TypeOf(srcType.Component);
            return _b.CompositeExtract(componentTypeId, srcVal,
                stackalloc uint[] { (uint)SwizzleIndex(sw.Components[0]) });
        }
        var resultTypeId = TypeOf(sw.ResultType!);
        Span<uint> components = stackalloc uint[sw.Components.Length];
        for (int i = 0; i < sw.Components.Length; i++) components[i] = (uint)SwizzleIndex(sw.Components[i]);
        return _b.VectorShuffle(resultTypeId, srcVal, srcVal, components);
    }

    // ---- Binary / Unary ----

    private uint EmitBinary(BinaryExpression bin)
    {
        var lt = bin.Left.ResultType!;
        var l = EmitValue(bin.Left);
        var r = EmitValue(bin.Right);
        var resultTypeId = TypeOf(bin.ResultType!);
        var componentScalar = ComponentScalar(lt) ?? lt;
        bool isFloat = componentScalar is FloatType;
        bool isSigned = componentScalar is IntType;
        bool isUnsigned = componentScalar is UIntType;

        SpvOp op = bin.Op switch
        {
            ShaderBinaryOp.Add => isFloat ? SpvOp.FAdd : SpvOp.IAdd,
            ShaderBinaryOp.Sub => isFloat ? SpvOp.FSub : SpvOp.ISub,
            ShaderBinaryOp.Mul => MulOp(bin),
            ShaderBinaryOp.Div => isFloat ? SpvOp.FDiv : isSigned ? SpvOp.SDiv : SpvOp.UDiv,
            ShaderBinaryOp.Rem => isFloat ? SpvOp.FRem : isSigned ? SpvOp.SRem : SpvOp.UMod,
            ShaderBinaryOp.MatMul => MatMulOp(bin),
            ShaderBinaryOp.Eq  => isFloat ? SpvOp.FOrdEqual    : SpvOp.IEqual,
            ShaderBinaryOp.Ne  => isFloat ? SpvOp.FOrdNotEqual : SpvOp.INotEqual,
            ShaderBinaryOp.Lt  => isFloat ? SpvOp.FOrdLessThan         : isSigned ? SpvOp.SLessThan         : SpvOp.ULessThan,
            ShaderBinaryOp.Le  => isFloat ? SpvOp.FOrdLessThanEqual    : isSigned ? SpvOp.SLessThanEqual    : SpvOp.ULessThanEqual,
            ShaderBinaryOp.Gt  => isFloat ? SpvOp.FOrdGreaterThan      : isSigned ? SpvOp.SGreaterThan      : SpvOp.UGreaterThan,
            ShaderBinaryOp.Ge  => isFloat ? SpvOp.FOrdGreaterThanEqual : isSigned ? SpvOp.SGreaterThanEqual : SpvOp.UGreaterThanEqual,
            ShaderBinaryOp.And => SpvOp.LogicalAnd,
            ShaderBinaryOp.Or  => SpvOp.LogicalOr,
            ShaderBinaryOp.BitAnd => SpvOp.BitwiseAnd,
            ShaderBinaryOp.BitOr  => SpvOp.BitwiseOr,
            ShaderBinaryOp.BitXor => SpvOp.BitwiseXor,
            ShaderBinaryOp.Shl    => SpvOp.ShiftLeftLogical,
            ShaderBinaryOp.Shr    => isUnsigned ? SpvOp.ShiftRightLogical : SpvOp.ShiftRightArithmetic,
            _ => throw new InvalidOperationException($"Unsupported binary op {bin.Op}."),
        };

        return _b.Binary(op, resultTypeId, l, r);

        SpvOp MulOp(BinaryExpression bb)
        {
            var rt = bb.Right.ResultType!;
            // Vector * scalar / matrix * scalar / matrix * matrix all need their own opcode.
            if (lt is VectorType && IsNumericScalar(rt)) return SpvOp.VectorTimesScalar;
            if (IsNumericScalar(lt) && rt is VectorType) return SpvOp.VectorTimesScalar; // operands may need swap
            if (lt is MatrixType && IsNumericScalar(rt)) return SpvOp.MatrixTimesScalar;
            if (IsNumericScalar(lt) && rt is MatrixType) return SpvOp.MatrixTimesScalar;
            return isFloat ? SpvOp.FMul : SpvOp.IMul;
        }

        SpvOp MatMulOp(BinaryExpression bb)
        {
            var rt = bb.Right.ResultType!;
            if (lt is MatrixType && rt is VectorType) return SpvOp.MatrixTimesVector;
            if (lt is VectorType && rt is MatrixType) return SpvOp.VectorTimesMatrix;
            return SpvOp.MatrixTimesMatrix;
        }
    }

    private uint EmitUnary(UnaryExpression un)
    {
        var t = un.Operand.ResultType!;
        var operand = EmitValue(un.Operand);
        var resultTypeId = TypeOf(un.ResultType!);
        var component = ComponentScalar(t) ?? t;

        SpvOp op = un.Op switch
        {
            ShaderUnaryOp.Neg    => component is FloatType ? SpvOp.FNegate : SpvOp.SNegate,
            ShaderUnaryOp.Not    => SpvOp.LogicalNot,
            ShaderUnaryOp.BitNot => SpvOp.Not,
            _ => throw new InvalidOperationException($"Unsupported unary op {un.Op}."),
        };
        return _b.Unary(op, resultTypeId, operand);
    }

    // ---- Construct ----

    private uint EmitConstruct(ConstructExpression ctor)
    {
        var typeId = TypeOf(ctor.ResultType!);
        Span<uint> args = stackalloc uint[ctor.Args.Length];
        for (int i = 0; i < ctor.Args.Length; i++) args[i] = EmitValue(ctor.Args[i]);

        // Splat: single scalar -> vector. SPIR-V: replicate via OpCompositeConstruct on N copies.
        if (ctor.ResultType is VectorType v && ctor.Args.Length == 1 && IsNumericScalar(ctor.Args[0].ResultType!))
        {
            Span<uint> repl = stackalloc uint[v.N];
            for (int i = 0; i < v.N; i++) repl[i] = args[0];
            return _b.CompositeConstruct(typeId, repl);
        }

        return _b.CompositeConstruct(typeId, args);
    }

    // ---- Calls ----

    private uint EmitCall(CallExpression call)
    {
        var args = new uint[call.Args.Length];
        for (int i = 0; i < args.Length; i++) args[i] = EmitValue(call.Args[i]);
        var resultTypeId = TypeOf(call.ResultType!);

        switch (call.Target)
        {
            case UserFunctionCallTarget user:
                return _b.FunctionCall(resultTypeId, _functions[user.Function], args);

            case IntrinsicCallTarget intr:
                return EmitIntrinsic(intr.Op, call.Args, args, resultTypeId);

            default:
                throw new InvalidOperationException($"Unknown call target {call.Target.GetType().Name}.");
        }
    }

    private uint EmitIntrinsic(ShaderIntrinsic op, ImmutableArray<ShaderExpression> origArgs, uint[] args, uint resultTypeId)
    {
        // Componentwise math: route through GLSL.std.450, picking the float/int/uint variant.
        // For our v1 the IR's arithmetic intrinsics are all float-typed; integer min/max/abs through
        // GLSL.std.450's S*/U* variants is left for when an integer demo arrives.
        var component = ComponentScalar(origArgs[0].ResultType!) ?? origArgs[0].ResultType!;
        bool isFloat = component is FloatType;
        bool isSigned = component is IntType;

        switch (op)
        {
            case ShaderIntrinsic.Saturate:
            {
                // clamp(x, 0.0, 1.0)
                var zero = _b.ConstantFloat32(0.0f);
                var one  = _b.ConstantFloat32(1.0f);
                // For vector x, build vector zeros/ones via OpCompositeConstruct.
                if (origArgs[0].ResultType is VectorType vv)
                {
                    Span<uint> zs = stackalloc uint[vv.N], os = stackalloc uint[vv.N];
                    for (int i = 0; i < vv.N; i++) { zs[i] = zero; os[i] = one; }
                    var vt = TypeOf(vv);
                    var zVec = _b.ConstantComposite(vt, zs);
                    var oVec = _b.ConstantComposite(vt, os);
                    return _b.ExtInstGlsl(resultTypeId, GlslStd450.FClamp, stackalloc uint[] { args[0], zVec, oVec });
                }
                return _b.ExtInstGlsl(resultTypeId, GlslStd450.FClamp, stackalloc uint[] { args[0], zero, one });
            }

            case ShaderIntrinsic.AsFloat: return _b.Unary(SpvOp.Bitcast, resultTypeId, args[0]);
            case ShaderIntrinsic.AsInt:   return _b.Unary(SpvOp.Bitcast, resultTypeId, args[0]);
            case ShaderIntrinsic.AsUInt:  return _b.Unary(SpvOp.Bitcast, resultTypeId, args[0]);

            case ShaderIntrinsic.Ddx:    return _b.Unary(SpvOp.DPdx,   resultTypeId, args[0]);
            case ShaderIntrinsic.Ddy:    return _b.Unary(SpvOp.DPdy,   resultTypeId, args[0]);
            case ShaderIntrinsic.FWidth: return _b.Unary(SpvOp.Fwidth, resultTypeId, args[0]);

            case ShaderIntrinsic.Dot:    return _b.Binary(SpvOp.Dot,       resultTypeId, args[0], args[1]);
            case ShaderIntrinsic.Transpose: return _b.Unary(SpvOp.Transpose, resultTypeId, args[0]);

            default:
            {
                var glslOp = MapToGlsl(op, isFloat, isSigned);
                return _b.ExtInstGlsl(resultTypeId, glslOp, args);
            }
        }
    }

    private static GlslStd450 MapToGlsl(ShaderIntrinsic op, bool isFloat, bool isSigned) => op switch
    {
        ShaderIntrinsic.Abs     => isFloat ? GlslStd450.FAbs : isSigned ? GlslStd450.SAbs : GlslStd450.SAbs,
        ShaderIntrinsic.Sign    => isFloat ? GlslStd450.FSign : GlslStd450.SSign,
        ShaderIntrinsic.Floor   => GlslStd450.Floor,
        ShaderIntrinsic.Ceil    => GlslStd450.Ceil,
        ShaderIntrinsic.Round   => GlslStd450.Round,
        ShaderIntrinsic.Trunc   => GlslStd450.Trunc,
        ShaderIntrinsic.Frac    => GlslStd450.Fract,
        ShaderIntrinsic.Mod     => GlslStd450.FMod,
        ShaderIntrinsic.Sin     => GlslStd450.Sin,
        ShaderIntrinsic.Cos     => GlslStd450.Cos,
        ShaderIntrinsic.Tan     => GlslStd450.Tan,
        ShaderIntrinsic.Asin    => GlslStd450.Asin,
        ShaderIntrinsic.Acos    => GlslStd450.Acos,
        ShaderIntrinsic.Atan    => GlslStd450.Atan,
        ShaderIntrinsic.Atan2   => GlslStd450.Atan2,
        ShaderIntrinsic.Exp     => GlslStd450.Exp,
        ShaderIntrinsic.Exp2    => GlslStd450.Exp2,
        ShaderIntrinsic.Log     => GlslStd450.Log,
        ShaderIntrinsic.Log2    => GlslStd450.Log2,
        ShaderIntrinsic.Pow     => GlslStd450.Pow,
        ShaderIntrinsic.Sqrt    => GlslStd450.Sqrt,
        ShaderIntrinsic.InverseSqrt => GlslStd450.InverseSqrt,
        ShaderIntrinsic.Min     => isFloat ? GlslStd450.FMin : isSigned ? GlslStd450.SMin : GlslStd450.UMin,
        ShaderIntrinsic.Max     => isFloat ? GlslStd450.FMax : isSigned ? GlslStd450.SMax : GlslStd450.UMax,
        ShaderIntrinsic.Clamp   => isFloat ? GlslStd450.FClamp : isSigned ? GlslStd450.SClamp : GlslStd450.UClamp,
        ShaderIntrinsic.Mix     => GlslStd450.FMix,
        ShaderIntrinsic.Step    => GlslStd450.Step,
        ShaderIntrinsic.SmoothStep => GlslStd450.SmoothStep,
        ShaderIntrinsic.Length  => GlslStd450.Length,
        ShaderIntrinsic.Distance => GlslStd450.Distance,
        ShaderIntrinsic.Cross   => GlslStd450.Cross,
        ShaderIntrinsic.Normalize => GlslStd450.Normalize,
        ShaderIntrinsic.Reflect => GlslStd450.Reflect,
        ShaderIntrinsic.Refract => GlslStd450.Refract,
        ShaderIntrinsic.Determinant => GlslStd450.Determinant,
        ShaderIntrinsic.Inverse => GlslStd450.MatrixInverse,
        _ => throw new InvalidOperationException($"GLSL.std.450 mapping missing for {op}."),
    };

    // ---- Sample ----

    private uint EmitSample(SampleExpression s)
    {
        var imageId = EmitValue(s.Texture);   // texture is an Image variable -> Load yields Image value
        var samplerId = EmitValue(s.Sampler); // sampler is a Sampler variable -> Load yields Sampler value
        var coordId = EmitValue(s.Coord);

        var imageType = TypeOf(s.Texture.ResultType!);
        var sampledType = _b.TypeSampledImage(imageType, ("SampledImage", s.Texture.ResultType!));
        var combined = _b.SampledImage(sampledType, imageId, samplerId);

        var resultTypeId = TypeOf(s.ResultType!);
        return s.Lod is null
            ? _b.ImageSampleImplicitLod(resultTypeId, combined, coordId)
            : _b.ImageSampleExplicitLod(resultTypeId, combined, coordId, EmitValue(s.Lod));
    }

    // ---- If as expression (ternary) ----

    private uint EmitIfExpression(IfExpression iff)
    {
        // Lower to OpSelect (no control flow) when both arms are pure values; this is sufficient
        // for the binder's "expression form" (ResultType non-void, both arms present).
        var c = EmitValue(iff.Test);
        var t = EmitValue(iff.IfTrue);
        var f = EmitValue(iff.IfFalse!);
        return _b.Select(TypeOf(iff.ResultType!), c, t, f);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static ShaderType? ComponentScalar(ShaderType t) => t switch
    {
        VectorType v => v.Component,
        MatrixType m => m.Component,
        _ => null,
    };

    private static bool IsNumericScalar(ShaderType t) => t is IntType or UIntType or FloatType;

    private static int SwizzleIndex(char c) => c switch
    {
        'x' or 'r' => 0,
        'y' or 'g' => 1,
        'z' or 'b' => 2,
        'w' or 'a' => 3,
        _ => throw new ArgumentException($"Invalid swizzle component '{c}'."),
    };
}
