Shader "Custom/DoubleSlitInterference_LUT"
{
    Properties
    {
        _LUT ("Interference LUT", 2D) = "white" {}
        _VisualScale ("Visual Scale", Range(0.1, 50.0)) = 5.0
        _Aspect ("Aspect Ratio (Width/Height)", Range(0.1, 4.0)) = 1.78
        _MaxRange ("Max Physical Range", Float) = 50.0 // 必须与C#脚本中的 maxRange 保持一致
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                half2 uv : TEXCOORD0; 
                float4 vertex : SV_POSITION;
            };

            sampler2D _LUT;
            half _VisualScale;
            half _Aspect;
            half _MaxRange;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 计算缩放后的物理坐标 x
                half physicalX = (i.uv.x - 0.5) * _Aspect * _VisualScale;
                
                // 2. 将物理坐标映射到 0~1 以采样LUT纹理
                half lutU = (physicalX / _MaxRange) * 0.5 + 0.5;
                
                // 3. 超出LUT覆盖范围的区域返回黑色
                // 使用step函数代替if分支，更符合GPU流水线特性[6](@ref)
                half validRange = step(0, lutU) * step(lutU, 1);
                lutU = clamp(lutU, 0, 1); // 防止越界采样

                // 4. 采样1D LUT (v坐标固定为0.5)
                fixed4 col = tex2D(_LUT, float2(lutU, 0.5));
                
                return col * validRange;
            }
            ENDCG
        }
    }
}
