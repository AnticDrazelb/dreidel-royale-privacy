// The whole screen treatment in as few passes as a phone can afford.
//
// The composite does tone mapping, bloom, radial blur, chromatic aberration and vignette in
// ONE pass. Chaining them as separate blits is how this is normally written and it is the
// wrong shape here: each blit re-reads and re-writes a full-resolution buffer, and on a
// mobile GPU that bandwidth, not the arithmetic, is the entire cost. Bloom needs its own
// small chain because a blur cannot be done in one tap, but it runs at quarter resolution.
Shader "DreidelRoyale/PostFx"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // ---- 0: bright pass, into a quarter-size target ------------------------
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            float _Threshold, _BloomIntensity;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed3 c = tex2D(_MainTex, i.uv).rgb;
                // Luminance, not max channel: a saturated red at 1.0 is not "bright" the way
                // a candle flame is, and thresholding on max makes every strong colour bloom.
                float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                float k = max(0.0, l - _Threshold) / max(l, 1e-4);
                return fixed4(c * k * _BloomIntensity, 1.0);
            }
            ENDCG
        }

        // ---- 1: separable blur, run twice ---------------------------------------
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            float2 _BlurDir;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 d = _BlurDir * _MainTex_TexelSize.xy;
                // A 5-tap Gaussian at quarter res reads like a much wider one at full res,
                // which is the only reason this is affordable at all.
                fixed3 c  = tex2D(_MainTex, i.uv).rgb            * 0.2270270270;
                c += tex2D(_MainTex, i.uv + d * 1.3846153846).rgb * 0.3162162162;
                c += tex2D(_MainTex, i.uv - d * 1.3846153846).rgb * 0.3162162162;
                c += tex2D(_MainTex, i.uv + d * 3.2307692308).rgb * 0.0702702703;
                c += tex2D(_MainTex, i.uv - d * 3.2307692308).rgb * 0.0702702703;
                return fixed4(c, 1.0);
            }
            ENDCG
        }

        // ---- 2: composite --------------------------------------------------------
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0

            sampler2D _BloomTex;
            float _Exposure, _Aberration, _RadialBlur, _Vignette, _Flash;
            float4 _FlashColor;

            // Narkowicz's fitted ACES curve — the same one three.js ships, so the port
            // matches the original's grade rather than merely resembling it.
            float3 ACESFilm(float3 x)
            {
                const float a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 fromCentre = uv - 0.5;
                float  r = length(fromCentre);

                // Radial blur, strongest at the edges: the frame smears past the dreidel
                // while the dreidel itself stays readable, which is what sells speed without
                // making the thing you are watching illegible.
                float3 col;
                if (_RadialBlur > 0.001)
                {
                    float amount = _RadialBlur * r * r;
                    col = 0;
                    [unroll] for (int s = 0; s < 6; s++)
                        col += tex2D(_MainTex, uv - fromCentre * amount * (s / 5.0)).rgb;
                    col /= 6.0;
                }
                else col = tex2D(_MainTex, uv).rgb;

                // Chromatic aberration, also radial. Zero in the middle so the letter on the
                // face never fringes; only the impact drives it above zero anyway.
                if (_Aberration > 0.0001)
                {
                    float2 off = fromCentre * _Aberration * r;
                    col.r = tex2D(_MainTex, uv + off).r;
                    col.b = tex2D(_MainTex, uv - off).b;
                }

                col += tex2D(_BloomTex, uv).rgb;
                col = lerp(col, _FlashColor.rgb, saturate(_Flash));
                col = ACESFilm(col * _Exposure);

                // Vignette last, in display space, so it darkens the picture rather than
                // feeding the tone mapper a lie about how bright the scene is.
                col *= 1.0 - _Vignette * smoothstep(0.35, 0.9, r);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
