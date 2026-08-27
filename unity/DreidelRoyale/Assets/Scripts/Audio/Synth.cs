using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreidelRoyale.Audio
{
    public enum Wave { Sine, Square, Triangle, Sawtooth }
    public enum Bus { Sfx, Music }
    public enum FilterKind { Lowpass, Highpass, Bandpass }

    /// <summary>
    /// A live synthesis graph, mixed on the audio thread.
    ///
    /// The web build's sound is Web Audio nodes driven per-frame — the scrape's pitch rides
    /// the dreidel's RPM, the rumble tracks the charge — so pre-rendering clips would lose
    /// exactly what makes it feel connected. This is the same graph: oscillators with
    /// exponential frequency and gain ramps, filtered noise, two buses and a reverb send.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class Synth : MonoBehaviour
    {
        public static Synth I;

        public const float MusicVol = 0.42f;

        public float MasterGain = 0.85f;
        public float SfxGain = 1f;
        public float MusicGain = MusicVol;

        int _rate = 48000;
        readonly object _lock = new object();
        readonly List<Voice> _voices = new List<Voice>();
        readonly List<Voice> _pending = new List<Voice>();
        Reverb _reverb;

        /// <summary>Audio-thread clock in seconds — the equivalent of AudioContext.currentTime.</summary>
        public double Now { get { return _now; } }
        double _now;

        void Awake()
        {
            I = this;
            _rate = AudioSettings.outputSampleRate;
            if (_rate <= 0) _rate = 48000;
            _reverb = new Reverb(_rate);
        }

        public void Add(Voice v)
        {
            if (v == null) return;
            lock (_lock) _pending.Add(v);
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (_lock)
            {
                if (_pending.Count > 0) { _voices.AddRange(_pending); _pending.Clear(); }
            }

            int frames = data.Length / channels;
            Array.Clear(data, 0, data.Length);

            float sfx = SfxGain, music = MusicGain, master = MasterGain;

            for (int f = 0; f < frames; f++)
            {
                float dry = 0f, wet = 0f;

                for (int i = _voices.Count - 1; i >= 0; i--)
                {
                    var v = _voices[i];
                    if (v.Done) { _voices.RemoveAt(i); continue; }
                    float s = v.Render(_now);
                    if (s == 0f && v.Done) { _voices.RemoveAt(i); continue; }
                    float busGain = v.Destination == Bus.Music ? music : sfx;
                    dry += s * busGain;
                    if (v.Verb > 0f) wet += s * v.Verb * busGain;
                }

                float outSample = (dry + _reverb.Process(wet) * 0.9f) * master;
                // The graph can stack a dozen voices on a jackpot; a soft knee keeps that
                // from clipping without pumping the quiet moments.
                outSample = SoftClip(outSample);

                for (int c = 0; c < channels; c++) data[f * channels + c] += outSample;
                _now += 1.0 / _rate;
            }
        }

        static float SoftClip(float x)
        {
            if (x > 1f || x < -1f) return Mathf.Sign(x) * (1f - 1f / (Mathf.Abs(x) + 1f) * 0.5f);
            return x - (x * x * x) / 3f * 0.35f;
        }

        public int Rate { get { return _rate; } }

        // ---------------------------------------------------------------
        /// <summary>Core tone with an optional pitch ramp and reverb send.</summary>
        public void Tone(float freq, Wave type, float dur, float vol = 0.1f, float ramp = 0f,
                         float when = 0f, Bus dest = Bus.Sfx, float verb = 0f)
        {
            var v = new OscVoice(_rate)
            {
                Type = type, Freq = freq, FreqRamp = ramp, Dur = dur,
                StartAt = _now + when, Destination = dest, Verb = verb
            };
            v.SetEnvExp(vol, 0f, dur);
            Add(v);
        }

        /// <summary>
        /// SNES/Genesis-style blip: a pulse wave with a fast ADSR and optional vibrato — the
        /// workhorse voice.
        /// </summary>
        public void Blip(float freq, float dur, Wave type = Wave.Square, float vol = 0.12f,
                         float attack = 0.005f, float when = 0f, float ramp = 0f,
                         float vibrato = 0f, float vibRate = 6f, Bus dest = Bus.Sfx, float verb = 0f)
        {
            var v = new OscVoice(_rate)
            {
                Type = type, Freq = freq, FreqRamp = ramp, Dur = dur,
                StartAt = _now + when, Destination = dest, Verb = verb,
                Vibrato = vibrato, VibRate = vibRate
            };
            v.SetEnvExp(vol, attack, dur);
            Add(v);
        }

        public void Noise(float dur, float vol, float fStart, float fEnd,
                          FilterKind kind = FilterKind.Bandpass, float when = 0f,
                          Bus dest = Bus.Sfx, float verb = 0f)
        {
            var v = new NoiseVoice(_rate)
            {
                Dur = dur, StartAt = _now + when, Destination = dest, Verb = verb,
                Kind = kind, FStart = fStart, FEnd = Mathf.Max(fEnd, 20f), Q = 1f
            };
            v.SetEnvExp(vol, 0f, dur);
            Add(v);
        }

        /// <summary>A voice that runs until stopped, for the scrape and the rumble.</summary>
        public LoopVoice StartLoop(bool noise, Wave type = Wave.Sine)
        {
            var v = new LoopVoice(_rate) { UseNoise = noise, Type = type, StartAt = _now };
            Add(v);
            return v;
        }
    }

    // ===============================================================
    public abstract class Voice
    {
        public double StartAt;
        public float Dur = 1f;
        public Bus Destination = Bus.Sfx;
        public float Verb;
        public bool Done;

        protected readonly int Rate;
        protected double T;                 // seconds since this voice started sounding
        protected bool Started;

        // gain envelope, mirroring setValueAtTime + exponentialRampToValueAtTime
        protected float EnvPeak = 0.1f, EnvAttack, EnvEnd;
        protected const float EnvFloor = 0.0001f;

        protected Voice(int rate) { Rate = rate; }

        public void SetEnvExp(float peak, float attack, float end)
        {
            EnvPeak = Mathf.Max(peak, EnvFloor * 2f);
            EnvAttack = attack;
            EnvEnd = end;
        }

        protected float Envelope(double t)
        {
            if (EnvAttack > 0f)
            {
                if (t < EnvAttack) return ExpRamp(EnvFloor, EnvPeak, (float)(t / EnvAttack));
                float span = Mathf.Max(EnvEnd - EnvAttack, 1e-5f);
                return ExpRamp(EnvPeak, EnvFloor, Mathf.Clamp01((float)(t - EnvAttack) / span));
            }
            return ExpRamp(EnvPeak, 0.001f, Mathf.Clamp01((float)(t / Mathf.Max(EnvEnd, 1e-5f))));
        }

        /// <summary>Web Audio's exponentialRampToValueAtTime, sampled at k in 0..1.</summary>
        protected static float ExpRamp(float from, float to, float k)
        {
            from = Mathf.Max(from, EnvFloor); to = Mathf.Max(to, EnvFloor);
            return from * Mathf.Pow(to / from, k);
        }

        public float Render(double now)
        {
            if (now < StartAt) return 0f;
            if (!Started) { Started = true; T = 0.0; }
            float s = Sample();
            T += 1.0 / Rate;
            if (T > Dur + 0.05f) Done = true;
            return s;
        }

        protected abstract float Sample();
    }

    // ---------------------------------------------------------------
    public class OscVoice : Voice
    {
        public Wave Type = Wave.Square;
        public float Freq = 440f, FreqRamp;
        public float Vibrato, VibRate = 6f;
        public float Detune;              // cents, for the two-oscillator clarinet lead

        double _phase, _vibPhase;

        public OscVoice(int rate) : base(rate) { }

        protected override float Sample()
        {
            float f = Freq;
            if (FreqRamp > 0f) f = ExpRamp(Freq, Mathf.Max(FreqRamp, 1f), Mathf.Clamp01((float)(T / Mathf.Max(Dur, 1e-5f))));
            if (Vibrato > 0f)
            {
                f += Mathf.Sin((float)_vibPhase) * Vibrato;
                _vibPhase += 2.0 * Math.PI * VibRate / Rate;
            }
            if (Detune != 0f) f *= Mathf.Pow(2f, Detune / 1200f);

            float s = Osc(Type, _phase);
            _phase += 2.0 * Math.PI * Mathf.Max(f, 1f) / Rate;
            if (_phase > Math.PI * 2.0) _phase -= Math.PI * 2.0;

            return s * Envelope(T);
        }

        public static float Osc(Wave w, double phase)
        {
            float p = (float)(phase % (Math.PI * 2.0));
            switch (w)
            {
                case Wave.Sine: return Mathf.Sin(p);
                case Wave.Square: return p < Mathf.PI ? 1f : -1f;
                case Wave.Triangle:
                {
                    // rises 0 -> 1 -> 0 -> -1 -> 0, matching Web Audio's triangle phase
                    float u = p / (2f * Mathf.PI) * 4f;
                    if (u < 1f) return u;
                    if (u < 3f) return 2f - u;
                    return u - 4f;
                }
                case Wave.Sawtooth: return 2f * (p / (2f * Mathf.PI)) - 1f;
            }
            return 0f;
        }
    }

    // ---------------------------------------------------------------
    public class NoiseVoice : Voice
    {
        public FilterKind Kind = FilterKind.Bandpass;
        public float FStart = 1000f, FEnd = 1000f, Q = 1f;

        readonly Biquad _filter = new Biquad();
        int _recalc;

        public NoiseVoice(int rate) : base(rate) { }

        protected override float Sample()
        {
            if (_recalc-- <= 0)
            {
                _recalc = 32;               // recompute coefficients every 32 samples
                float f = ExpRamp(FStart, FEnd, Mathf.Clamp01((float)(T / Mathf.Max(Dur, 1e-5f))));
                _filter.Set(Kind, f, Q, Rate);
            }
            float n = UnityEngine.Random.value * 2f - 1f;
            return _filter.Process(n) * Envelope(T);
        }
    }

    // ---------------------------------------------------------------
    /// <summary>
    /// A held voice whose pitch and level are written from the game loop every frame — the
    /// wood-on-wood scrape that rides the RPM, and the low rumble that tracks the wind-up.
    /// </summary>
    public class LoopVoice : Voice
    {
        public bool UseNoise;
        public Wave Type = Wave.Sine;

        public volatile float TargetFreq = 400f;
        public volatile float TargetGain;
        public volatile float FilterFreq = 900f;
        public volatile float Q = 1.4f;
        public volatile bool Stopping;

        float _gain, _freq = 400f, _ffreq = 900f;
        double _phase;
        readonly Biquad _filter = new Biquad();
        int _recalc;

        public LoopVoice(int rate) : base(rate) { Dur = float.MaxValue; }

        public void Stop() { Stopping = true; }

        protected override float Sample()
        {
            // one-pole smoothing, so a parameter written per frame doesn't zipper
            const float k = 0.002f;
            _gain += ((Stopping ? 0f : TargetGain) - _gain) * k;
            _freq += (TargetFreq - _freq) * k;
            _ffreq += (FilterFreq - _ffreq) * k;

            if (Stopping && _gain < 0.0002f) { Done = true; return 0f; }

            float s;
            if (UseNoise)
            {
                if (_recalc-- <= 0) { _recalc = 32; _filter.Set(FilterKind.Bandpass, _ffreq, Q, Rate); }
                s = _filter.Process(UnityEngine.Random.value * 2f - 1f);
            }
            else
            {
                s = OscVoice.Osc(Type, _phase);
                _phase += 2.0 * Math.PI * Mathf.Max(_freq, 1f) / Rate;
                if (_phase > Math.PI * 2.0) _phase -= Math.PI * 2.0;
            }
            return s * _gain;
        }
    }

    // ---------------------------------------------------------------
    /// <summary>RBJ cookbook biquad — the Web Audio BiquadFilterNode shapes the port uses.</summary>
    public class Biquad
    {
        float _a0, _a1, _a2, _b1, _b2;
        float _x1, _x2, _y1, _y2;

        public void Set(FilterKind kind, float freq, float q, int rate)
        {
            freq = Mathf.Clamp(freq, 20f, rate * 0.45f);
            q = Mathf.Max(q, 0.0001f);
            float w0 = 2f * Mathf.PI * freq / rate;
            float cw = Mathf.Cos(w0), sw = Mathf.Sin(w0);
            float alpha = sw / (2f * q);
            float b0, b1, b2, a0, a1, a2;

            switch (kind)
            {
                case FilterKind.Highpass:
                    b0 = (1f + cw) / 2f; b1 = -(1f + cw); b2 = (1f + cw) / 2f;
                    a0 = 1f + alpha; a1 = -2f * cw; a2 = 1f - alpha;
                    break;
                case FilterKind.Bandpass:                       // constant 0 dB peak gain
                    b0 = alpha; b1 = 0f; b2 = -alpha;
                    a0 = 1f + alpha; a1 = -2f * cw; a2 = 1f - alpha;
                    break;
                default:                                        // lowpass
                    b0 = (1f - cw) / 2f; b1 = 1f - cw; b2 = (1f - cw) / 2f;
                    a0 = 1f + alpha; a1 = -2f * cw; a2 = 1f - alpha;
                    break;
            }
            _a0 = b0 / a0; _a1 = b1 / a0; _a2 = b2 / a0;
            _b1 = a1 / a0; _b2 = a2 / a0;
        }

        public float Process(float x)
        {
            float y = _a0 * x + _a1 * _x1 + _a2 * _x2 - _b1 * _y1 - _b2 * _y2;
            _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
            if (float.IsNaN(y) || float.IsInfinity(y)) { Reset(); return 0f; }
            return y;
        }

        public void Reset() { _x1 = _x2 = _y1 = _y2 = 0f; }
    }

    // ---------------------------------------------------------------
    /// <summary>
    /// A short synthetic room, giving the retro FX a bit of console-cartridge space. The web
    /// build convolves a 1.1s decaying-noise impulse; a Schroeder comb-and-allpass network is
    /// the real-time equivalent and costs a fraction of the convolution.
    /// </summary>
    public class Reverb
    {
        readonly Comb[] _combs;
        readonly Allpass[] _aps;

        public Reverb(int rate)
        {
            float s = rate / 44100f;
            int[] combLens = { 1557, 1617, 1491, 1422 };
            int[] apLens = { 225, 556 };
            _combs = new Comb[combLens.Length];
            for (int i = 0; i < combLens.Length; i++)
                _combs[i] = new Comb(Mathf.RoundToInt(combLens[i] * s), 0.84f, 0.2f);
            _aps = new Allpass[apLens.Length];
            for (int i = 0; i < apLens.Length; i++)
                _aps[i] = new Allpass(Mathf.RoundToInt(apLens[i] * s), 0.5f);
        }

        public float Process(float x)
        {
            if (x == 0f && _silent) return 0f;
            float y = 0f;
            foreach (var c in _combs) y += c.Process(x);
            y *= 0.25f;
            foreach (var a in _aps) y = a.Process(y);
            _silent = Mathf.Abs(y) < 1e-6f;
            return y;
        }
        bool _silent = true;

        class Comb
        {
            readonly float[] _buf; int _i; readonly float _fb, _damp; float _store;
            public Comb(int n, float fb, float damp) { _buf = new float[Mathf.Max(n, 1)]; _fb = fb; _damp = damp; }
            public float Process(float x)
            {
                float y = _buf[_i];
                _store = y * (1f - _damp) + _store * _damp;
                _buf[_i] = x + _store * _fb;
                if (++_i >= _buf.Length) _i = 0;
                return y;
            }
        }

        class Allpass
        {
            readonly float[] _buf; int _i; readonly float _fb;
            public Allpass(int n, float fb) { _buf = new float[Mathf.Max(n, 1)]; _fb = fb; }
            public float Process(float x)
            {
                float b = _buf[_i];
                float y = -x + b;
                _buf[_i] = x + b * _fb;
                if (++_i >= _buf.Length) _i = 0;
                return y;
            }
        }
    }
}
