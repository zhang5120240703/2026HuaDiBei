Shader "Custom/DoubleSlit"
{
    Properties
    {
        // ── LUT（由 C# 运行时注入，无需手动赋值）──────────────────────────
        _LUT            ("Interference LUT",        2D)             = "black" {}

        // ── 视觉调参 ──────────────────────────────────────────────────────
        _Brightness     ("Brightness",              Range(0.1, 4))  = 1.2        
        _Contrast       ("Contrast (>1=加深暗区)",  Range(0.5, 3))  = 1.0        
        _EdgeSoftness   ("Vertical Edge Softness",  Range(0.01,0.5))= 0.08
    }

    SubShader
    {
        // ★ 核心修复：改为 Opaque，暗纹像素直接渲染为黑色，不再透出背景
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        ZWrite On
        ZTest LEqual
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _LUT;
            float     _Brightness;
            float     _Contrast;
            float     _EdgeSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. 采样 LUT（1D，y 锁定 0.5）
                float4 lut = tex2D(_LUT, float2(i.uv.x, 0.5));

                // 2. ★ Fix：直接使用 LUT 中的颜色
                // LUT 由 C# 端根据波长生成，已包含正确的物理色
                // 白光模式下：LUT 含可见光谱颜色分布
                // 单色模式下：LUT 含该波长对应的纯色（由 WavelengthToColor 计算）
                float3 col = lut.rgb;

                // 3. 对比度增强（>1 压深暗区，让亮纹更突出）
                col = pow(max(col, 0.0), _Contrast);

                // 4. 亮度
                col *= _Brightness;

                // 5. 垂直柔边（仅影响上下边缘，不影响条纹对比）
                float edge = smoothstep(0.0, _EdgeSoftness, i.uv.y)
                           * smoothstep(0.0, _EdgeSoftness, 1.0 - i.uv.y);
                col *= edge;

                // ★ 完全不透明输出：暗纹 = 纯黑，亮纹 = 波长色
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
