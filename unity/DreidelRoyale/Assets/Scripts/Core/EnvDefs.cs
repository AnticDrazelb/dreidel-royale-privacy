using System.Collections.Generic;
using UnityEngine;

namespace DreidelRoyale.Core
{
    /// <summary>A gradient stop on the sky dome / screen backdrop.</summary>
    public struct SkyStop
    {
        public float T; public Color C;
        public SkyStop(float t, string hex) { T = t; C = Hex.To(hex); }
    }

    /// <summary>A soft nebula blob painted into the sky texture.</summary>
    public struct Neb
    {
        public float X, Y, R; public Color C;
        public Neb(float x, float y, float r, string rgba) { X = x; Y = y; R = r; C = Hex.To(rgba); }
    }

    public class EmberCfg
    {
        public string Mode = "rise";   // "rise" | "snow"
        public int Count = 26;
        public float Size = 1.8f;
        public float Speed = 0.3f;
        public EmberCfg(string mode, int count, float size, float speed)
        { Mode = mode; Count = count; Size = size; Speed = speed; }
    }

    /// <summary>One table: its light, its floor, its weather and its prop kit.</summary>
    public class EnvDef
    {
        public string Id, Name, Kit;
        public Color Fog, Ground, Grid, Pool, Ambient, Key, Rim, Glow;
        public bool HasGrid, Planks, Blocks, Room, Lawn, Candles, Stars, FlatClouds;
        public Color[] Embers;
        public EmberCfg Ember;
        public SkyStop[] Sky;
        public Neb[] Nebs;
        public Color CubeHi, CubeMid, CubeLo;
        public Color SwA, SwB;           // menu swatch gradient
    }

    public static class Hex
    {
        static float Num(string raw)
        {
            float v;
            return float.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        /// <summary>Accepts "#rrggbb", "rrggbb" and "rgba(r,g,b,a)" — the CSS forms the source uses.</summary>
        public static Color To(string s)
        {
            if (string.IsNullOrEmpty(s)) return Color.clear;
            s = s.Trim();
            if (s.StartsWith("rgba") || s.StartsWith("rgb"))
            {
                int o = s.IndexOf('('), c = s.IndexOf(')');
                var parts = s.Substring(o + 1, c - o - 1).Split(',');
                // InvariantCulture, always. These strings are CSS quoted from the web build,
                // so "0.25" is a quarter in every one of them - but a phone set to German,
                // French, Spanish, Russian or Portuguese reads a full stop as a group
                // separator, and float.Parse would return 25 or throw. Every colour with a
                // fractional alpha in the game would be wrong, on a large share of devices.
                return new Color(Num(parts[0]) / 255f, Num(parts[1]) / 255f, Num(parts[2]) / 255f,
                                 parts.Length > 3 ? Num(parts[3]) : 1f);
            }
            Color col;
            if (!s.StartsWith("#")) s = "#" + s;
            return ColorUtility.TryParseHtmlString(s, out col) ? col : Color.magenta;
        }

        public static Color FromInt(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
    }

    public static class EnvDefs
    {
        public static readonly Dictionary<string, EnvDef> All = new Dictionary<string, EnvDef>
        {
            { "midnight", new EnvDef {
                Id="midnight", Name="Midnight", Kit="midnight",
                Fog=Hex.FromInt(0x05081a), Ground=Hex.To("#0a102c"),
                Grid=Hex.To("rgba(190,150,70,0.10)"), HasGrid=true,
                Pool=Hex.To("rgba(242,193,78,0.10)"),
                Ambient=Hex.FromInt(0x2e3c6e), Key=Hex.FromInt(0xfff0d2), Rim=Hex.FromInt(0x4f7cff),
                Embers=new[]{ Hex.To("#f2c14e"), Hex.To("#7f96ff") },
                Candles=true, Ember=new EmberCfg("rise",26,1.8f,0.3f),
                Glow=Hex.FromInt(0xf2c14e), Stars=true,
                Sky=new[]{ new SkyStop(0,"#02030c"), new SkyStop(0.55f,"#0a1236"), new SkyStop(1,"#1a2a5e") },
                Nebs=new[]{ new Neb(70,90,90,"rgba(80,60,160,0.25)"), new Neb(190,60,70,"rgba(40,80,160,0.22)") },
                CubeHi=Hex.To("#8ea0e0"), CubeMid=Hex.To("#2a3a78"), CubeLo=Hex.To("#070b1e"),
                SwA=Hex.To("#182254"), SwB=Hex.To("#05081a") } },

            { "den", new EnvDef {
                Id="den", Name="Maple Den", Kit="den",
                Fog=Hex.FromInt(0x140b04), Ground=Hex.To("#4a2f16"), Planks=true,
                Pool=Hex.To("rgba(255,180,90,0.13)"),
                Ambient=Hex.FromInt(0x5a4530), Key=Hex.FromInt(0xffe0b0), Rim=Hex.FromInt(0xff9d45),
                Embers=new[]{ Hex.To("#ffb45e"), Hex.To("#ff7d3a") },
                Candles=true, Ember=new EmberCfg("rise",34,2.2f,0.45f),
                Glow=Hex.FromInt(0xffb45e), Stars=false,
                Sky=new[]{ new SkyStop(0,"#0a0602"), new SkyStop(0.6f,"#2a1608"), new SkyStop(1,"#4a2c12") },
                Nebs=new[]{ new Neb(128,150,120,"rgba(120,60,20,0.3)") },
                CubeHi=Hex.To("#ffcf8a"), CubeMid=Hex.To("#7a4e22"), CubeLo=Hex.To("#120a03"),
                SwA=Hex.To("#5a3a1c"), SwB=Hex.To("#1e1108") } },

            { "frost", new EnvDef {
                Id="frost", Name="Silver Frost", Kit="frost",
                Fog=Hex.FromInt(0x0a1420), Ground=Hex.To("#12283c"),
                Grid=Hex.To("rgba(170,215,255,0.10)"), HasGrid=true,
                Pool=Hex.To("rgba(200,230,255,0.12)"),
                Ambient=Hex.FromInt(0x3c5a78), Key=Hex.FromInt(0xeaf4ff), Rim=Hex.FromInt(0x9fd4ff),
                Embers=new[]{ Hex.To("#dff0ff"), Hex.To("#9fd4ff") },
                Candles=false, Ember=new EmberCfg("snow",44,2.6f,0.55f),
                Glow=Hex.FromInt(0x9fd4ff), Stars=true,
                Sky=new[]{ new SkyStop(0,"#04080e"), new SkyStop(0.55f,"#0e2536"), new SkyStop(1,"#1c4358") },
                Nebs=new[]{ new Neb(80,80,90,"rgba(80,150,200,0.22)"), new Neb(190,110,80,"rgba(120,180,220,0.2)") },
                CubeHi=Hex.To("#d8ecff"), CubeMid=Hex.To("#3a5f7a"), CubeLo=Hex.To("#060d14"),
                SwA=Hex.To("#2c4f6e"), SwB=Hex.To("#0a1420") } },

            { "felt", new EnvDef {
                Id="felt", Name="Casino Felt", Kit="felt",
                Fog=Hex.FromInt(0x03130b), Ground=Hex.To("#0b3b24"),
                Grid=Hex.To("rgba(255,255,255,0.05)"), HasGrid=true,
                Pool=Hex.To("rgba(255,220,120,0.12)"),
                Ambient=Hex.FromInt(0x2a4a38), Key=Hex.FromInt(0xfff2cc), Rim=Hex.FromInt(0x57e6a8),
                Embers=new[]{ Hex.To("#f2c14e"), Hex.To("#57e6a8") },
                Candles=false, Ember=new EmberCfg("rise",14,1.5f,0.2f),
                Glow=Hex.FromInt(0x57e6a8), Stars=false,
                Sky=new[]{ new SkyStop(0,"#02100a"), new SkyStop(0.6f,"#08331f"), new SkyStop(1,"#0f5030") },
                Nebs=new[]{ new Neb(128,140,130,"rgba(20,120,70,0.28)") },
                CubeHi=Hex.To("#bfe8cf"), CubeMid=Hex.To("#12432c"), CubeLo=Hex.To("#03130b"),
                SwA=Hex.To("#12432c"), SwB=Hex.To("#03130b") } },

            { "blocky", new EnvDef {
                Id="blocky", Name="Blocky Biome", Kit="blocky",
                Fog=Hex.FromInt(0x87b8e8), Ground=Hex.To("#5d9440"), Blocks=true,
                Pool=Hex.To("rgba(255,245,180,0.12)"),
                Ambient=Hex.FromInt(0x8fa4b8), Key=Hex.FromInt(0xfff6dc), Rim=Hex.FromInt(0x9ad0ff),
                Embers=new[]{ Hex.To("#a8e07a"), Hex.To("#fff3b0") },
                Candles=false, Ember=new EmberCfg("rise",20,2.6f,0.22f),
                Glow=Hex.FromInt(0x8fe06a), Stars=false,
                Sky=new[]{ new SkyStop(0,"#5a9ede"), new SkyStop(0.55f,"#8ec4f2"), new SkyStop(1,"#cfe8fc") },
                Nebs=new[]{ new Neb(64,58,42,"rgba(255,255,255,0.75)"), new Neb(176,96,52,"rgba(255,255,255,0.65)"),
                            new Neb(120,150,38,"rgba(255,255,255,0.5)") },
                CubeHi=Hex.To("#eaf6ff"), CubeMid=Hex.To("#8fc2ea"), CubeLo=Hex.To("#3f6b32"),
                SwA=Hex.To("#79b7f0"), SwB=Hex.To("#5d9440") } },

            { "backyard", new EnvDef {
                Id="backyard", Name="Backyard Games", Kit="backyard",
                Fog=Hex.FromInt(0xfdf3c8), Ground=Hex.To("#b4743e"), Room=true,
                Pool=new Color(0,0,0,0),
                Ambient=Hex.FromInt(0xc8ccc0), Key=Hex.FromInt(0xfff6e0), Rim=Hex.FromInt(0xffc890),
                Embers=new[]{ Hex.To("#ffd27a"), Hex.To("#8fd0ff"), Hex.To("#ffa0c0") },
                Candles=false, Ember=new EmberCfg("rise",18,2.4f,0.18f),
                Glow=Hex.FromInt(0xffc06a), Stars=false, FlatClouds=true,
                Sky=new[]{ new SkyStop(0,"#7ec3ea"), new SkyStop(0.40f,"#7ec3ea"), new SkyStop(0.40f,"#a8d888"),
                           new SkyStop(0.52f,"#a8d888"), new SkyStop(0.52f,"#fdf3c8"), new SkyStop(1,"#fdf3c8") },
                Nebs=new Neb[0],
                CubeHi=Hex.To("#fdf3c8"), CubeMid=Hex.To("#a8d4ea"), CubeLo=Hex.To("#b4743e"),
                SwA=Hex.To("#7ec3ea"), SwB=Hex.To("#8fca6e") } }
        };

        public static readonly string[] Order = { "midnight", "den", "frost", "felt", "blocky", "backyard" };

        public static EnvDef Get(string id)
        {
            EnvDef e;
            return All.TryGetValue(id ?? "", out e) ? e : All["midnight"];
        }
    }
}
