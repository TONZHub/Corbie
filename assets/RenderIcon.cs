using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class RenderCorbieIcon {
  static byte[] Render(DrawingImage artwork,int size) {
    var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(artwork,new Rect(0,0,size,size));
    var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=new MemoryStream()) {encoder.Save(stream);return stream.ToArray();}
  }
  [STAThread] public static int Main(string[] args) {
    try {
      string folder=Path.GetFullPath(args[0]);DrawingImage artwork;
      using(var file=File.OpenRead(Path.Combine(folder,"corbie-icon.xaml")))artwork=(DrawingImage)XamlReader.Load(file);
      artwork.Freeze();int[] sizes={256,128,64,48,40,32,24,20,16};var frames=new List<byte[]>();
      foreach(int size in sizes) {var bytes=Render(artwork,size);frames.Add(bytes);File.WriteAllBytes(Path.Combine(folder,"corbie-icon-"+size+".png"),bytes);}
      using(var file=File.Create(Path.Combine(folder,"corbie.ico")))using(var writer=new BinaryWriter(file)) {
        writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
        for(int i=0;i<sizes.Length;i++) {int size=sizes[i];writer.Write((byte)(size==256?0:size));writer.Write((byte)(size==256?0:size));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;}
        foreach(var frame in frames)writer.Write(frame);
      }
      return 0;
    }catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
  }
}
