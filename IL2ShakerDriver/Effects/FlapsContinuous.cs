using System.Numerics;
using IL2ShakerDriver.Samplers;
using IL2TelemetryRelay.State;
using NAudio.Wave;

namespace IL2ShakerDriver.Effects;

internal class FlapsContinuous : Effect
{
    private readonly HarmonicsGenerator _harmonicsGenerator = new(2, 3, 4);
    private          bool               _active = false;
    private float _previousCoeff = 0;

    private const float ActuatingBaseFreq = 28;

    private Vector4 _actuatingAmplitudes;

    public FlapsContinuous(ISampleProvider source, Audio audio) : base(source, audio)
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
        bool gearDown = false;
        for (int i = 0; i < 4; i++) {
            if (stateData.LandingGearPosition[i] > 0) {
                gearDown = true;
            }
        }

        float position = stateData.FlapsPosition;
        bool active = position != 0 && !gearDown;
        bool wasActive = _active;
        float coeff = 0;
        if (active)
        {
            //start at 50% of max volume and go up
            coeff = (float)0.75 + position * (float)0.25;
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
        else if (active)
        {
            Vector4 amplitudes = new Vector4(_actuatingAmplitudes[0] * coeff,
                                                _actuatingAmplitudes[1] * coeff,
                                                _actuatingAmplitudes[2] * coeff,
                                                _actuatingAmplitudes[3] * coeff);

            if (!wasActive || coeff != _previousCoeff)
            {
                Logging.At(this).Debug("position {Pos} coeff {c}", position, coeff);
                _harmonicsGenerator.SetTarget(ActuatingBaseFreq, amplitudes, 0.25f);
            }
        }

        _active = active;
        _previousCoeff = coeff;
    }
}