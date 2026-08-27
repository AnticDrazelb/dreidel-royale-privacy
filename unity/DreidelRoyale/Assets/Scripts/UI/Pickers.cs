using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DreidelRoyale.Audio;
using DreidelRoyale.Core;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// The table and dreidel pickers. A locked chip trades its name for a live progress
    /// counter and a bar, so the goal reads as something you are part-way through rather
    /// than a closed door.
    /// </summary>
    public static class Pickers
    {
        public static void RenderEnv(Transform container, string selected, Action<string> onPick, bool locked = false)
        {
            UIKit.Clear(container);
            var S = Stats.Load();
            foreach (var id in EnvDefs.Order)
            {
                var env = EnvDefs.Get(id);
                bool unlocked = Unlocks.EnvUnlocked(id, S);
                var def = Unlocks.EnvUnlock(id);
                string sub = unlocked ? null : (def != null ? def.Unlock.LockLabel(S) : "Locked");
                float pct = (!unlocked && def != null) ? def.Unlock.Pct(S) : 0f;

                string capturedId = id;
                Chip(container, env.Name, sub, pct, env.SwA, env.SwB, id == selected, unlocked && !locked,
                     () =>
                     {
                         if (!unlocked)
                         {
                             UI().Toast(def != null ? def.Unlock.Hint(env.Name, S) : "Locked", true);
                             return;
                         }
                         Sfx.Play("tick"); Sfx.Buzz(10);
                         onPick(capturedId);
                     });
            }
        }

        public static void RenderSkin(Transform container, string selected, Action<string> onPick)
        {
            UIKit.Clear(container);
            var S = Stats.Load();
            foreach (var d in Unlocks.Skins)
            {
                bool unlocked = Unlocks.SkinUnlocked(d, S);
                string sub = unlocked ? null
                           : d.Premium ? "Full Collection"
                           : d.Unlock != null ? d.Unlock.LockLabel(S) : "Locked";
                float pct = (!unlocked && d.Unlock != null) ? d.Unlock.Pct(S) : 0f;
                var sw = SwatchFor(d.Id);

                var captured = d;
                Chip(container, d.Name, sub, pct, sw.Key, sw.Value, d.Id == selected, unlocked,
                     () =>
                     {
                         if (!unlocked)
                         {
                             UI().Toast(captured.Premium
                                 ? captured.Name + " — unlock the Full Collection"
                                 : captured.Unlock.Hint(captured.Name, S), true);
                             return;
                         }
                         Sfx.Play("tick"); Sfx.Buzz(10);
                         onPick(captured.Id);
                     });
            }
        }

        static UIManager UI() { return UIManager.I; }

        /// <summary>Menu swatch colours, mirroring the stylesheet's gradient for each piece.</summary>
        static KeyValuePair<Color, Color> SwatchFor(string id)
        {
            switch (id)
            {
                case "wood":     return P("#a06a32", "#4a2f16");
                case "blocky":   return P("#6ba84c", "#5c3f22");
                case "heeler":   return P("#7ab8e8", "#2a5580");
                case "ruby":     return P("#ff5c7a", "#7a0e26");
                case "frost":    return P("#eaf6ff", "#4a7ba0");
                case "onyx":     return P("#4a5470", "#0a0c14");
                case "emerald":  return P("#4fe8a4", "#06452c");
                case "amber":    return P("#ffc46a", "#7a4408");
                case "diamond":  return P("#f4fbff", "#7ca8cc");
                case "streaker": return P("#ffd36a", "#8a5410");
                case "goldpup":  return P("#fff0b0", "#c8901e");
                case "nertamid": return P("#ffcf6a", "#8a5410");
                case "oil":      return P("#6a3a08", "#1a0f01");
                case "founder":  return P("#fff0b0", "#8a5c0c");
                default:         return P("#a06a32", "#4a2f16");
            }
        }

        static KeyValuePair<Color, Color> P(string a, string b)
        {
            return new KeyValuePair<Color, Color>(Hex.To(a), Hex.To(b));
        }

        static void Chip(Transform parent, string name, string lockedSub, float pct,
                         Color swA, Color swB, bool selected, bool enabled, Action onClick)
        {
            var go = UIKit.Node("pick", parent);
            UIKit.Rect(go).sizeDelta = new Vector2(96, 92);
            var bg = go.AddComponent<Image>();
            bg.sprite = Theme.Rounded(Theme.RSm);
            bg.type = Image.Type.Sliced;
            bg.color = selected ? new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.16f) : Theme.Surface2;
            if (selected) UIKit.Border(go.transform, Theme.Gold, Theme.RSm, 1.5f);

            var sw = UIKit.Node("swatch", go.transform);
            var srt = UIKit.Rect(sw);
            srt.anchorMin = new Vector2(0.5f, 1f); srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0, -8);
            srt.sizeDelta = new Vector2(74, 40);
            var swImg = sw.AddComponent<Image>();
            swImg.sprite = Theme.Gradient(swA, swB);
            swImg.color = enabled ? Color.white : new Color(0.55f, 0.55f, 0.6f, 0.75f);
            swImg.raycastTarget = false;

            var nameT = UIKit.Label(go.transform, name, 11,
                                    enabled ? Theme.Text : Theme.Sub, TextAnchor.UpperCenter, false, FontStyle.Bold);
            var nrt = nameT.rectTransform;
            nrt.anchorMin = new Vector2(0, 1); nrt.anchorMax = new Vector2(1, 1);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.offsetMin = new Vector2(4, 0); nrt.offsetMax = new Vector2(-4, 0);
            nrt.anchoredPosition = new Vector2(0, -52);
            nrt.sizeDelta = new Vector2(nrt.sizeDelta.x, 16);

            if (lockedSub != null)
            {
                var sub = UIKit.Label(go.transform, lockedSub, 9, Theme.Sub, TextAnchor.UpperCenter);
                var brt = sub.rectTransform;
                brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1);
                brt.pivot = new Vector2(0.5f, 1f);
                brt.offsetMin = new Vector2(3, 0); brt.offsetMax = new Vector2(-3, 0);
                brt.anchoredPosition = new Vector2(0, -68);
                brt.sizeDelta = new Vector2(brt.sizeDelta.x, 12);

                if (pct > 0f)
                {
                    // a bar that can only be empty or full is noise, so it appears only on
                    // multi-step goals, where progress actually means something
                    var track = UIKit.Node("prog", go.transform);
                    var trt = UIKit.Rect(track);
                    trt.anchorMin = new Vector2(0.5f, 0); trt.anchorMax = new Vector2(0.5f, 0);
                    trt.pivot = new Vector2(0.5f, 0);
                    trt.anchoredPosition = new Vector2(0, 6);
                    trt.sizeDelta = new Vector2(70, 3);
                    var timg = track.AddComponent<Image>();
                    timg.sprite = Theme.Rounded(2f); timg.type = Image.Type.Sliced;
                    timg.color = Theme.Surface3; timg.raycastTarget = false;

                    var fill = UIKit.Node("fill", track.transform);
                    var frt = UIKit.Rect(fill);
                    frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(Mathf.Clamp01(pct), 1);
                    frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
                    var fimg = fill.AddComponent<Image>();
                    fimg.sprite = Theme.Rounded(2f); fimg.type = Image.Type.Sliced;
                    fimg.color = Theme.Gold; fimg.raycastTarget = false;
                }
            }

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick());
            go.AddComponent<PressScale>();
        }
    }
}
