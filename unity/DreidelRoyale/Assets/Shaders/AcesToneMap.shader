// The original renders through ACESFilmicToneMapping at exposure 0.95. Unity's built-in
// pipeline has no tone mapper at all, so without this the port clips every highlight hard
// at 1.0: the gold body, the candle flames and every emissive blow out to flat white
// instead of rolling off, and the whole image reads harsher and flatter than the web build.
//
// This is the same fitted ACES curve three.js uses (Narkowicz's approximation), applied in
// linear space before Unity writes sRGB, so the result matches rather than merely resembles.
Shader "DreidelRoyale/AcesToneMap"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Exposure ("Exposure", Float) = 0.95
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Exposure;

            // Narkowicz 2015, "ACES Filmic Tone Mapping Curve" — the same fit three.js ships.
            float3 ACESFilm(float3 x)
            {
                const float a = 2.51;
                const float b = 0.03;
                const float c = 2.43;
                const float d = 0.59;
                const float e = 0.14;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv);
                return fixed4(ACESFilm(src.rgb * _Exposure), src.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
