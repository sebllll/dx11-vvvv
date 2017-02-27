#region usings
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using FeralTic.DX11;
using System.Linq;
using ShaderGraphExperiment;
using SharpDX;
#endregion usings

namespace VVVV.DX11.Nodes.Layers
{
    #region PluginInfo
    [PluginInfo(Name = "PixelShader", Category = ShaderGraph.Category)]
	#endregion PluginInfo
	public class ShaderGraphPixelShader : DX11ShaderNode, IPartImportsSatisfiedNotification, IPluginEvaluate
    {
        [Input("Shader")]
        public Pin<IFunctionNode<Vector4>> FShaderProvider; //=> FShaderIn as ISpread<IFunctionNode<float>>;
        Field<IFunctionNode<Vector4>> ShaderField = new Field<IFunctionNode<Vector4>>();

        //public INodeIn FShaderIn;
        //static IGlobalVarNode<float> RadiusDefault = new Default<float>("Default Color", 0.5f);

        [Input("Apply Shader")]
        public ISpread<bool> FApply;

        [Output("Shader Code")]
        public ISpread<string> FCode;

        [ImportingConstructor()]
        public ShaderGraphPixelShader(IPluginHost host, IIOFactory factory)
        : base(host, factory)
        { }

        public new void Evaluate(int SpreadMax)
        {
            base.Evaluate(SpreadMax);
            if (FApply[0] || ShaderField.Changed(FShaderProvider))
                RecalcShader(false);
        }

        void IPartImportsSatisfiedNotification.OnImportsSatisfied()
        {
            base.OnImportsSatisfied();
            FShaderProvider.Connected += FShaderProvider_Connected;
            RecalcShader(true);
        }

        private void FShaderProvider_Connected(object sender, PinConnectionEventArgs args)
        {
            RecalcShader(false);
        }

        public void RecalcShader(bool isNew)
        { 
            var declarations = "";
            var pscode = @"
    float4 col = cAmb;
    return col;";

            if (FShaderProvider.SliceCount > 0 && FShaderProvider[0] != null)
            {
                var result = FShaderProvider[0].Traverse();

                declarations = string.Join(@"
", result.FunctionDeclarations.Values.Concat(result.GlobalVars.Select(gv => gv.Value.ToString())));

                pscode = string.Join(@"
    ", result.LocalDeclarations.Values) + $@"
    return {result.LocalDeclarations[FShaderProvider[0]].Identifier};";
;
            }

            var effectText =
@" 
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
    float4 cAmb < bool color = true; String uiname = ""Color"";> = { 1.0f,1.0f,1.0f,1.0f };
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
"
    + declarations +
@"

float4 PS(vs2ps In): SV_Target
{
    "
    + pscode +
@"
}

technique10 Patched
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_4_0, VS()));
        SetPixelShader(CompileShader(ps_4_0, PS()));
    }
}";

            FCode.SliceCount = 1;
            FCode[0] = effectText;

            SetShader(DX11Effect.FromString(effectText), isNew, "");
        }

	}
}
