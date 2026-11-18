using Godot;

namespace ButtonAndTimber;

public partial class ShopGame
{
    private AudioStreamWav[] risingNotes = [];
    private AudioStreamWav floorStep = null!;
    private AudioStreamWav drawer = null!;
    private AudioStreamWav arrival = null!;
    private int craftStreak;
    private double stepSoundIn;

    private void MakeShopAudio()
    {
        risingNotes = Enumerable.Range(0, 12).Select(semitone => RisingNote(semitone)).ToArray();
        arrival = Chime([698.46, 1046.5], .32f);
        floorStep = Synthesize(.085, t =>
        {
            double low = Math.Sin(Math.Tau * 117 * t);
            double wood = Math.Sin(Math.Tau * 277 * t) * .16;
            return (low + wood) * Math.Exp(-t * 67) * Math.Min(1, t * 660) * .32;
        });
        drawer = Synthesize(.15, t =>
        {
            double tap = Math.Sin(Math.Tau * 183 * t) + Math.Sin(Math.Tau * 357 * t) * .25;
            double slide = Math.Sin(163 * t + 1180 * t * t) * Math.Sin(2129 * t) * .23;
            return (tap * Math.Exp(-t * 44) + slide * Math.Exp(-t * 19)) * Math.Min(1, t * 350) * .37;
        });
    }

    private static AudioStreamWav RisingNote(int semitone)
    {
        // Cross-fade octave-spaced partials around a fixed pitch range (a Shepard scale).
        double[] frequencies = new double[6];
        double[] weights = new double[6];
        for (int i = 0; i < frequencies.Length; i++)
        {
            double frequency = 220 * Math.Pow(2, i - 2 + semitone / 12d);
            double distance = Math.Log2(frequency / 480) / .72;
            frequencies[i] = frequency;
            weights[i] = Math.Exp(-.5 * distance * distance);
        }
        double amplitude = .65 / weights.Sum();
        return Synthesize(.212, t =>
        {
            double signal = 0;
            for (int i = 0; i < frequencies.Length; i++)
                signal += weights[i] * Math.Sin(Math.Tau * frequencies[i] * t);
            double envelope = Math.Min(1, t * 95) * Math.Exp(-t * 12) * Math.Min(1, (.212 - t) * 36);
            return amplitude * signal * envelope;
        });
    }

    private void PlayCraftSuccess(BenchCue cue)
    {
        Play(risingNotes[craftStreak], cue == BenchCue.LevelUp ? -15 : -18);
        craftStreak = (craftStreak + 1) % risingNotes.Length;
    }

    private void PlayShopMovement(double elapsed, int visitorsBefore)
    {
        if (room != Room.Shop || speed == 0)
            return;
        if (speed <= 3 && state.Today.Visitors > visitorsBefore)
            Play(arrival, -22);

        bool nearby = state.Customers.Any(customer => customer.Walking && customer.Position.Y < 6);
        stepSoundIn -= elapsed;
        if (stepSoundIn > 0 || speed > 3)
            return;
        stepSoundIn = nearby ? .54 : 2.7;
        Play(floorStep, nearby ? -29 : -38, nearby ? 1.04f : .82f);
    }

    private static AudioStreamWav Synthesize(double duration, Func<double, double> sampleAt)
    {
        const int sampleRate = 22050;
        byte[] pcm = new byte[(int)(duration * sampleRate) * 2];
        for (int i = 0; i < pcm.Length / 2; i++)
        {
            short sample = (short)Math.Round(Math.Clamp(sampleAt(i / (double)sampleRate), -1, 1) * short.MaxValue);
            pcm[i * 2] = (byte)sample;
            pcm[i * 2 + 1] = (byte)(sample >> 8);
        }
        return new AudioStreamWav { Data = pcm, MixRate = sampleRate, Format = AudioStreamWav.FormatEnum.Format16Bits };
    }
}
