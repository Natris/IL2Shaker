using System.Numerics;
using IL2ShakerDriver.Samplers;
using IL2TelemetryRelay.State;
using NAudio.Wave;
using System.Media;
using System;
using System.IO;
using System.Media;
using System.Reflection;

namespace IL2ShakerDriver.Effects;

internal class FlapsContinuous : Effect
{
    private readonly List<ImpulseGenerator>   _impulseGenerators   = new();

    private readonly HarmonicsGenerator _harmonicsGenerator = new(2, 3, 4);
    private          bool               _active = false;
    private bool _flapsHigh = false;
    int _flapsPos = 0;

    private const float SwitchedFreq = 100;

    private const float ActuatingBaseFreq = 28;
    private Volume  _switchedVolume;

    private Vector4 _actuatingAmplitudes;

    public FlapsContinuous(ISampleProvider source, Audio audio) : base(source, audio)
    {
    }

    protected override void OnSettingsUpdated()
    {
        _actuatingAmplitudes = new Vector4(0, GetAmplitude(-18), GetAmplitude(-9), 0);
        _switchedVolume      = GetVolume(0);
    }

    protected override void Write(float[] buffer, int offset, int count)
    {
         for (int i = 0; i < _impulseGenerators.Count; i++)
        {
            if (_impulseGenerators[i].Complete)
            {
                _impulseGenerators.RemoveAt(i--);
                continue;
            }

            _impulseGenerators[i].Write(buffer, offset, count, Audio.SimClock.Time);
        }
       // Wait 200ms before reducing the output when not at x1 to avoid hiccups due to the sim struggling
        if (Audio.TicksAtAbnormalSpeed > 10)
            _harmonicsGenerator.SetTarget(0, Vector4.Zero, 0.1f);
        _harmonicsGenerator.Write(buffer, offset, count, Audio.SimClock.Time);
    }

    protected override void OnStateDataReceived(StateData stateData)
    {
        bool gearDown = false;
        for (int i = 0; i < 4; i++)
        {
            if (stateData.LandingGearPosition[i] > 0)
            {
                gearDown = true;
            }
        }

        float position = stateData.FlapsPosition;
        bool active = position != 0 && !gearDown;
        bool wasActive = _active;
        bool wasFlapsHigh = _flapsHigh;
        bool flapsHigh = active && position > 0.25f;
        int flapsPos = 0;
        int oldFlapsPos = _flapsPos;
        if (active)
        {
            if (position > 0.4f)
            {
                flapsPos = 5;
            }
            else if (position == 0.4f)
            {
                flapsPos = 4;
            }
            else if (position > 0.2f)
            {
                flapsPos = 3;
            }
            else if (position == 0.2f)
            {
                flapsPos = 2;
            }
            else
            {
                flapsPos = 1;
            }
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
            float coeff = flapsHigh ? 0.16f : 0.19f;
            Vector4 amplitudes = new Vector4(_actuatingAmplitudes[0] * coeff,
                                                _actuatingAmplitudes[1] * coeff,
                                                _actuatingAmplitudes[2] * coeff,
                                                _actuatingAmplitudes[3] * coeff);

            if (!wasActive || wasFlapsHigh != flapsHigh)
            {
                if (wasFlapsHigh != flapsHigh)
                {
                    // Play flaps position change sound
                    float amp = Attenuate(SwitchedFreq, _switchedVolume.Amplitude, 1);
                    _impulseGenerators.Add(new ImpulseGenerator(SwitchedFreq, amp, 7, 5));
                    _impulseGenerators.Add(new ImpulseGenerator(SwitchedFreq, amp, 7, 5, 0.2f));
                }

                Logging.At(this).Debug("position {Pos}", position);
                _harmonicsGenerator.SetTarget(ActuatingBaseFreq, amplitudes, 0.25f);
            }
            if (flapsPos != oldFlapsPos)
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "";
                if ((flapsPos >= 2 && oldFlapsPos < 2) || (flapsPos <= 2 && oldFlapsPos > 2))
                {
                    resourceName = "IL2ShakerDriver.Resources.2.wav";
                }
                else if ((flapsPos >= 4 && oldFlapsPos < 4) || (flapsPos <= 4 && oldFlapsPos > 4))
                {
                    resourceName = "IL2ShakerDriver.Resources.4.wav";
                }
                if (resourceName.Length > 0)
                {
                    Logging.At(this).Debug("flapPos {fp} old {ofp}, res {r}", flapsPos, oldFlapsPos, resourceName);
                    using Stream stream = assembly.GetManifestResourceStream(resourceName)!;

                    SoundPlayer player = new SoundPlayer(stream);
                    player.Play();
                }
            }
        }

        _active = active;
        _flapsHigh = flapsHigh;
        _flapsPos = flapsPos;
    }
}