using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Device = SlimDX.Direct3D11.Device;
using SlimDX.DXGI;
using SlimDX.Direct3D11;
using SlimDX;

using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VMath;
using VVVV.Utils.VColor;
using VVVV.PluginInterfaces.V1;
using VVVV.DX11.Internals;
using System.Drawing;

using System.ComponentModel.Composition;
using VVVV.Core.Logging;
using System.Diagnostics;
using VVVV.DX11.Lib.Devices;
using VVVV.DX11.Lib.Rendering;

using VVVV.Utils.IO;
using System.Windows.Forms;


using System.Runtime.InteropServices;
using VVVV.PluginInterfaces.V2.Graph;
using FeralTic.DX11.Queries;
using FeralTic.DX11;
using FeralTic.DX11.Resources;
using VVVV.DX11.Nodes.Renderers.Graphics.Touch;

using OculusWrap;

namespace VVVV.DX11.Nodes
{
    [PluginInfo(Name = "OVR", Category = "DX11", Author = "vux,tonfilm", AutoEvaluate = true,
        InitialWindowHeight=300,InitialWindowWidth=400,InitialBoxWidth=400,InitialBoxHeight=300, InitialComponentMode=TComponentMode.InAWindow)]
    public partial class OVRNode : IPluginEvaluate, IDisposable, IDX11RendererProvider, IDX11RenderWindow, IDX11Queryable, IUserInputWindow, IBackgroundColor
    {
        #region Touch Stuff
        private object m_touchlock = new object();
        private Dictionary<int, TouchData> touches = new Dictionary<int, TouchData>();

        private event EventHandler<WMTouchEventArgs> Touchdown;
        private event EventHandler<WMTouchEventArgs> Touchup;
        private event EventHandler<WMTouchEventArgs> TouchMove;

        private void OnTouchDownHandler(object sender, WMTouchEventArgs e)
        {
            lock (m_touchlock)
            {
                TouchData t = new TouchData();
                t.Id = e.Id;
                t.IsNew = true;
                t.Pos = new Vector2(e.LocationX, e.LocationY);
                this.touches.Add(e.Id, t);
            }
        }

        private void OnTouchUpHandler(object sender, WMTouchEventArgs e)
        {
            lock (m_touchlock)
            {
                this.touches.Remove(e.Id);
            }
        }

        private void OnTouchMoveHandler(object sender, WMTouchEventArgs e)
        {
            lock (m_touchlock)
            {
                TouchData t = this.touches[e.Id];
                t.Pos = new Vector2(e.LocationX, e.LocationY);
            }
        }


        protected override void WndProc(ref Message m) // Decode and handle WM_TOUCH message.
        {
            bool handled;
            switch (m.Msg)
            {
                case TouchConstants.WM_TOUCH:
                    handled = DecodeTouch(ref m);
                    break;
                default:
                    handled = false;
                    break;
            }
            base.WndProc(ref m);  // Call parent WndProc for default message processing.

            if (handled) // Acknowledge event if handled.
                m.Result = new System.IntPtr(1);
        }

        private bool DecodeTouch(ref Message m)
        {
            // More than one touchinput may be associated with a touch message,
            int inputCount = (m.WParam.ToInt32() & 0xffff); // Number of touch inputs, actual per-contact messages
            TOUCHINPUT[] inputs = new TOUCHINPUT[inputCount];

            if (!TouchConstants.GetTouchInputInfo(m.LParam, inputCount, inputs, Marshal.SizeOf(new TOUCHINPUT())))
            {
                return false;
            }

            bool handled = false;
            for (int i = 0; i < inputCount; i++)
            {
                TOUCHINPUT ti = inputs[i];

                EventHandler<WMTouchEventArgs> handler = null;
                if ((ti.dwFlags & TouchConstants.TOUCHEVENTF_DOWN) != 0)
                {
                    handler = Touchdown;
                }
                else if ((ti.dwFlags & TouchConstants.TOUCHEVENTF_UP) != 0)
                {
                    handler = Touchup;
                }
                else if ((ti.dwFlags & TouchConstants.TOUCHEVENTF_MOVE) != 0)
                {
                    handler = TouchMove;
                }

                // Convert message parameters into touch event arguments and handle the event.
                if (handler != null)
                {
                    WMTouchEventArgs te = new WMTouchEventArgs();

                    // TOUCHINFO point coordinates and contact size is in 1/100 of a pixel; convert it to pixels.
                    // Also convert screen to client coordinates.
                    te.ContactY = ti.cyContact / 100;
                    te.ContactX = ti.cxContact / 100;
                    te.Id = ti.dwID;
                    {
                        Point pt = PointToClient(new Point(ti.x / 100, ti.y / 100));
                        te.LocationX = pt.X;
                        te.LocationY = pt.Y;
                    }
                    te.Time = ti.dwTime;
                    te.Mask = ti.dwMask;
                    te.Flags = ti.dwFlags;

                    handler(this, te);

                    // Mark this event as handled.
                    handled = true;
                }
            }
            TouchConstants.CloseTouchInputHandle(m.LParam);

            return handled;
        }
        #endregion

        #region Input Pins
        IPluginHost FHost;

        protected IHDEHost hde;



        [Import()]
        protected IPluginHost2 host2;

        [Import()]
        protected ILogger logger;

        [Input("Layers", Order=1,IsSingle = true)]
        protected Pin<DX11Resource<DX11Layer>> FInLayer;

        [Input("Clear",DefaultValue=1,Order = 2)]
        protected ISpread<bool> FInClear;

        [Input("Clear Depth", DefaultValue = 1, Order = 2)]
        protected ISpread<bool> FInClearDepth;

        [Input("Background Color",DefaultColor=new double[] { 0,0,0,1 },Order=3)]
        protected ISpread<RGBAColor> FInBgColor;

        [Input("VSync",Visibility=PinVisibility.OnlyInspector, IsSingle=true)]
        protected ISpread<bool> FInVsync;

        [Input("Buffer Count", Visibility = PinVisibility.OnlyInspector, DefaultValue=1, IsSingle=true)]
        protected ISpread<int> FInBufferCount;

        [Input("Do Not Wait", Visibility = PinVisibility.OnlyInspector, IsSingle=true)]
        protected ISpread<bool> FInDNW;

        [Input("Show Cursor", DefaultValue = 0, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<bool> FInShowCursor;

        [Input("Fullscreen", Order = 5)]
        protected IDiffSpread<bool> FInFullScreen;

        [Input("Enable Depth Buffer", Order = 6,DefaultValue=1)]
        protected IDiffSpread<bool> FInDepthBuffer;

        [Input("AA Samples per Pixel", DefaultEnumEntry="1",EnumName="DX11_AASamples")]
        protected IDiffSpread<EnumEntry> FInAASamplesPerPixel;

        /*[Input("AA Quality", Order = 8)]
        protected IDiffSpread<int> FInAAQuality;*/

        [Input("Enabled", DefaultValue = 1, Order = 9)]
        protected ISpread<bool> FInEnabled;

        [Input("View", Order = 10)]
        protected IDiffSpread<Matrix> FInView;

        [Input("Projection", Order = 11)]
        protected IDiffSpread<Matrix> FInProjection;

        [Input("Aspect Ratio", Order = 12,Visibility=PinVisibility.Hidden)]
        protected IDiffSpread<Matrix> FInAspect;

        [Input("Crop", Order = 13, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<Matrix> FInCrop;

        [Input("ViewPort", Order = 20)]
        protected Pin<Viewport> FInViewPort;

        string oldbbformat = "";
        #endregion

        #region Output Pins
        [Output("RenderScaleAndOffset")] //2 Vectors, 2 Eyes
        public ISpread<Vector2D> FRenderScaleAndOffset;

        [Output("Device Handle", AllowFeedback = true)]
        protected ISpread<int> FOutDeviceHandle;

        [Output("Mouse State",AllowFeedback=true)]
        protected ISpread<MouseState> FOutMouseState;

        [Output("Keyboard State", AllowFeedback = true)]
        protected ISpread<KeyboardState> FOutKState;

        [Output("Touch Supported",IsSingle=true)]
        protected ISpread<bool> FOutTouchSupport;

        [Output("Touch Data", AllowFeedback = true)]
        protected ISpread<TouchData> FOutTouchData;

        [Output("Actual BackBuffer Size", AllowFeedback = true)]
        protected ISpread<Vector2D> FOutBackBufferSize;

        [Output("Texture Out")]
        protected ISpread<DX11Resource<DX11SwapChain>> FOutBackBuffer;

        protected ISpread<DX11Resource<DX11SwapChain>> FOuFS;

        [Output("Present Time",IsSingle=true)]
        protected ISpread<double> FOutPresent;

        [Output("Query", Order = 200, IsSingle = true)]
        protected ISpread<IDX11Queryable> FOutQueryable;

        [Output("Control", Order = 201, IsSingle = true, Visibility = PinVisibility.OnlyInspector)]
        protected ISpread<Control> FOutCtrl;

        [Output("Node Ref", Order = 201, IsSingle = true, Visibility = PinVisibility.OnlyInspector)]
        protected ISpread<INode> FOutRef;
        #endregion

        #region Fields
        public event DX11QueryableDelegate BeginQuery;
        public event DX11QueryableDelegate EndQuery;

        private Vector2D FMousePos;
        private Vector3D FMouseButtons;
        private List<Keys> FKeys = new List<Keys>();
        private int wheel = 0;

        public Dictionary<DX11RenderContext, DX11GraphicsRenderer> renderers = new Dictionary<DX11RenderContext, DX11GraphicsRenderer>();
        public List<DX11RenderContext> updateddevices = new List<DX11RenderContext>();
        public List<DX11RenderContext> rendereddevices = new List<DX11RenderContext>();
        public DepthBufferManager depthmanager;

        public DX11SwapChain chain;
        public DX11RenderSettings settings = new DX11RenderSettings();

        private bool FInvalidateSwapChain;
        private bool FResized = false;
        private DX11RenderContext primary;

        


        private Wrap oculus = new Wrap();
        private Hmd hmd;

        DateTime startTime;
        
        private bool isInitialized = false;

        private OVR.Sizei backBufferSize;
        private OVR.Sizei recommenedTex0Size;
        private OVR.Sizei recommenedTex1Size;
        OVR.Sizei renderTargetTextureSize;

        private OVR.FovPort[] eyeFov;
        private OVR.EyeRenderDesc[] eyeRenderDesc;
        private OVR.Recti[] eyeRenderViewport;
        private OVR.D3D11.D3D11TextureData[] eyeTexture;
        //private OVR.Vector2f[] uvScaleOffsetOut;

        OVR.Posef[] eyeRenderPose;
        Viewport viewport;

        uint framecounter = 0;
        #endregion

        #region Evaluate
        public virtual void Evaluate(int SpreadMax)
        {
            this.FOutCtrl[0] = this;
            this.FOutRef[0] = (INode)this.FHost;

            if (this.FOutQueryable[0] == null) { this.FOutQueryable[0] = this; }
            if (this.FOutBackBuffer[0] == null)
            {
                this.FOutBackBuffer[0] = new DX11Resource<DX11SwapChain>();
                this.FOuFS = new Spread<DX11Resource<DX11SwapChain>>();
                this.FOuFS.SliceCount = 1;
                this.FOuFS[0] = new DX11Resource<DX11SwapChain>();
            }

            this.updateddevices.Clear();
            this.rendereddevices.Clear();
            this.FInvalidateSwapChain = false;

            if (!this.depthmanager.FormatChanged) // do not clear reset if format changed
            {
                this.depthmanager.NeedReset = false;
            } 
            else
            {
                this.depthmanager.FormatChanged = false; //Clear flag ok
            }
            
            if (FInAASamplesPerPixel.IsChanged || this.FInBufferCount.IsChanged)
            {
                this.depthmanager.NeedReset = true;
                this.FInvalidateSwapChain = true;
            }

            if (this.FInFullScreen.IsChanged)
            {
                string path;
                this.FHost.GetNodePath(false, out path);
                INode2 n2 = hde.GetNodeFromPath(path);

                if (n2.Window != null)
                {
                    if (n2.Window.IsVisible)
                    {
                        if (this.FInFullScreen[0])
                        {
                            hde.SetComponentMode(n2, ComponentMode.Fullscreen);
                        }
                        else
                        {
                            hde.SetComponentMode(n2, ComponentMode.InAWindow);
                        }
                    }
                }
            }


            this.FOutKState[0] = new KeyboardState(this.FKeys);
            this.FOutMouseState[0] = MouseState.Create(this.FMousePos.x, this.FMousePos.y, this.FMouseButtons.x > 0.5f, this.FMouseButtons.y > 0.5f, this.FMouseButtons.z> 0.5f, false, false, this.wheel);
            this.FOutBackBufferSize[0] = new Vector2D(this.Width, this.Height);

            this.FOutTouchSupport[0] = this.touchsupport;

            this.FOutTouchData.SliceCount = this.touches.Count;

            int tcnt = 0;
            float fw = (float)this.ClientSize.Width;
            float fh = (float)this.ClientSize.Height;
            lock (m_touchlock)
            {
                foreach (int key in touches.Keys)
                {
                    TouchData t = touches[key];

                    this.FOutTouchData[tcnt] = t.Clone(fw, fh);
                    t.IsNew = false;
                    tcnt++;
                }
            }

            this.FOutDeviceHandle.SliceCount = 1;
            this.FOutDeviceHandle[0] = this.WindowHandle.ToInt32();

            if (FInEnabled[0] && !isInitialized)
            {
                initOVR();
            }

            if (!FInEnabled[0] && isInitialized)
            {
                //oculus.Shutdown();
                //oculus.Dispose();
                //hmd.Dispose();
                //logger.Log(LogType.Debug, "Oculus:  Disposed:" + oculus.Disposed);               
                //isInitialized = false;
            }
        }
        #endregion

        #region Dispose
        void IDisposable.Dispose()
        {
            if (this.FOutBackBuffer[0] != null) { this.FOutBackBuffer[0].Dispose(); }
            
            if (oculus != null)
            {
                oculus.Dispose();
                hmd.Dispose();
                logger.Log(LogType.Debug, "Oculus:  Disposed:" + oculus.Disposed);
            }
        }
        #endregion

        #region Is Enabled
        public bool IsEnabled
        {
            get { return this.FInEnabled[0]; }
        }
        #endregion

        #region Render
        public virtual void Render(DX11RenderContext context)
        {
            Device device = context.Device;
            
            if (!this.updateddevices.Contains(context)) 
            { 
                this.Update(null, context);
            }

            if (this.rendereddevices.Contains(context)) 
            { 
                return; 
            }

            Exception exception = null;

            if (this.FInEnabled[0])
            {
                if (this.BeginQuery != null)
                {
                    this.BeginQuery(context);
                }

                this.chain = this.FOutBackBuffer[0][context];

                DX11GraphicsRenderer renderer = this.renderers[context];

                renderer.EnableDepth = this.FInDepthBuffer[0];
                renderer.DepthStencil = this.depthmanager.GetDepthStencil(context);
                renderer.DepthMode = this.depthmanager.Mode;
                renderer.SetRenderTargets(chain);
                renderer.SetTargets();

                configOVR(context);
                
                disableWarning();

                hmd.BeginFrame(0);

                calcViewProj(context);

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


                    if (this.EndQuery != null)
                    {
                        this.EndQuery(context);
                    }

                    

                    //hmd.EndFrame(eyeRenderPose, eyeTexture);

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
        }

        
        
        #endregion

        #region RenderSlice
        public void RenderSlice(DX11RenderContext context,DX11RenderSettings settings, int i, bool viewportpop)
        {
            float cw = (float)this.ClientSize.Width;
            float ch = (float)this.ClientSize.Height;

            settings.ViewportIndex = i;
            settings.View = this.FInView[i];

            Matrix proj = this.FInProjection[i];
            Matrix aspect = Matrix.Invert(this.FInAspect[i]);
            Matrix crop = Matrix.Invert(this.FInCrop[i]);


            settings.Projection = proj * aspect * crop;
            settings.ViewProjection = settings.View * settings.Projection;
            settings.BackBuffer = this.FOutBackBuffer[0][context];
            settings.RenderWidth = this.FOutBackBuffer[0][context].Resource.Description.Width;
            settings.RenderHeight = this.FOutBackBuffer[0][context].Resource.Description.Height;
            //settings.RenderWidth = 2364;
            //settings.RenderHeight = 1461;     //too late here


            settings.ResourceSemantics.Clear();
            settings.CustomSemantics.Clear();

            if (viewportpop)
            {
                context.RenderTargetStack.PushViewport(this.FInViewPort[i].Normalize(cw, ch));
                //context.RenderTargetStack.PushViewport(viewport);
            }

            //Call render on all layers
            for (int j = 0; j < this.FInLayer.SliceCount; j++)
            {
                this.FInLayer[j][context].Render(this.FInLayer.PluginIO, context, settings);
            }

            if (viewportpop)
            {
                context.RenderTargetStack.PopViewport();
            }

            hmd.EndFrame(eyeRenderPose, eyeTexture);

            
        }
        #endregion

        #region Update
        public void Update(IPluginIO pin, DX11RenderContext context)
        {
            Device device = context.Device;

            if (this.updateddevices.Contains(context)) { return; }

            int samplecount = Convert.ToInt32(FInAASamplesPerPixel[0].Name);

            SampleDescription sd = new SampleDescription(samplecount, 0);

            if (this.FResized || this.FInvalidateSwapChain || this.FOutBackBuffer[0][context] == null)
            {
                this.FOutBackBuffer[0].Dispose(context);

                List<SampleDescription> sds = context.GetMultisampleFormatInfo(Format.R8G8B8A8_UNorm);
                int maxlevels = sds[sds.Count - 1].Count;

                if (sd.Count > maxlevels)
                {
                    logger.Log(LogType.Warning, "Multisample count too high for this format, reverted to: " + maxlevels);
                    sd.Count = maxlevels;
                }

                this.FOutBackBuffer[0][context] = new DX11SwapChain(context, this.Handle, Format.R8G8B8A8_UNorm, sd, 60,
                    this.FInBufferCount[0]);

                #if DEBUG
                this.FOutBackBuffer[0][context].Resource.DebugName = "BackBuffer";
                #endif
                this.depthmanager.NeedReset = true;
            }

            DX11SwapChain sc = this.FOutBackBuffer[0][context];

            if (this.FResized)
            {
                
                //if (!sc.IsFullScreen)
                //{
                   // sc.Resize();
               // }
               //this.FInvalidateSwapChain = true;
            }

            
            if (!this.renderers.ContainsKey(context)) { this.renderers.Add(context, new DX11GraphicsRenderer(this.FHost, context)); }

            this.depthmanager.Update(context, sc.Width, sc.Height, sd);

            this.updateddevices.Add(context);
        }
        #endregion

        #region Destroy
        public void Destroy(IPluginIO pin, DX11RenderContext context, bool force)
        {
            //if (this.FDepthManager != null) { this.FDepthManager.Dispose(); }

            if (this.renderers.ContainsKey(context)) { this.renderers.Remove(context); }

            this.FOutBackBuffer[0].Dispose(context);
        }
        #endregion

        #region Render Window
        public void Present()
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                PresentFlags flags = this.FInDNW[0] ? (PresentFlags)8 : PresentFlags.None;
                if (this.FInVsync[0])
                {
                    this.FOutBackBuffer[0][this.RenderContext].Present(1, flags); 
                }
                else
                {
                    this.FOutBackBuffer[0][this.RenderContext].Present(0, flags); 
                }
            }
            catch
            {
                
            }

            sw.Stop();
            this.FOutPresent[0] = sw.Elapsed.TotalMilliseconds;

            this.FResized = false;
        }

        public DX11RenderContext RenderContext
        {
            get { return this.primary; }
            set
            {
                this.primary = value;
            }
        }

        public IntPtr WindowHandle
        {
            get 
            {
                return this.Handle;
            }
        }
        #endregion

        #region OVR

        private void initOVR()
        {
            logger.Log(LogType.Debug, "Oculus:  Starting Initialization");

            startTime = DateTime.Now;

            oculus.Initialize();

            // Use the head mounted display, if it's available, otherwise use the debug HMD.
            int numberOfHeadMountedDisplays = oculus.Hmd_Detect();
            if (numberOfHeadMountedDisplays > 0)
                hmd = oculus.Hmd_Create(0);
            else
                hmd = oculus.Hmd_CreateDebug(OculusWrap.OVR.HmdType.DK2);

            if (hmd == null)
            {
                logger.Log(LogType.Warning, "Oculus Rift not detected.");
                return;
            }
            if (hmd.ProductName == string.Empty)
                logger.Log(LogType.Warning, "The HMD is not enabled. There's a tear in the Rift");

            // attach hmd
            OVR.Recti destMirrorRect;
            OVR.Recti sourceRenderTargetRect;
            hmd.AttachToWindow(this.WindowHandle, out destMirrorRect, out sourceRenderTargetRect);

            // Create a backbuffer that's the same size as the HMD's resolution.
            this.backBufferSize.Width = hmd.Resolution.Width;
            this.backBufferSize.Height = hmd.Resolution.Height;

            eyeFov = new OVR.FovPort[]
			{ 
				hmd.DefaultEyeFov[0], 
				hmd.DefaultEyeFov[1] 
			};

            eyeRenderDesc = new OVR.EyeRenderDesc[2];
            eyeRenderDesc[0] = hmd.GetRenderDesc(OVR.EyeType.Left, eyeFov[0]);
            eyeRenderDesc[1] = hmd.GetRenderDesc(OVR.EyeType.Right, eyeFov[1]);


            isInitialized = true;
            logger.Log(LogType.Debug, "Oculus:  Finished Initialization");
        }

        private void calcViewProj(DX11RenderContext context)
        {
            float timeSinceStart = (float)(DateTime.Now - startTime).TotalSeconds;

            float bodyYaw = 3.141592f;
            OVR.Vector3f headPos = new OVR.Vector3f(0.0f, hmd.GetFloat(OVR.OVR_KEY_EYE_HEIGHT, 1.6f), -5.0f);
            //Viewport viewport = new Viewport(0, 0, renderTargetTexture.Description.Width, renderTargetTexture.Description.Height, 0.0f, 1.0f);
            viewport = new Viewport(0, 0, this.FOutBackBuffer[0][context].Resource.Description.Width, this.FOutBackBuffer[0][context].Resource.Description.Height, 0.0f, 1.0f);
            //Viewport viewport = new Viewport(0, 0, 2364, 1461, 0.0f, 1.0f);
            eyeRenderPose = new OVR.Posef[2];

            for (int eyeIndex = 0; eyeIndex < OVR.Eye_Count; eyeIndex++)
            {
                OVR.EyeType eye = hmd.EyeRenderOrder[eyeIndex];

                eyeRenderPose = new OVR.Posef[2];
                eyeRenderPose[(int)eye] = hmd.GetHmdPosePerEye(eye);


                // get viewproj from ovr

                // Get view and projection matrices
                Quaternion rotationQuaternion = Helpers.ToQuaternion(eyeRenderPose[(int)eye].Orientation);

                Vector3 eyePosition = eyeRenderPose[(int)eye].Position.ToVector3();

                Matrix rollPitchYaw = Matrix.RotationY(bodyYaw);
                Matrix rotation = Matrix.RotationQuaternion(rotationQuaternion);
                Matrix finalRollPitchYaw = rollPitchYaw * rotation;
                Vector3 finalUp = Vector3.Transform(new Vector3(0, -1, 0), finalRollPitchYaw).ToVector3();
                Vector3 finalForward = Vector3.Transform(new Vector3(0, 0, -1), finalRollPitchYaw).ToVector3();
                Vector3 shiftedEyePos = headPos.ToVector3() + Vector3.Transform(eyePosition, rollPitchYaw).ToVector3();
                Matrix viewMatrix = Matrix.LookAtLH(shiftedEyePos, shiftedEyePos + finalForward, finalUp);
                Matrix projectionMatrix = oculus.Matrix4f_Projection(eyeRenderDesc[(int)eye].Fov, 0.1f, 100.0f, false).ToMatrix();

                //projectionMatrix.Transpose();

                Matrix.Transpose(projectionMatrix);

                // Set the viewport for the current eye.
                viewport = new Viewport(this.eyeRenderViewport[(int)eye].Position.x, eyeRenderViewport[(int)eye].Position.y, eyeRenderViewport[(int)eye].Size.Width, eyeRenderViewport[(int)eye].Size.Height, 0.0f, 1.0f);

                //immediateContext.Rasterizer.SetViewport(viewport);
                //device.ImmediateContext.Rasterizer.SetViewports(viewport);

                Matrix world = Matrix.RotationX(timeSinceStart) * Matrix.RotationY(timeSinceStart * 2) * Matrix.RotationZ(timeSinceStart * 3);
                Matrix worldViewProjection = world * viewMatrix * projectionMatrix;
                //worldViewProjection.Transpose();

                Matrix.Transpose(worldViewProjection);

                //immediateContext.UpdateSubresource(ref worldViewProjection, contantBuffer);
                //device.ImmediateContext.UpdateSubresource(ref worldViewProjection);  

            }
        }

        private void disableWarning()
        {
            OculusWrap.OVR.HSWDisplayState hasWarningState;
            hmd.GetHSWDisplayState(out hasWarningState);

            // Remove the health and safety warning.
            if (hasWarningState.Displayed == 1)
                hmd.DismissHSWDisplay();
        }

        private void configOVR(DX11RenderContext context)
        {
            if (isInitialized && this.chain != null)
            {
                // Configure d3d11.
                OVR.D3D11.D3D11ConfigData d3d11cfg = new OVR.D3D11.D3D11ConfigData();
                d3d11cfg.Header.API = OVR.RenderAPIType.D3D11;
                d3d11cfg.Header.BackBufferSize = new OVR.Sizei(hmd.Resolution.Width, hmd.Resolution.Height);
                //d3d11cfg.Header.Multisample = this.chain.SwapChain.Description.BufferCount;
                d3d11cfg.Header.Multisample = 1;

                d3d11cfg.Device = this.chain.UAV.Device.ComPointer;
                //d3d11cfg.Device = this.chain.SwapChain.Device.ComPointer;
                //d3d11cfg.Device = device.ComPointer;

                //d3d11cfg.DeviceContext = immediateContext.NativePointer;
                //d3d11cfg.DeviceContext = context.Device.ComPointer;
                d3d11cfg.DeviceContext = context.CurrentDeviceContext.ComPointer;

                //d3d11cfg.BackBufferRenderTargetView = backBufferRenderTargetView.NativePointer;
                //d3d11cfg.BackBufferRenderTargetView = chain.RTV.ComPointer;
                d3d11cfg.BackBufferRenderTargetView = this.FOutBackBuffer[0][context].RTV.ComPointer;
                //d3d11cfg.SwapChain = swapChain.NativePointer;
                //d3d11cfg.SwapChain = this.chain.Resource.ComPointer;
                //d3d11cfg.SwapChain = this.chain.Resource.ComPointer;
                //d3d11cfg.SwapChain = this.chain.SwapChain.ComPointer;
                d3d11cfg.SwapChain = this.FOutBackBuffer[0][context].SwapChain.ComPointer;
                //d3d11cfg.SwapChain = this.FOutBackBuffer[0][context].Resource.ComPointer;
                //d3d11cfg.SwapChain = this.chain.SwapChain.Description.OutputHandle;

                OVR.EyeRenderDesc[] eyeRenderDesc = hmd.ConfigureRendering(d3d11cfg, OVR.DistortionCaps.ovrDistortionCap_Chromatic | OVR.DistortionCaps.ovrDistortionCap_Vignette | OVR.DistortionCaps.ovrDistortionCap_TimeWarp | OVR.DistortionCaps.ovrDistortionCap_Overdrive, eyeFov);

                // Specify which head tracking capabilities to enable.
                hmd.SetEnabledCaps(OVR.HmdCaps.LowPersistence | OVR.HmdCaps.DynamicPrediction);

                // Start the sensor which informs of the Rift's pose and motion
                hmd.ConfigureTracking(OVR.TrackingCaps.ovrTrackingCap_Orientation | OVR.TrackingCaps.ovrTrackingCap_MagYawCorrection | OVR.TrackingCaps.ovrTrackingCap_Position, OVR.TrackingCaps.None);

                // -------------------------

                renderTargetTextureSize.Width = this.Width;
                renderTargetTextureSize.Height = this.Height;

                //OVR.Sizei renderTargetSize = new OVR.Sizei(recommenedTex0Size.Width + recommenedTex1Size.Width, Math.Max(recommenedTex0Size.Height, recommenedTex1Size.Height));
                OVR.Sizei renderTargetSize = new OVR.Sizei();
                renderTargetSize.Width = this.Width;
                renderTargetSize.Height = this.Height;

                eyeRenderViewport = new OVR.Recti[2];
                eyeRenderViewport[0].Position = new OVR.Vector2i(0, 0);
                eyeRenderViewport[0].Size = new OVR.Sizei(renderTargetSize.Width / 2, renderTargetSize.Height);
                eyeRenderViewport[1].Position = new OVR.Vector2i((renderTargetSize.Width + 1) / 2, 0);
                eyeRenderViewport[1].Size = eyeRenderViewport[0].Size;

                // Query D3D texture data.
                eyeTexture = new OVR.D3D11.D3D11TextureData[2];
                eyeTexture[0].Header.API = OVR.RenderAPIType.D3D11;
                eyeTexture[0].Header.TextureSize = renderTargetTextureSize;
                eyeTexture[0].Header.RenderViewport = eyeRenderViewport[0];
                eyeTexture[0].Texture = this.chain.Resource.ComPointer;

                eyeTexture[0].Texture = Texture2D.FromSwapChain<Texture2D>(this.chain.SwapChain, 0).ComPointer;

                eyeTexture[0].ShaderResourceView = this.chain.SRV.ComPointer;

                // Right eye uses the same texture, but different rendering viewport.
                eyeTexture[1] = eyeTexture[0];
                eyeTexture[1].Header.RenderViewport = eyeRenderViewport[1];
            }
        }
        #endregion

        #region helpers
        private Vector2D ToVector2D(OVR.Vector2f vector)
        {
            return new Vector2D(vector.X, vector.Y);
        }


        private Vector3D ToVector3D(OVR.Vector3f vector)
        {
            return new Vector3D(vector.X, vector.Y, vector.Z);
        }

        private Matrix4x4 ToMatrix4X4(OVR.Matrix4f matrix)
        {
            return new Matrix4x4(matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                                 matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                                 matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                                 matrix.M41, matrix.M42, matrix.M43, matrix.M44);
        }

        
        private void OutputDistortionMesh(ISpread<int> FMeshIndices, ISpread<float> FDistortionMesh, OVR.EyeType eye, OVR.FovPort eyeFov)
        {
            OVR.DistortionVertex[] dv;
            ushort[] indexData;
            bool result = hmd.CreateDistortionMesh(eye, eyeFov, OVR.DistortionCaps.ovrDistortionCap_Chromatic, out indexData, out dv);


            //Mesh indices
            FMeshIndices.SliceCount = indexData.Length;
            for (int i = 0; i < indexData.Length; i++)
            {
                FMeshIndices[i] = indexData[i];
            }
            //Mesh Vertices
            int elementCount = 10;
            FDistortionMesh.SliceCount = dv.Length * elementCount;
            for (int i = 0; i < dv.Length; i++)
            {

                FDistortionMesh[i * elementCount + 0] = dv[i].ScreenPosNDC.X;
                FDistortionMesh[i * elementCount + 1] = dv[i].ScreenPosNDC.Y;
                FDistortionMesh[i * elementCount + 2] = dv[i].TanEyeAnglesR.X;
                FDistortionMesh[i * elementCount + 3] = dv[i].TanEyeAnglesR.Y;
                FDistortionMesh[i * elementCount + 4] = dv[i].TanEyeAnglesG.X;
                FDistortionMesh[i * elementCount + 5] = dv[i].TanEyeAnglesG.Y;
                FDistortionMesh[i * elementCount + 6] = dv[i].TanEyeAnglesB.X;
                FDistortionMesh[i * elementCount + 7] = dv[i].TanEyeAnglesB.Y;
                FDistortionMesh[i * elementCount + 8] = dv[i].TimeWarpFactor;
                FDistortionMesh[i * elementCount + 9] = dv[i].VignetteFactor;
            }
        }
        

        public bool IsVisible
        {
            get
            {
                INode node = (INode)this.FHost;

                if (node.Window != null)
                {
                    return node.Window.IsVisible();
                }
                else
                {
                    return false;
                }
            }
        }

        /*private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // DX11RendererNode
            // 
            this.Name = "DX11RendererNode";
            this.ResumeLayout(false);

        }*/

        public IntPtr InputWindowHandle
        {
            get { return this.Handle; }
        }

        public RGBAColor BackgroundColor
        {
            get { return new RGBAColor(0, 0, 0, 1); }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // DX11RendererNode
            // 
            this.BackColor = System.Drawing.Color.Black;
            this.Name = "OVR";
            this.ResumeLayout(false);

        }
    }
        #endregion helpers


}