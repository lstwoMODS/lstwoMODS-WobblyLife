using System;

namespace WLProxChat.Audio
{
    /// <summary>
    /// Everything that happens to captured audio between the microphone and the encoder: rumble
    /// filtering, a noise gate driven by a tracked noise floor, automatic gain, and a limiter.
    /// <para>
    /// This is the part Steam's voice API never let us at. It runs on the capture thread, one sample
    /// at a time rather than per block, so every control signal is continuous and there is no seam
    /// where one buffer ends and the next begins.
    /// </para>
    /// <para>
    /// Settings are plain fields written from the game thread and read here. Every one of them is a
    /// single float or bool, so a torn read is impossible and the worst a mid-buffer change can do is
    /// take effect one sample early.
    /// </para>
    /// </summary>
    public sealed class VoicePreprocessor
    {
        #region Tuning

        /// <summary>Rumble filter corner. Below this a voice has nothing but handling noise and hum.</summary>
        private const float HighPassHz = 90f;

        /// <summary>Butterworth. Flat, with no resonance at the corner.</summary>
        private const float HighPassQ = 0.7071f;

        /// <summary>Window the level meter and the gate both work from.</summary>
        private const float EnvelopeTau = 0.020f;

        /// <summary>The floor follows a drop almost at once and a rise slowly, so speech cannot drag it up.</summary>
        private const float FloorFallTau = 0.100f;
        private const float FloorRiseTau = 4.000f;

        /// <summary>Clamps on the tracked floor, as linear RMS. Stops a digitally silent input from
        /// making the gate infinitely sensitive, and a very noisy one from gating out speech.</summary>
        private const float FloorMin = 0.00018f;  // about -75 dBFS
        private const float FloorMax = 0.05623f;  // about -25 dBFS

        /// <summary>How far the close threshold sits below the open threshold.</summary>
        private const float GateHysteresisDb = 6f;

        /// <summary>How long the gate stays open after the level drops, so word gaps do not chop.</summary>
        private const float GateHangoverSeconds = 0.250f;

        private const float GateAttackTau = 0.005f;
        private const float GateReleaseTau = 0.060f;

        /// <summary>Level automatic gain aims for. Leaves headroom for peaks well above the average.</summary>
        private const float AgcTargetRms = 0.1f;    // -20 dBFS
        private const float AgcMaxGain = 10f;       // +20 dB
        private const float AgcMinGain = 0.25f;     // -12 dB
        private const float AgcRiseTau = 2.0f;
        private const float AgcFallTau = 0.100f;

        /// <summary>Peak ceiling. Just under full scale, so nothing clips on the way into the encoder.</summary>
        private const float LimiterCeiling = 0.891f; // -1 dBFS
        private const float LimiterReleaseTau = 0.200f;

        #endregion

        #region Settings (game thread)

        /// <summary>Fixed gain applied before everything else, in decibels.</summary>
        public float InputGainDb;

        /// <summary>Whether the noise gate attenuates, and whether it gates transmission at all.</summary>
        public bool NoiseGateEnabled = true;

        /// <summary>How far above the tracked noise floor the level has to rise to open the gate.</summary>
        public float GateThresholdDb = 9f;

        public bool AutoGainEnabled = true;

        /// <summary>
        /// Holds the gate open regardless of level. Push to talk sets this: holding the key is a
        /// statement of intent, and a gate that swallowed the first syllable of every sentence would
        /// be worse than no gate at all.
        /// </summary>
        public bool ForceOpen;

        #endregion

        #region State (capture thread)

        private readonly float envelopeCoeff;
        private readonly float floorFallCoeff;
        private readonly float floorRiseCoeff;
        private readonly float gateAttackCoeff;
        private readonly float gateReleaseCoeff;
        private readonly float agcRiseCoeff;
        private readonly float agcFallCoeff;
        private readonly float limiterReleaseCoeff;
        private readonly int hangoverSamples;

        // High pass biquad, direct form 1.
        private readonly float b0, b1, b2, a1, a2;
        private float x1, x2, y1, y2;

        // Level trackers, kept as mean square so the per sample path has no square root in it.
        private float envelopeMs;

        // Starts at the ceiling rather than the floor. The tracker falls fast and rises slowly, so
        // starting high means the gate is shut for the half second it takes to find the real noise
        // level, while starting low would mean transmitting the room for the several seconds the
        // slow rise needs to catch up. Being briefly deaf beats being briefly noisy.
        private float floorMs = FloorMax * FloorMax;

        private float gateGain;
        private int hangoverLeft;
        private bool gateOpen;

        private float agcGain = 1f;
        private float limiterGain = 1f;

        #endregion

        #region Diagnostics (read from any thread)

        /// <summary>Linear RMS after the input gain and before the gate. What a level meter shows.</summary>
        public float Level { get; private set; }

        /// <summary>Linear RMS the noise floor tracker has settled on.</summary>
        public float NoiseFloor { get; private set; }

        /// <summary>True while the gate is passing audio, whether by level or by <see cref="ForceOpen"/>.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Gain automatic gain control is currently applying, linear.</summary>
        public float AutoGain => agcGain;

        #endregion

        public VoicePreprocessor()
        {
            envelopeCoeff = Coeff(EnvelopeTau);
            floorFallCoeff = Coeff(FloorFallTau);
            floorRiseCoeff = Coeff(FloorRiseTau);
            gateAttackCoeff = Coeff(GateAttackTau);
            gateReleaseCoeff = Coeff(GateReleaseTau);
            agcRiseCoeff = Coeff(AgcRiseTau);
            agcFallCoeff = Coeff(AgcFallTau);
            limiterReleaseCoeff = Coeff(LimiterReleaseTau);

            hangoverSamples = (int)(GateHangoverSeconds * VoiceFormat.SampleRate);

            // RBJ cookbook high pass.
            var w0 = 2.0 * Math.PI * HighPassHz / VoiceFormat.SampleRate;
            var cos0 = Math.Cos(w0);
            var alpha = Math.Sin(w0) / (2.0 * HighPassQ);
            var a0 = 1.0 + alpha;

            b0 = (float)((1.0 + cos0) / 2.0 / a0);
            b1 = (float)(-(1.0 + cos0) / a0);
            b2 = b0;
            a1 = (float)(-2.0 * cos0 / a0);
            a2 = (float)((1.0 - alpha) / a0);

            NoiseFloor = FloorMax;
        }

        /// <summary>One pole smoothing coefficient for a time constant in seconds.</summary>
        private static float Coeff(float tau)
            => (float)(1.0 - Math.Exp(-1.0 / (tau * VoiceFormat.SampleRate)));

        /// <summary>Drops every filter and follower back to rest, for when capture restarts.</summary>
        public void Reset()
        {
            x1 = x2 = y1 = y2 = 0f;
            envelopeMs = 0f;
            floorMs = FloorMax * FloorMax;
            gateGain = 0f;
            hangoverLeft = 0;
            gateOpen = false;
            agcGain = 1f;
            limiterGain = 1f;
            Level = 0f;
            NoiseFloor = FloorMax;
            IsOpen = false;
        }

        /// <summary>Processes a block of mono samples in place. Capture thread only.</summary>
        public void Process(float[] buffer, int offset, int count)
        {
            if (count <= 0) return;

            var inputGain = InputGainDb == 0f ? 1f : (float)Math.Pow(10.0, InputGainDb / 20.0);

            // Squared once here rather than rooting the level on every sample.
            var openFactor = (float)Math.Pow(10.0, GateThresholdDb / 10.0);
            var closeFactor = (float)Math.Pow(10.0, (GateThresholdDb - GateHysteresisDb) / 10.0);

            var gateEnabled = NoiseGateEnabled;
            var forceOpen = ForceOpen;
            var agcEnabled = AutoGainEnabled;

            for (var i = 0; i < count; i++)
            {
                var index = offset + i;
                var x = buffer[index];

                // Rumble filter.
                var filtered = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x;
                y2 = y1; y1 = filtered;

                var sample = filtered * inputGain;

                // Level and noise floor, both as mean square.
                var ms = sample * sample;
                envelopeMs += (ms - envelopeMs) * envelopeCoeff;

                floorMs += (envelopeMs - floorMs) * (envelopeMs < floorMs ? floorFallCoeff : floorRiseCoeff);

                if (floorMs < FloorMin * FloorMin) floorMs = FloorMin * FloorMin;
                else if (floorMs > FloorMax * FloorMax) floorMs = FloorMax * FloorMax;

                // Gate.
                if (forceOpen || !gateEnabled)
                {
                    gateOpen = true;
                    hangoverLeft = hangoverSamples;
                }
                else if (envelopeMs > floorMs * openFactor)
                {
                    gateOpen = true;
                    hangoverLeft = hangoverSamples;
                }
                else if (envelopeMs < floorMs * closeFactor)
                {
                    if (hangoverLeft > 0) hangoverLeft--;
                    else gateOpen = false;
                }

                gateGain += ((gateOpen ? 1f : 0f) - gateGain)
                          * (gateOpen ? gateAttackCoeff : gateReleaseCoeff);

                // Automatic gain, adapting only on audio the gate is actually passing, so it cannot
                // spend a quiet moment winding itself up onto the room noise.
                if (agcEnabled)
                {
                    if (gateOpen && envelopeMs > floorMs)
                    {
                        var rms = (float)Math.Sqrt(envelopeMs);
                        var wanted = AgcTargetRms / Math.Max(rms, 1e-6f);

                        if (wanted > AgcMaxGain) wanted = AgcMaxGain;
                        else if (wanted < AgcMinGain) wanted = AgcMinGain;

                        agcGain += (wanted - agcGain) * (wanted < agcGain ? agcFallCoeff : agcRiseCoeff);
                    }
                }
                else if (agcGain != 1f)
                {
                    agcGain += (1f - agcGain) * agcFallCoeff;
                }

                var output = sample * agcGain * gateGain;

                // Peak limiter. Clamps at once on the way up and lets go slowly, so one loud syllable
                // does not duck the rest of the sentence.
                var peak = output < 0f ? -output : output;

                if (peak * limiterGain > LimiterCeiling)
                    limiterGain = LimiterCeiling / peak;
                else
                    limiterGain += (1f - limiterGain) * limiterReleaseCoeff;

                output *= limiterGain;

                if (output > 1f) output = 1f;
                else if (output < -1f) output = -1f;

                buffer[index] = output;
            }

            Level = (float)Math.Sqrt(envelopeMs);
            NoiseFloor = (float)Math.Sqrt(floorMs);
            IsOpen = gateOpen;
        }
    }
}
