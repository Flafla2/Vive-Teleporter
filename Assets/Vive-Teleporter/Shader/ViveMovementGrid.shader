// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Vive Movement Grid"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", Color) = (1, 1, 1, 1)
		_BackColor ("Color Behind Objects", Color) = (0.5,0.5,0.5,1)
		_Alpha("Total Alpha (Transparency)", Float) = 1.0
		_TexScale ("Texture Scale", Float) = 1.0
	}
	SubShader
	{
		Tags{ "Queue" = "Overlay" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

		CGINCLUDE
		#pragma vertex vert
		#pragma fragment frag

		#include "UnityCG.cginc"

		struct appdata
		{
			float4 vertex : POSITION;
		};

		struct v2f
		{
			float2 uv : TEXCOORD0;
			float4 vertex : SV_POSITION;
		};

		sampler2D _MainTex;
		float _TexScale;
		fixed4 _Color;
		fixed4 _BackColor;
		fixed _Alpha;

		v2f vert(appdata v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);

			float3 worldPos = mul(unity_ObjectToWorld, v.vertex);
			o.uv = worldPos.xz / _TexScale;
			return o;
		}
		ENDCG

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZTest Greater
			ZWrite Off

			CGPROGRAM
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv) * _BackColor;
				col.a *= _Alpha;
				return col;
			}
			ENDCG
		}

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZTest LEqual
			ZWrite Off

			CGPROGRAM
			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv) * _Color;
				col.a *= _Alpha;
				return col;
			}
			ENDCG
		}
	}
}
