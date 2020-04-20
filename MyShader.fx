﻿float4x4 worldViewProj;

struct VS_IN
{
	float3 pos : POSITION;
	float3 nor : NORMAL;
	float3 tan : TANGENTU;
	float2 tex : TEXC;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float4 col : COLOR;
};

PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;
	//output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
	output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
	output.col = float4(input.nor, 1.0f);
	return output;
}

float4 PS(PS_IN input) : SV_Target
{
	return input.col;
}
