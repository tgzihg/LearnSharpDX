cbuffer cbPerObject {
	float4x4 worldViewProj;
};

struct VS_IN
{
	float3 pos : POSITION;
	float4 col : COLOR;
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
	output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
	output.col = input.col;
	return output;
}

float4 PS(PS_IN input) : SV_Target
{
	return input.col;
}

float4 PS_Tex(PS_IN pin, uniform bool gApplyTexture) : SV_Target
{
	//Do Common Work
	if (gApplyTexture)
	{
		//Apply texture
	}
	//Do more work
	return float4(0, 0, 0, 0);
}

RasterizerState WireFrameRS
{
	FillMode = Wireframe;
	CullMode = None;
	FrontCounterClockwise = false;
};

RasterizerState SolidFrameRS
{
	FillMode = Solid;
	CullMode = None;
	FrontCounterClockwise = false;
};

technique11 ColorTech
{
	pass P0
	{
		SetVertexShader(CompileShader(vs_5_0, VS()));
		SetPixelShader(CompileShader(ps_5_0, PS()));
		SetRasterizerState(WireFrameRS);
	}

	pass P1
	{
		SetVertexShader(CompileShader(vs_5_0, VS()));
		SetPixelShader(CompileShader(ps_5_0, PS()));
		SetRasterizerState(SolidFrameRS);
	}
}
