using System.Buffers.Binary;
using System.Text;

namespace Grape.Shaders.Emitters.Spv;

/// <summary>
/// Accumulates a SPIR-V module in canonical section order from a ShaderStage, 
/// allocating SSA IDs  and interning types and constants. Provides per-opcode emit helpers that
/// return the result ID (or 0 for instructions with no result).
///
/// All instructions go to one of nine section buffers; <see cref="SpvStageWriter"/>
/// concatenates them in spec order. Inside a section, instructions appear in
/// the order they were emitted.
/// </summary>
internal sealed class SpvStageBuilder
{
    // ---- Sections (in canonical SPIR-V order) ----

    private readonly List<uint> _capabilities    = new();
    private readonly List<uint> _extensions      = new();
    private readonly List<uint> _extInstImports  = new();
    private readonly List<uint> _memoryModel     = new();
    private readonly List<uint> _entryPoints     = new();
    private readonly List<uint> _executionModes  = new();
    private readonly List<uint> _debug           = new();
    private readonly List<uint> _annotations     = new();
    private readonly List<uint> _typesConstants  = new();
    private readonly List<uint> _functions       = new();

    /// <summary>Currently-targeted instruction stream inside a function. Null outside a function body.</summary>
    private List<uint>? _currentBlock;

    // ---- ID allocator ----

    private uint _nextId = 1;
    public uint AllocId() => _nextId++;
    /// <summary>Upper bound on IDs in use (header field).</summary>
    public uint IdBound => _nextId;

    // ---- Interning ----

    private readonly Dictionary<object, uint> _typeIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(SpvStorageClass, uint), uint> _pointerTypeIds = new();
    private readonly Dictionary<(uint Ret, ulong ParamsHash), uint> _functionTypeIds = new();
    private readonly Dictionary<(uint TypeId, ulong Bits), uint> _scalarConstants = new();
    private uint _typeVoid;
    private uint _typeBool;
    private uint _typeInt;
    private uint _typeUInt;
    private uint _typeFloat;
    private uint _glslExtSet;

    // ---- Section access (SpvWriter only) ----

    public IReadOnlyList<uint> Capabilities    => _capabilities;
    public IReadOnlyList<uint> Extensions      => _extensions;
    public IReadOnlyList<uint> ExtInstImports  => _extInstImports;
    public IReadOnlyList<uint> MemoryModel     => _memoryModel;
    public IReadOnlyList<uint> EntryPoints     => _entryPoints;
    public IReadOnlyList<uint> ExecutionModes  => _executionModes;
    public IReadOnlyList<uint> Debug           => _debug;
    public IReadOnlyList<uint> Annotations     => _annotations;
    public IReadOnlyList<uint> TypesConstants  => _typesConstants;
    public IReadOnlyList<uint> Functions       => _functions;

    // =========================================================================
    // Module-level scaffolding
    // =========================================================================

    public void AddCapability(SpvCapability cap)
        => Emit(_capabilities, SpvOp.Capability, (uint)cap);

    public void SetMemoryModel(SpvAddressingModel addr, SpvMemoryModel mem)
    {
        _memoryModel.Clear();
        Emit(_memoryModel, SpvOp.MemoryModel, (uint)addr, (uint)mem);
    }

    /// <summary>Imports an extended instruction set (e.g. GLSL.std.450) and returns its ID.</summary>
    public uint ImportExtInst(string name)
    {
        var id = AllocId();
        var words = new List<uint> { id };
        EncodeString(words, name);
        Emit(_extInstImports, SpvOp.ExtInstImport, words.ToArray());
        return id;
    }

    /// <summary>Lazily imports GLSL.std.450 and caches its set ID.</summary>
    public uint GlslExtSet()
    {
        if (_glslExtSet == 0) _glslExtSet = ImportExtInst("GLSL.std.450");
        return _glslExtSet;
    }

    public void AddEntryPoint(SpvExecutionModel model, uint entryFnId, string name, IEnumerable<uint> interfaceIds)
    {
        var words = new List<uint> { (uint)model, entryFnId };
        EncodeString(words, name);
        foreach (var iid in interfaceIds) words.Add(iid);
        Emit(_entryPoints, SpvOp.EntryPoint, words.ToArray());
    }

    public void AddExecutionMode(uint entryFnId, SpvExecutionMode mode, params uint[] literals)
    {
        var words = new List<uint> { entryFnId, (uint)mode };
        words.AddRange(literals);
        Emit(_executionModes, SpvOp.ExecutionMode, words.ToArray());
    }

    public void AddName(uint id, string name)
    {
        var words = new List<uint> { id };
        EncodeString(words, name);
        Emit(_debug, SpvOp.Name, words.ToArray());
    }

    public void Decorate(uint id, SpvDecoration decoration, params uint[] literals)
    {
        var words = new List<uint> { id, (uint)decoration };
        words.AddRange(literals);
        Emit(_annotations, SpvOp.Decorate, words.ToArray());
    }

    public void MemberDecorate(uint structId, uint member, SpvDecoration decoration, params uint[] literals)
    {
        var words = new List<uint> { structId, member, (uint)decoration };
        words.AddRange(literals);
        Emit(_annotations, SpvOp.MemberDecorate, words.ToArray());
    }

    // =========================================================================
    // Type emission (with interning)
    // =========================================================================

    public uint TypeVoid()
    {
        if (_typeVoid != 0) return _typeVoid;
        _typeVoid = AllocId();
        Emit(_typesConstants, SpvOp.TypeVoid, _typeVoid);
        return _typeVoid;
    }

    public uint TypeBool()
    {
        if (_typeBool != 0) return _typeBool;
        _typeBool = AllocId();
        Emit(_typesConstants, SpvOp.TypeBool, _typeBool);
        return _typeBool;
    }

    public uint TypeInt32(bool signed)
    {
        ref uint slot = ref signed ? ref _typeInt : ref _typeUInt;
        if (slot != 0) return slot;
        slot = AllocId();
        Emit(_typesConstants, SpvOp.TypeInt, slot, 32, signed ? 1u : 0u);
        return slot;
    }

    public uint TypeFloat32()
    {
        if (_typeFloat != 0) return _typeFloat;
        _typeFloat = AllocId();
        Emit(_typesConstants, SpvOp.TypeFloat, _typeFloat, 32);
        return _typeFloat;
    }

    /// <summary>Interns a vector type given component scalar id and length.</summary>
    public uint TypeVector(uint componentId, int n, object key)
    {
        if (_typeIds.TryGetValue(key, out var id)) return id;
        id = AllocId();
        Emit(_typesConstants, SpvOp.TypeVector, id, componentId, (uint)n);
        _typeIds[key] = id;
        return id;
    }

    public uint TypeMatrix(uint columnVectorId, int cols, object key)
    {
        if (_typeIds.TryGetValue(key, out var id)) return id;
        id = AllocId();
        Emit(_typesConstants, SpvOp.TypeMatrix, id, columnVectorId, (uint)cols);
        _typeIds[key] = id;
        return id;
    }

    public uint TypeArray(uint elementId, uint lengthConstId, object key)
    {
        if (_typeIds.TryGetValue(key, out var id)) return id;
        id = AllocId();
        Emit(_typesConstants, SpvOp.TypeArray, id, elementId, lengthConstId);
        _typeIds[key] = id;
        return id;
    }

    public uint TypeStruct(ReadOnlySpan<uint> memberTypeIds, object key)
    {
        if (_typeIds.TryGetValue(key, out var id)) return id;
        id = AllocId();
        var words = new uint[1 + memberTypeIds.Length];
        words[0] = id;
        for (int i = 0; i < memberTypeIds.Length; i++) words[i + 1] = memberTypeIds[i];
        Emit(_typesConstants, SpvOp.TypeStruct, words);
        _typeIds[key] = id;
        return id;
    }

    public uint TypeImage(uint sampledTypeId, SpvDim dim, bool arrayed, bool ms, bool sampled, SpvImageFormat fmt, object key)
    {
        if (_typeIds.TryGetValue(key, out var id)) return id;
        id = AllocId();
        // OpTypeImage: result, sampledType, dim, depth(0=non-depth), arrayed, MS, sampled(1=usable with sampler), format
        Emit(_typesConstants, SpvOp.TypeImage,
            id, sampledTypeId, (uint)dim, 0u, arrayed ? 1u : 0u, ms ? 1u : 0u, sampled ? 1u : 2u, (uint)fmt);
        _typeIds[key] = id;
        return id;
    }

    public uint TypeSampler(object key)
    {
        if (_typeIds.TryGetValue(key, out var id)) return id;
        id = AllocId();
        Emit(_typesConstants, SpvOp.TypeSampler, id);
        _typeIds[key] = id;
        return id;
    }

    public uint TypeSampledImage(uint imageId, object key)
    {
        if (_typeIds.TryGetValue(key, out var id)) return id;
        id = AllocId();
        Emit(_typesConstants, SpvOp.TypeSampledImage, id, imageId);
        _typeIds[key] = id;
        return id;
    }

    public uint TypePointer(SpvStorageClass storage, uint pointeeId)
    {
        if (_pointerTypeIds.TryGetValue((storage, pointeeId), out var id)) return id;
        id = AllocId();
        Emit(_typesConstants, SpvOp.TypePointer, id, (uint)storage, pointeeId);
        _pointerTypeIds[(storage, pointeeId)] = id;
        return id;
    }

    public uint TypeFunction(uint returnId, ReadOnlySpan<uint> paramIds)
    {
        ulong hash = (ulong)returnId * 1469598103934665603UL;
        for (int i = 0; i < paramIds.Length; i++) hash = (hash ^ paramIds[i]) * 1099511628211UL;
        var key = (returnId, hash);
        if (_functionTypeIds.TryGetValue(key, out var id)) return id;

        id = AllocId();
        var words = new uint[2 + paramIds.Length];
        words[0] = id;
        words[1] = returnId;
        for (int i = 0; i < paramIds.Length; i++) words[i + 2] = paramIds[i];
        Emit(_typesConstants, SpvOp.TypeFunction, words);
        _functionTypeIds[key] = id;
        return id;
    }

    // =========================================================================
    // Constants
    // =========================================================================

    public uint ConstantBool(bool v)
    {
        var typeId = TypeBool();
        ulong bits = v ? 1ul : 0ul;
        if (_scalarConstants.TryGetValue((typeId, bits), out var id)) return id;
        id = AllocId();
        Emit(_typesConstants, v ? SpvOp.ConstantTrue : SpvOp.ConstantFalse, typeId, id);
        _scalarConstants[(typeId, bits)] = id;
        return id;
    }

    public uint ConstantInt32(int v)
    {
        var typeId = TypeInt32(signed: true);
        return ScalarConstant(typeId, unchecked((uint)v));
    }

    public uint ConstantUInt32(uint v)
    {
        var typeId = TypeInt32(signed: false);
        return ScalarConstant(typeId, v);
    }

    public uint ConstantFloat32(float v)
    {
        var typeId = TypeFloat32();
        return ScalarConstant(typeId, BitConverter.SingleToUInt32Bits(v));
    }

    private uint ScalarConstant(uint typeId, uint word)
    {
        ulong bits = ((ulong)typeId << 32) | word; // unique key per (type, bit-pattern)
        if (_scalarConstants.TryGetValue((typeId, bits), out var id)) return id;
        id = AllocId();
        Emit(_typesConstants, SpvOp.Constant, typeId, id, word);
        _scalarConstants[(typeId, bits)] = id;
        return id;
    }

    public uint ConstantComposite(uint typeId, ReadOnlySpan<uint> componentIds)
    {
        // Composites are not interned (rarely repeat; keying by content is expensive).
        var id = AllocId();
        var words = new uint[2 + componentIds.Length];
        words[0] = typeId;
        words[1] = id;
        for (int i = 0; i < componentIds.Length; i++) words[i + 2] = componentIds[i];
        Emit(_typesConstants, SpvOp.ConstantComposite, words);
        return id;
    }

    // =========================================================================
    // Module-scope variables (emitted in the types/constants section)
    // =========================================================================

    public uint Variable(uint pointerTypeId, SpvStorageClass storage, uint? initializerId = null)
    {
        var id = AllocId();
        if (initializerId is uint init)
            Emit(_typesConstants, SpvOp.Variable, pointerTypeId, id, (uint)storage, init);
        else
            Emit(_typesConstants, SpvOp.Variable, pointerTypeId, id, (uint)storage);
        return id;
    }

    // =========================================================================
    // Function emission
    // =========================================================================

    /// <summary>
    /// Begins a function definition. Subsequent block-scope helpers emit into
    /// the function's instruction stream until <see cref="EndFunction"/>.
    /// Returns the function's result ID.
    /// </summary>
    public uint BeginFunction(uint returnTypeId, uint functionTypeId, SpvFunctionControl control = SpvFunctionControl.None)
    {
        var fnId = AllocId();
        Emit(_functions, SpvOp.Function, returnTypeId, fnId, (uint)control, functionTypeId);
        _currentBlock = _functions;
        return fnId;
    }

    public void EndFunction()
    {
        if (_currentBlock is null)
            throw new InvalidOperationException("EndFunction with no active function.");
        Emit(_functions, SpvOp.FunctionEnd);
        _currentBlock = null;
    }

    /// <summary>Emits OpLabel for a fresh block; returns the block's ID.</summary>
    public uint Label()
    {
        var id = AllocId();
        EmitInBlock(SpvOp.Label, id);
        return id;
    }

    /// <summary>Reserves a block ID without emitting; use <see cref="LabelHere"/> later.</summary>
    public uint AllocLabel() => AllocId();

    /// <summary>Emits OpLabel for a previously-allocated ID.</summary>
    public void LabelHere(uint blockId) => EmitInBlock(SpvOp.Label, blockId);

    public void Branch(uint targetBlockId)        => EmitInBlock(SpvOp.Branch, targetBlockId);
    public void BranchConditional(uint condId, uint trueBlockId, uint falseBlockId)
        => EmitInBlock(SpvOp.BranchConditional, condId, trueBlockId, falseBlockId);
    public void SelectionMerge(uint mergeBlockId, SpvSelectionControl control)
        => EmitInBlock(SpvOp.SelectionMerge, mergeBlockId, (uint)control);
    public void LoopMerge(uint mergeBlockId, uint continueBlockId, SpvLoopControl control)
        => EmitInBlock(SpvOp.LoopMerge, mergeBlockId, continueBlockId, (uint)control);

    public void Return()                        => EmitInBlock(SpvOp.Return);
    public void ReturnValue(uint id)            => EmitInBlock(SpvOp.ReturnValue, id);
    public void Kill()                          => EmitInBlock(SpvOp.Kill);

    // =========================================================================
    // Instruction helpers (in-block)
    // =========================================================================

    /// <summary>OpVariable in Function storage class. Per spec, must be the first instructions in the entry block.</summary>
    public uint FunctionVariable(uint pointerTypeId, uint? initializerId = null)
    {
        var id = AllocId();
        if (initializerId is uint init)
            EmitInBlock(SpvOp.Variable, pointerTypeId, id, (uint)SpvStorageClass.Function, init);
        else
            EmitInBlock(SpvOp.Variable, pointerTypeId, id, (uint)SpvStorageClass.Function);
        return id;
    }

    /// <summary>OpFunctionParameter. Must come immediately after OpFunction, before any OpLabel.</summary>
    public void FunctionParameterRaw(uint typeId, uint resultId)
        => EmitInBlock(SpvOp.FunctionParameter, typeId, resultId);

    public uint Load(uint resultTypeId, uint pointerId)
    {
        var id = AllocId();
        EmitInBlock(SpvOp.Load, resultTypeId, id, pointerId);
        return id;
    }

    public void Store(uint pointerId, uint valueId)
        => EmitInBlock(SpvOp.Store, pointerId, valueId);

    public uint AccessChain(uint pointerTypeId, uint baseId, ReadOnlySpan<uint> indexIds)
    {
        var id = AllocId();
        var words = new uint[3 + indexIds.Length];
        words[0] = pointerTypeId;
        words[1] = id;
        words[2] = baseId;
        for (int i = 0; i < indexIds.Length; i++) words[i + 3] = indexIds[i];
        EmitInBlock(SpvOp.AccessChain, words);
        return id;
    }

    public uint CompositeConstruct(uint resultTypeId, ReadOnlySpan<uint> partIds)
    {
        var id = AllocId();
        var words = new uint[2 + partIds.Length];
        words[0] = resultTypeId;
        words[1] = id;
        for (int i = 0; i < partIds.Length; i++) words[i + 2] = partIds[i];
        EmitInBlock(SpvOp.CompositeConstruct, words);
        return id;
    }

    public uint CompositeExtract(uint resultTypeId, uint compositeId, ReadOnlySpan<uint> indices)
    {
        var id = AllocId();
        var words = new uint[3 + indices.Length];
        words[0] = resultTypeId;
        words[1] = id;
        words[2] = compositeId;
        for (int i = 0; i < indices.Length; i++) words[i + 3] = indices[i];
        EmitInBlock(SpvOp.CompositeExtract, words);
        return id;
    }

    public uint VectorShuffle(uint resultTypeId, uint vec1, uint vec2, ReadOnlySpan<uint> components)
    {
        var id = AllocId();
        var words = new uint[4 + components.Length];
        words[0] = resultTypeId;
        words[1] = id;
        words[2] = vec1;
        words[3] = vec2;
        for (int i = 0; i < components.Length; i++) words[i + 4] = components[i];
        EmitInBlock(SpvOp.VectorShuffle, words);
        return id;
    }

    /// <summary>Single-result binary ALU op.</summary>
    public uint Binary(SpvOp op, uint resultTypeId, uint a, uint b)
    {
        var id = AllocId();
        EmitInBlock(op, resultTypeId, id, a, b);
        return id;
    }

    /// <summary>Single-result unary ALU op.</summary>
    public uint Unary(SpvOp op, uint resultTypeId, uint a)
    {
        var id = AllocId();
        EmitInBlock(op, resultTypeId, id, a);
        return id;
    }

    /// <summary>OpExtInst into the GLSL.std.450 extended set.</summary>
    public uint ExtInstGlsl(uint resultTypeId, GlslStd450 op, ReadOnlySpan<uint> args)
    {
        var setId = GlslExtSet();
        var id = AllocId();
        var words = new uint[4 + args.Length];
        words[0] = resultTypeId;
        words[1] = id;
        words[2] = setId;
        words[3] = (uint)op;
        for (int i = 0; i < args.Length; i++) words[i + 4] = args[i];
        EmitInBlock(SpvOp.ExtInst, words);
        return id;
    }

    public uint SampledImage(uint resultTypeId, uint imageId, uint samplerId)
    {
        var id = AllocId();
        EmitInBlock(SpvOp.SampledImage, resultTypeId, id, imageId, samplerId);
        return id;
    }

    public uint ImageSampleImplicitLod(uint resultTypeId, uint sampledImageId, uint coordId)
    {
        var id = AllocId();
        EmitInBlock(SpvOp.ImageSampleImplicitLod, resultTypeId, id, sampledImageId, coordId);
        return id;
    }

    public uint ImageSampleExplicitLod(uint resultTypeId, uint sampledImageId, uint coordId, uint lodId)
    {
        var id = AllocId();
        EmitInBlock(
            SpvOp.ImageSampleExplicitLod,
            resultTypeId, id, sampledImageId, coordId,
            (uint)SpvImageOperands.Lod, lodId);
        return id;
    }

    public uint Select(uint resultTypeId, uint condId, uint trueId, uint falseId)
    {
        var id = AllocId();
        EmitInBlock(SpvOp.Select, resultTypeId, id, condId, trueId, falseId);
        return id;
    }

    public uint Phi(uint resultTypeId, ReadOnlySpan<(uint Value, uint Block)> incoming)
    {
        var id = AllocId();
        var words = new uint[2 + 2 * incoming.Length];
        words[0] = resultTypeId;
        words[1] = id;
        for (int i = 0; i < incoming.Length; i++)
        {
            words[2 + 2 * i]     = incoming[i].Value;
            words[2 + 2 * i + 1] = incoming[i].Block;
        }
        EmitInBlock(SpvOp.Phi, words);
        return id;
    }

    public uint FunctionCall(uint resultTypeId, uint functionId, ReadOnlySpan<uint> argIds)
    {
        var id = AllocId();
        var words = new uint[3 + argIds.Length];
        words[0] = resultTypeId;
        words[1] = id;
        words[2] = functionId;
        for (int i = 0; i < argIds.Length; i++) words[i + 3] = argIds[i];
        EmitInBlock(SpvOp.FunctionCall, words);
        return id;
    }

    // =========================================================================
    // Low-level emission
    // =========================================================================

    private void EmitInBlock(SpvOp op, params uint[] operands)
    {
        if (_currentBlock is null)
            throw new InvalidOperationException(
                $"Cannot emit {op} outside a function body.");
        Emit(_currentBlock, op, operands);
    }

    private static void Emit(List<uint> section, SpvOp op, params uint[] operands)
    {
        uint header = (uint)((operands.Length + 1) << 16) | (uint)op;
        section.Add(header);
        section.AddRange(operands);
    }

    /// <summary>Encodes a UTF-8 string padded to 4-byte words (null-terminated, then zero-padded).</summary>
    private static void EncodeString(List<uint> sink, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        // SPIR-V literal strings: bytes are packed into uint32s little-endian, terminated by NUL,
        // padded with NULs so length is a multiple of 4.
        int byteCount = bytes.Length + 1;                      // include NUL
        int wordCount = (byteCount + 3) / 4;
        Span<byte> buf = stackalloc byte[wordCount * 4];
        bytes.CopyTo(buf);
        // remaining bytes (including the NUL we left for) are already 0.
        for (int i = 0; i < wordCount; i++)
            sink.Add(BinaryPrimitives.ReadUInt32LittleEndian(buf.Slice(i * 4, 4)));
    }
}
