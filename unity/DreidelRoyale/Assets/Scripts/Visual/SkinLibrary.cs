using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;

namespace DreidelRoyale.Visual
{
    /// <summary>Body, tip and handle materials for one dreidel.</summary>
    public class SkinMats
    {
        public Material Body, Tip, Handle;
        public SkinMats(Material body, Material tip, Material handle) { Body = body; Tip = tip; Handle = handle; }
    }

    /// <summary>
    /// The full set of dreidels. Same geometry throughout — what changes is the material,
    /// and the three premium pieces are deliberately made from three DIFFERENT materials so
    /// they never read as "three golds": warm candle-metal, dark amber glass, pale carved stone.
    /// </summary>
    public static class SkinLibrary
    {
        public static Dictionary<string, SkinMats> Skins;
        public static Material OilHandle, OilKnob;

        public static void Build()
        {
            if (Skins != null) return;

            var wTex = Tex.Wood();
            var goldMat = MatUtil.Pbr(Hex.FromInt(0xe6b845), 0.95f, 0.18f, Hex.FromInt(0x3a2405), 0.25f);
            var goldFlat = MatUtil.Pbr(Hex.FromInt(0xe6b845), 0.90f, 0.24f);
            var woodMat = MatUtil.Pbr(Color.white, 0.05f, 0.62f, null, 0f, wTex);
            var woodFlat = MatUtil.Pbr(Color.white, 0.05f, 0.70f, null, 0f, wTex);

            // unlockable skins — plain metal variants, same geometry
            System.Func<int, int, float, Material> mkMetal =
                (color, emissive, rough) => MatUtil.Pbr(Hex.FromInt(color), 0.92f, rough, Hex.FromInt(emissive), 0.3f);
            System.Func<int, Material> mkFlat =
                color => MatUtil.Pbr(Hex.FromInt(color), 0.88f, 0.28f);

            // Gems: clearcoated translucent glass, inner emissive, high specular.
            System.Func<int, int, float, float, Material> mkGem =
                (color, emissive, opacity, rough) => MatUtil.Gem(Hex.FromInt(color), Hex.FromInt(emissive), opacity, rough);
            System.Func<int, int, float, Material> mkGemFlat =
                (color, emissive, opacity) => MatUtil.Gem(Hex.FromInt(color), Hex.FromInt(emissive),
                                                          Mathf.Min(1f, opacity + 0.12f), 0.04f, 0.35f);

            var rubyMat = mkGem(0xc21847, 0x6e0c22, 0.72f, 0.06f);
            var rubyFlat = mkGemFlat(0xa8123c, 0x5c0a1e, 0.72f);
            var frostMat = mkGem(0xcfe4f5, 0x14384e, 0.90f, 0.26f);
            var frostFlat = mkGemFlat(0xbcd8ec, 0x12303f, 0.90f);
            var onyxMat = mkMetal(0x232838, 0x05060c, 0.30f);
            var onyxFlat = mkFlat(0x232838);
            var diamondMat = mkGem(0xeef6ff, 0x8fb8d8, 0.55f, 0.02f);
            var diamondFlat = mkGemFlat(0xe2f0fc, 0x7ca8cc, 0.55f);
            var emeraldMat = mkGem(0x14915c, 0x063d26, 0.70f, 0.08f);
            var emeraldFlat = mkGemFlat(0x0f7a4c, 0x05301e, 0.70f);
            var amberMat = mkGem(0xd6801c, 0x5c3204, 0.68f, 0.10f);
            var amberFlat = mkGemFlat(0xc06f12, 0x4a2803, 0.68f);

            // voxel skin: grass-block body, cobble tip, oak-log handle — all matte pixels,
            // deliberately the one non-shiny thing on the table
            var blockyMat = MatUtil.Pbr(Color.white, 0f, 0.90f, null, 0f, Tex.GrassSide());
            var blockyTip = MatUtil.Pbr(Color.white, 0f, 0.95f, null, 0f,
                Tex.Pixel("#8a8a8a", new[] { "#767676", "#9a9a9a", "#6a6a6a", "#a8a8a8" }, 0.6f));
            var blockyHandle = MatUtil.Pbr(Color.white, 0f, 0.90f, null, 0f,
                Tex.Pixel("#9a7442", new[] { "#8a6538", "#a8814c", "#7c5a30", "#b08a54" }, 0.5f));

            // pup skin: soft blue coat, deep-blue tip, tan handle — matte and friendly
            var heelerMat = MatUtil.Pbr(Color.white, 0.02f, 0.60f, null, 0f, Tex.Heeler());
            var heelerTip = MatUtil.Pbr(Hex.FromInt(0x3f719f), 0.05f, 0.55f);
            var heelerHandle = MatUtil.Pbr(Hex.FromInt(0xd8a86a), 0.02f, 0.65f);

            // streak reward: warm menorah gold, richer and redder than the plain gold
            var streakerMat = mkGem(0xf0a828, 0x6a3c08, 0.55f, 0.35f);
            var streakerFlat = mkGemFlat(0xd88f18, 0x4a2a04, 0.55f);
            // spin-milestone reward: bright pale champion gold, high shine
            var champMat = mkGem(0xffd24a, 0x8a5c10, 0.90f, 0.20f);
            var champFlat = mkGemFlat(0xf0be34, 0x6a4408, 0.90f);

            // ---- PREMIUM (Full Collection) ----
            // 1) NER TAMID — brushed warm brass that self-lights, contained candle-amber.
            var nerMat = MatUtil.Pbr(Hex.FromInt(0xd99a2c), 0.85f, 0.35f, Hex.FromInt(0xff7a1a), 0.55f);
            var nerFlat = MatUtil.Pbr(Hex.FromInt(0xc8861e), 0.90f, 0.30f, Hex.FromInt(0xff6a10), 0.50f);

            // 2) OIL MIRACLE — dark amber glass vessel; the fill is a separate mesh.
            var oilGlass = MatUtil.Gem(Hex.FromInt(0x6a3a08), Hex.FromInt(0x1a0d02), 0.45f, 0.08f, 0.15f);
            var oilGlassFlat = MatUtil.Gem(Hex.FromInt(0x5a3206), Hex.FromInt(0x160b02), 0.50f, 0.06f, 0.15f);
            OilHandle = MatUtil.Gem(Hex.FromInt(0x7a4a0a), Hex.FromInt(0x2a1602), 0.60f, 0.08f, 0.20f);
            OilKnob = MatUtil.Pbr(Hex.FromInt(0x1a0f02), 0.20f, 0.35f, Hex.FromInt(0x0a0500), 0.20f);

            // 3) FOUNDER — solid mirror gold trophy, restrained but detailed.
            var founderMat = MatUtil.Pbr(Hex.FromInt(0xf5c542), 1.0f, 0.09f, Hex.FromInt(0x3a2604), 0.18f);
            var founderFlat = MatUtil.Pbr(Hex.FromInt(0xffdf78), 1.0f, 0.06f, Hex.FromInt(0x4a3208), 0.20f);

            Skins = new Dictionary<string, SkinMats>
            {
                { "gold",     new SkinMats(goldMat,    goldFlat,     goldMat) },
                { "wood",     new SkinMats(woodMat,    woodFlat,     woodMat) },
                { "ruby",     new SkinMats(rubyMat,    rubyFlat,     rubyMat) },
                { "frost",    new SkinMats(frostMat,   frostFlat,    frostMat) },
                { "onyx",     new SkinMats(onyxMat,    onyxFlat,     onyxMat) },
                { "diamond",  new SkinMats(diamondMat, diamondFlat,  diamondMat) },
                { "emerald",  new SkinMats(emeraldMat, emeraldFlat,  emeraldMat) },
                { "amber",    new SkinMats(amberMat,   amberFlat,    amberMat) },
                { "blocky",   new SkinMats(blockyMat,  blockyTip,    blockyHandle) },
                { "heeler",   new SkinMats(heelerMat,  heelerTip,    heelerHandle) },
                { "streaker", new SkinMats(streakerMat,streakerFlat, streakerMat) },
                { "goldpup",  new SkinMats(champMat,   champFlat,    champMat) },
                { "nertamid", new SkinMats(nerMat,     nerFlat,      nerMat) },
                { "oil",      new SkinMats(oilGlass,   oilGlassFlat, oilGlassFlat) },
                { "founder",  new SkinMats(founderMat, founderFlat,  founderMat) }
            };
        }

        public static SkinMats Get(string id)
        {
            Build();
            SkinMats s;
            return Skins.TryGetValue(id ?? "", out s) ? s : Skins["wood"];
        }

        /// <summary>
        /// Landing burst colour per dreidel, matched to its look. Base wood, Grass Block and
        /// Blue Pup intentionally get none — the splash is a reward.
        /// </summary>
        public static readonly Dictionary<string, Color> BurstColor = new Dictionary<string, Color>
        {
            { "ruby",     Hex.FromInt(0xff3b5c) },   // deep red
            { "frost",    Hex.FromInt(0xbfeaff) },   // pale silver-blue
            { "onyx",     Hex.FromInt(0x9aa4d0) },   // cool violet-slate
            { "emerald",  Hex.FromInt(0x2fe38a) },
            { "amber",    Hex.FromInt(0xffa022) },   // honey-orange
            { "diamond",  Hex.FromInt(0x9fd8ff) },   // icy blue
            { "streaker", Hex.FromInt(0xffcf5a) },   // menorah gold
            { "goldpup",  Hex.FromInt(0xffe08a) },   // pale gold
            { "nertamid", Hex.FromInt(0xff7a1a) },   // flame orange
            { "oil",      Hex.FromInt(0xd9962c) },   // amber-brown
            { "founder",  Hex.FromInt(0xffe07a) }    // bright trophy gold
        };
    }
}
