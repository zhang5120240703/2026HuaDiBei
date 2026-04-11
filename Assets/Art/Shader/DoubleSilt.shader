Shader "Custom/DoubleSlitInterference"
{
    Properties
    {
        _Wavelength ("Wavelength (nm)", Range(380, 780)) = 550
        _SlitDistance ("Slit Distance d (mm)", Range(0.01, 1.0)) = 0.2
        _SlitWidth ("Slit Width a (mm)", Range(0.001, 0.5)) = 0.05
        _ScreenDistance ("Screen Distance l (m)", Range(0.1, 10.0)) = 1.0
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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float _Wavelength;
            float _SlitDistance;
            float _SlitWidth;
            float _ScreenDistance;

            v2f vert (appdata v)
            {
                v2f o;
                // 将顶点从模型空间转换到裁剪空间[4](@ref)
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 简易的波长转RGB颜色函数，使不同波长的光呈现不同颜色
            float3 wavelengthToRGB(float wl) 
            {
                float3 col = float3(0,0,0);
                if(wl < 440) col = float3(-(wl-440)/(440-380), 0.0, 1.0);
                else if(wl < 490) col = float3(0.0, (wl-440)/(490-440), 1.0);
                else if(wl < 510) col = float3(0.0, 1.0, -(wl-510)/(510-490));
                else if(wl < 580) col = float3((wl-510)/(580-510), 1.0, 0.0);
                else if(wl < 645) col = float3(1.0, -(wl-645)/(645-580), 0.0);
                else col = float3(1.0, 0.0, 0.0);
                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 将UV坐标(0~1)映射到物理屏幕位置，以中心为0点
                float x = (i.uv.x - 0.5) * 2.0;
                float y = (i.uv.y - 0.5) * 2.0;

                // 单位换算与缩放因子(为了在Shader中呈现合理的视觉效果)
                float lambda = _Wavelength * 1e-6; // nm转mm
                float d = _SlitDistance;
                float a = _SlitWidth;
                float l = _ScreenDistance * 1000;   // m转mm
                
                // 视觉缩放系数，让条纹肉眼可见
                float scale = 5.0; 
                x *= scale;

                // 1. 双缝干涉计算
                // 干涉强度 I_interference = cos^2(π * d * sinθ / λ)
                // 当角度很小时，sinθ ≈ tanθ = x / l
                float phase_interference = 3.1415926 * d * x / (lambda * l);
                float intensity_interference = pow(cos(phase_interference), 2);

                // 2. 单缝衍射计算（包络线）
                // 衍射强度 I_diffraction = sinc^2(π * a * sinθ / λ)
                float phase_diffraction = 3.1415926 * a * x / (lambda * l);
                float intensity_diffraction = 1.0;
                if(abs(phase_diffraction) > 0.001) {
                    intensity_diffraction = pow(sin(phase_diffraction) / phase_diffraction, 2);
                }

                // 总光强 = 干涉强度 * 衍射包络
                float intensity = intensity_interference * intensity_diffraction;

                // 获取对应波长的光色
                float3 col = wavelengthToRGB(_Wavelength);
                
                return fixed4(col * intensity, 1.0);
            }
            ENDCG
        }
    }
}
