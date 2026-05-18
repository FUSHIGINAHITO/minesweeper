Shader "Custom/SpritePolygonButtonLikeWorldFixed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SDFTex ("SDF Texture", 2D) = "gray" {}
        _Color ("Sprite Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)

        _BaseColor ("Base Color", Color) = (0.62,0.62,0.62,1)

        _TopBright ("Top Bright", Range(0,1)) = 0.35
        _BottomDark ("Bottom Dark", Range(0,1)) = 0.30
        _RimLight ("Rim Light", Range(0,1)) = 0.40
        _RimDark ("Rim Dark", Range(0,1)) = 0.30

        _SpecColor ("Spec Color", Color) = (1,1,1,1)
        _SpecStrength ("Spec Strength", Range(0,2)) = 0.45
        _SpecPower ("Spec Power", Range(1,128)) = 36
        _BevelSpecBoost ("Bevel Spec Boost", Range(0,2)) = 0.65

        _BevelWidth ("Bevel Width (0-1)", Range(0,1)) = 0.1
        _EdgeSoftness ("Edge Softness", Range(0.0001,1)) = 0.20
        _ScalePivot ("Scale Pivot UV", Vector) = (0.5, 0.5, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            sampler2D _SDFTex;
            float4 _SDFTex_ST;
            float4 _SDFTex_TexelSize;

            fixed4 _Color;
            fixed4 _RendererColor;
            fixed4 _BaseColor;

            float _TopBright;
            float _BottomDark;
            float _RimLight;
            float _RimDark;

            fixed4 _SpecColor;
            float _SpecStrength;
            float _SpecPower;
            float _BevelSpecBoost;

            float _BevelWidth;
            float _EdgeSoftness;
            float4 _ScalePivot;

            // 全局光方向（由 Shader.SetGlobalVector("_LightDirWS", ...) 设置）
            float4 _LightDirWS;

            // ===== 全局叠图参数（由脚本 Shader.SetGlobalXXX 设置）=====
            sampler2D _CellOverlayTex;
            float4 _CellOverlayRectMinSize; // xy = min, zw = size
            float4 _CellOverlayTint;        // rgb tint
            float4 _CellOverlayParams;      // x = enabled(0/1), y = strength(0..1)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvSdf : TEXCOORD1;
                fixed4 color : COLOR;
                float2 worldXY : TEXCOORD2;
            };

            float2 SafeNormalize2(float2 v, float2 fallback)
            {
                float len = length(v);
                return (len > 1e-5) ? (v / len) : fallback;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.uvSdf = TRANSFORM_TEX(v.texcoord, _SDFTex);
                o.color = v.color * _RendererColor;

                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldXY = worldPos.xy;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                if (tex.a <= 0.0001)
                {
                    discard;
                }

                float2 p = i.uv * 2.0 - 1.0;
                float2 dirForLighting = (dot(p, p) > 1e-6) ? normalize(p) : float2(0, 1);

                // ===== 关键：片元阶段由 UV->World 的雅可比反推局部方向，避免受合批矩阵路径影响 =====
                float2 lightWS = SafeNormalize2(_LightDirWS.xy, float2(0.0, 1.0));
                float2 worldUp = float2(0.0, 1.0);

                float2 dpdx = ddx(i.worldXY);
                float2 dpdy = ddy(i.worldXY);
                float2 duvdx = ddx(i.uv);
                float2 duvdy = ddy(i.uv);

                float detUv = duvdx.x * duvdy.y - duvdx.y * duvdy.x;
                float invDetUv = (abs(detUv) > 1e-8) ? rcp(detUv) : 0.0;

                // J = d(world)/d(uv)，列向量分别是 dPdu, dPdv
                float2 dPdu = (dpdx * duvdy.y - dpdy * duvdx.y) * invDetUv;
                float2 dPdv = (-dpdx * duvdy.x + dpdy * duvdx.x) * invDetUv;

                float detJ = dPdu.x * dPdv.y - dPdu.y * dPdv.x;
                float invDetJ = (abs(detJ) > 1e-8) ? rcp(detJ) : 0.0;

                // local = J^{-1} * world
                float2 lightDirLocal = float2(
                    dPdv.y * lightWS.x - dPdv.x * lightWS.y,
                   -dPdu.y * lightWS.x + dPdu.x * lightWS.y
                ) * invDetJ;

                float2 upLocal = float2(
                    dPdv.y * worldUp.x - dPdv.x * worldUp.y,
                   -dPdu.y * worldUp.x + dPdu.x * worldUp.y
                ) * invDetJ;

                lightDirLocal = SafeNormalize2(lightDirLocal, float2(0.0, 1.0));
                upLocal = SafeNormalize2(upLocal, float2(0.0, 1.0));

                float vertical01 = saturate(dot(p, upLocal) * 0.5 + 0.5);

                fixed3 baseColor = _Color.rgb * i.color.rgb;

                // ===== 世界坐标叠图采样 =====
                float2 rectSize = max(_CellOverlayRectMinSize.zw, float2(1e-4, 1e-4));
                float2 overlayUv = (i.worldXY - _CellOverlayRectMinSize.xy) / rectSize;

                float inside =
                    step(0.0, overlayUv.x) * step(overlayUv.x, 1.0) *
                    step(0.0, overlayUv.y) * step(overlayUv.y, 1.0);

                fixed4 overlaySample = tex2D(_CellOverlayTex, saturate(overlayUv));
                float overlayWeight = _CellOverlayParams.x * _CellOverlayParams.y * inside * overlaySample.a;
                fixed3 overlayColor = lerp(fixed3(1, 1, 1), overlaySample.rgb * _CellOverlayTint.rgb, overlayWeight);

                baseColor *= overlayColor;

                fixed3 topColor = lerp(baseColor, fixed3(1,1,1), _TopBright);
                fixed3 bottomColor = lerp(baseColor, fixed3(0,0,0), _BottomDark);
                fixed3 c = lerp(bottomColor, topColor, vertical01);

                float soft = max(_EdgeSoftness * 0.25, 1e-4);

                float sdfOuter = tex2D(_SDFTex, i.uvSdf).r;
                float outerMask = smoothstep(0.5 - soft, 0.5 + soft, sdfOuter);

                float innerScale = max(1.0 - _BevelWidth, 0.01);
                float2 pivot = _ScalePivot.xy;
                float2 uvInner = (i.uvSdf - pivot) / innerScale + pivot;

                float2 in01 = step(float2(0.0, 0.0), uvInner) * step(uvInner, float2(1.0, 1.0));
                float innerUvValid = in01.x * in01.y;

                float sdfInner = tex2D(_SDFTex, uvInner).r;
                float innerMask = smoothstep(0.5 - soft, 0.5 + soft, sdfInner) * innerUvValid;

                float edge01 = saturate(outerMask - innerMask);

                float rimLight = edge01 * saturate(dot(dirForLighting, lightDirLocal)) * _RimLight;
                float rimDark = edge01 * saturate(dot(dirForLighting, -lightDirLocal)) * _RimDark;

                c = lerp(c, fixed3(0,0,0), rimDark);
                c = lerp(c, fixed3(1,1,1), rimLight);

                // ===== 镜面高光（增强光泽与立体感）=====
                float3 n = normalize(float3(p.x, p.y, 1.35));
                float3 l = normalize(float3(lightDirLocal.x, lightDirLocal.y, 0.55));
                float3 v = float3(0.0, 0.0, 1.0);
                float3 h = normalize(l + v);

                float spec = pow(saturate(dot(n, h)), _SpecPower) * _SpecStrength;
                float specMask = saturate(innerMask + edge01 * _BevelSpecBoost);

                c = saturate(c + _SpecColor.rgb * (spec * specMask));

                fixed alpha = tex.a * _Color.a * i.color.a;
                return fixed4(c, alpha);
            }

            ENDCG
        }
    }
}