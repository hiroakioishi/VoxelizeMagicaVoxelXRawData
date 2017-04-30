Shader "Hidden/irishoak/VoxelizeMagicaVoxelXRawData/VoxelRenderGouraud"
{
	Properties
	{
		_Ambient("Ambient Color", Color) = (0.3, 0.3, 1.0, 1.0)
		_Ka("Ka", Range(0.01, 1.0)) = 0.5

		_Diffuse("Dissuse Color", Color) = (0.3, 0.3, 1.0, 1.0)
		_Kd("Kd", Range(0.01, 1.0)) = 0.8

		_Specular("Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Ks("Ks", Range(0.01, 1.0)) = 1.0
		_Shinness("Shinness", Range(0.01, 1.0)) = 0.7
	}

	CGINCLUDE
	#include "UnityCG.cginc"

	struct VoxelData
	{
		float3 position;
		float4 rotation;
		float  scale;
		float4 color;
	};

	struct v2g
	{
		float4 position : POSITION;
		float4 rotation : TEXCOORD1;
		float4 color    : COLOR;
	//	float  id : TEXCOORD2;
	};

	struct g2f
	{
		float4 position    : SV_Position;
		float4 color       : COLOR;
		float2 texcoord    : TEXCOORD0;
	};

	sampler2D _MainTex;
	
	float4x4 _LocalToWorldMatrix;

	StructuredBuffer<VoxelData> _VoxelDataBuffer;

	float4 _DistractorPosition;
	float4 _DistractorRadius;

	float  _VoxelScaleRate;

	fixed4 _Ambient;
	float  _Ka;

	fixed4 _Diffuse;
	float  _Kd;

	fixed4 _Specular;
	float  _Ks;
	float  _Shinness;

	// --------------------------------------------------------------
	// Functions
	// --------------------------------------------------------------
	float rand(float2 co)
	{
		return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
	}

	// Quaternion multiplication.
	float4 qmul(float4 q1, float4 q2)
	{
		return float4
		(
			q2.xyz * q1.w + q1.xyz * q2.w + cross(q1.xyz, q2.xyz),
			q1.w * q2.w - dot(q1.xyz, q2.xyz)
		);
	}

	// Rotate a vector with a rotation quaternion.
	float3 rotate_vector(float3 v, float4 r)
	{
		float4 r_c = r * float4(-1, -1, -1, 1);
		return qmul(r, qmul(float4(v, 0), r_c)).xyz;
	}

	// --------------------------------------------------------------
	// Vertex Shader
	// --------------------------------------------------------------
	v2g vert(uint id : SV_VertexID)
	{
		v2g o = (v2g)0;
		o.position = float4(_VoxelDataBuffer[id].position.xyz, _VoxelDataBuffer[id].scale);
		o.rotation = _VoxelDataBuffer[id].rotation;
		o.color    = _VoxelDataBuffer[id].color;
		return o;
	}

	// --------------------------------------------------------------
	// Geometry Shader
	// --------------------------------------------------------------
	static const float2 g_texcoord[4] = { float2(1, 0), float2(0, 0), float2(0, 1), float2(1, 1) };

	static const float3 g_cv[8] = 
	{
		float3(-0.5, -0.5, -0.5), // 0
		float3(0.5, -0.5, -0.5),  // 1
		float3(0.5,  0.5, -0.5),  // 2
		float3(-0.5,  0.5, -0.5), // 3
		float3(-0.5, -0.5,  0.5), // 4
		float3(0.5, -0.5,  0.5),  // 5
		float3(0.5,  0.5,  0.5),  // 6
		float3(-0.5,  0.5,  0.5)  // 7
	};

	static const float3 g_norm[6] = 
	{
		float3(0, 1, 0),	// Top
		float3(0, -1, 0),	// Bottom
		float3(0, 0, 1),	// Front
		float3(0, 0, -1),	// Back
		float3(1, 0, 0),	// Left
		float3(-1, 0, 0),	// Right
	};

	g2f getVertex(float3 center, float3 cv, float4 rot, float3 scl, float3 norm, float4 col, float2 texcoord)
	{
		g2f o = (g2f)0;

		float3 pos = center + rotate_vector(cv * scl, rot);
		o.position = mul(UNITY_MATRIX_MVP, float4(pos, 1.0));

		o.texcoord = texcoord;

		float3 N  = normalize(rotate_vector(norm, rot));
		float3 L  = normalize(ObjSpaceLightDir(float4(pos, 1.0)));
		float3 RV = normalize(reflect(-ObjSpaceViewDir(float4(pos, 1.0)), N));

		float4 I_a = _Ka * _Ambient;
		float  LdN = clamp(dot(L, N), 0, 1);
		float4 I_d = _Kd * LdN * col.rgba;
		float  LdRV = clamp(dot(L, RV), 0, 1);
		float  shinness = pow(500.0, _Shinness);
		float4 I_s = _Ks * pow(LdRV, shinness) * _Specular;
		float4 I = I_a + I_d + I_s;
		
		o.color = I;

		return o;
	}

	[maxvertexcount(24)]
	void geom(point v2g input[1], inout TriangleStream<g2f> outStream)
	{
		g2f o = (g2f)0;
		float3 pos = input[0].position.xyz;
		float  scl = input[0].position.w;
		float4 col = input[0].color;
		float4 rot = input[0].rotation;

		float  size = scl * _VoxelScaleRate;
	
		
		float3 posDiff = mul(unity_ObjectToWorld, pos) - _DistractorPosition.xyz;
		float  dist = sqrt(dot(posDiff, posDiff));

		if (dist < _DistractorRadius.x)
		{
			float x = dist * _DistractorRadius.y;
			float rr = pow(min(cos(x * _DistractorRadius.z), 1.0 - abs(x)), 3.0);
			pos.xyz += posDiff * rr * 5.0;
			size *= pow((dist * _DistractorRadius.y), 3.0);
		}
		

		// Top
		outStream.Append(getVertex(pos, g_cv[2], rot, (float3)size, g_norm[0], col, float2(0, 0)));
		outStream.Append(getVertex(pos, g_cv[3], rot, (float3)size, g_norm[0], col, float2(1, 0)));
		outStream.Append(getVertex(pos, g_cv[6], rot, (float3)size, g_norm[0], col, float2(0, 1)));
		outStream.Append(getVertex(pos, g_cv[7], rot, (float3)size, g_norm[0], col, float2(1, 1)));
		outStream.RestartStrip();

		// Bottom
		outStream.Append(getVertex(pos, g_cv[0], rot, (float3)size, g_norm[1], col, float2(0, 0)));
		outStream.Append(getVertex(pos, g_cv[1], rot, (float3)size, g_norm[1], col, float2(1, 0)));
		outStream.Append(getVertex(pos, g_cv[4], rot, (float3)size, g_norm[1], col, float2(0, 1)));
		outStream.Append(getVertex(pos, g_cv[5], rot, (float3)size, g_norm[1], col, float2(1, 1)));
		outStream.RestartStrip();

		// Front
		outStream.Append(getVertex(pos, g_cv[4], rot, (float3)size, g_norm[2], col, float2(0, 0)));
		outStream.Append(getVertex(pos, g_cv[5], rot, (float3)size, g_norm[2], col, float2(1, 0)));
		outStream.Append(getVertex(pos, g_cv[7], rot, (float3)size, g_norm[2], col, float2(0, 1)));
		outStream.Append(getVertex(pos, g_cv[6], rot, (float3)size, g_norm[2], col, float2(1, 1)));
		outStream.RestartStrip();

		// Back
		outStream.Append(getVertex(pos, g_cv[1], rot, (float3)size, g_norm[3], col, float2(0, 0)));
		outStream.Append(getVertex(pos, g_cv[0], rot, (float3)size, g_norm[3], col, float2(1, 0)));
		outStream.Append(getVertex(pos, g_cv[2], rot, (float3)size, g_norm[3], col, float2(0, 1)));
		outStream.Append(getVertex(pos, g_cv[3], rot, (float3)size, g_norm[3], col, float2(1, 1)));
		outStream.RestartStrip();

		// Left
		outStream.Append(getVertex(pos, g_cv[0], rot, (float3)size, g_norm[4], col, float2(0, 0)));
		outStream.Append(getVertex(pos, g_cv[4], rot, (float3)size, g_norm[4], col, float2(1, 0)));
		outStream.Append(getVertex(pos, g_cv[3], rot, (float3)size, g_norm[4], col, float2(0, 1)));
		outStream.Append(getVertex(pos, g_cv[7], rot, (float3)size, g_norm[4], col, float2(1, 1)));
		outStream.RestartStrip();

		// Right
		outStream.Append(getVertex(pos, g_cv[5], rot, (float3)size, g_norm[5], col, float2(0, 0)));
		outStream.Append(getVertex(pos, g_cv[1], rot, (float3)size, g_norm[5], col, float2(1, 0)));
		outStream.Append(getVertex(pos, g_cv[6], rot, (float3)size, g_norm[5], col, float2(0, 1)));
		outStream.Append(getVertex(pos, g_cv[2], rot, (float3)size, g_norm[5], col, float2(1, 1)));
		outStream.RestartStrip();

	}

	// --------------------------------------------------------------
	// Fragment Shader
	// --------------------------------------------------------------
	fixed4 frag(g2f i) : Color
	{
		return i.color;
	}
	ENDCG

	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma target   5.0
			#pragma vertex   vert
			#pragma geometry geom
			#pragma fragment frag
			ENDCG
		}
	}
}