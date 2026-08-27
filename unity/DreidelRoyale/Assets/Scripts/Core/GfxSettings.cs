using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreidelRoyale.Core
{
    /// <summary>
    /// Auto / High / Medium / Potato, carried across from the web build. The browser needed a
    /// tier picker to keep a canvas alive; a phone needs one because "Android" spans a decade
    /// of GPUs, and the player is the only one who knows whether they would rather have the
    /// embers or the frame rate.
    ///
    /// What a tier moves is what the original moved: render resolution, shadows, and the
    /// ambient particle layer. Frame rate is deliberately left alone - fidelity should follow
    /// what the GPU can draw, pacing should follow what the device can sustain, and pinning a
    /// fast phone to Potato is asking it to do less, not to run slower.
    /// </summary>
    public static class GfxSettings
    {
        public const string Auto = "auto", High = "high", Med = "med", Low = "low";

        public static readonly List<KeyValuePair<string, string>> Labels =
            new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(Auto, "Auto"),
                new KeyValuePair<string, string>(High, "High"),
                new KeyValuePair<string, string>(Med,  "Medium"),
                new KeyValuePair<string, string>(Low,  "Potato")
            };

        /// <summary>What the player picked. "auto" resolves per device through <see cref="Tier"/>.</summary>
        public static string Mode { get; private set; }

        /// <summary>The tier actually in force - never "auto".</summary>
        public static string Tier { get; private set; }

        /// <summary>Ambient embers and snow. Off at Potato.</summary>
        public static bool EmbersOn { get { return Tier != Low; } }

        /// <summary>Half the ambient particles at Medium, as the original did.</summary>
        public static float EmberMultiplier { get { return Tier == Med ? 0.5f : 1f; } }

        /// <summary>Raised after a tier change so the layers that care can rebuild.</summary>
        public static event Action OnChanged;

        public static string LabelFor(string id)
        {
            foreach (var kv in Labels) if (kv.Key == id) return kv.Value;
            return id;
        }

        public static void Load()
        {
            var saved = Store.Get("drdl-gfx");
            Mode = IsValid(saved) ? saved : Auto;
            Apply();
        }

        public static void Pick(string id)
        {
            if (!IsValid(id)) return;
            Mode = id;
            Store.Set("drdl-gfx", id);
            // Pinning a tier clears whatever the auto memory had settled on, so the pin wins
            // and a later return to Auto re-measures instead of inheriting an old verdict.
            if (id != Auto) Store.Set("drdl-gfx-auto", "");
            Apply();
        }

        static bool IsValid(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var kv in Labels) if (kv.Key == id) return true;
            return false;
        }

        /// <summary>
        /// Sticky: if this device has already been stepped down once, Auto starts there
        /// rather than re-learning the same lesson on every launch.
        /// </summary>
        static string AutoTier()
        {
            var saved = Store.Get("drdl-gfx-auto");
            if (saved == Low || saved == Med) return saved;
            try
            {
                int mem = SystemInfo.systemMemorySize;      // MB
                int cores = SystemInfo.processorCount;
                if ((mem > 0 && mem <= 2048) || (cores > 0 && cores <= 3)) return Low;
                if ((mem > 0 && mem <= 4096) || (cores > 0 && cores <= 4)) return Med;
            }
            catch { }
            return High;
        }

        /// <summary>Record that the device could not hold the tier it was given.</summary>
        public static void StepDown()
        {
            if (Tier == Low) return;
            var next = Tier == High ? Med : Low;
            Store.Set("drdl-gfx-auto", next);
            if (Mode == Auto) Apply();
        }

        public static void Apply()
        {
            Tier = Mode == Auto ? AutoTier() : Mode;

            // Resolution is the single biggest lever on a phone, and the one the browser
            // build pulled first (it moved devicePixelRatio). Native at High, a modest drop
            // at Medium, a real one at Potato.
            float scale = Tier == Low ? 0.65f : Tier == Med ? 0.82f : 1f;
            ApplyResolution(scale);

            QualitySettings.shadows = Tier == Low ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowResolution = Tier == High ? ShadowResolution.Medium
                                                            : ShadowResolution.Low;
            QualitySettings.antiAliasing = Tier == High ? 2 : 0;
            QualitySettings.shadowCascades = Tier == High ? 2 : 1;

            if (OnChanged != null) OnChanged();
        }

        static void ApplyResolution(float scale)
        {
            try
            {
                // Screen.width/height already report the current render target, so scaling
                // them repeatedly would compound. The native size is captured once.
                if (_nativeW <= 0) { _nativeW = Screen.currentResolution.width; _nativeH = Screen.currentResolution.height; }
                if (_nativeW <= 0 || _nativeH <= 0) { _nativeW = Screen.width; _nativeH = Screen.height; }

                int w = Mathf.Max(320, Mathf.RoundToInt(_nativeW * scale));
                int h = Mathf.Max(480, Mathf.RoundToInt(_nativeH * scale));
                if (w == Screen.width && h == Screen.height) return;
                Screen.SetResolution(w, h, Screen.fullScreen);
            }
            catch { /* a platform that refuses is simply left at native */ }
        }

        static int _nativeW, _nativeH;
    }
}
