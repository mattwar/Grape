namespace Grape;

/// <summary>
/// A loaded sound: an <see cref="AudioSpec"/> describing the format
/// plus the raw PCM bytes. Pass to <see cref="Audio.Play(Sound, float)"/>
/// or <see cref="AudioPlaybackDevice.Play(Sound, float)"/>.
/// </summary>
public sealed class Sound
{
    public AudioSpec Spec { get; }
    public ReadOnlyMemory<byte> Data { get; }

    public Sound(AudioSpec spec, ReadOnlyMemory<byte> data)
    {
        this.Spec = spec;
        this.Data = data;
    }

    /// <summary>
    /// Loads a WAV file from the specified path.
    /// </summary>
    public static Sound LoadWAV(string path)
    {
        if (!SDL.LoadWAV(path, out var spec, out var audioBuffer, out var audioLength))
            throw new InvalidOperationException($"SDL_LoadWAV Error: {SDL.GetError()}");
        unsafe
        {
            byte* sourceBytesPtr = (byte*)audioBuffer;
            var bytes = new byte[audioLength];
            fixed (byte* targetBytePtr = bytes)
            {
                Buffer.MemoryCopy(sourceBytesPtr, targetBytePtr, audioLength, audioLength);
            }
            var data = new ReadOnlyMemory<byte>(bytes);
            return new Sound(AudioSpec.From(spec), data);
        }
    }
}
