using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V1;

using SlimDX.Direct3D11;
using System.ComponentModel.Composition;
using FeralTic.DX11.Resources;
using FeralTic.DX11;

namespace VVVV.DX11.Nodes.Textures
{
   
    [PluginInfo(Name = "ToPointer", Category = "DX11.Texture", Version = "2d", Author = "vux,tonfilm")]
    public class ToPointer2DNode : ToPointerGenericNode<DX11Texture2D>
    {
        public override long calcPointer(int i, DX11RenderContext context)
        {
            return this.FTextureIn[i][context].Resource.ComPointer.ToInt64();
        }
    }

    //[PluginInfo(Name = "ToPointer", Category = "DX11.Texture", Version = "Cube", Author = "vux,tonfilm")]
    //public class ToPointerCubeNode : ToPointerGenericNode<DX11CubeRenderTarget>
    //{
    //    public override long calcPointer(int i, DX11RenderContext context)
    //    {
    //        return this.FTextureIn[i][context].Resource.ComPointer.ToInt64();
    //    }
    //}

    [PluginInfo(Name = "ToPointer", Category = "DX11.Texture", Version = "3d", Author = "vux,tonfilm")]
    public class ToPointer3DNode : ToPointerGenericNode<DX11Texture3D>
    {
        public override long calcPointer(int i, DX11RenderContext context)
        {
            return this.FTextureIn[i][context].Resource.ComPointer.ToInt64();
        }
    }

    public abstract class ToPointerGenericNode<T> : IPluginEvaluate, IDX11ResourceDataRetriever, IDisposable where T : IDX11Resource
    {
        [Import()]
        protected IPluginHost FHost;

        [Input("Texture In")]
        protected Pin<DX11Resource<T>> FTextureIn;

        [Output("Pointer", AsInt = true)]
        protected ISpread<long> FPointer;

        public void Evaluate(int SpreadMax)
        {
            if (this.FTextureIn.PluginIO.IsConnected)
            {

                if (this.RenderRequest != null) { this.RenderRequest(this, this.FHost); }

                if (this.AssignedContext == null)
                {
                    return;
                }

                this.FPointer.SliceCount = SpreadMax;

                DX11RenderContext context = this.AssignedContext;



                for (int i = 0; i < SpreadMax; i++)
                {
                    if (this.FTextureIn[0].Contains(context))
                    {
                        FPointer[i] = calcPointer(i, context);
                    }
                    else
                    {
                        FPointer[i] = -1;
                    }
                }

            }
            else
            {

            }
        }

        public abstract Int64 calcPointer(int i, DX11RenderContext context);


        private void SetDefault(int i)
        {
            FPointer[i] = -1;
        }

        public void Dispose()
        {
            
        }

        public DX11RenderContext AssignedContext
        {
            get;
            set;
        }

        public event DX11RenderRequestDelegate RenderRequest;
    }
    
}
