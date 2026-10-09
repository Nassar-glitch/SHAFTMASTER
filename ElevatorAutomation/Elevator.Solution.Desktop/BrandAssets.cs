using System;
using System.IO;
using System.Windows.Media.Imaging;
namespace Elevator.Solution.Desktop
{
    public static class BrandAssets
    {
        public static BitmapImage Image(string name)
        {
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
            // Embedded assets keep the EXE/window brand available independently of installation paths.
            image.StreamSource=typeof(BrandAssets).Assembly.GetManifestResourceStream("ShaftMaster.Assets."+name);
            if(image.StreamSource==null)throw new FileNotFoundException("Bundled ShaftMaster asset missing: "+name);
            image.EndInit();image.StreamSource.Dispose();image.Freeze();return image;
        }
    }
}
