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
   
    [PluginInfo(Name = "ToPointer", Category = "DX11.Texture", Version = "2d", Author = "sebl")]
    public class ToPointer2DNode : ToPointerGenericNode<DX11Texture2D>
    {
        public override long calcPointer(int i, DX11RenderContext context)
        {
            if (this.FTextureIn[i][context].Resource != null)
                return this.FTextureIn[i][context].Resource.ComPointer.ToInt64();
            else
                return 0;
        }
    }

    //[PluginInfo(Name = "ToPointer", Category = "DX11.Texture", Version = "Cube", Author = "sebl")]
    //public class ToPointerCubeNode : ToPointerGenericNode<DX11CubeRenderTarget>
    //{
    //    public override long calcPointer(int i, DX11RenderContext context)
    //    {
    //        return this.FTextureIn[i][context].Resource.ComPointer.ToInt64();
    //    }
    //}

    [PluginInfo(Name = "ToPointer", Category = "DX11.Texture", Version = "3d", Author = "sebl")]
    public class ToPointer3DNode : ToPointerGenericNode<DX11Texture3D>
    {
        public override long calcPointer(int i, DX11RenderContext context)
        {
            return this.FTextureIn[i][context].Resource.ComPointer.ToInt64();
        }
    }

    public abstract class ToPointerGenericNode<T> : IPluginEvaluate, IDX11ResourceDataRetriever where T : IDX11Resource
    {
        [Import()]
        protected IPluginHost FHost;

        [Input("Texture In")]
        protected Pin<DX11Resource<T>> FTextureIn;

        [Input("Apply", IsSingle = true, IsBang = true)]
        protected ISpread<bool> FApply;

        [Output("Pointer", AsInt = true)]
        protected ISpread<long> FPointer;

        public void Evaluate(int SpreadMax)
        {
            if (this.FTextureIn.PluginIO.IsConnected && FApply[0])
            {
                if (this.RenderRequest != null)
                {
                    this.RenderRequest(this, this.FHost);
                }

                if (this.AssignedContext == null)
                {
                    return;
                }

                this.FPointer.SliceCount = SpreadMax;

                DX11RenderContext context = this.AssignedContext;

                for (int i = 0; i < SpreadMax; i++)
                {
                    if (this.FTextureIn[i].Contains(context) )
                    {
                        //try
                        //{
                            FPointer[i] = calcPointer(i, context);
                        //}
                        //catch
                        //{
                        //    FPointer[i] = 0;
                        //}
                    }
                    else
                    {
                        FPointer[i] = 0;
                    }
                }
            }
            else
            {

            }
        }

        public abstract long calcPointer(int i, DX11RenderContext context);

        public DX11RenderContext AssignedContext
        {
            get;
            set;
        }

        public event DX11RenderRequestDelegate RenderRequest;
    }

}
