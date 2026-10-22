// Cursor sizing / hotspot handling adapted from 0cch/ShakeToFindCursor (MIT).
// Copyright (c) 2024 0CCh. See third-party/ShakeToFindCursor/LICENSE.
// High-resolution images for native cursor replacement; no overlay window.
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32;

namespace grapher.Desktop
{
    public static class CursorNative
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X=x; Y=y; } }
        [StructLayout(LayoutKind.Sequential)] private struct CursorInfo { public int Size, Flags; public IntPtr Handle; public Point Position; }
        [StructLayout(LayoutKind.Sequential)] internal struct IconInfo { public int Icon; public uint HotX, HotY; public IntPtr Mask, Color; }
        [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo { public int Type, Width, Height, WidthBytes; public ushort Planes, BitsPixel; public IntPtr Bits; }
        public struct Sample { public bool Visible; public IntPtr Handle; public System.Drawing.Point Position; }
        [DllImport("user32.dll", SetLastError=true)] private static extern bool GetCursorInfo(ref CursorInfo info);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
        [DllImport("gdi32.dll", EntryPoint="GetObjectW")] internal static extern int GetObject(IntPtr value, int size, out BitmapInfo bitmap);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
        [DllImport("user32.dll", SetLastError=true)] internal static extern IntPtr CopyImage(IntPtr image, uint type, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool DestroyCursor(IntPtr cursor);
        [DllImport("user32.dll",EntryPoint="LoadCursorW")] internal static extern IntPtr LoadCursor(IntPtr instance,IntPtr id);
        [DllImport("user32.dll",EntryPoint="LoadImageW",CharSet=CharSet.Unicode)] internal static extern IntPtr LoadImage(IntPtr instance,string path,uint type,int width,int height,uint flags);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetSystemCursor(IntPtr cursor,uint id);
        [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr CreateIconIndirect(ref IconInfo info);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateBitmap(int width,int height,uint planes,uint bits,byte[] data);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, uint step, IntPtr brush, uint flags);
        public static Sample Read()
        {
            var info = new CursorInfo { Size=Marshal.SizeOf(typeof(CursorInfo)) };
            bool available=GetCursorInfo(ref info);
            return new Sample { Visible=available && info.Flags==1 && info.Handle!=IntPtr.Zero, Handle=info.Handle, Position=new System.Drawing.Point(info.Position.X,info.Position.Y) };
        }
        public static bool ButtonHeld()
        {
            foreach (int key in new[] {1,2,4,5,6}) if ((GetAsyncKeyState(key)&0x8000)!=0) return true;
            return false;
        }
    }

    public sealed class CursorPicture : IDisposable
    {
        public Bitmap Image { get; private set; }
        public System.Drawing.Point Hotspot { get; private set; }
        public bool HighResolutionSource { get; private set; }
        private CursorPicture() { }
        public static CursorPicture CaptureStandard(uint id,IntPtr original,double scale)
        {
            using(var native=Capture(original,1))
            {
                int width=Math.Min(512,(int)Math.Round(native.Image.Width*scale)), height=Math.Min(512,(int)Math.Round(native.Image.Height*scale));
                string key;
                switch(id)
                {
                    case 32512:key="Arrow";break;case 32513:key="IBeam";break;case 32514:key="Wait";break;
                    case 32515:key="Crosshair";break;case 32516:key="UpArrow";break;case 32642:key="SizeNWSE";break;
                    case 32643:key="SizeNESW";break;case 32644:key="SizeWE";break;case 32645:key="SizeNS";break;
                    case 32646:key="SizeAll";break;case 32648:key="No";break;case 32649:key="Hand";break;default:key="AppStarting";break;
                }
                string file=null;
                using(var settings=Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors"))file=settings?.GetValue(key) as string;
                file=string.IsNullOrWhiteSpace(file) ? null : Environment.ExpandEnvironmentVariables(file);
                if(id==32512 && (file==null || (Path.GetFileName(file).StartsWith("aero_arrow",StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFullPath(file).StartsWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Cursors")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))))
                    return SmoothArrow(width,height,new System.Drawing.Point((int)Math.Round(native.Hotspot.X*scale),(int)Math.Round(native.Hotspot.Y*scale)));
                if(file!=null && File.Exists(file))
                {
                    IntPtr source=CursorNative.LoadImage(IntPtr.Zero,file,2,width,height,0x10);
                    if(source!=IntPtr.Zero)
                    {
                        try
                        {
                            var picture=Capture(source,1);picture.HighResolutionSource=true;
                            picture.Hotspot=new System.Drawing.Point((int)Math.Round(native.Hotspot.X*scale),(int)Math.Round(native.Hotspot.Y*scale));
                            return picture;
                        }
                        finally {CursorNative.DestroyCursor(source);}
                    }
                }
                if(id==32513 && file==null)
                {
                    // The built-in monochrome caret has no large bitmap resource.
                    // Render the same I shape as geometry at its final resolution.
                    var image=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
                    var hotspot=new System.Drawing.Point((int)Math.Round(native.Hotspot.X*scale),(int)Math.Round(native.Hotspot.Y*scale));
                    using(var graphics=Graphics.FromImage(image))
                    using(var outline=new Pen(Color.Black,(float)Math.Max(2,scale)))
                    using(var ink=new Pen(Color.White,(float)Math.Max(1,scale*.38)))
                    {
                        graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                        float x=hotspot.X,y=hotspot.Y,halfWidth=(float)(4*scale),halfHeight=(float)(8*scale);
                        foreach(var pen in new[]{outline,ink})
                        {
                            pen.StartCap=pen.EndCap=LineCap.Round;
                            graphics.DrawLine(pen,x,y-halfHeight,x,y+halfHeight);
                            graphics.DrawLine(pen,x-halfWidth,y-halfHeight,x+halfWidth,y-halfHeight);
                            graphics.DrawLine(pen,x-halfWidth,y+halfHeight,x+halfWidth,y+halfHeight);
                        }
                    }
                    return new CursorPicture {Image=image,Hotspot=hotspot,HighResolutionSource=true};
                }
                return Capture(original,scale);
            }
        }
        public IntPtr CreateCursor(double ratio=1)
        {
            int width=Math.Max(1,(int)Math.Round(Image.Width*ratio)),height=Math.Max(1,(int)Math.Round(Image.Height*ratio));
            using(var frame=new Bitmap(width,height,PixelFormat.Format32bppPArgb))
            {
                using(var graphics=Graphics.FromImage(frame))
                {
                    graphics.CompositingMode=CompositingMode.SourceCopy;graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    if(width==Image.Width && height==Image.Height)graphics.DrawImageUnscaled(Image,0,0);
                    else using(var attributes=new ImageAttributes())
                    {attributes.SetWrapMode(WrapMode.TileFlipXY);graphics.DrawImage(Image,new Rectangle(0,0,width,height),0,0,Image.Width,Image.Height,GraphicsUnit.Pixel,attributes);}
                }
                IntPtr color=frame.GetHbitmap(Color.FromArgb(0));
                IntPtr mask=CursorNative.CreateBitmap(width,height,1,1,new byte[((width+15)/16)*2*height]);
                try
                {
                    var info=new CursorNative.IconInfo {Color=color,Mask=mask,HotX=(uint)Math.Round(Hotspot.X*ratio),HotY=(uint)Math.Round(Hotspot.Y*ratio)};
                    IntPtr cursor=CursorNative.CreateIconIndirect(ref info);if(cursor==IntPtr.Zero)throw new Win32Exception();return cursor;
                }
                finally {CursorNative.DeleteObject(color);if(mask!=IntPtr.Zero)CursorNative.DeleteObject(mask);}
            }
        }
        private static CursorPicture SmoothArrow(int width,int height,System.Drawing.Point hotspot)
        {
            const int samples=4;
            using(var source=new Bitmap(width*samples,height*samples,PixelFormat.Format32bppPArgb))
            {
                using(var graphics=Graphics.FromImage(source))
                using(var path=new GraphicsPath())
                {
                    graphics.ScaleTransform(width*samples/32f,height*samples/32f);
                    graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    path.AddPolygon(new[]{new PointF(.65f,.65f),new PointF(.65f,25.3f),new PointF(7.2f,19.2f),
                        new PointF(12.2f,30.4f),new PointF(17.0f,28.2f),new PointF(12.0f,17.0f),new PointF(22.4f,17.0f)});
                    using(var fill=new SolidBrush(Color.White))graphics.FillPath(fill,path);
                    using(var outline=new Pen(Color.FromArgb(24,24,27),.65f) {LineJoin=LineJoin.Round})graphics.DrawPath(outline,path);
                }
                var image=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
                try
                {
                    using(var graphics=Graphics.FromImage(image))
                    {
                        graphics.CompositingMode=CompositingMode.SourceCopy;graphics.CompositingQuality=CompositingQuality.HighQuality;
                        graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                        using(var attributes=new ImageAttributes())
                        {attributes.SetWrapMode(WrapMode.TileFlipXY);graphics.DrawImage(source,new Rectangle(0,0,width,height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);}
                    }
                    return new CursorPicture {Image=image,Hotspot=hotspot,HighResolutionSource=true};
                }
                catch {image.Dispose();throw;}
            }
        }
        public static CursorPicture Capture(IntPtr cursor, double scale)
        {
            if (!CursorNative.GetIconInfo(cursor,out var info)) throw new Win32Exception();
            int width, height;
            try
            {
                if (CursorNative.GetObject(info.Color!=IntPtr.Zero ? info.Color : info.Mask,Marshal.SizeOf(typeof(CursorNative.BitmapInfo)),out var bitmap)==0) throw new Win32Exception();
                width=bitmap.Width; height=info.Color!=IntPtr.Zero ? bitmap.Height : bitmap.Height/2;
            }
            finally { if(info.Color!=IntPtr.Zero) CursorNative.DeleteObject(info.Color); if(info.Mask!=IntPtr.Zero) CursorNative.DeleteObject(info.Mask); }
            if (width<1 || height<1 || width>512 || height>512) throw new InvalidOperationException("指针图像尺寸不支持。");
            scale=Math.Max(1,Math.Min(Math.Min(3,512.0/Math.Max(width,height)),scale));
            int w=Math.Max(1,(int)Math.Round(width*scale)), h=Math.Max(1,(int)Math.Round(height*scale));
            // Capture at native resolution, then resample the alpha image. CopyImage's
            // direct enlargement uses hard pixel steps on common Windows cursors.
            IntPtr scaled=CursorNative.CopyImage(cursor,2,width,height,0);
            if (scaled==IntPtr.Zero) throw new Win32Exception();
            try
            {
                using(var black=Draw(scaled,width,height,Color.Black))
                using(var white=Draw(scaled,width,height,Color.White))
                {
                    var image=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
                    try
                    {
                        byte[] dark=ReadPixels(black), light=ReadPixels(white), pixels=new byte[width*height*4];
                        for(int i=0;i<pixels.Length;i+=4)
                        {
                            int difference=Math.Max(light[i]-dark[i],Math.Max(light[i+1]-dark[i+1],light[i+2]-dark[i+2]));
                            int alpha=255-Math.Max(0,difference);
                            bool inverse=light[i]<dark[i] && light[i+1]<dark[i+1] && light[i+2]<dark[i+2];
                            for(int c=0;c<3;c++) pixels[i+c]=inverse ? (byte)0 : (byte)Math.Min(alpha,dark[i+c]);
                            pixels[i+3]=(byte)alpha;
                        }
                        // Some pointer themes use a single light or dark ink for the
                        // text caret. Give those shapes a contrasting one-pixel rim.
                        bool darkInk=false, lightInk=false;
                        for(int i=0;i<pixels.Length;i+=4)
                            if(pixels[i+3]>128)
                            { if(Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2]))<128) darkInk=true; else lightInk=true; }
                        if(darkInk!=lightInk)
                        {
                            var original=(byte[])pixels.Clone(); byte rim=darkInk ? (byte)255 : (byte)0;
                            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
                                if(original[(y*width+x)*4+3]>128)
                                    for(int oy=-1;oy<=1;oy++) for(int ox=-1;ox<=1;ox++)
                                    {
                                        int nx=x+ox, ny=y+oy;
                                        if(nx<0 || ny<0 || nx>=width || ny>=height) continue;
                                        int index=(ny*width+nx)*4;
                                        if(original[index+3]>64) continue;
                                        pixels[index]=pixels[index+1]=pixels[index+2]=rim; pixels[index+3]=255;
                                    }
                        }
                        var data=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
                        try { for(int y=0;y<height;y++) Marshal.Copy(pixels,y*width*4,IntPtr.Add(data.Scan0,y*data.Stride),width*4); }
                        finally { image.UnlockBits(data); }
                        if(w!=width || h!=height)
                        {
                            var enlarged=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
                            try
                            {
                                using(var graphics=Graphics.FromImage(enlarged))
                                {
                                    graphics.CompositingMode=CompositingMode.SourceCopy;
                                    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                                    graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                                    using(var attributes=new ImageAttributes())
                                    { attributes.SetWrapMode(WrapMode.TileFlipXY); graphics.DrawImage(image,new Rectangle(0,0,w,h),0,0,width,height,GraphicsUnit.Pixel,attributes); }
                                }
                            }
                            catch { enlarged.Dispose(); throw; }
                            image.Dispose(); image=enlarged;
                        }
                        return new CursorPicture { Image=image,Hotspot=new System.Drawing.Point((int)Math.Round(info.HotX*scale),(int)Math.Round(info.HotY*scale)) };
                    }
                    catch { image.Dispose(); throw; }
                }
            }
            finally { if(scaled!=cursor) CursorNative.DestroyCursor(scaled); }
        }
        private static Bitmap Draw(IntPtr cursor,int width,int height,Color background)
        {
            var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb);
            try
            {
                using(var graphics=Graphics.FromImage(bitmap))
                {
                    graphics.Clear(background); var dc=graphics.GetHdc();
                    try { if(!CursorNative.DrawIconEx(dc,0,0,cursor,width,height,0,IntPtr.Zero,3)) throw new Win32Exception(); }
                    finally { graphics.ReleaseHdc(dc); }
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }
        private static byte[] ReadPixels(Bitmap bitmap)
        {
            var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try
            {
                var pixels=new byte[bitmap.Width*bitmap.Height*4];
                for(int y=0;y<bitmap.Height;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*bitmap.Width*4,bitmap.Width*4);
                return pixels;
            }
            finally { bitmap.UnlockBits(data); }
        }
        public void Dispose() { Image?.Dispose(); Image=null; }
    }

}
