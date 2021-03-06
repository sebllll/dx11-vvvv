﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V1;
using FeralTic.DX11;
using FeralTic.DX11.Resources;
using System.ComponentModel.Composition;

namespace VVVV.DX11.Nodes
{
    [PluginInfo(Name = "StencilView", Category = "DX11.Texture", Version = "2d",
        AutoEvaluate = true,
        Author = "vux",
        Warnings = "")]
    public class StencilTextureNode : IPluginEvaluate, IDX11ResourceHost
    {
        [Input("Depth Stencil In", IsSingle = true)]
        protected Pin<DX11Resource<DX11DepthStencil>> FTextureInput;

        [Output("Texture Out", IsSingle = true)]
        protected Pin<DX11Resource<DX11Texture2D>> FTextureOutput;

        [ImportingConstructor()]
        public StencilTextureNode(IHDEHost hde)
        {

        }

        public void Evaluate(int SpreadMax)
        {
            if (this.FTextureOutput[0] == null)
            {
                this.FTextureOutput[0] = new DX11Resource<DX11Texture2D>();
            }
        }

        public void Update(DX11RenderContext context)
        {
            if (this.FTextureInput.IsConnected)
            {
                this.FTextureOutput[0][context] = this.FTextureInput[0][context].Stencil;
            }
            else
            {
                this.FTextureOutput[0].Remove(context);
            }
        }

        public void Destroy(DX11RenderContext context, bool force)
        {

        }
    }
}
