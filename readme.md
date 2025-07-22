# BlipBuf.Net

A .NET port of [blip_buf](https://code.google.com/archive/p/blip-buf/) by Shay Green (aka blargg). 
BlipBuf.Net enables easy high-quality resampling of PSG-like audio (the kind that was present in 1980s-era sound chips, featuring square and sawtooth waves).
Simply set the input clock rate and output sample rate, add waveforms by specifying the clock times where their amplitude changes,
then reads the resulting output samples. BlipBuf.Net uses band-limited synthesis to avoid the aliasing that would typically be present
when resampling this kind of audio. For more information on the algorithm, see Shay Green's articles: [Band-Limited Sound Synthesis](https://www.slack.net/~ant/bl-synth/).

## Usage
#### Install NuGet Package
```
dotnet add package BlipBuf.Net
```

```
Install-Package BlipBuf.Net
```

#### Use BlipBuf.Net
```C#
using BlipBuf.Net;

double clockRate = 1000000.0; // Input clock rate
double sampleRate = 48000.0; // Output sample rate
int bufLength = 4800; // Number of samples the buffer can hold
var blipBuf = new BlipBuf(bufLength, clockRate, sampleRate); // Create a BlipBuf
blipBuf.SetRates(clockRate / 2, sampleRate); // Adjust clock/sample rates if needed.
blipBuf.AddDelta(30, -5000); // Add a delta of -5000 at clock offset 30. Deltas should be in the range of signed 16-bit samples.
blipBuf.AddDeltaFast(30, -5000); // Same as above, but using a faster and lower-quality synthesis.
int clocksNeeded = blipBuf.GetClocksNeeded(100); // Number of clocks needed to make 100 additional samples available.
blipBuf.EndFrame(200); // Makes input clocks before 200 available for reading as output samples, and restarts the frame at 0 clocks.
short[] outputBuffer = new short[200];
blipBuf.ReadSamples(outputBuffer, 200, stereo: false); // Read and remove 200 16-bit signed samples and write them to outputBuffer.
float[] floatBuffer = new float[200]
blipBuf.ReadFloatSamples(floatBuffer, 200, stereo: false); // Same as above, but produces 32-bit floating point samples.
blipBuf.Clear(); // Clear the buffer
```

