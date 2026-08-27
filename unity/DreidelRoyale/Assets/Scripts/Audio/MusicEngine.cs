using UnityEngine;

namespace DreidelRoyale.Audio
{
    /// <summary>
    /// Generative soundtrack in D freygish (Ahava Raba) — the klezmer mode — layered by
    /// intensity: a bass pulse in the menu, full oom-pah in play, a tension shimmer and
    /// call-and-answer harmony in the showdown.
    ///
    /// Notes are scheduled a little ahead of the audio clock, exactly as the web build does,
    /// so the groove never depends on the frame rate.
    /// </summary>
    public class MusicEngine : MonoBehaviour
    {
        // D  Eb  F#  G  A  Bb  C  (and up the octave)
        static readonly float[] SCALE =
            { 146.83f, 155.56f, 185.00f, 196.00f, 220.00f, 233.08f, 261.63f,
              293.66f, 311.13f, 369.99f, 392.00f, 440.00f };
        static readonly float[] BASS = { 73.42f, 98.00f, 110.00f, 73.42f };   // D G A D

        const float BPM = 88f;
        static readonly float SPB = 60f / BPM / 2f;    // eighth notes

        int _step, _bar;
        double _nextTime = -1;
        int _intensity;                                 // 0 menu, 1 gameplay, 2 showdown
        int _melodyHold, _lastDegree;
        float _currentRoot = BASS[0];

        public void SetIntensity(int v) { _intensity = Mathf.Clamp(v, 0, 2); }

        void Update()
        {
            var S = Synth.I;
            if (S == null || !Sfx.MusicOn) return;
            if (_nextTime < 0) _nextTime = S.Now + 0.1;
            Schedule(S);
        }

        void Schedule(Synth S)
        {
            double ahead = S.Now + 0.25;
            int guard = 0;
            while (_nextTime < ahead && guard++ < 64)
            {
                float when = (float)System.Math.Max(_nextTime - S.Now, 0.0);
                int inBar = _step % 16;

                // bass root plus a harmony pad on the two-bar phrase start
                if (inBar == 0)
                {
                    _currentRoot = BASS[_bar % 4];
                    Pad(S, _currentRoot * 2f, when, SPB * 15f);
                    if (_intensity >= 2) Pad(S, _currentRoot * 3.02f, when, SPB * 15f);  // tension shimmer
                    _bar++;
                }

                // oom-pah: bass thump on beats 1 and 3, chord stab on 2 and 4
                if (_intensity >= 1)
                {
                    if (inBar % 8 == 0) Oom(S, _currentRoot, when, 0.075f + _intensity * 0.015f);
                    else if (inBar % 8 == 4) Oom(S, _currentRoot, when, 0.05f + _intensity * 0.01f);
                    else if (inBar % 4 == 2) Pah(S, _currentRoot, when, 0.05f + _intensity * 0.015f);
                }
                else if (inBar % 8 == 0)
                {
                    Oom(S, _currentRoot, when, 0.05f);   // gentle bass-only pulse in the menu
                }

                // melody: clarinet lead, sparse on the menu, walking and ornamented in play
                float density = _intensity == 0 ? 0.2f : _intensity == 1 ? 0.38f : 0.56f;
                if (_melodyHold > 0) _melodyHold--;
                else if (Random.value < density)
                {
                    int[] steps = { -2, -1, -1, 1, 1, 2, 3 };
                    _lastDegree = Mathf.Clamp(_lastDegree + steps[Random.Range(0, steps.Length)], 0, SCALE.Length - 1);
                    float noteDur = SPB * (1.3f + Random.value * 0.6f);
                    Clarinet(S, SCALE[_lastDegree], 0.075f, when, noteDur);
                    // showdown: an occasional third above, klezmer-style call and answer
                    if (_intensity >= 2 && Random.value < 0.22f)
                    {
                        int harm = Mathf.Min(SCALE.Length - 1, _lastDegree + 2);
                        Clarinet(S, SCALE[harm], 0.045f, when + SPB * 0.5f, noteDur * 0.8f);
                    }
                    if (Random.value < 0.3f) _melodyHold = 1;
                }

                // percussion only in play
                if (_intensity >= 1)
                {
                    if (inBar % 4 == 2) Hat(S, when, 0.018f + _intensity * 0.008f);
                    if (_intensity >= 2 && inBar % 8 == 4) Tom(S, when, 0.05f);
                }

                _nextTime += SPB;
                _step++;
            }
        }

        /// <summary>
        /// Clarinet-style lead: two square oscillators (reedy, chiptune-friendly) through a
        /// lowpass, with vibrato and a fast grace-note scoop up into pitch — a krechts.
        /// </summary>
        static void Clarinet(Synth S, float f, float vol, float when, float dur)
        {
            for (int i = 0; i < 2; i++)
            {
                var v = new OscVoice(S.Rate)
                {
                    Type = Wave.Square,
                    Freq = f * 0.89f,       // scoop up into pitch
                    FreqRamp = f,
                    Dur = dur,
                    StartAt = S.Now + when,
                    Destination = Bus.Music,
                    Verb = 0.18f,
                    Vibrato = f * 0.011f,
                    VibRate = 5.6f,
                    Detune = i == 0 ? 0f : 7f
                };
                v.SetEnvExp(vol, 0.028f, dur);
                S.Add(v);
            }
        }

        /// <summary>"oom" — the low downbeat thump.</summary>
        static void Oom(Synth S, float root, float when, float vol)
        {
            S.Tone(root, Wave.Sine, SPB * 1.7f, vol, 0f, when, Bus.Music);
            S.Tone(root * 2f, Wave.Triangle, SPB * 1.1f, vol * 0.35f, 0f, when, Bus.Music);
        }

        /// <summary>"pah" — the offbeat accordion-ish chord stab.</summary>
        static void Pah(Synth S, float root, float when, float vol)
        {
            var fs = new[] { root * 2f, root * 2f * 1.19f, root * 3f };
            for (int i = 0; i < fs.Length; i++)
                S.Blip(fs[i], SPB * 0.9f, Wave.Square, vol * (i == 0 ? 1f : 0.6f), 0.004f, when,
                       0f, 0f, 6f, Bus.Music);
        }

        static void Pad(Synth S, float f, float when, float dur)
        {
            foreach (var ff in new[] { f, f * 1.004f })
            {
                var v = new OscVoice(S.Rate)
                {
                    Type = Wave.Sawtooth, Freq = ff, Dur = dur,
                    StartAt = S.Now + when, Destination = Bus.Music
                };
                v.SetEnvExp(0.024f, dur * 0.3f, dur);
                S.Add(v);
            }
        }

        static void Hat(Synth S, float when, float vol)
        {
            S.Noise(0.05f, vol, 7000f, 5000f, FilterKind.Highpass, when, Bus.Music);
        }

        static void Tom(Synth S, float when, float vol)
        {
            S.Tone(110f, Wave.Sine, 0.18f, vol, 60f, when, Bus.Music);
        }
    }
}
