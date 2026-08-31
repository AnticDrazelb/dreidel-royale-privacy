// Clearcoated translucent gemstone.
//
// The original's gems are MeshPhysicalMaterial with clearcoat 1.0 at roughness 0.06 over a
// rough, emissive, transparent base. Unity's built-in Standard shader has no clearcoat at
// all, and the port was approximating it by pushing smoothness very high — which produces
// one broad highlight where a real clearcoat produces two: a soft one from the stone and a
// tight, bright one from the lacquer over it. That second lobe is most of what makes a gem
// read as a gem rather than as shiny plastic.
//
// So the coat is an explicit second specular lobe with its own Fresnel, added on top of a
// Standard base. It is not a full layered-material model and does not claim to be; it is the
// term that was missing.
Shader "DreidelRoyale/Gem"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
        _EmissionColor ("Inner Glow", Color) = (0,0,0,0)
        _Glossiness ("Smoothness", Range(0,1)) = 0.9
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _CoatStrength ("Clearcoat", Range(0,1)) = 1.0
        _CoatSmoothness ("Clearcoat Smoothness", Range(0,1)) = 0.94
    }

    SubShader
    {
        // depthWrite stays ON, as the original notes: the gem body is convex, so it sorts
        // against itself correctly, and you see the stem through the cube - which is the
        // whole point of it being translucent.
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 250
        ZWrite On

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma target 3.0

        struct Input { float3 worldNormal; float3 viewDir; INTERNAL_DATA };

        fixed4 _Color;
        fixed4 _EmissionColor;
        half _Glossiness, _Metallic, _CoatStrength, _CoatSmoothness;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Alpha = _Color.a;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;

            // The coat: a Schlick Fresnel at a dielectric's 0.04 normal reflectance, times a
            // tight specular lobe from the key light. Grazing angles get the rim brightening
            // that lacquer gives, head-on stays clear so the stone underneath still reads.
            float3 n = normalize(IN.worldNormal);
            float3 v = normalize(IN.viewDir);
            float ndv = saturate(dot(n, v));
            float fresnel = 0.04 + 0.96 * pow(1.0 - ndv, 5.0);

            float3 l = normalize(_WorldSpaceLightPos0.xyz);
            float3 h = normalize(l + v);
            float ndh = saturate(dot(n, h));
            // Blinn-Phong, with the exponent driven from smoothness so the coat's highlight
            // stays tight while the base's stays broad.
            float power = exp2(_CoatSmoothness * 11.0 + 1.0);
            float spec = pow(ndh, power) * (power + 8.0) * 0.0397887;

            o.Emission += _LightColor0.rgb * spec * fresnel * _CoatStrength;

            // A translucent surface seen edge-on shows more of itself, so the coat's Fresnel
            // also lifts opacity. Without this the gem thins out exactly where a real one
            // would look densest.
            o.Alpha = saturate(o.Alpha + fresnel * _CoatStrength * 0.35);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
