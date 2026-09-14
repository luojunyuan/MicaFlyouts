namespace MicaFlyouts.Domain.Visualizer;

public sealed record VisualizerSnapshot(
    IReadOnlyList<float> Bars,
    bool BaselineVisible,
    bool HasContent,
    DateTimeOffset Timestamp)
{
    public static VisualizerSnapshot Empty { get; } = new([], false, false, default);
}

public static class FftProcessor
{
    public const int FftLength = 4096;
    public const int TargetFramesPerSecond = 30;

    public static float[] ComputeBars(
        ReadOnlySpan<float> samples,
        int sampleRate,
        int barCount,
        int sensitivity,
        int peak,
        ReadOnlySpan<float> previousBars = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(barCount);

        var real = new double[FftLength];
        var imaginary = new double[FftLength];
        int copyCount = Math.Min(samples.Length, FftLength);
        for (int i = 0; i < copyCount; i++)
        {
            // Hamming window, matching the original visualizer's 4096-sample frame.
            double window = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (FftLength - 1));
            real[i] = samples[i] * window;
        }

        Transform(real, imaginary);

        float[] bars = new float[barCount];
        double minFrequency = 40;
        double maxFrequency = Math.Min(8000, sampleRate / 2.0);
        if (maxFrequency <= minFrequency)
            maxFrequency = minFrequency + 1;
        double frequencyPerBin = (double)sampleRate / FftLength;
        float minDb = sensitivity * -10f - 30f;
        float maxDb = peak * 10f - 30f;
        if (maxDb <= minDb)
            maxDb = minDb + 1;

        for (int i = 0; i < barCount; i++)
        {
            double startFrequency = minFrequency * Math.Pow(maxFrequency / minFrequency, (double)i / barCount);
            double endFrequency = minFrequency * Math.Pow(maxFrequency / minFrequency, (double)(i + 1) / barCount);
            int startBin = Math.Clamp((int)(startFrequency / frequencyPerBin), 0, FftLength / 2 - 2);
            int endBin = Math.Clamp((int)(endFrequency / frequencyPerBin), startBin + 1, FftLength / 2 - 1);
            double maxAmplitude = 0;
            for (int bin = startBin; bin < endBin; bin++)
            {
                double amplitude = Math.Sqrt(real[bin] * real[bin] + imaginary[bin] * imaginary[bin]);
                if (amplitude > maxAmplitude)
                    maxAmplitude = amplitude;
            }

            maxAmplitude *= 1 + (double)i / barCount * 75;
            maxAmplitude = Math.Max(maxAmplitude, 0.001);
            float db = 20f * (float)Math.Log10(maxAmplitude);
            bars[i] = Math.Clamp((db - minDb) / (maxDb - minDb), 0f, 1f);
        }

        if (previousBars.Length == barCount)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = bars[i] > previousBars[i]
                    ? bars[i]
                    : previousBars[i] * 0.8f + bars[i] * 0.2f;
            }
        }

        return bars;
    }

    private static void Transform(double[] real, double[] imaginary)
    {
        for (int i = 1, j = 0; i < FftLength; i++)
        {
            int bit = FftLength >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
        }

        for (int length = 2; length <= FftLength; length <<= 1)
        {
            double angle = -2 * Math.PI / length;
            double wReal = Math.Cos(angle);
            double wImaginary = Math.Sin(angle);
            for (int start = 0; start < FftLength; start += length)
            {
                double currentReal = 1;
                double currentImaginary = 0;
                int half = length >> 1;
                for (int i = 0; i < half; i++)
                {
                    int even = start + i;
                    int odd = even + half;
                    double oddReal = real[odd] * currentReal - imaginary[odd] * currentImaginary;
                    double oddImaginary = real[odd] * currentImaginary + imaginary[odd] * currentReal;
                    real[odd] = real[even] - oddReal;
                    imaginary[odd] = imaginary[even] - oddImaginary;
                    real[even] += oddReal;
                    imaginary[even] += oddImaginary;
                    (currentReal, currentImaginary) = (
                        currentReal * wReal - currentImaginary * wImaginary,
                        currentReal * wImaginary + currentImaginary * wReal);
                }
            }
        }
    }
}

public sealed class FrameRateLimiter
{
    private readonly TimeSpan _minimumFrameTime;
    private DateTimeOffset _lastFrame;

    public FrameRateLimiter(int framesPerSecond = FftProcessor.TargetFramesPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framesPerSecond);
        _minimumFrameTime = TimeSpan.FromSeconds(1d / framesPerSecond);
    }

    public bool ShouldPublish(DateTimeOffset timestamp)
    {
        if (_lastFrame == default || timestamp - _lastFrame >= _minimumFrameTime)
        {
            _lastFrame = timestamp;
            return true;
        }
        return false;
    }
}
