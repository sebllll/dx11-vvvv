using FeralTic.DX11;
using FeralTic.DX11.Resources;
using OculusSharp.CAPI;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VVVV.DX11.Lib.Rendering;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VMath;

namespace VVVV.DX11.Nodes
{
    [PluginInfo(Name="Renderer",Category="DX11", Version = "OVR" ,Author="vux, tonfilm, sebl", AutoEvaluate=true,
        InitialWindowHeight = 731, InitialWindowWidth = 1182, InitialBoxWidth = 1182, InitialBoxHeight = 731, InitialComponentMode = TComponentMode.InAWindow)]
    public class OVRRendererNode : DX11RendererNode
    {
      
        #region Input Pins
        
        #endregion

        #region Output Pins
        [Output("Pose Position")]
        ISpread<Vector3D> FPosePos;

        [Output("Pose Orientation")]
        ISpread<Vector4D> FPoseOrient;
        #endregion

        #region Fields
        private static Hmd hmd = null;
        private static OculusSharp.CAPI.HmdDesc hmdDesc;
        private Sizei texSize;

        private bool isInit = false;
        private uint frameIndex = 0;

        Texture2D[] eyetextures = new Texture2D[2];
        List<EyeRenderDesc> eyeList = new List<EyeRenderDesc>();

        private Sizei renderTargetSize;


        #endregion

        [ImportingConstructor()]
        public OVRRendererNode(IPluginHost host, IIOFactory iofactory, IHDEHost hdehost)
            : base(host, iofactory, hdehost)
        {
            
        }

        #region Evaluate
        //public override void Evaluate(int SpreadMax)
        //{
        //    base.Evaluate(SpreadMax);

            
        //}
        #endregion

        public override void Render(DX11RenderContext context)
        {
            Exception exception = null;

            //base.Render(context);
            chain = this.FOutBackBuffer[0][context];

            if (!this.updateddevices.Contains(context)) 
            {
                base.Update(null, context);

                shutdownOVR();
                this.InitOVR(context); 

            }

            if (this.rendereddevices.Contains(context)) { return; }

            // base Render():            
            if (this.FInEnabled[0])
            {

                //if (this.BeginQuery != null)
                //{
                //    this.BeginQuery(context);
                //}

                this.chain = this.FOutBackBuffer[0][context];
                DX11GraphicsRenderer renderer = this.renderers[context];

                renderer.EnableDepth = this.FInDepthBuffer[0];
                renderer.DepthStencil = this.depthmanager.GetDepthStencil(context);
                renderer.DepthMode = this.depthmanager.Mode;
                renderer.SetRenderTargets(chain);
                renderer.SetTargets();

                try
                {
                    if (this.FInClear[0])
                    {
                        //Remove Shader view if bound as is
                        context.CurrentDeviceContext.ClearRenderTargetView(chain.RTV, this.FInBgColor[0].Color);
                    }

                    if (this.FInClearDepth[0])
                    {
                        if (this.FInDepthBuffer[0])
                        {
                            this.depthmanager.Clear(context);
                        }
                    }

                    //Only call render if layer connected
                    if (this.FInLayer.PluginIO.IsConnected)
                    {
                        int rtmax = Math.Max(this.FInProjection.SliceCount, this.FInView.SliceCount);
                        rtmax = Math.Max(rtmax, this.FInViewPort.SliceCount);

                        settings.ViewportCount = rtmax;

                        bool viewportpop = this.FInViewPort.PluginIO.IsConnected;

                        for (int i = 0; i < rtmax; i++)
                        {
                            this.RenderSlice(context, settings, i, viewportpop);
                        }
                    }

                    //if (this.EndQuery != null)
                    //{
                    //    this.EndQuery(context);
                    //}
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                finally
                {
                    renderer.CleanTargets();
                }
            }

            this.rendereddevices.Add(context);

            //Rethrow
            if (exception != null)
            {
                throw exception;
            }

            // ---------------- OVR ----------------

            if (isInit)
            {
                try
                {
                    FrameTiming timing = hmd.BeginFrame(frameIndex);

                    frameIndex++;

                    List<Posef> poseList = new List<Posef>();
                    poseList.Add(hmd.GetEyePose(hmdDesc.EyeRenderOrder1));
                    poseList.Add(hmd.GetEyePose(hmdDesc.EyeRenderOrder2));

                    FPosePos.SliceCount = FPoseOrient.SliceCount = poseList.Count;
                    for (int i = 0; i < poseList.Count; i++)
                    {
                        FPosePos[i] = new Vector3D(poseList[i].Position.x, poseList[i].Position.y, poseList[i].Position.z);
                        FPoseOrient[i] = new Vector4D(poseList[i].Orientation.x, poseList[i].Orientation.y, poseList[i].Orientation.z, poseList[i].Orientation.w);
                    }

                    // fill textures
                    List<D3D11TextureData> textureList = new List<D3D11TextureData>();

                    for (int i = 0; i < 2; i++)
                    {
                        D3D11TextureData tex = new D3D11TextureData();
                        SlimDX.Direct3D11.Viewport viewport;

                        tex.Header.API = RenderAPIType.RenderAPI_D3D11;
                        tex.Header.TextureSize = renderTargetSize;
                        tex.pTexture = this.FOutBackBuffer[0][context].Resource.ComPointer;
                        tex.pSRView = this.FOutBackBuffer[0][context].SRV.ComPointer;
                        textureList.Add(tex);

                        if (i == 0)
                            viewport = new SlimDX.Direct3D11.Viewport(0, 0, renderTargetSize.w / 2, renderTargetSize.h);
                        else
                            viewport = new SlimDX.Direct3D11.Viewport((renderTargetSize.w + 1) / 2, 0, renderTargetSize.w / 2, renderTargetSize.h);


                        //context.Rasterizer.SetViewport(viewport);
                        context.CurrentDeviceContext.Rasterizer.SetViewports(viewport);
                        tex.Header.RenderViewport.Pos.x = (int)viewport.X;
                        tex.Header.RenderViewport.Pos.y = (int)viewport.Y;
                        tex.Header.RenderViewport.Size.w = (int)viewport.Width;
                        tex.Header.RenderViewport.Size.h = (int)viewport.Height;
                    }

                    hmd.EndFrame(poseList.ToArray(), textureList.ToArray());  // Exception in here :(


                }
                catch (Exception e)
                {
                    exception = e;
                }

                //Rethrow
                if (exception != null)
                {
                    throw exception;
                }

            }
        
        }

        private void InitOVR(DX11RenderContext context)
        {
            frameIndex = 0;

            Hmd.Initialize();

            if (Hmd.DetectHmd() > 0)
                hmd = new Hmd(0);
            else
                hmd = new Hmd(OculusSharp.CAPI.HmdType.Hmd_DK2);
            hmdDesc = hmd.GetDesc();

            hmd.ConfigureTracking(OculusSharp.CAPI.TrackingCap.TrackingCap_Orientation | OculusSharp.CAPI.TrackingCap.TrackingCap_MagYawCorrection | OculusSharp.CAPI.TrackingCap.TrackingCap_Position, 0);

            Sizei recommenedTex0Size = hmd.GetFovTextureSize(EyeType.Eye_Left, hmdDesc.DefaultEyeFov1, 0.5f);
            Sizei recommenedTex1Size = hmd.GetFovTextureSize(EyeType.Eye_Right, hmdDesc.DefaultEyeFov2, 0.5f);

            this.renderTargetSize.w = recommenedTex0Size.w + recommenedTex1Size.w;
            this.renderTargetSize.h = Math.Max(recommenedTex0Size.h, recommenedTex1Size.h);

            List<FovPort> fovList = new List<FovPort>();
            fovList.Add(hmdDesc.DefaultEyeFov1);
            fovList.Add(hmdDesc.DefaultEyeFov2);


            this.chain = this.FOutBackBuffer[0][context];

            D3D11ConfigData config = new D3D11ConfigData();
            config.Header.API = RenderAPIType.RenderAPI_D3D11;
            config.Header.Multisample = 1;
            config.Header.RTSize = renderTargetSize; 
            config.pDevice = context.Device.ComPointer;
            config.pDeviceContext = context.CurrentDeviceContext.ComPointer;
            config.pSwapChain = this.chain.swapchain.ComPointer;
            config.pBackBufferRT = this.chain.RTV.ComPointer;

            // hmd stuff -------------------------------

            hmd.ConfigureRendering(config, DistortionCap.DistortionCap_Chromatic | DistortionCap.DistortionCap_TimeWarp | DistortionCap.DistortionCap_Vignette, fovList, eyeList);

            hmd.AttachToWindow(base.WindowHandle, null, null);

            isInit = true;
        }

        private void shutdownOVR()
        {
            frameIndex = 0;
            isInit = false;

            if (hmd != null)
            {
                hmd.Dispose();
                OculusSharp.CAPI.Hmd.Shutdown();
            }

        }
    }
}