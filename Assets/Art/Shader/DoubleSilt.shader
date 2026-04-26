Shader "Custom/DoubleSlit"
{
    Properties
    {
        // ���� LUT���� C# ����ʱע�룬�����ֶ���ֵ������������������������������������������������������
        _LUT            ("Interference LUT",        2D)             = "black" {}

        // ���� �Ӿ����� ������������������������������������������������������������������������������������������������������������
        _Brightness     ("Brightness",              Range(0.1, 4))  = 1.2        
        _Contrast       ("Contrast (>1=�����)",  Range(0.5, 3))  = 1.0        
        _EdgeSoftness   ("Vertical Edge Softness",  Range(0.01,0.5))= 0.08

        // 拖拽视觉反馈（由 ExperimentItem.SetVS 通过 MaterialPropertyBlock 写入）
        _EmissionColor  ("Emission Color", Color)              = (0,0,0,1)
    }

    SubShader
    {
        // �� �����޸�����Ϊ Opaque����������ֱ����ȾΪ��ɫ������͸������
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
            float4    _EmissionColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. ���� LUT��1D��y ���� 0.5��
                float4 lut = tex2D(_LUT, float2(i.uv.x, 0.5));

                // 2. �� Fix��ֱ��ʹ�� LUT �е���ɫ
                // LUT �� C# �˸��ݲ������ɣ��Ѱ�����ȷ������ɫ
                // �׹�ģʽ�£�LUT ���ɼ�������ɫ�ֲ�
                // ��ɫģʽ�£�LUT ���ò�����Ӧ�Ĵ�ɫ���� WavelengthToColor ���㣩
                float3 col = lut.rgb;

                // 3. �Աȶ���ǿ��>1 ѹ����������Ƹ�ͻ����
                col = pow(max(col, 0.0), _Contrast);

                // 4. ����
                col *= _Brightness;

                // 5. ��ֱ��ߣ���Ӱ�����±�Ե����Ӱ�����ƶԱȣ�
                float edge = smoothstep(0.0, _EdgeSoftness, i.uv.y)
                           * smoothstep(0.0, _EdgeSoftness, 1.0 - i.uv.y);
                col *= edge;

                // �� ��ȫ��͸����������� = ���ڣ����� = ����ɫ
                col += _EmissionColor.rgb;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
