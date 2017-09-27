using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using VVVV.Core;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

using FeralTic.DX11;
using FeralTic.DX11.Resources;
using System.ComponentModel.Composition;
using VVVV.Core.Logging;
using VVVV.DX11.Lib.Devices;

using SlimDX.Direct3D11;
//using SlimDX.DXGI;

namespace VVVV.DX11.Nodes
{
    [PluginInfo(Name = "FrameDelay", Category = "DX11.Texture", Version = "3d",
        AutoEvaluate = true,
        Author = "vux, sebl, timpernagel",
        Warnings = "Doesn't suppport multicontext, experimental,non spreadable")]
    public class FrameDelayTexture3DNode : IPluginEvaluate, IDX11ResourceHost, IDisposable
    {
        [Input("Texture In", IsSingle = true)]
        protected Pin<DX11Resource<DX11Texture3D>> FTextureInput;

        [Input("Flush", IsSingle = true)]
        protected ISpread<bool> FInFlush;

        [Output("Texture Out", IsSingle = true, AllowFeedback = true)]
        protected Pin<DX11Resource<DX11Texture3D>> FTextureOutput;

        //[Output("Original Texture Out", IsSingle = true, AllowFeedback = true)]
        //protected Pin<DX11Resource<DX11Texture3D>> lastTex;

        private IHDEHost hde;
        private DX11Texture3D lasttexture = null;

        private DX11Resource<DX11Texture3D> lastTex = null;
        private DX11Resource<DX11Texture3D> currentTex = null;

        private ILogger logger;

        [ImportingConstructor()]
        public FrameDelayTexture3DNode(IHDEHost hde, ILogger logger)
        {
            this.hde = hde;
            this.hde.MainLoop.OnResetCache += this.MainLoop_OnPresent;
            this.logger = logger;
        }

        private void MainLoop_OnPresent(object sender, EventArgs e)
        {
            //Rendering is finished, so should be ok to grab texture now
            if (this.FTextureInput.PluginIO.IsConnected && this.FTextureInput.SliceCount > 0)
            {

                //Little temp hack, grab context from global, since for now we have one context anyway
                DX11RenderContext context = DX11GlobalDevice.DeviceManager.RenderContexts[0];

                if (this.FTextureInput[0].Contains(context))
                {
                    //DX11Texture3D texture = this.FTextureInput[0][context];


                    Texture3D texture = this.FTextureInput[0][context].Resource;
                    Texture3DDescription tDesc = this.FTextureInput[0][context].Resource.Description;

                    //DX11Texture3D texture = DX11Texture3D.FromDescription(context, this.FTextureInput[0][context].Resource.Description);


                    if (this.lasttexture != null)
                    {
                        if (this.lasttexture.Resource.Description != texture.Description) { this.DisposeTexture(); }
                    }

                    if (this.lasttexture == null)
                    {
                        this.lasttexture = DX11Texture3D_extension.FromDescription(context, texture.Description);
                        //logger.Log(LogType.Debug, "init texture " + texture.Description.Width);
                    }

                    //context.CurrentDeviceContext.CopyResource(texture.Resource, this.lasttexture.Resource);
                    context.CurrentDeviceContext.CopyResource(texture, this.lasttexture.Resource);

                    if (this.FInFlush[0]) { context.CurrentDeviceContext.Flush(); }
                }
                else
                {
                    this.DisposeTexture();
                    // logger.Log(LogType.Debug, "dispose");
                }
            }
            else
            {
                this.DisposeTexture();
                //logger.Log(LogType.Debug, "dispose");
            }
        }

        private void DisposeTexture()
        {
            if (this.lasttexture != null)
            {
                //logger.Log(LogType.Debug, "disp");
                this.lasttexture.Dispose(); this.lasttexture = null;
            }
        }

        public void Evaluate(int SpreadMax)
        {
            if (this.FTextureOutput[0] == null)
            {
                this.FTextureOutput[0] = new DX11Resource<DX11Texture3D>();
            }

            if (this.lastTex == null)
            {
                this.lastTex = new DX11Resource<DX11Texture3D>();
            }

            if (this.currentTex == null)
            {
                this.currentTex = new DX11Resource<DX11Texture3D>();
            }
        }

        public void Update(DX11RenderContext context)
        {
            if (this.lasttexture != null)
            {
                this.FTextureOutput[0][context] = this.lasttexture;

                //logger.Log(LogType.Debug, "Update(): " + this.lasttexture.Description.Width + " " + this.lasttexture.Resource.ComPointer.ToString());

                //this.currentTex[context] = this.FTextureInput[0][context];

                //this.FTextureOutput[0][context] = this.lastTex[context];

                //context.CurrentDeviceContext.CopyResource(currentTex[context].Resource, this.lastTex[context].Resource);
                //this.lastTex[context] = this.currentTex[context];


            }
            else
            {
                this.FTextureOutput[0].Dispose(context);
                //this.lastTex.Dispose(context);
                //this.currentTex.Dispose(context);
            }
        }

        public void Destroy(DX11RenderContext context, bool force)
        {

        }

        public void Dispose()
        {
            this.hde.MainLoop.OnResetCache -= this.MainLoop_OnPresent;
        }
    }
}
