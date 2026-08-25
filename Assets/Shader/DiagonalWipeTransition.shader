Shader "UI/DiagonalWipeTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _NormalTex ("Normal Background", 2D) = "white" {}
        _VipTex ("VIP Background", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Angle ("Angle", Range(-180, 180)) = 45
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.15
        _InnerEdgeColor ("Inner Edge Color", Color) = (1, 1, 0, 1)
        _OuterEdgeColor ("Outer Edge Color", Color) = (1, 1, 1, 1)
        _NormalOffset ("Normal UV Offset", Vector) = (0, 0, 0, 0)
        _VipOffset ("VIP UV Offset", Vector) = (0, 0, 0, 0)
        _NormalScale ("Normal UV Scale", Vector) = (1, 1, 1, 1)
        _VipScale ("VIP UV Scale", Vector) = (1, 1, 1, 1)
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _NormalTex;
            sampler2D _VipTex;
            float _Progress;
            float _Angle;
            float _EdgeWidth;
            fixed4 _InnerEdgeColor;
            fixed4 _OuterEdgeColor;
            float4 _MainTex_ST;
            float2 _NormalOffset;
            float2 _VipOffset;
            float2 _NormalScale;
            float2 _VipScale;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV 좌표를 중심 기준으로 변환
                float2 centeredUV = i.uv - 0.5;
                
                // 각도를 라디안으로 변환
                float angleRad = _Angle * 3.14159 / 180.0;
                
                // 회전 행렬 적용
                float cosA = cos(angleRad);
                float sinA = sin(angleRad);
                float2 rotatedUV = float2(
                    centeredUV.x * cosA - centeredUV.y * sinA,
                    centeredUV.x * sinA + centeredUV.y * cosA
                );
                
                // 대각선 진행도 계산 (-1 ~ 1 범위를 0 ~ 1로 매핑)
                float diagonal = (rotatedUV.x + 1.0) * 0.5;
                
                // Progress 확장 (대각선 전체를 커버하기 위해)
                float adjustedProgress = _Progress * 1.5 - 0.25;
                
                // 현재 픽셀이 전환되었는지 확인
                float mask = step(diagonal, adjustedProgress);
                
                // Edge glow 계산 (개선된 버전)
                float edgeDist = diagonal - adjustedProgress;
                
                // EdgeWidth를 더 넓은 범위로 스케일링
                float scaledEdgeWidth = _EdgeWidth * 0.5;
                
                // 그라데이션 계산 (0 = inner, 1 = outer)
                float edgeGradient = saturate(edgeDist / scaledEdgeWidth);
                
                // Edge 영역 마스크 (양쪽 모두)
                float edgeMask = 1.0 - smoothstep(0, scaledEdgeWidth, abs(edgeDist));
                
                // 두 텍스처 샘플링 (타일링 스케일 + 스크롤링 오프셋 적용)
                fixed4 normalCol = tex2D(_NormalTex, i.uv * _NormalScale + _NormalOffset);
                fixed4 vipCol = tex2D(_VipTex, i.uv * _VipScale + _VipOffset);
                
                // 마스크에 따라 블렌딩
                fixed4 finalCol = lerp(normalCol, vipCol, mask);
                
                // Edge 그라데이션 색상 (Inner에서 Outer로)
                fixed4 edgeColor = lerp(_InnerEdgeColor, _OuterEdgeColor, edgeGradient);
                
                // Edge glow 추가 (알파값 고려)
                finalCol.rgb = lerp(finalCol.rgb, edgeColor.rgb, edgeMask * edgeColor.a);
                
                // UI 컬러 적용
                finalCol *= i.color;
                
                return finalCol;
            }
            ENDCG
        }
    }
}
