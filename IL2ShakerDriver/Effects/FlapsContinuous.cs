using System.Numerics;
using IL2ShakerDriver.Samplers;
using IL2TelemetryRelay.State;
using NAudio.Wave;

namespace IL2ShakerDriver.Effects;

internal class FlapsContinuous : Effect
{
    private readonly HarmonicsGenerator _harmonicsGenerator = new(2, 3, 4);
    private          bool               _active;
    private          float              _previousCoeff = 0;

    private const float ActuatingBaseFreq = 28;

    private Vector4 _actuatingAmplitudes;

    public Flaps(ISampleProvider source, Audio audio) : base(source, audio)
    {
    }

    protected override void OnSettingsUpdated()
    {
        _actuatingAmplitudes = new Vector4(0, GetAmplitude(-18), GetAmplitude(-9), 0);
    }

    protected override void Write(float[] buffer, int offset, int count)
    {
       // Wait 200ms before reducing the output when not at x1 to avoid hiccups due to the sim struggling
        if (Audio.TicksAtAbnormalSpeed > 10)
            _harmonicsGenerator.SetTarget(0, Vector4.Zero, 0.1f);
        _harmonicsGenerator.Write(buffer, offset, count, Audio.SimClock.Time);
    }

    protected override void OnStateDataReceived(StateData stateData)
    {
        float position = stateData.FlapsPosition;
        bool active = position != 0;
        bool wasActive = _active;
        float coeff = 0;
        if (active) {
            //start at 20% of max volume and go up
            coeff = 0.2 + position / 2;
        }
        
        if (stateData.Paused && active)
        {
            active = false;
            // Stop the generator
            _harmonicsGenerator.SetTarget(ActuatingBaseFreq, Vector4.Zero, 0.25f);
        }
        else if (!active && wasActive)
        {
            // Stop the generator
            _harmonicsGenerator.SetTarget(ActuatingBaseFreq, Vector4.Zero, 0.25f);
        }
        else if (active && !wasActive)
        {
            Vector4 amplitudes = new Vector4(_actuatingAmplitudes[0] * coeff,
                                             _actuatingAmplitudes[1] * coeff,
                                             _actuatingAmplitudes[2] * coeff,
                                             _actuatingAmplitudes[3] * coeff);
            
            _harmonicsGenerator.SetTarget(ActuatingBaseFreq, amplitudes, 0.25f);
        }

        _active        = active;
        _previousCoeff = coeff;
    }
}