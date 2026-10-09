using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace Elevator.Solution.Desktop
{
    public static class WindowLayout
    {
        public static Rect Fit(Rect requested,Rect work)
        {
            double width=Math.Min(Math.Max(720,requested.Width),Math.Max(320,work.Width-16));
            double height=Math.Min(Math.Max(420,requested.Height),Math.Max(240,work.Height-16));
            return new Rect(Math.Max(work.Left,Math.Min(requested.Left,work.Right-width)),Math.Max(work.Top,Math.Min(requested.Top,work.Bottom-height)),width,height);
        }
        public static void Initial(Window window)
        {
            var work=SystemParameters.WorkArea;var bounds=Fit(new Rect(work.Left+24,work.Top+24,1220,820),work);
            window.MinWidth=Math.Min(720,bounds.Width);window.MinHeight=Math.Min(420,bounds.Height);window.Width=bounds.Width;window.Height=bounds.Height;window.Left=work.Left+(work.Width-bounds.Width)/2;window.Top=work.Top+(work.Height-bounds.Height)/2;
            window.WindowStartupLocation=WindowStartupLocation.Manual;window.WindowStyle=WindowStyle.SingleBorderWindow;window.ResizeMode=ResizeMode.CanResize;
        }
        public static void Clamp(Window window)
        {
            if(window.WindowState!=WindowState.Normal)return;
            var work=SystemParameters.WorkArea;var handle=new WindowInteropHelper(window).Handle;
            if(handle!=IntPtr.Zero)
            {
                var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};var monitor=MonitorFromWindow(handle,2);
                if(GetMonitorInfo(monitor,ref info))
                {
                    var source=HwndSource.FromHwnd(handle);var transform=source==null||source.CompositionTarget==null?System.Windows.Media.Matrix.Identity:source.CompositionTarget.TransformFromDevice;
                    work=new Rect(transform.Transform(new Point(info.Work.Left,info.Work.Top)),transform.Transform(new Point(info.Work.Right,info.Work.Bottom)));
                }
            }
            var rect=Fit(new Rect(Double.IsNaN(window.Left)?work.Left:window.Left,Double.IsNaN(window.Top)?work.Top:window.Top,window.Width,window.Height),work);
            window.MinWidth=Math.Min(720,rect.Width);window.MinHeight=Math.Min(420,rect.Height);window.Left=rect.Left;window.Top=rect.Top;window.Width=rect.Width;window.Height=rect.Height;
        }
        [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]struct MonitorInfo{public int Size;public NativeRect Monitor,Work;public int Flags;}
        [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Auto)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    }
}
