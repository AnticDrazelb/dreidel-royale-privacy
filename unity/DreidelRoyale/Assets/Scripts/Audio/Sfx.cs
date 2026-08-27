using UnityEngine;

namespace DreidelRoyale.Audio
{
    /// <summary>
    /// Every sound the game makes, synthesised on the spot. Each letter gets its own
    /// stinger, the charge climbs in pitch and brightness with power, and the wooden
    /// clatter is a knuckle of tone plus a click of noise, rerolled per knock.
    /// </summary>
    public static class Sfx
    {
        public static bool SfxOn = true;
        public static bool MusicOn = true;
        public static bool HapticsOn = true;

        static Synth S { get { return Synth.I; } }
        static float R01 { get { return Random.value; } }

        public static void Play(string fx, float p = 1f)
        {
            if (!SfxOn || S == null) return;
            switch (fx)
            {
                // ---- charge (holding the spin button) — pitch and brightness climb with power ----
                case "chargeTick":
                    S.Blip(240f + p * 640f, 0.045f, Wave.Square, 0.05f + p * 0.03f, 0.002f);
                    if (p > 0.7f) S.Blip(360f + p * 640f, 0.03f, Wave.Square, 0.02f, 0.001f, 0.02f);
                    break;

                case "knock":   // wood-on-wood clatter
                {
                    float v = 0.05f + p * 0.13f;
                    S.Blip(150f + R01 * 70f + p * 90f, 0.07f, Wave.Triangle, v, 0.001f);
                    S.Noise(0.03f, v * 0.5f, 2600f, 700f);
                    break;
                }

                case "flop":    // the nun anti-fanfare: one soft, slightly deflated thud
                    S.Blip(140f, 0.09f, Wave.Sine, 0.07f, 0.002f, 0f, 88f);
                    S.Noise(0.05f, 0.02f, 900f, 300f, FilterKind.Lowpass, 0.01f);
                    break;

                case "clinkWood":   // coin on the wooden board — warm, dead quickly
                    S.Blip(620f + R01 * 160f, 0.05f, Wave.Triangle, 0.04f + p * 0.05f, 0.001f);
                    break;

                case "clinkFloor":  // coin off the edge onto the hard floor — bright ring, a touch of room
                    S.Blip(2300f + R01 * 900f, 0.16f, Wave.Sine, 0.05f + p * 0.05f, 0.001f, 0f, 0f, 0f, 6f, Bus.Sfx, 0.35f);
                    S.Blip(3400f + R01 * 600f, 0.06f, Wave.Sine, 0.02f, 0.001f, 0.008f);
                    break;

                case "chargePulse": // low wub as a pressure ring pushes out
                    S.Blip(70f + p * 46f, 0.26f, Wave.Sine, 0.05f + p * 0.05f, 0.01f, 0f, 44f);
                    break;

                // ---- launch — rising whir that tracks charge power ----
                case "launch":
                {
                    float dur = 0.5f + p * 0.25f;
                    S.Noise(dur, 0.16f + p * 0.22f, 600f + p * 300f, 3200f + p * 1800f);
                    S.Blip(90f + p * 70f, dur, Wave.Sawtooth, 0.28f + p * 0.18f, 0.01f, 0f, 260f + p * 260f);
                    S.Tone(55f, Wave.Sine, 0.3f, 0.32f * p + 0.12f, 32f);
                    break;
                }

                // ---- whirl — continuous spin texture (layered under the chants) ----
                case "whirl":
                {
                    float dur = Mathf.Min(p * 2f + 1.5f, 4.5f);
                    S.Noise(dur, 0.045f + p * 0.045f, 900f, 2400f);
                    S.Noise(dur, 0.02f + p * 0.02f, 3000f, 5200f, FilterKind.Highpass);
                    break;
                }

                // ---- land — wooden "tok" impact plus settle-wobble ----
                case "land":
                    S.Blip(180f + p * 40f, 0.09f, Wave.Square, 0.22f + p * 0.22f, 0.001f, 0f, 70f);
                    S.Tone(58f, Wave.Sine, 0.24f, 0.5f * p + 0.2f, 28f);
                    S.Noise(0.22f, 0.3f * p + 0.12f, 1800f, 150f, FilterKind.Lowpass);
                    // wobble-settle: quick decaying clicks as the dreidel tips onto its face
                    for (int i = 0; i < 4; i++)
                    {
                        float t = new[] { 0.1f, 0.19f, 0.27f, 0.34f }[i];
                        S.Blip(340f - i * 40f + R01 * 20f, 0.03f, Wave.Triangle, 0.09f - i * 0.018f, 0.001f, t);
                    }
                    break;

                // ---- letter stingers — each outcome sounds distinct ----
                case "stinger-nun":   // nisht: a flat, anticlimactic little blip
                    S.Blip(330f, 0.09f, Wave.Triangle, 0.14f, 0.004f);
                    S.Blip(280f, 0.14f, Wave.Triangle, 0.10f, 0.004f, 0.08f);
                    break;

                case "stinger-hei":   // halb: a bright two-note rise
                    S.Blip(523.3f, 0.12f, Wave.Square, 0.16f, 0.003f);
                    S.Blip(659.3f, 0.22f, Wave.Square, 0.16f, 0.003f, 0.09f);
                    S.Noise(0.2f, 0.05f, 3000f, 900f, FilterKind.Bandpass, 0.09f);
                    break;

                case "stinger-shin":  // shtel: a small descending "pay-in" tick
                    S.Blip(440f, 0.08f, Wave.Sawtooth, 0.13f, 0.002f);
                    S.Blip(349.2f, 0.14f, Wave.Sawtooth, 0.11f, 0.002f, 0.07f);
                    break;

                case "coin":
                    S.Blip(1900f + R01 * 500f, 0.14f, Wave.Triangle, 0.13f, 0.001f, 0f, 0f, 0f, 6f, Bus.Sfx, 0.15f);
                    break;

                case "chant":
                    S.Tone(95f, Wave.Sine, 0.28f, 0.22f + p * 0.15f, 55f);
                    S.Noise(0.12f, 0.06f, 500f, 150f, FilterKind.Lowpass);
                    break;

                case "perfect":
                {
                    var fs = new[] { 880f, 1174f, 1568f };
                    for (int i = 0; i < fs.Length; i++)
                        S.Blip(fs[i], 0.28f, Wave.Square, 0.13f, 0.002f, i * 0.06f, 0f, 0f, 6f, Bus.Sfx, 0.2f);
                    break;
                }

                case "tick":
                    S.Blip(520f, 0.05f, Wave.Square, 0.05f, 0.001f);
                    break;

                case "go":
                    S.Blip(392f, 0.1f, Wave.Square, 0.18f, 0.002f);
                    S.Blip(784f, 0.28f, Wave.Square, 0.2f, 0.002f, 0.1f, 0f, 0f, 6f, Bus.Sfx, 0.15f);
                    break;

                case "heartbeat":
                    S.Tone(75f, Wave.Sine, 0.1f, 0.12f, 45f);
                    S.Tone(60f, Wave.Sine, 0.1f, 0.1f, 40f, 0.16f);
                    break;

                case "elim":
                    S.Blip(220f, 0.14f, Wave.Sawtooth, 0.14f, 0.002f, 0f, 1f);   // a long slide down, as in the original
                    S.Tone(140f, Wave.Sawtooth, 0.4f, 0.12f, 60f, 0.08f);
                    S.Noise(0.3f, 0.05f, 500f, 150f, FilterKind.Lowpass, 0.05f, Bus.Sfx, 0.05f);
                    break;

                // ---- gimel (jackpot) — bright triumphant flourish plus coin-cascade shimmer ----
                case "gimel":
                {
                    var fs = new[] { 587.3f, 740f, 880f, 1174.7f, 1480f };
                    for (int i = 0; i < fs.Length; i++)
                        S.Blip(fs[i], 0.32f, Wave.Square, 0.17f, 0.002f, i * 0.06f, 0f, 0f, 6f, Bus.Sfx, 0.25f);
                    for (int i = 0; i < 10; i++)
                        S.Blip(1600f + R01 * 1400f, 0.1f, Wave.Triangle, 0.05f, 0.001f,
                               0.3f + i * 0.045f, 0f, 0f, 6f, Bus.Sfx, 0.3f);
                    S.Noise(0.5f, 0.1f, 4000f, 800f, FilterKind.Bandpass, 0f, Bus.Sfx, 0.2f);
                    break;
                }

                case "win":
                {
                    var scale = new[] { 293.7f, 311.1f, 370f, 392f, 440f, 466.2f, 523.3f, 587.3f };
                    for (int i = 0; i < scale.Length; i++)
                        S.Blip(scale[i], 0.4f, Wave.Square, 0.14f, 0.003f, i * 0.11f, 0f, 0f, 6f, Bus.Sfx, 0.2f);
                    var tail = new[] { 587.3f, 740f, 880f };
                    for (int i = 0; i < tail.Length; i++)
                        S.Blip(tail[i], 0.9f, Wave.Square, 0.13f, 0.005f, 0.95f + i * 0.05f, 0f, 0f, 6f, Bus.Sfx, 0.3f);
                    break;
                }

                case "lose":
                {
                    // descending minor "defeat" cadence
                    var fall = new[] { 440f, 392f, 349.2f, 293.7f, 261.6f };
                    for (int i = 0; i < fall.Length; i++)
                        S.Blip(fall[i], 0.45f, Wave.Square, 0.13f, 0.004f, i * 0.16f, 0f, 0f, 6f, Bus.Sfx, 0.2f);
                    S.Tone(196f, Wave.Sawtooth, 1.0f, 0.12f, 130f, 0.8f);
                    S.Noise(0.6f, 0.06f, 600f, 200f, FilterKind.Lowpass, 0.85f, Bus.Sfx, 0.85f);
                    break;
                }
            }
        }

        // ---------------------------------------------------------------
        /// <summary>
        /// Wood-on-wood scrape: a looping noise voice whose pitch and level ride the RPM,
        /// driven every frame from the 3D layer, so the whirr genuinely drops as the top slows
        /// and dies through the topple.
        /// </summary>
        static LoopVoice _scrape, _scrapeTone;
        static float _scrapeIdle;

        public static void SetScrape(float speed01, float power)
        {
            if (!SfxOn || Synth.I == null) { StopScrape(); return; }
            if (speed01 <= 0.01f) { _scrapeIdle += Time.deltaTime; if (_scrapeIdle > 0.25f) StopScrape(); return; }
            _scrapeIdle = 0f;

            if (_scrape == null)
            {
                _scrape = Synth.I.StartLoop(true);
                _scrapeTone = Synth.I.StartLoop(false, Wave.Sawtooth);
            }
            _scrape.FilterFreq = 420f + speed01 * 1500f * (0.6f + power * 0.6f);
            _scrape.Q = 1.4f;
            _scrape.TargetGain = (0.02f + speed01 * 0.05f) * (0.5f + power * 0.7f);
            _scrapeTone.TargetFreq = 42f + speed01 * 90f;
            _scrapeTone.TargetGain = speed01 * 0.035f * (0.4f + power * 0.8f);
        }

        public static void StopScrape()
        {
            if (_scrape != null) { _scrape.Stop(); _scrape = null; }
            if (_scrapeTone != null) { _scrapeTone.Stop(); _scrapeTone = null; }
        }

        /// <summary>The low rumble under a wind-up, tracking charge.</summary>
        static LoopVoice _rumble, _rumbleNoise;

        public static void StartRumble()
        {
            if (!SfxOn || Synth.I == null || _rumble != null) return;
            _rumble = Synth.I.StartLoop(false, Wave.Sine);
            _rumble.TargetFreq = 42f;
            _rumbleNoise = Synth.I.StartLoop(true);
            _rumbleNoise.FilterFreq = 120f;
            _rumbleNoise.Q = 0.7f;
        }

        public static void SetRumble(float p)
        {
            if (_rumble == null) return;
            _rumble.TargetFreq = 38f + p * 34f;
            _rumble.TargetGain = 0.05f + p * 0.16f;
            _rumbleNoise.FilterFreq = 90f + p * 220f;
            _rumbleNoise.TargetGain = 0.01f + p * 0.05f;
        }

        public static void StopRumble()
        {
            if (_rumble != null) { _rumble.Stop(); _rumble = null; }
            if (_rumbleNoise != null) { _rumbleNoise.Stop(); _rumbleNoise = null; }
        }

        // ---------------------------------------------------------------
        /// <summary>Haptics, where the platform offers them.</summary>
        public static void Buzz(params int[] patternMs)
        {
            if (!HapticsOn) return;
            try { Haptics.Vibrate(patternMs); } catch { }
        }
    }
}
