using Godot;

namespace ButtonAndTimber;

public partial class ShopGame
{
    private AudioStreamPlayer? music;

    private void MakeMusic()
    {
        if (sandboxRun)
            return;

        music = new AudioStreamPlayer { Name = "Music", Stream = MusicBox(), VolumeDb = -21 };
        AddChild(music);
        music.Play();
    }

    private void UpdateMusic()
    {
        if (music == null)
            return;

        music.StreamPaused = state.Muted;
        float target = state.Shift == Shift.Closed || state.Minute > 480 ? -25 : room == Room.Workshop ? -23 : -21;
        music.VolumeDb = Mathf.Lerp(music.VolumeDb, target, .02f);
    }

    /// <summary>A slow, looping music-box tune built from a pentatonic scale, with a soft echo.</summary>
    private static AudioStreamWav MusicBox()
    {
        const int rate = 22050;
        const double beat = .5;
        const int beats = 32;
        double[] scale = [261.63, 293.66, 329.63, 392.00, 440.00, 523.25, 587.33, 659.25];
        int[] bass = [0, 0, 3, 4];
        float[] mix = new float[(int)(rate * beat * beats)];
        uint seed = 7;
        int Next(int max)
        {
            seed = unchecked(seed * 1664525 + 1013904223);
            return (int)((seed >> 8) % (uint)max);
        }

        int note = 2;
        for (int step = 0; step < beats * 2; step++)
        {
            double start = step * beat / 2;
            if (step % 8 == 0)
                AddTone(scale[bass[step / 8 % bass.Length]] / 2, start, 1.9, .5);
            if (step % 4 == 3 && Next(3) == 0)
                continue;
            note = Math.Clamp(note + Next(5) - 2, 0, scale.Length - 1);
            AddTone(scale[note], start, 1.2, .36);
        }

        int delay = (int)(rate * beat * 1.5);
        for (int i = 0; i < mix.Length; i++)
            mix[(i + delay) % mix.Length] += mix[i] * .33f;

        byte[] data = new byte[mix.Length * 2];
        for (int i = 0; i < mix.Length; i++)
        {
            short sample = (short)Math.Clamp(mix[i] * 9000, short.MinValue, short.MaxValue);
            data[i * 2] = (byte)sample;
            data[i * 2 + 1] = (byte)(sample >> 8);
        }
        return new AudioStreamWav
        {
            Data = data, MixRate = rate, Format = AudioStreamWav.FormatEnum.Format16Bits,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = mix.Length
        };

        void AddTone(double frequency, double at, double length, double volume)
        {
            int first = (int)(at * rate);
            int count = (int)(length * rate);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)rate;
                double envelope = Math.Exp(-t * 3.4) * Math.Min(t * 400, 1);
                double wave = Math.Sin(Math.Tau * frequency * t) + .3 * Math.Sin(Math.Tau * frequency * 2 * t) * Math.Exp(-t * 6);
                mix[(first + i) % mix.Length] += (float)(wave * envelope * volume);
            }
        }
    }
}
