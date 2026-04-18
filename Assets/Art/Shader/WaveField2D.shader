Shader "Custom/WaveField2D"
{
    Properties
    {
        // 以下参数均由 WaveFieldVisualizer 脚本在运行时自动写入，无需手动调节
        _Slit1Y    ("Slit 1 UV.y", Float)           = 0.60
        _Slit2Y    ("Slit 2 UV.y", Float)           = 0.40
        _VisualK   ("Visual Wave Number", Float)     = 35.0
        _Aspect    ("Plane Aspect W/H", Float)       = 2.0
        _TimeScale ("Animation Speed", Range(0,5))   = 1.0
        _Brightness("Brightness", Range(0.1,5))      = 2.0
        _WaveColor ("Wave Color", Color)             = (1,1,0.3,1)
        [Toggle] _ShowPhase ("Phase Wave Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            float  _Slit1Y, _Slit2Y, _VisualK, _Aspect;
            float  _TimeScale, _Brightness, _ShowPhase;
            fixed4 _WaveColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 修正宽高比，使波纹在非正方形 Quad 上不拉伸
                float2 p  = float2(i.uv.x * _Aspect, i.uv.y);
                float2 s1 = float2(0.0, _Slit1Y);
                float2 s2 = float2(0.0, _Slit2Y);

                // 只渲染双缝右侧区域（光传播方向）
                float fadeIn = smoothstep(0.0, 0.02 * _Aspect, p.x);
                if (fadeIn < 0.001) return fixed4(0,0,0,0);

                float r1 = distance(p, s1);
                float r2 = distance(p, s2);
                float t  = _Time.y * _TimeScale;
                float TWO_PI = 6.28318530718;

                fixed4 result;

                if (_ShowPhase > 0.5)
                {
                    // ── 模式A：瞬态波形动画 ──────────────────────────
                    // 两列柱面波叠加，正振幅显示波长色，负振幅显示暗蓝
                    float w1 = sin(_VisualK * r1 - t * TWO_PI);
                    float w2 = sin(_VisualK * r2 - t * TWO_PI);
                    float field = (w1 + w2) * 0.5; // -1 ~ 1

                    float pos = max( field, 0.0);
                    float neg = max(-field, 0.0);

                    float3 col = _WaveColor.rgb * pos * _Brightness
                               + float3(0.05, 0.15, 0.6) * neg * _Brightness;
                    float  alpha = saturate(abs(field) * _Brightness * 0.75) * fadeIn;

                    result = fixed4(col, alpha);
                }
                else
                {
                    // ── 模式B：时间平均强度（静态干涉条纹 + 慢速波纹动态）──
                    // I_total = I1 + I2 + 2*sqrt(I1*I2)*cos(k*(r1-r2))
                    // 简化：单位振幅，故 I1=I2=1
                    float Itot = 2.0 + 2.0 * cos(_VisualK * (r1 - r2)); // 0 ~ 4

                    // 叠加极慢速波纹感（保留动感而不破坏干涉纹路）
                    float ripple = sin(_VisualK * min(r1, r2) - t * TWO_PI * 0.4) * 0.12 + 0.88;
                    float display = saturate(Itot * 0.25 * ripple * _Brightness * 0.55);

                    // 靠近光屏处淡出（避免与屏纹理硬接触）
                    float fadeOut = 1.0 - smoothstep(0.88, 1.0, i.uv.x);
                    float alpha   = display * fadeIn * fadeOut;

                    result = fixed4(_WaveColor.rgb * display, alpha);
                }

                return result;
            }
            ENDCG
        }
    }
}