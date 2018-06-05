using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FeralTic.DX11;
using FeralTic.DX11.Resources;

using SlimDX.Direct3D11;

namespace FeralTic.DX11.Resources
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
        public bool isowner;
        protected Texture3DDescription desc;


        public DX11Texture3D_extension(DX11RenderContext context) : base(context)
        {
            // nothing to add
        }


        public static DX11Texture3D FromPointer(DX11RenderContext context, IntPtr pointer)
        {
            Texture3D tex = Texture3D.FromPointer(pointer);
            ShaderResourceView srv = new ShaderResourceView(context.Device, tex);

            DX11Texture3D_extension result = new DX11Texture3D_extension(context);
            result.context = context;
            result.Resource = tex;
            result.SRV = srv;
            result.desc = tex.Description; // desc will not be used by pipeline?

            result.Format = tex.Description.Format;
            result.Width = tex.Description.Width;
            result.Height = tex.Description.Height;
            result.Depth = tex.Description.Depth;

            result.isowner = false; // isowner will not be taken into account by pipeline, yet

            return result;
        }

        // is now a member of DX11Texture3D
        //public static DX11Texture3D_extension FromDescription(DX11RenderContext context, Texture3DDescription desc)
        //{
        //    DX11Texture3D_extension result = new DX11Texture3D_extension(context);
        //    result.context = context;
        //    result.Resource = new Texture3D(context.Device, desc);
        //    result.isowner = true;
        //    result.desc = desc;
        //    result.SRV = new ShaderResourceView(context.Device, result.Resource);

        //    result.Format = desc.Format;
        //    result.Width = desc.Width;
        //    result.Height = desc.Height;
        //    result.Depth = desc.Depth;

        //    result.isowner = false; // isowner will not be taken into account by pipeline, yet

        //    return result;
        //}
    }
}
