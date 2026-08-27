// Invisible except where shadows fall, so the dreidel casts its shadow straight onto YOUR
// table. The difference between "a model on a disc" and "a dreidel in my kitchen".
Shader "DreidelRoyale/ShadowCatcher"
{
    Properties
    {
        _Opacity ("Shadow Opacity", Range(0,1)) = 0.38
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                SHADOW_COORDS(0)
            };

            fixed _Opacity;

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed atten = SHADOW_ATTENUATION(i);
                return fixed4(0, 0, 0, (1.0 - atten) * _Opacity);
            }
            ENDCG
        }
    }
    Fallback Off
}
