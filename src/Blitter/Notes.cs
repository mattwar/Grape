namespace Blitter;

/// <summary>
/// Equal-tempered note frequencies in Hertz (A4 = 440 Hz). 
/// Pass directly to <see cref="Sound.Tone"/> or <see cref="Sound.Sweep"/>.
/// Sharp notes use the <c>s</c> suffix (e.g. <see cref="Cs4"/> = C#4 = Db4).
/// </summary>
public static class Notes
{
    public const float C0 = 16.3516f;
    public const float Cs0 = 17.3239f;
    public const float D0 = 18.3540f;
    public const float Ds0 = 19.4454f;
    public const float E0 = 20.6017f;
    public const float F0 = 21.8268f;
    public const float Fs0 = 23.1247f;
    public const float G0 = 24.4997f;
    public const float Gs0 = 25.9565f;
    public const float A0 = 27.5000f;
    public const float As0 = 29.1352f;
    public const float B0 = 30.8677f;

    public const float C1 = 32.7032f;
    public const float Cs1 = 34.6478f;
    public const float D1 = 36.7081f;
    public const float Ds1 = 38.8909f;
    public const float E1 = 41.2034f;
    public const float F1 = 43.6535f;
    public const float Fs1 = 46.2493f;
    public const float G1 = 48.9994f;
    public const float Gs1 = 51.9131f;
    public const float A1 = 55.0000f;
    public const float As1 = 58.2705f;
    public const float B1 = 61.7354f;

    public const float C2 = 65.4064f;
    public const float Cs2 = 69.2957f;
    public const float D2 = 73.4162f;
    public const float Ds2 = 77.7817f;
    public const float E2 = 82.4069f;
    public const float F2 = 87.3071f;
    public const float Fs2 = 92.4986f;
    public const float G2 = 97.9989f;
    public const float Gs2 = 103.8262f;
    public const float A2 = 110.0000f;
    public const float As2 = 116.5409f;
    public const float B2 = 123.4708f;

    public const float C3 = 130.8128f;
    public const float Cs3 = 138.5913f;
    public const float D3 = 146.8324f;
    public const float Ds3 = 155.5635f;
    public const float E3 = 164.8138f;
    public const float F3 = 174.6141f;
    public const float Fs3 = 184.9972f;
    public const float G3 = 195.9977f;
    public const float Gs3 = 207.6523f;
    public const float A3 = 220.0000f;
    public const float As3 = 233.0819f;
    public const float B3 = 246.9417f;

    public const float C4 = 261.6256f;
    public const float Cs4 = 277.1826f;
    public const float D4 = 293.6648f;
    public const float Ds4 = 311.1270f;
    public const float E4 = 329.6276f;
    public const float F4 = 349.2282f;
    public const float Fs4 = 369.9944f;
    public const float G4 = 391.9954f;
    public const float Gs4 = 415.3047f;
    public const float A4 = 440.0000f;
    public const float As4 = 466.1638f;
    public const float B4 = 493.8833f;

    public const float C5 = 523.2511f;
    public const float Cs5 = 554.3653f;
    public const float D5 = 587.3295f;
    public const float Ds5 = 622.2540f;
    public const float E5 = 659.2551f;
    public const float F5 = 698.4565f;
    public const float Fs5 = 739.9888f;
    public const float G5 = 783.9909f;
    public const float Gs5 = 830.6094f;
    public const float A5 = 880.0000f;
    public const float As5 = 932.3275f;
    public const float B5 = 987.7666f;

    public const float C6 = 1046.5023f;
    public const float Cs6 = 1108.7305f;
    public const float D6 = 1174.6591f;
    public const float Ds6 = 1244.5079f;
    public const float E6 = 1318.5102f;
    public const float F6 = 1396.9129f;
    public const float Fs6 = 1479.9777f;
    public const float G6 = 1567.9817f;
    public const float Gs6 = 1661.2188f;
    public const float A6 = 1760.0000f;
    public const float As6 = 1864.6550f;
    public const float B6 = 1975.5332f;

    public const float C7 = 2093.0045f;
    public const float Cs7 = 2217.4610f;
    public const float D7 = 2349.3181f;
    public const float Ds7 = 2489.0159f;
    public const float E7 = 2637.0205f;
    public const float F7 = 2793.8259f;
    public const float Fs7 = 2959.9554f;
    public const float G7 = 3135.9635f;
    public const float Gs7 = 3322.4376f;
    public const float A7 = 3520.0000f;
    public const float As7 = 3729.3101f;
    public const float B7 = 3951.0664f;

    public const float C8 = 4186.0090f;
    public const float Cs8 = 4434.9221f;
    public const float D8 = 4698.6363f;
    public const float Ds8 = 4978.0317f;
    public const float E8 = 5274.0409f;
    public const float F8 = 5587.6517f;
    public const float Fs8 = 5919.9108f;
    public const float G8 = 6271.9270f;
    public const float Gs8 = 6644.8752f;
    public const float A8 = 7040.0000f;
    public const float As8 = 7458.6202f;
    public const float B8 = 7902.1328f;
}
