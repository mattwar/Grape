namespace SDL3.Model.Scenes;

public struct UpdateContext
{
    public TimeSpan Time { get; init; }
    public SDL.Rect Bounds { get; init; }
}
