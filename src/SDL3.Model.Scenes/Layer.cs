using SDL3.Model.Utilities;
using System.Collections.Immutable;

namespace SDL3.Model.Scenes;

public abstract class Layer : Prop
{
    private ImmutableList<Prop> _props = ImmutableList<Prop>.Empty;
}
