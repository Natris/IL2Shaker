using IL2ShakerDriver.Samplers;
using IL2TelemetryRelay.State;
using NAudio.Wave;

namespace IL2ShakerDriver.Effects;

internal class StallBuffet : Effect
{
    private const    float         FrequencyMultiplier = 4f;
    private const    float         AmplitudeMultiplier = 3.5f;
    private const    float         TransitionTime      = 0.05f;
    private readonly WaveGenerator _waveGenerator = new();


    private DateTime last = DateTime.UtcNow;
    public float max_freq_freq = 0;
    public float max_freq_amp = 0;

    public float max_amp_freq = 0;
    public float max_amp_amp = 0;

    private float _maxAmp;

    public StallBuffet(ISampleProvider source, Audio audio) : base(source, audio)
    {
    }

    protected override void Write(float[] buffer, int offset, int count)
    {
        // Wait 200ms before reducing the output when not at x1 to avoid hiccups due to the sim struggling
        if (Audio.TicksAtAbnormalSpeed > 10)
            _waveGenerator.SetTarget(0, 0, TransitionTime);
        _waveGenerator.Write(buffer, offset, count, Audio.SimClock.Time);
    }

    protected override void OnStateDataReceived(StateData stateData)
    {
        bool isOlderThan500ms = (DateTime.UtcNow - last) > TimeSpan.FromMilliseconds(500);
        if (isOlderThan500ms)
        {
            Logging.At(this).Debug("StallBuffet: max freq f {max_freq_freq} a {max_freq_amp}, max amp f {max_amp_freq} a {max_amp_amp}",
                max_freq_freq, max_freq_amp, max_amp_freq, max_amp_amp);
            max_freq_freq = 0;
            max_freq_amp = 0;
            max_amp_freq = 0;
            max_amp_amp = 0;
            last = DateTime.UtcNow;
        }
        if (max_freq_freq < stateData.StallBuffetFrequency)
        {
            max_freq_freq = stateData.StallBuffetFrequency;
            max_freq_amp = stateData.StallBuffetAmplitude;
        }
        if (max_amp_amp < stateData.StallBuffetAmplitude)
        {
            max_amp_freq = stateData.StallBuffetFrequency;
            max_amp_amp = stateData.StallBuffetAmplitude;
        }
        float nextFreq = stateData.StallBuffetFrequency * FrequencyMultiplier;
        float correctedAmp = stateData.StallBuffetAmplitude;
        if (correctedAmp > 0.10F)
        {
            correctedAmp = 0.10F;
        }
        float nextAmp = correctedAmp * AmplitudeMultiplier;

        if (stateData.StallBuffetAmplitude > _maxAmp)
        {
            _maxAmp = stateData.StallBuffetAmplitude;
            // Logging.At(this).Debug("Stall buffet amplitude suggested multiplier {MaxAmp}",
            //                        1 / _maxAmp);
        }

        if (nextAmp > 1)
        {
            Logging.At(this).Debug("Stall buffet amplitude greater than 1, capping - suggested multiplier {MaxAmp}",
                                   1 / _maxAmp);
            nextAmp = 1;
        }

        nextAmp *= Volume.Amplitude;

        _waveGenerator.SetTarget(nextFreq, nextAmp, TransitionTime);
    }
}