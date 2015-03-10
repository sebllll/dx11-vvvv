using FeralTic.DX11;
using FeralTic.DX11.Resources;

using SlimDX;
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

using OculusWrap;

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

        //private static Hmd hmd = null;
        //private static OculusSharp.CAPI.HmdDesc hmdDesc;
        //private Sizei texSize;

        private bool isInit = false;
        private uint frameIndex = 0;

        //List<EyeRenderDesc> eyeList = new List<EyeRenderDesc>();

        //private Sizei renderTargetSize;

        Wrap oculus = new Wrap();
        Hmd hmd;

        DateTime startTime;

        OculusWrap.OVR.EyeRenderDesc[] eyeRenderDesc;
        OculusWrap.OVR.Recti[] eyeRenderViewport;
        SlimDX.Direct3D11.Device device;
        OculusWrap.OVR.D3D11.D3D11TextureData[] eyeTexture;

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
                startTime = DateTime.Now;
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
                    float timeSinceStart = (float)(DateTime.Now - startTime).TotalSeconds;

                    OculusWrap.OVR.HSWDisplayState hasWarningState;
                    hmd.GetHSWDisplayState(out hasWarningState);

                    // Remove the health and safety warning.
                    if (hasWarningState.Displayed == 1)
                        hmd.DismissHSWDisplay();

                    hmd.BeginFrame(0);

                    // Clear views
                    //immediateContext.OutputMerger.SetDepthStencilState(clearDepthStencilState);
                    //immediateContext.ClearDepthStencilView(renderTargetDepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
                    //immediateContext.ClearRenderTargetView(renderTargetRenderTargetView, Color.Black);

                    float bodyYaw = 3.141592f;
                    OVR.Vector3f headPos = new OVR.Vector3f(0.0f, hmd.GetFloat(OVR.OVR_KEY_EYE_HEIGHT, 1.6f), -5.0f);
                    //Viewport viewport = new Viewport(0, 0, renderTargetTexture.Description.Width, renderTargetTexture.Description.Height, 0.0f, 1.0f);
                    Viewport viewport = new Viewport(0, 0, this.FOutBackBuffer[0][context].Resource.Description.Width, this.FOutBackBuffer[0][context].Resource.Description.Height, 0.0f, 1.0f);
                    OVR.Posef[] eyeRenderPose = new OVR.Posef[2];

                    

                    //immediateContext.OutputMerger.SetDepthStencilState(renderTargetDepthStencilState);
                    //immediateContext.OutputMerger.SetRenderTargets(renderTargetDepthStencilView, renderTargetRenderTargetView);
                    //immediateContext.Rasterizer.SetViewport(viewport);

                    for (int eyeIndex = 0; eyeIndex < OVR.Eye_Count; eyeIndex++)
                    {
                        OVR.EyeType eye = hmd.EyeRenderOrder[eyeIndex];

                        eyeRenderPose[(int)eye] = hmd.GetHmdPosePerEye(eye);

                        // Get view and projection matrices
                        Quaternion rotationQuaternion = Helpers.ToQuaternion(eyeRenderPose[(int)eye].Orientation);

                        Vector3 eyePosition = eyeRenderPose[(int)eye].Position.ToVector3();

                        Matrix          rollPitchYaw = Matrix.RotationY(bodyYaw);
                        Matrix          rotation = Matrix.RotationQuaternion(rotationQuaternion);
                        Matrix          finalRollPitchYaw = rollPitchYaw * rotation;
                        Vector3         finalUp = Vector3.Transform(new Vector3(0, -1, 0), finalRollPitchYaw).ToVector3();
                        Vector3         finalForward = Vector3.Transform(new Vector3(0, 0, -1), finalRollPitchYaw).ToVector3();
                        Vector3         shiftedEyePos = headPos.ToVector3() + Vector3.Transform(eyePosition, rollPitchYaw).ToVector3();
                        Matrix          viewMatrix = Matrix.LookAtLH(shiftedEyePos, shiftedEyePos + finalForward, finalUp);
                        Matrix       projectionMatrix = oculus.Matrix4f_Projection(eyeRenderDesc[(int)eye].Fov, 0.1f, 100.0f, false).ToMatrix();
                        
                        //projectionMatrix.Transpose();

                        Matrix.Transpose(projectionMatrix);

                        // Set the viewport for the current eye.
                        viewport = new Viewport(eyeRenderViewport[(int)eye].Position.x, eyeRenderViewport[(int)eye].Position.y, eyeRenderViewport[(int)eye].Size.Width, eyeRenderViewport[(int)eye].Size.Height, 0.0f, 1.0f);
                        //immediateContext.Rasterizer.SetViewport(viewport);
                        device.ImmediateContext.Rasterizer.SetViewports(viewport);

                        Matrix world = Matrix.RotationX(timeSinceStart) * Matrix.RotationY(timeSinceStart * 2) * Matrix.RotationZ(timeSinceStart * 3);
                        Matrix worldViewProjection = world * viewMatrix * projectionMatrix;
                        //worldViewProjection.Transpose();

                        Matrix.Transpose(worldViewProjection);

                        //immediateContext.UpdateSubresource(ref worldViewProjection, contantBuffer);
                        //device.ImmediateContext.UpdateSubresource(ref worldViewProjection)

                        // Draw the cube
                        //immediateContext.Draw(36, 0);
                    }


                    hmd.EndFrame(eyeRenderPose, eyeTexture);


                    # region ood
                    /*
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
                    */
                    # endregion
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

            oculus.Initialize();

            // Use the head mounted display, if it's available, otherwise use the debug HMD.
            int numberOfHeadMountedDisplays = oculus.Hmd_Detect();
            if (numberOfHeadMountedDisplays > 0)
                hmd = oculus.Hmd_Create(0);
            else
                hmd = oculus.Hmd_CreateDebug(OculusWrap.OVR.HmdType.DK2);

            if (hmd == null)
            {
                FLogger.Log(LogType.Warning, "Oculus Rift not detected.");
                return;
            }
            if (hmd.ProductName == string.Empty)
                FLogger.Log(LogType.Warning, "The HMD is not enabled. There's a tear in the Rift");

            OVR.Recti destMirrorRect;
            OVR.Recti sourceRenderTargetRect;
            //hmd.AttachToWindow(form.Handle, out destMirrorRect, out sourceRenderTargetRect);
            hmd.AttachToWindow(base.WindowHandle, out destMirrorRect, out sourceRenderTargetRect);


            // Create a backbuffer that's the same size as the HMD's resolution.
            OVR.Sizei backBufferSize;
            backBufferSize.Width = hmd.Resolution.Width;
            backBufferSize.Height = hmd.Resolution.Height;

            // Configure Stereo settings.
            OVR.Sizei recommenedTex0Size = hmd.GetFovTextureSize(OVR.EyeType.Left, hmd.DefaultEyeFov[0], 1.0f);
            OVR.Sizei recommenedTex1Size = hmd.GetFovTextureSize(OVR.EyeType.Right, hmd.DefaultEyeFov[1], 1.0f);

            // Define a render target texture that's the size that the Oculus SDK recommends, for it's default field of view.
            OVR.Sizei renderTargetTextureSize;
            renderTargetTextureSize.Width = recommenedTex0Size.Width + recommenedTex1Size.Width;
            renderTargetTextureSize.Height = Math.Max(recommenedTex0Size.Height, recommenedTex1Size.Height);

            // Create DirectX drawing device.
            //SharpDX.Direct3D11.Device device = new Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.Debug);
            //SlimDX.Direct3D11.Device device = context.Device;
            device = context.Device;

            // Create DirectX Graphics Interface factory, used to create the swap chain.
            //Factory factory = new Factory();
            Factory factory = context.Factory;

            // Ignore all windows events.
            //factory.MakeWindowAssociation(form.Handle, WindowAssociationFlags.IgnoreAll);

            // Define the properties of the swap chain.
            SwapChainDescription swapChainDescription = new SwapChainDescription();
            swapChainDescription.BufferCount = 1;
            swapChainDescription.IsWindowed = true;
            //swapChainDescription.OutputHandle = form.Handle;
            swapChainDescription.OutputHandle = base.WindowHandle;
            swapChainDescription.SampleDescription = new SampleDescription(1, 0);
            swapChainDescription.Usage = Usage.RenderTargetOutput | Usage.ShaderInput;
            swapChainDescription.SwapEffect = SwapEffect.Sequential;
            swapChainDescription.Flags = SwapChainFlags.AllowModeSwitch;
            //swapChainDescription.ModeDescription.Width = backBufferSize.Width;
            //swapChainDescription.ModeDescription.Height = backBufferSize.Height;
            //swapChainDescription.ModeDescription.Format = Format.R8G8B8A8_UNorm;
            //swapChainDescription.ModeDescription.RefreshRate.Numerator = 0;
            //swapChainDescription.ModeDescription.RefreshRate.Denominator = 1;
            SlimDX.Rational rate = new SlimDX.Rational(0, 1);
            swapChainDescription.ModeDescription = new ModeDescription(backBufferSize.Width, backBufferSize.Height, rate, Format.R8G8B8A8_UNorm);

            // Create the swap chain.
            //SharpDX.DXGI.SwapChain swapChain = new SwapChain(factory, device, swapChainDescription);
            SlimDX.DXGI.SwapChain swapChain = new SwapChain(factory, device, swapChainDescription);



            // ----------------- testing stuff


            //this.FOutBackBuffer[0][context].SwapChain.Description = swapChainDescription;




            DX11Resource<DX11SwapChain> s = this.FOuFS[0];
            DX11Resource<DX11SwapChain> bb = this.FOutBackBuffer[0];
            ISpread<int> handle = this.FOutDeviceHandle;
            DX11SwapChain c = this.chain;
            VVVV.DX11.DX11RenderSettings setinger = this.settings;

            // -----------------




            // Retrieve the back buffer of the swap chain.
            //Texture2D backBufferTexture = swapChain.GetBackBuffer<Texture2D>(0);				// = BackBuffer
            Texture2D backBufferTexture = SlimDX.Direct3D11.Texture2D.FromSwapChain<SlimDX.Direct3D11.Texture2D>(swapChain, 0);

            RenderTargetView backBufferRenderTargetView = new RenderTargetView(device, backBufferTexture);		// = BackBufferRTV

            // Create a depth buffer, using the same width and height as the back buffer.
            Texture2DDescription depthBufferDescription = new Texture2DDescription();
            depthBufferDescription.Format = Format.D32_Float;
            depthBufferDescription.ArraySize = 1;
            depthBufferDescription.MipLevels = 1;
            depthBufferDescription.Width = backBufferSize.Width;
            depthBufferDescription.Height = backBufferSize.Height;
            depthBufferDescription.SampleDescription = new SampleDescription(1, 0);
            depthBufferDescription.Usage = ResourceUsage.Default;
            depthBufferDescription.BindFlags = BindFlags.DepthStencil;
            depthBufferDescription.CpuAccessFlags = CpuAccessFlags.None;
            depthBufferDescription.OptionFlags = ResourceOptionFlags.None;

            // Define how the depth buffer will be used to filter out objects, based on their distance from the viewer.
            DepthStencilStateDescription depthStencilStateDescription = new DepthStencilStateDescription();
            depthStencilStateDescription.IsDepthEnabled = true;
            depthStencilStateDescription.DepthComparison = Comparison.Less;
            depthStencilStateDescription.DepthWriteMask = DepthWriteMask.Zero;

            // Create the depth buffer.
            Texture2D depthBufferTexture = new Texture2D(device, depthBufferDescription);
            DepthStencilView depthStencilView = new DepthStencilView(device, depthBufferTexture);
            //DepthStencilState depthStencilState = new DepthStencilState(device, depthStencilStateDescription);
            DepthStencilState depthStencilState = DepthStencilState.FromDescription(device, depthStencilStateDescription);

            // Define a texture that will contain the rendered graphics.
            Texture2DDescription texture2DDescription = new Texture2DDescription();
            texture2DDescription.Width = renderTargetTextureSize.Width;
            texture2DDescription.Height = renderTargetTextureSize.Height;
            texture2DDescription.ArraySize = 1;
            texture2DDescription.MipLevels = 1;
            texture2DDescription.Format = Format.R8G8B8A8_UNorm;
            texture2DDescription.SampleDescription = new SampleDescription(1, 0);
            texture2DDescription.BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget;
            texture2DDescription.Usage = ResourceUsage.Default;
            texture2DDescription.CpuAccessFlags = CpuAccessFlags.None;

            // Create the texture that will contain the rendered graphics.
            Texture2D           renderTargetTexture = new Texture2D(device, texture2DDescription);			// = pRendertargetTexture
            RenderTargetView    renderTargetRenderTargetView = new RenderTargetView(device, renderTargetTexture);	// = pRendertargetTexture->TexRtv
            ShaderResourceView  renderTargetShaderResourceView = new ShaderResourceView(device, renderTargetTexture);	// = pRendertargetTexture->TexSv

            // Update the actual size of the render target texture. 
            // This may differ from the requested size, for certain kinds of graphics adapters.
            renderTargetTextureSize.Width = renderTargetTexture.Description.Width;
            renderTargetTextureSize.Height = renderTargetTexture.Description.Height;

            // Define a depth buffer for the render target texture, matching the dimensions of the texture.
            Texture2DDescription renderTargetDepthBufferDescription = new Texture2DDescription();
            renderTargetDepthBufferDescription.Format = Format.D32_Float;
            renderTargetDepthBufferDescription.ArraySize = 1;
            renderTargetDepthBufferDescription.MipLevels = 1;
            renderTargetDepthBufferDescription.Width = renderTargetTexture.Description.Width;
            renderTargetDepthBufferDescription.Height = renderTargetTexture.Description.Height;
            renderTargetDepthBufferDescription.SampleDescription = new SampleDescription(1, 0);
            renderTargetDepthBufferDescription.Usage = ResourceUsage.Default;
            renderTargetDepthBufferDescription.BindFlags = BindFlags.DepthStencil;
            renderTargetDepthBufferDescription.CpuAccessFlags = CpuAccessFlags.None;
            renderTargetDepthBufferDescription.OptionFlags = ResourceOptionFlags.None;

            // Define how the depth buffer will be used to filter out objects, based on their distance from the viewer.
            DepthStencilStateDescription renderTargetDepthStencilStateDescription = new DepthStencilStateDescription();
            renderTargetDepthStencilStateDescription.IsDepthEnabled = true;
            renderTargetDepthStencilStateDescription.DepthComparison = Comparison.Less;
            renderTargetDepthStencilStateDescription.DepthWriteMask = DepthWriteMask.Zero;

            // Create depth buffer for the render target texture, matching the dimensions of the texture.
            Texture2D renderTargetDepthBufferTexture = new Texture2D(device, renderTargetDepthBufferDescription);
            DepthStencilView renderTargetDepthStencilView = new DepthStencilView(device, renderTargetDepthBufferTexture);
            //DepthStencilState renderTargetDepthStencilState = new DepthStencilState(device, renderTargetDepthStencilStateDescription);
            DepthStencilState renderTargetDepthStencilState = DepthStencilState.FromDescription(device, renderTargetDepthStencilStateDescription);

            // Create a depth stencil for clearing the renderTargetDepthBufferTexture.
            DepthStencilStateDescription clearDepthStencilStateDescription = new DepthStencilStateDescription();
            clearDepthStencilStateDescription.IsDepthEnabled = true;
            clearDepthStencilStateDescription.DepthComparison = Comparison.Always;
            clearDepthStencilStateDescription.DepthWriteMask = DepthWriteMask.All;

            //DepthStencilState clearDepthStencilState = new DepthStencilState(device, clearDepthStencilStateDescription);
            DepthStencilState clearDepthStencilState = DepthStencilState.FromDescription(device, clearDepthStencilStateDescription);




            OVR.FovPort[] eyeFov = new OVR.FovPort[]
			{ 
				hmd.DefaultEyeFov[0], 
				hmd.DefaultEyeFov[1] 
			};

            //OVR.Recti[] eyeRenderViewport = new OVR.Recti[2];
            eyeRenderViewport = new OVR.Recti[2];
            eyeRenderViewport[0].Position = new OVR.Vector2i(0, 0);
            eyeRenderViewport[0].Size = new OVR.Sizei(renderTargetTextureSize.Width / 2, renderTargetTextureSize.Height);
            eyeRenderViewport[1].Position = new OVR.Vector2i((renderTargetTextureSize.Width + 1) / 2, 0);
            eyeRenderViewport[1].Size = eyeRenderViewport[0].Size;


            // Query D3D texture data.
            //OVR.D3D11.D3D11TextureData[] eyeTexture = new OVR.D3D11.D3D11TextureData[2];
            eyeTexture = new OVR.D3D11.D3D11TextureData[2];
            eyeTexture[0].Header.API = OVR.RenderAPIType.D3D11;
            eyeTexture[0].Header.TextureSize = renderTargetTextureSize;
            eyeTexture[0].Header.RenderViewport = eyeRenderViewport[0];
            //eyeTexture[0].Texture = renderTargetTexture.NativePointer;
            eyeTexture[0].Texture = renderTargetTexture.ComPointer;             // does thos work yet?
            //eyeTexture[0].ShaderResourceView = renderTargetShaderResourceView.NativePointer;
            eyeTexture[0].ShaderResourceView = renderTargetShaderResourceView.ComPointer;

            // Right eye uses the same texture, but different rendering viewport.
            eyeTexture[1] = eyeTexture[0];
            eyeTexture[1].Header.RenderViewport = eyeRenderViewport[1];

            // Configure d3d11.
            OVR.D3D11.D3D11ConfigData d3d11cfg = new OVR.D3D11.D3D11ConfigData();
            d3d11cfg.Header.API = OVR.RenderAPIType.D3D11;
            d3d11cfg.Header.BackBufferSize = new OVR.Sizei(hmd.Resolution.Width, hmd.Resolution.Height);
            d3d11cfg.Header.Multisample = 1;
            //d3d11cfg.Device = device.NativePointer;
            d3d11cfg.Device = device.ComPointer;
            //d3d11cfg.DeviceContext = immediateContext.NativePointer;
            d3d11cfg.DeviceContext = device.ImmediateContext.ComPointer;
            //d3d11cfg.BackBufferRenderTargetView = backBufferRenderTargetView.NativePointer;
            d3d11cfg.BackBufferRenderTargetView = backBufferRenderTargetView.ComPointer;
            //d3d11cfg.SwapChain = swapChain.NativePointer;
            d3d11cfg.SwapChain = swapChain.ComPointer;

            //OVR.EyeRenderDesc[] eyeRenderDesc = hmd.ConfigureRendering(d3d11cfg, OVR.DistortionCaps.ovrDistortionCap_Chromatic | OVR.DistortionCaps.ovrDistortionCap_Vignette | OVR.DistortionCaps.ovrDistortionCap_TimeWarp | OVR.DistortionCaps.ovrDistortionCap_Overdrive, eyeFov);
            eyeRenderDesc = hmd.ConfigureRendering(d3d11cfg, OVR.DistortionCaps.ovrDistortionCap_Chromatic | OVR.DistortionCaps.ovrDistortionCap_Vignette | OVR.DistortionCaps.ovrDistortionCap_TimeWarp | OVR.DistortionCaps.ovrDistortionCap_Overdrive, eyeFov);
            if (eyeRenderDesc == null)
                return;

            // Specify which head tracking capabilities to enable.
            hmd.SetEnabledCaps(OVR.HmdCaps.LowPersistence | OVR.HmdCaps.DynamicPrediction);

            // Start the sensor which informs of the Rift's pose and motion
            hmd.ConfigureTracking(OVR.TrackingCaps.ovrTrackingCap_Orientation | OVR.TrackingCaps.ovrTrackingCap_MagYawCorrection | OVR.TrackingCaps.ovrTrackingCap_Position, OVR.TrackingCaps.None);


           
            #region ood
            /*
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
            config.pSwapChain = this.FOutBackBuffer[0][context].SwapChain.ComPointer;

            FLogger.Log(LogType.Debug, "pSwapChain: ");
            FLogger.Log(LogType.Debug, "this.chain.swapchain.ComPointer:                      " + this.chain.SwapChain.ComPointer);
            FLogger.Log(LogType.Debug, "this.FOutBackBuffer[0][context].swapchain.ComPointer: " + this.FOutBackBuffer[0][context].SwapChain.ComPointer);
            FLogger.Log(LogType.Debug, " ");

            //config.pBackBufferRT = this.chain.RTV.ComPointer;
            config.pBackBufferRT = this.FOutBackBuffer[0][context].RTV.ComPointer;
            FLogger.Log(LogType.Debug, "pBackBufferRT: ");
            FLogger.Log(LogType.Debug, "this.chain.RTV.ComPointer:                      " + this.chain.RTV.ComPointer);
            FLogger.Log(LogType.Debug, "this.FOutBackBuffer[0][context].RTV.ComPointer: " + this.FOutBackBuffer[0][context].RTV.ComPointer);
            // hmd stuff -------------------------------

            hmd.ConfigureRendering(config, DistortionCap.DistortionCap_Chromatic | DistortionCap.DistortionCap_TimeWarp | DistortionCap.DistortionCap_Vignette, fovList, eyeList);

            hmd.AttachToWindow(base.WindowHandle, null, null);
            */
            #endregion ood

            isInit = true;
        }

        private void shutdownOVR()
        {
            frameIndex = 0;
            isInit = false;

            if (hmd != null)
            {
                hmd.Dispose();
                oculus.Shutdown();
            }

        }   

    }

    # region helpers
    public static class Helpers
    {
        /// <summary>
        /// Convert a Vector4 to a Vector3
        /// </summary>
        /// <param name="vector4">Vector4 to convert to a Vector3.</param>
        /// <returns>Vector3 based on the X, Y and Z coordinates of the Vector4.</returns>
        public static Vector3 ToVector3(this Vector4 vector4)
        {
            return new Vector3(vector4.X, vector4.Y, vector4.Z);
        }

        /// <summary>
        /// Convert an ovrVector3f to SharpDX Vector3.
        /// </summary>
        /// <param name="ovrVector3f">ovrVector3f to convert to a SharpDX Vector3.</param>
        /// <returns>SharpDX Vector3, based on the ovrVector3f.</returns>
        public static Vector3 ToVector3(this OculusWrap.OVR.Vector3f ovrVector3f)
        {
            return new Vector3(ovrVector3f.X, ovrVector3f.Y, ovrVector3f.Z);
        }

        /// <summary>
        /// Convert an ovrMatrix4f to a SharpDX Matrix.
        /// </summary>
        /// <param name="ovrMatrix4f">ovrMatrix4f to convert to a SharpDX Matrix.</param>
        /// <returns>SharpDX Matrix, based on the ovrMatrix4f.</returns>
        public static Matrix ToMatrix(this OculusWrap.OVR.Matrix4f ovrMatrix4f)
        {
            Matrix m = new Matrix();
            m.M11 = ovrMatrix4f.M11;
            m.M12 = ovrMatrix4f.M12;
            m.M13 = ovrMatrix4f.M13;
            m.M14 = ovrMatrix4f.M14;
            m.M21 = ovrMatrix4f.M21;
            m.M22 = ovrMatrix4f.M22;
            m.M23 = ovrMatrix4f.M23;
            m.M24 = ovrMatrix4f.M24;
            m.M31 = ovrMatrix4f.M31;
            m.M32 = ovrMatrix4f.M32;
            m.M33 = ovrMatrix4f.M33;
            m.M34 = ovrMatrix4f.M34;
            m.M41 = ovrMatrix4f.M41;
            m.M42 = ovrMatrix4f.M42;
            m.M43 = ovrMatrix4f.M43;
            m.M44 = ovrMatrix4f.M44;
           
            return m;
        }

        /// <summary>
        /// Converts an ovrQuatf to a SharpDX Quaternion.
        /// </summary>
        public static Quaternion ToQuaternion(OculusWrap.OVR.Quaternionf ovrQuatf)
        {
            return new Quaternion(ovrQuatf.X, ovrQuatf.Y, ovrQuatf.Z, ovrQuatf.W);
        }
    }
    # endregion helpers
}