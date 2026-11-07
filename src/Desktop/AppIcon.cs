using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace CorbiePet {
  public static class AppIcon {
    const string Resource="CorbiePet.AppIcon.ico";
    static BitmapFrame windowImage;
    public static bool Embedded {get {using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource))return stream!=null;}}
    public static BitmapFrame WindowImage {
      get {
        if(windowImage!=null)return windowImage;
        try {using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)) {if(stream==null)return null;windowImage=BitmapFrame.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);windowImage.Freeze();return windowImage;}}
        catch {return null;} // Cosmetic assets must not prevent startup.
      }
    }
    public static System.Drawing.Icon CreateTrayIcon() {
      try {using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)) {if(stream!=null)using(var icon=new System.Drawing.Icon(stream,new System.Drawing.Size(32,32)))return (System.Drawing.Icon)icon.Clone();}}
      catch { }
      return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }
    public static string Check() {
      if(!Embedded || WindowImage==null || !WindowImage.IsFrozen)throw new Exception("Window icon was not embedded or loaded.");
      int[] expected={256,128,64,48,40,32,24,20,16};
      using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)) {
        var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        if(decoder.Frames.Count!=expected.Length)throw new Exception("Missing icon resolutions.");
        var sizes=new System.Collections.Generic.HashSet<int>(expected);
        foreach(var frame in decoder.Frames)if(frame.PixelWidth!=frame.PixelHeight || !sizes.Remove(frame.PixelWidth))throw new Exception("Icon frame dimensions incorrect: "+frame.PixelWidth+"x"+frame.PixelHeight);
        if(sizes.Count!=0)throw new Exception("Missing icon sizes.");
      }
      using(var tray=CreateTrayIcon()) {if(tray.Width!=32 || tray.Height!=32)throw new Exception("Tray didn't receive its 32-pixel icon.");}
      using(var shell=System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location)) {
        if(shell==null)throw new Exception("Windows could not extract the executable icon.");
        using(var bitmap=shell.ToBitmap()) {
          int purple=0,cream=0;
          for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++) {var c=bitmap.GetPixel(x,y);if(c.A>0 && c.R<70 && c.G<70 && c.B>c.R+5)purple++;if(c.A>0 && c.R>200 && c.G>180 && c.B>140)cream++;}
          if(purple<10 || cream<10)throw new Exception("Executable shell icon doesn't contain Corbie's artwork.");
        }
      }
      return "PASS: nine embedded icon resolutions (16–256px), frozen WPF image, owned tray icon and real Windows extraction of Corbie's executable icon.";
    }
  }
}
