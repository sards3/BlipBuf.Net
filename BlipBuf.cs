namespace BlipBufDotNet;

/// <summary>
/// BlipBuf assists in the resampling of high-frequency PSG-like audio (as in 1980s sound chips).
/// </summary>
public class BlipBuf
{
    /// <summary>
    /// Creates a new <see cref="BlipBuf"/> that can hold at most <paramref name="sampleCount"/> samples.
    /// Sets rates so that there are <see cref="MaxRatio"/> clocks per sample.
    /// </summary>
    /// <param name="sampleCount">The number of output samples the buffer can hold.</param>
    public BlipBuf(int sampleCount) : this(sampleCount, MaxRatio, 1.0)
    {
    }

    /// <summary>
    /// Creates a new <see cref="BlipBuf"/> that can hold at most <paramref name="sampleCount"/> samples
    /// and sets approximate input clock rate and output sample rate.
    /// </summary>
    /// <param name="sampleCount">The number of output samples the buffer can hold.</param>
    /// <param name="clockRate">The input clock rate in Hz.</param>
    /// <param name="sampleRate">The output sample rate in Hz.</param>
    public BlipBuf(int sampleCount, double clockRate, double sampleRate)
    {
        if (sampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));
        SetRates(clockRate, sampleRate);
        buffer = new int[sampleCount + BufExtra];
        size = sampleCount;
        Clear();
    }

    /// <summary>
    /// Number of buffered samples available for reading.
    /// </summary>
    public int SamplesAvailable { get; private set; }

    /// <summary>
    /// Maximum ClockRate/SampleRate ratio. For a given SampleRate, ClockRate must not be greater than SampleRate * MaxRatio.
    /// </summary>
    public const int MaxRatio = 1 << 20;

    /// <summary>
    /// Maximum number of samples that can be generated from one time frame. 
    /// </summary>
    public const int MaxFrame = 4000;
    
    /// <summary>
    /// Sets approximate input clock rate and output sample rate. For every <paramref name="clockRate"/> input clocks, approximately <paramref name="sampleRate"/> samples are generated.
    /// </summary>
    /// <param name="clockRate">The input clock rate in Hz.</param>
    /// <param name="sampleRate">The output sample rate in Hz.</param>
    public void SetRates(double clockRate, double sampleRate)
    {
        double factor = TimeUnit * sampleRate / clockRate;
        ulong factorInt = (ulong)factor;

        if (factor - factorInt < 0 || factor - factorInt >= 1) throw new ArgumentOutOfRangeException($"factorInt: {factorInt}, factor: {this.factor}");

        if (factorInt < factor)
            factorInt++;

        this.factor = factorInt;
    }

    /// <summary>
    /// Clears the entire buffer.
    /// </summary>
    public void Clear() 
    {
        offset = factor / 2;
        SamplesAvailable = 0;
        integrator = 0;
        buffer.AsSpan().Clear();
    }
    
    /// <summary>
    /// Adds positive/negative delta into buffer at specified clock time.
    /// </summary>
    /// <param name="clockTime">The clock time relative to the start of the frame.</param>
    /// <param name="delta">The output delta.</param>
    public void AddDelta(uint clockTime, int delta)
    {
        uint fixedVal = (uint) ((clockTime * factor + offset) >> PreShift);
        var outIndex = SamplesAvailable + (fixedVal >> FracBits);
        if (outIndex >= buffer.Length) throw new ArgumentOutOfRangeException(nameof(clockTime));

	    int phaseShift = FracBits - PhaseBits;
	    int phase = (int) ((fixedVal >> phaseShift) & (PhaseCount - 1));
	    int interp = (int) ((fixedVal >> (phaseShift - DeltaBits)) & (DeltaUnit - 1));
	    int delta2 = (delta * interp) >> DeltaBits;
	    delta -= delta2;

        // Sinc_Generator( 0.9, 0.55, 4.5 )
        ReadOnlySpan<short> bandLimitedSteps =
        [
            43, -115,  350, -488, 1136, -914,  5861, 21022,
            44, -118,  348, -473, 1076, -799,  5274, 21001,
            45, -121,  344, -454, 1011, -677,  4706, 20936,
            46, -122,  336, -431,  942, -549,  4156, 20829,
            47, -123,  327, -404,  868, -418,  3629, 20679,
            47, -122,  316, -375,  792, -285,  3124, 20488,
            47, -120,  303, -344,  714, -151,  2644, 20256,
            46, -117,  289, -310,  634,  -17,  2188, 19985,
            46, -114,  273, -275,  553,  117,  1758, 19675,
            44, -108,  255, -237,  471,  247,  1356, 19327,
            43, -103,  237, -199,  390,  373,   981, 18944,
            42,  -98,  218, -160,  310,  495,   633, 18527,
            40,  -91,  198, -121,  231,  611,   314, 18078,
            38,  -84,  178,  -81,  153,  722,    22, 17599,
            36,  -76,  157,  -43,   80,  824,  -241, 17092,
            34,  -68,  135,   -3,    8,  919,  -476, 16558,
            32,  -61,  115,   34,  -60, 1006,  -683, 16001,
            29,  -52,   94,   70, -123, 1083,  -862, 15422,
            27,  -44,   73,  106, -184, 1152, -1015, 14824,
            25,  -36,   53,  139, -239, 1211, -1142, 14210,
            22,  -27,   34,  170, -290, 1261, -1244, 13582,
            20,  -20,   16,  199, -335, 1301, -1322, 12942,
            18,  -12,   -3,  226, -375, 1331, -1376, 12293,
            15,   -4,  -19,  250, -410, 1351, -1408, 11638,
            13,    3,  -35,  272, -439, 1361, -1419, 10979,
            11,    9,  -49,  292, -464, 1362, -1410, 10319,
             9,   16,  -63,  309, -483, 1354, -1383,  9660,
             7,   22,  -75,  322, -496, 1337, -1339,  9005,
             6,   26,  -85,  333, -504, 1312, -1280,  8355,
             4,   31,  -94,  341, -507, 1278, -1205,  7713,
             3,   35, -102,  347, -506, 1238, -1119,  7082,
             1,   40, -110,  350, -499, 1190, -1021,  6464,
             0,   43, -115,  350, -488, 1136,  -914,  5861
        ];

        unsafe
        {
            fixed (int* outPtr = &buffer[outIndex])
            fixed (short* inPtr = &bandLimitedSteps[phase * HalfWidth])
            fixed (short* revPtr = &bandLimitedSteps[(PhaseCount - phase) * HalfWidth])
            {
                outPtr [0] += inPtr[0] * delta + inPtr[HalfWidth + 0] * delta2;
                outPtr [1] += inPtr[1] * delta + inPtr[HalfWidth + 1] * delta2;
                outPtr [2] += inPtr[2] * delta + inPtr[HalfWidth + 2] * delta2;
                outPtr [3] += inPtr[3] * delta + inPtr[HalfWidth + 3] * delta2;
                outPtr [4] += inPtr[4] * delta + inPtr[HalfWidth + 4] * delta2;
                outPtr [5] += inPtr[5] * delta + inPtr[HalfWidth + 5] * delta2;
                outPtr [6] += inPtr[6] * delta + inPtr[HalfWidth + 6] * delta2;
                outPtr [7] += inPtr[7] * delta + inPtr[HalfWidth + 7] * delta2;
                
                outPtr [ 8] += revPtr[7] * delta + revPtr[7 - HalfWidth] * delta2;
                outPtr [ 9] += revPtr[6] * delta + revPtr[6 - HalfWidth] * delta2;
                outPtr [10] += revPtr[5] * delta + revPtr[5 - HalfWidth] * delta2;
                outPtr [11] += revPtr[4] * delta + revPtr[4 - HalfWidth] * delta2;
                outPtr [12] += revPtr[3] * delta + revPtr[3 - HalfWidth] * delta2;
                outPtr [13] += revPtr[2] * delta + revPtr[2 - HalfWidth] * delta2;
                outPtr [14] += revPtr[1] * delta + revPtr[1 - HalfWidth] * delta2;
                outPtr [15] += revPtr[0] * delta + revPtr[0 - HalfWidth] * delta2;
            }
        }
    }

    /// <summary>
    /// Same as <see cref="AddDelta(uint, int)"/>, but uses faster, lower-quality synthesis.
    /// </summary>
    /// <param name="clockTime">The clock time relative to the start of the frame.</param>
    /// <param name="delta">The output delta.</param>
    public void AddDeltaFast(uint clockTime, int delta)
    {
        uint fixedVal = (uint) ((clockTime * factor + offset) >> PreShift);
        int bufIndex = (int) (SamplesAvailable + (fixedVal >> FracBits));
        int interp = (int) ((fixedVal >> (FracBits - DeltaBits)) & (DeltaUnit - 1));
        int delta2 = delta * interp;
	    buffer[bufIndex + 7] += delta * DeltaUnit - delta2;
	    buffer[bufIndex + 8] += delta2;
    }

    /// <summary>
    /// Length of time frame, in clocks, needed to make <paramref name="sampleCount"/> additional samples available.
    /// </summary>
    /// <param name="sampleCount">The number of samples.</param>
    public int GetClocksNeeded(int sampleCount)
    {
        if (sampleCount < 0 || SamplesAvailable + sampleCount > size) throw new ArgumentOutOfRangeException(nameof(sampleCount));

        ulong needed = (ulong)sampleCount * TimeUnit;
        if (needed < offset)
            return 0;

        return (int) ((needed - offset + factor - 1) / factor);
    }

    /// <summary>
    /// Makes input clocks before <paramref name="clockDuration"/> available for reading as output
    /// samples. Also begins new time frame at <paramref name="clockDuration"/>, so that clock time 0 in
    /// the new time frame specifies the same clock as <paramref name="clockDuration"/> in the old timeframe 
    /// specified. Deltas can have been added slightly past <paramref name="clockDuration"/> (up to however many clocks there are in two output samples).
    /// </summary>
    /// <param name="clockDuration">The number of clocks in the frame.</param>
    public void EndFrame(uint clockDuration)
    {
        ulong off = clockDuration * factor + offset;
        SamplesAvailable += (int) (off >> TimeBits);
        offset = off & (TimeUnit - 1);
        if (SamplesAvailable > size) throw new InvalidOperationException();
    }    

    /// <summary>
    /// Reads and removes at most <paramref name="sampleCount"/> samples and writes them to <paramref name="outputBuffer"/>. Outputs 16-bit signed samples.
    /// </summary>
    /// <param name="outputBuffer">The buffer into which output samples will be written.</param>
    /// <param name="sampleCount">The number of samples to render to the output buffer.</param>
    /// <param name="stereo">If true, write samples to every other element of <paramref name="outputBuffer"/>, allowing easy interleaving of two buffers into a stereo sample stream.</param>
    /// <returns>The number of samples actually written to <paramref name="outputBuffer"/>.</returns>
    public int ReadSamples(Span<short> outputBuffer, int sampleCount, bool stereo)
    {
        if (sampleCount < 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));

        if (sampleCount > SamplesAvailable)
            sampleCount = SamplesAvailable;

        if (sampleCount == 0)
            return 0;

        int step = stereo ? 2 : 1;
        int sum = integrator;

        unsafe
        {
            fixed (int* buf = this.buffer)
            fixed (short* outBuf = outputBuffer)
            {
                int* inBufPtr = buf;
                short* outBufPtr = outBuf;
                int* end = inBufPtr + sampleCount;

                do
                {                    
                    int s = sum >> DeltaBits; // Eliminate fraction
                    sum += *inBufPtr++;
                    s = Math.Clamp(s, short.MinValue, short.MaxValue);
                    *outBufPtr = (short)s;
			        outBufPtr += step;
                    sum -= s << (DeltaBits - BassShift); // High-pass filter
                }
                while (inBufPtr != end);
            }
        }        

        integrator = sum;

        int remain = SamplesAvailable + BufExtra - sampleCount;
        SamplesAvailable -= sampleCount;
        var destBufSpan = buffer.AsSpan(0, remain);
        var srcBufSpan = buffer.AsSpan(sampleCount, remain);
        var remainBufSpan = buffer.AsSpan(remain, sampleCount);
        srcBufSpan.CopyTo(destBufSpan);
        remainBufSpan.Clear();

        return sampleCount;
    }

    /// <summary>
    /// Reads and removes at most <paramref name="sampleCount"/> samples and writes them to <paramref name="outputBuffer"/>. Outputs 32-bit floating point samples.
    /// </summary>
    /// <param name="outputBuffer">The buffer into which output samples will be written.</param>
    /// <param name="sampleCount">The number of samples to render to the output buffer.</param>
    /// <param name="stereo">If true, write samples to every other element of <paramref name="outputBuffer"/>, allowing easy interleaving of two buffers into a stereo sample stream.</param>
    /// <returns>The number of samples actually written to <paramref name="outputBuffer"/>.</returns>
    public int ReadFloatSamples(Span<float> outputBuffer, int sampleCount, bool stereo)
    {
        if (sampleCount < 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));

        if (sampleCount > SamplesAvailable)
            sampleCount = SamplesAvailable;

        if (sampleCount == 0)
            return 0;

        int step = stereo ? 2 : 1;
        int sum = integrator;

        unsafe
        {
            fixed (int* buf = this.buffer)
            fixed (float* outBuf = outputBuffer)
            {
                int* inBufPtr = buf;
                float* outBufPtr = outBuf;
                int* end = inBufPtr + sampleCount;

                do
                {
                    int s = sum >> DeltaBits; // Eliminate fraction
                    sum += *inBufPtr++;
                    s = Math.Clamp(s, short.MinValue, short.MaxValue);
                    const float floatScale = (1.0f / 32768.0f);
                    *outBufPtr = floatScale * s;
                    outBufPtr += step;
                    sum -= s << (DeltaBits - BassShift); // High-pass filter
                }
                while (inBufPtr != end);
            }
        }

        integrator = sum;

        int remain = SamplesAvailable + BufExtra - sampleCount;
        SamplesAvailable -= sampleCount;
        var destBufSpan = buffer.AsSpan(0, remain);
        var srcBufSpan = buffer.AsSpan(sampleCount, remain);
        var remainBufSpan = buffer.AsSpan(remain, sampleCount);
        srcBufSpan.CopyTo(destBufSpan);
        remainBufSpan.Clear();

        return sampleCount;
    }

    private const int PreShift = 32;
    private const int TimeBits = PreShift + 20;
    private const ulong TimeUnit = 1ul << TimeBits;
    private const int BassShift = 9; // Affects high-pass filter breakpoint frequency.    
    private const int EndFrameExtra = 2; // Allows deltas slightly after frame length.
    private const int HalfWidth = 8;
    private const int BufExtra = HalfWidth * 2 + EndFrameExtra;
    private const int PhaseBits = 5;
    private const int PhaseCount = 1 << PhaseBits;
    private const int DeltaBits = 15;
    private const int DeltaUnit = 1 << DeltaBits;
    private const int FracBits = TimeBits - PreShift;

    private int[] buffer;
    private ulong factor;
    private ulong offset;    
    private readonly int size;
    private int integrator;    
}
