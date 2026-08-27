using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// Per-skin face styling. The ivory plaque with a royal blue letter is right for wood
    /// and gold and jarring on everything else — a ruby with a cream sticker on it is a toy;
    /// a ruby with a smoked-glass panel and rose-lit letters is a jewel. Gem plaques are
    /// PARTIALLY TRANSPARENT, so the translucent body glows through and the letters read as
    /// embedded in the stone rather than printed on it.
    /// </summary>
    public class FaceStyle
    {
        public Color[] Grad = new Color[3];
        public Color Shadow, Outline, Fill, Sheen, Emissive;

        public FaceStyle(string g0, string g1, string g2, string shadow, string outline,
                         string fill, string sheen, int emissive)
        {
            Grad[0] = Hex.To(g0); Grad[1] = Hex.To(g1); Grad[2] = Hex.To(g2);
            Shadow = Hex.To(shadow); Outline = Hex.To(outline);
            Fill = Hex.To(fill); Sheen = Hex.To(sheen);
            Emissive = Hex.FromInt(emissive);
        }
    }

    public static class FaceStyles
    {
        public static readonly Dictionary<string, FaceStyle> All = new Dictionary<string, FaceStyle>
        {
            { "wood",    new FaceStyle("#fffdf4","#f4ecd8","#ddcfae","rgba(10,18,60,0.4)","#14245e","#24409c","rgba(255,255,255,0.28)",0x8a8578) },
            { "gold",    new FaceStyle("#fffdf4","#f4ecd8","#ddcfae","rgba(10,18,60,0.4)","#14245e","#24409c","rgba(255,255,255,0.28)",0x8a8578) },
            { "ruby",    new FaceStyle("rgba(150,22,52,0.58)","rgba(96,10,32,0.62)","rgba(58,4,18,0.7)","rgba(20,0,6,0.5)","#4a0616","#ffd9e2","rgba(255,240,244,0.35)",0x38101a) },
            { "frost",   new FaceStyle("rgba(235,247,255,0.8)","rgba(205,230,246,0.78)","rgba(168,205,228,0.8)","rgba(8,30,52,0.35)","#0e3552","#1d6a9c","rgba(255,255,255,0.4)",0x6a7d8c) },
            { "onyx",    new FaceStyle("rgba(30,36,58,0.9)","rgba(16,20,36,0.92)","rgba(6,8,16,0.95)","rgba(0,0,0,0.6)","#6b4a10","#f2c14e","rgba(255,238,190,0.35)",0x1a1c28) },
            { "diamond", new FaceStyle("rgba(238,248,255,0.5)","rgba(214,236,252,0.45)","rgba(184,214,240,0.5)","rgba(12,30,64,0.3)","#0a1c4a","#173a8c","rgba(255,255,255,0.5)",0x93a4b8) },
            { "emerald", new FaceStyle("rgba(26,120,80,0.55)","rgba(14,86,56,0.6)","rgba(6,52,34,0.68)","rgba(0,20,10,0.5)","#053322","#d8ffe9","rgba(240,255,246,0.35)",0x14382a) },
            { "amber",   new FaceStyle("rgba(214,142,34,0.5)","rgba(168,100,18,0.55)","rgba(118,64,8,0.62)","rgba(40,18,0,0.5)","#4a2a06","#ffedc2","rgba(255,246,224,0.4)",0x3a2410) },
            { "blocky",  new FaceStyle("#8a6540","#74512e","#5c3f22","rgba(12,24,6,0.55)","#1e3a12","#8fe06a","rgba(220,255,190,0.35)",0x2e2416) },
            { "heeler",  new FaceStyle("#fff8ea","#ffedc8","#f2d8a4","rgba(30,58,100,0.4)","#2a4a7c","#4a86c8","rgba(255,255,255,0.4)",0x8a8272) },
            { "streaker",new FaceStyle("rgba(240,168,40,0.55)","rgba(200,130,20,0.6)","rgba(120,72,10,0.65)","rgba(70,40,4,0.5)","#5a3608","#ffe6a0","rgba(255,244,210,0.5)",0x3a2404) },
            { "goldpup", new FaceStyle("rgba(255,240,176,0.55)","rgba(240,200,60,0.55)","rgba(180,130,20,0.6)","rgba(90,60,8,0.45)","#8a5c10","#fff6d0","rgba(255,255,240,0.55)",0x4a3406) },
            { "nertamid",new FaceStyle("#ffcf6a","#e0a028","#a0680c","rgba(90,50,4,0.5)","#7a4a08","#fff0c0","rgba(255,240,200,0.55)",0xff8a1a) },
            { "oil",     new FaceStyle("#5a3206","#3a2004","#1a0f01","rgba(10,6,0,0.6)","#2a1a02","#ffcf5a","rgba(255,200,90,0.4)",0x2a1804) },
            { "founder", new FaceStyle("#fff0b0","#f5c542","#b8860c","rgba(90,60,4,0.5)","#7a5410","#fff8d8","rgba(255,255,240,0.65)",0x6a4a08) }
        };

        public static FaceStyle Get(string skin)
        {
            FaceStyle s;
            return All.TryGetValue(skin ?? "", out s) ? s : All["wood"];
        }
    }
}
