 
SamplerState linearSampler: IMMUTABLE
{
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = Clamp;
    AddressV = Clamp;
};

cbuffer cbPerDraw : register(b0)
{
    float4x4 tVP : LAYERVIEWPROJECTION;
};

cbuffer cbPerObj : register(b1)
{
    float4x4 tW : WORLD;
};

struct VS_IN
{
    float4 PosO : POSITION;
	float4 TexCd : TEXCOORD0;
};

struct vs2ps
{
    float4 PosWVP: SV_Position;
    float2 TexCd: TEXCOORD0;
};

vs2ps VS(VS_IN input)
{
    vs2ps output;
    output.PosWVP = mul(input.PosO, mul(tW, tVP));
    output.TexCd = input.TexCd.xy;
    return output;
}

float Quadratic2DDF(float2 center, float radius, float2 samplePos)
{
    float2 d = abs(samplePos - center) - radius;
    return max(d.x, d.y); 
}

float Circular2DDF(float2 center, float radius, float2 samplePos)
{
    return length(samplePos - center) - radius;
}

float Union2DDF(float d, float d2)
{
    return max(d, d2);
}

float Outline2DDF(float d, float radius)
{
    return abs(d) - radius;
}

float4 Colorize2DDF(float d, float4 color, float4 backcolor)
{
    return lerp(color, backcolor, saturate(d * 500));
}

float Grow2DDF(float d, float radius)
{
    return d - radius;
}

float IsIn2DDF(float d)
{
    return saturate(d * 500);
}

float4 Blend(float4 color, float4 color2, float blend)
{
    return lerp(color, color2, blend);
}
float2 Center = float2(0.5, 0.5);
float QuadRadius = 0.48;
float CircleRadius = 0.24;
float Thickness = 0.02;
float4 RingColor <bool color=true; String uiname="RingColor";> = float4(1, 1, 1, 1);
float4 BodyColor <bool color=true; String uiname="BodyColor";> = float4(0, 0, 0, 1);
float4 BackColor <bool color=true; String uiname="BackColor";> = float4(0.5848, 0.5848, 0.5848, 0);
float MaskGrow = 0.004;

float4 PS(vs2ps In): SV_Target
{
    float var0 = Quadratic2DDF(Center, QuadRadius, In.TexCd);
    float var1 = Circular2DDF(Center, CircleRadius, In.TexCd);
    float var2 = Union2DDF(var0, var1);
    float var3 = Outline2DDF(var2, Thickness);
    float4 var4 = Colorize2DDF(var3, RingColor, BodyColor);
    float var5 = Grow2DDF(var2, MaskGrow);
    float var6 = IsIn2DDF(var5);
    float4 var7 = Blend(var4, BackColor, var6);
    return var7;
}

technique10 Patched
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_4_0, VS()));
        SetPixelShader(CompileShader(ps_4_0, PS()));
    }
}