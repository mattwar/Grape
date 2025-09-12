namespace SDL3.Model.Scenes;

[Flags]
public enum DockPosition
{
    Left        = 1 << 1,
    Top         = 1 << 2,
    Right       = 1 << 3,
    Bottom      = 1 << 4,
}
