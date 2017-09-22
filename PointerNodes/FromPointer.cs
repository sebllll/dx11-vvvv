using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;

using FeralTic.DX11;
using FeralTic.DX11.Resources;

using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V1;
using SlimDX.Direct3D11;


namespace VVVV.DX11.Nodes.Textures
{
    public class DX11Texture2D_extension : DX11Texture2D
    {
        public static DX11Texture2D_extension FromPointer(DX11RenderContext context, IntPtr pointer)
        {
            Texture2D tex = Texture2D.FromPointer(pointer);
            ShaderResourceView srv = new ShaderResourceView(context.Device, tex);

            DX11Texture2D_extension result = new DX11Texture2D_extension();

            result.context = context;
            result.Resource = tex;
            result.SRV = srv;
            result.desc = tex.Description;

            result.isowner = false;
            return result;
        }
    }

    public class DX11Texture3D_extension : DX11Texture3D
    {
        public static DX11Texture3D FromPointer(DX11RenderContext context, IntPtr pointer)
        {
            Texture3D tex = Texture3D.FromPointer(pointer);
            ShaderResourceView srv = new ShaderResourceView(context.Device, tex);

            DX11Texture3D_extension result = new DX11Texture3D_extension();
            result.context = context;
            result.Resource = tex;
            result.SRV = srv;
            result.desc = tex.Description;

            result.isowner = false;
            return result;
        }
    }


    [PluginInfo(Name = "FromPointer", Category = "DX11.Texture", Version = "2d", Author = "sebl")]
    public class FromPointer2DNode : FromPointerGenericNode<DX11Texture2D>
    {
        public override DX11Texture2D newTexture(DX11RenderContext context, IntPtr handle)
        {
            return DX11Texture2D_extension.FromPointer(context, handle);
        }
    }

    [PluginInfo(Name = "FromPointer", Category = "DX11.Texture", Version = "3d", Author = "sebl")]
    public class FromPointer3DNode : FromPointerGenericNode<DX11Texture3D>
    {
        public override DX11Texture3D newTexture(DX11RenderContext context, IntPtr handle)
        {
            return DX11Texture3D_extension.FromPointer(context, handle);
        }
    }

    public abstract class FromPointerGenericNode<T> : IPluginEvaluate, IDX11ResourceHost, IDisposable where T : IDX11Resource
    {
        [Input("Pointer", AsInt = true)]
        protected IDiffSpread<long> FPointer;

        [Output("Texture")]
        protected Pin<DX11Resource<T>> FTextureOutput;

        [Output("Is Valid")]
        protected ISpread<bool> FValid;

        protected bool FInvalidate;


        public void Evaluate(int SpreadMax)
        {
            if (this.FPointer.SliceCount == 0)
            {
                this.FTextureOutput.SafeDisposeAll();
                this.FTextureOutput.SliceCount = 0;

                return;
            }

            this.FValid.SliceCount = SpreadMax;

            if (this.FPointer.IsChanged)
            {
                this.FInvalidate = true;
                this.FTextureOutput.SafeDisposeAll();
            }
        }


        public void Update(DX11RenderContext context)
        {
            if (this.FInvalidate)
            {
                this.FTextureOutput.SliceCount = 0;

                for (int i = 0; i < FPointer.SliceCount; i++)
                {
                    this.FTextureOutput.SliceCount += 1;

                    if (this.FTextureOutput[i] == null)
                    {
                        FTextureOutput[i] = new DX11Resource<T>();
                    }

                    try
                    {
                        IntPtr handle = new IntPtr(FPointer[i]);
                        if (handle.ToInt64() < 0)
                        {
                            handle = IntPtr.Zero;
                        }

                        this.FTextureOutput[i][context] = newTexture(context, handle);
                        this.FValid[i] = true;
                    }
                    catch
                    {
                        this.FValid[i] = false;
                    }
                }
                this.FInvalidate = false;
            }
        }


        public abstract T newTexture(DX11RenderContext context, IntPtr handle);


        public void Destroy(DX11RenderContext context, bool force)
        {
            this.FTextureOutput.SafeDisposeAll(context);
        }


        public void Dispose()
        {
            this.FTextureOutput.SafeDisposeAll();
        }
    }
}
