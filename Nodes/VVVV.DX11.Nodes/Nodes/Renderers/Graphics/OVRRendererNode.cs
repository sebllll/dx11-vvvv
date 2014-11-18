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
using VVVV.Core.Logging;

namespace VVVV.DX11.Nodes
{
    [PluginInfo(Name = "Renderer", Category = "DX11", Version = "OVR", Author = "vux, tonfilm, sebl", AutoEvaluate = true,
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
        [Import()]
        public ILogger FLogger;

        private static Hmd hmd = null;
        private static OculusSharp.CAPI.HmdDesc hmdDesc;
        private Sizei texSize;

        private bool isInit = false;
        private uint frameIndex = 0;

        List<EyeRenderDesc> eyeList = new List<EyeRenderDesc>();

        private Sizei renderTargetSize;


        #endregion

        [ImportingConstructor()]
        public OVRRendererNode(IPluginHost host, IIOFactory iofactory, IHDEHost hdehost)
            : base(host, iofactory, hdehost)
        {

        }

        #region Evaluate

        #endregion

        public override void Render(DX11RenderContext context)
        {
            
            if (!this.updateddevices.Contains(context))
            {
                this.Update(null, context);

                //shutdownOVR();
                //this.InitOVR(context);
            }

            if (!isInit && FInEnabled[0])
            {
                shutdownOVR();
                this.InitOVR(context);
            }

            if (!FInEnabled[0])
            {
                shutdownOVR();
            }

            if (this.rendereddevices.Contains(context)) { return; }

            baseRendering(context);

            //OVRRender(context);

        }

        /*
        original swapchainsettings:
            renderTargetTexture = new Texture2D(device, new Texture2DDescription()
            {
                Format = Format.R8G8B8A8_UNorm,
                ArraySize = 1,
                MipLevels = 1,
                Width = renderTargetSize.w,
                Height = renderTargetSize.h,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

        used here:
             SwapChainDescription sd = new SwapChainDescription()
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription(0, 0, new Rational(rate, 1), format),
                IsWindowed = true,
                OutputHandle = handle,
                SampleDescription = sampledesc,
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput | Usage.ShaderInput,
                Flags = SwapChainFlags.None
            };
        */

        private void OVRRender(DX11RenderContext context)
        {
            FLogger.Log(LogType.Debug, "OVRRender(): ");
            Exception exception = null;

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

                        int w = this.FOutBackBuffer[0][context].Resource.Description.Width;
                        int h = this.FOutBackBuffer[0][context].Resource.Description.Height;
                        //FLogger.Log(LogType.Debug, "this.FOutBackBuffer[0][context].Resource.Description.width/height: " + w + " x " + h);

                        tex.Header.API = RenderAPIType.RenderAPI_D3D11;
                        tex.Header.TextureSize = renderTargetSize;
                        tex.pTexture = this.FOutBackBuffer[0][context].Resource.ComPointer;
                        //FLogger.Log(LogType.Debug, "this.FOutBackBuffer[0][context].Resource.ComPointer: " + this.FOutBackBuffer[0][context].Resource.ComPointer);

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
                        FLogger.Log(LogType.Debug, "viewport " + i + " : " + viewport);
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

        private void baseRendering(DX11RenderContext context)
        {
            Exception exception = null;

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

                        // RENDER OVR ---------------------------------------------------------------------
                        
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

            OVRRender(context);
            //Rethrow
            if (exception != null)
            {
                throw exception;
            }

        }

        private void InitOVR(DX11RenderContext context)
        {
            frameIndex = 0;

            Hmd.Initialize();

            if (Hmd.DetectHmd() > 0)
            {
                hmd = new Hmd(0);
            }
            else
            {
                hmd = new Hmd(OculusSharp.CAPI.HmdType.Hmd_DK2);
                //hmd = new Hmd(OculusSharp.CAPI.HmdType.Hmd_DKHD);
            }
            hmdDesc = hmd.GetDesc();

            hmd.ConfigureTracking(OculusSharp.CAPI.TrackingCap.TrackingCap_Orientation | OculusSharp.CAPI.TrackingCap.TrackingCap_MagYawCorrection | OculusSharp.CAPI.TrackingCap.TrackingCap_Position, 0);

            Sizei recommenedTex0Size = hmd.GetFovTextureSize(EyeType.Eye_Left, hmdDesc.DefaultEyeFov1, 0.5f);
            Sizei recommenedTex1Size = hmd.GetFovTextureSize(EyeType.Eye_Right, hmdDesc.DefaultEyeFov2, 0.5f);

            this.renderTargetSize.w = recommenedTex0Size.w + recommenedTex1Size.w;
            this.renderTargetSize.h = Math.Max(recommenedTex0Size.h, recommenedTex1Size.h);

            List<FovPort> fovList = new List<FovPort>();
            fovList.Add(hmdDesc.DefaultEyeFov1);
            fovList.Add(hmdDesc.DefaultEyeFov2);

            Sizei rtSize;
            rtSize.w = this.FOutBackBuffer[0][context].Resource.Description.Width;
            rtSize.h = this.FOutBackBuffer[0][context].Resource.Description.Height;

            FLogger.Log(LogType.Debug, "texture Size:      " + rtSize.w + " x " + rtSize.h);
            FLogger.Log(LogType.Debug, "renderTarget Size: " + renderTargetSize.w + " x " + renderTargetSize.h);

            this.chain = this.FOutBackBuffer[0][context];

            FLogger.Log(LogType.Debug, " ");
            FLogger.Log(LogType.Debug, "C O N F I G: ");

            D3D11ConfigData config = new D3D11ConfigData();
            config.Header.API = RenderAPIType.RenderAPI_D3D11;
            config.Header.Multisample = 1;
            config.Header.RTSize = renderTargetSize;
            //config.pDevice = context.CurrentDeviceContext.Device.ComPointer;
            config.pDevice = context.Device.ComPointer;
                FLogger.Log(LogType.Debug, "pDevice: ");
                FLogger.Log(LogType.Debug, "context.CurrentDeviceContext.Device.ComPointer: " + context.CurrentDeviceContext.Device.ComPointer);
                FLogger.Log(LogType.Debug, "context.Device.ComPointer:                      " + context.Device.ComPointer);
                FLogger.Log(LogType.Debug, " ");
        
            //config.pDeviceContext = context.CurrentDeviceContext.Device.ImmediateContext.ComPointer;
            config.pDeviceContext = context.CurrentDeviceContext.ComPointer;
                FLogger.Log(LogType.Debug, "pDeviceContext: ");    
                FLogger.Log(LogType.Debug, "context.CurrentDeviceContext.Device.ImmediateContext.ComPointer: " + context.CurrentDeviceContext.Device.ImmediateContext.ComPointer);
                FLogger.Log(LogType.Debug, "context.CurrentDeviceContext.ComPointer:                         " + context.CurrentDeviceContext.ComPointer);
                FLogger.Log(LogType.Debug, " ");

            //config.pSwapChain = this.chain.swapchain.ComPointer;
            config.pSwapChain = this.FOutBackBuffer[0][context].swapchain.ComPointer;
                FLogger.Log(LogType.Debug, "pSwapChain: ");
                FLogger.Log(LogType.Debug, "this.chain.swapchain.ComPointer:                      " + this.chain.swapchain.ComPointer);
                FLogger.Log(LogType.Debug, "this.FOutBackBuffer[0][context].swapchain.ComPointer: " + this.FOutBackBuffer[0][context].swapchain.ComPointer);
                FLogger.Log(LogType.Debug, " ");

            //config.pBackBufferRT = this.chain.RTV.ComPointer;
            config.pBackBufferRT = this.FOutBackBuffer[0][context].RTV.ComPointer;
            FLogger.Log(LogType.Debug, "pBackBufferRT: ");
            FLogger.Log(LogType.Debug, "this.chain.RTV.ComPointer:                      " + this.chain.RTV.ComPointer);
            FLogger.Log(LogType.Debug, "this.FOutBackBuffer[0][context].RTV.ComPointer: " + this.FOutBackBuffer[0][context].RTV.ComPointer);
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