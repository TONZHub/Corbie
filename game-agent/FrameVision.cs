using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

static class FrameVision {
  // A template taken from Refine's visible dialogue marker, not an assumed click location.
  // Search the dialogue region on every fresh frame; absent markers produce no candidate.
  static object[] Controls(Bitmap bmp) {
    var controls=new System.Collections.Generic.List<object>();
    controls.AddRange(DialogControl(bmp));
    string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"schedule.png");
    if(!File.Exists(path))return controls.ToArray();
    using(var normalized=new Bitmap(bmp,new Size(1200,900)))using(var marker=new Bitmap(path)) {
      double best=Double.MaxValue;int bestX=0,bestY=0;
      for(int y=350;y<=650;y++)for(int x=850;x<=1200-marker.Width;x++) {
        bool anchors=true;
        foreach(var a in new[]{new Point(30,8),new Point(140,18),new Point(250,25)}) {
          var c=normalized.GetPixel(x+a.X,y+a.Y);var t=marker.GetPixel(a.X,a.Y);
          if(Math.Abs(c.R-t.R)+Math.Abs(c.G-t.G)+Math.Abs(c.B-t.B)>65){anchors=false;break;}
        }
        if(!anchors)continue;
        double error=0;int n=0;
        for(int yy=0;yy<marker.Height;yy+=3)for(int xx=0;xx<marker.Width;xx+=5) {
          var c=normalized.GetPixel(x+xx,y+yy);var t=marker.GetPixel(xx,yy);
          error+=Math.Abs(c.R-t.R)+Math.Abs(c.G-t.G)+Math.Abs(c.B-t.B);n+=3;
        }
        error/=n;if(error<best){best=error;bestX=x;bestY=y;}
      }
      if(best<15)controls.Add(new {id="schedule",label="Schedule",x=(bestX+marker.Width/2.0)/1200*1000,y=(bestY+marker.Height/2.0)/900*1000,matchError=best});
    }
    return controls.ToArray();
  }
  static object[] DialogControl(Bitmap bmp) {
    using(var normalized=new Bitmap(bmp,new Size(1200,900))) {
      var data=normalized.LockBits(new Rectangle(0,0,1200,900),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
      int stride=data.Stride;var bytes=new byte[stride*900];
      try {System.Runtime.InteropServices.Marshal.Copy(data.Scan0,bytes,0,bytes.Length);}finally {normalized.UnlockBits(data);}
      Func<int,int,Color> pixel=(x,y)=>{int i=y*stride+x*4;return Color.FromArgb(bytes[i+2],bytes[i+1],bytes[i]);};
      int bestX=0,bestY=0,bestWidth=0,bestHeight=0;double best=Double.MaxValue;
      foreach(string name in new[]{"dialog-next.png","dialog-next-dark.png","dialog-next-portrait.png"}) {
      string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name);
      if(!File.Exists(path))continue;
      using(var marker=new Bitmap(path)) {
      var template=new Color[marker.Width,marker.Height];
      for(int ty=0;ty<marker.Height;ty++)for(int tx=0;tx<marker.Width;tx++)template[tx,ty]=marker.GetPixel(tx,ty);
      for(int y=250;y<=900-marker.Height;y++)for(int x=20;x<=850-marker.Width;x++) {
        // Both the lit and dark animation frames retain the triangle's border.
        bool anchors=true;
        foreach(var p in new[]{new Point(12,5),new Point(30,5),new Point(21,14)}) {
          var a=pixel(x+p.X,y+p.Y);var b=template[p.X,p.Y];
          if(Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)>140){anchors=false;break;}
        }
        if(!anchors)continue;
        double error=0;int n=0;
        // Compare the triangular icon itself, excluding portrait/room pixels around it.
        for(int yy=2;yy<Math.Min(22,marker.Height);yy+=3)for(int xx=yy;xx<marker.Width-yy;xx+=3) {
          var a=pixel(x+xx,y+yy);var b=template[xx,yy];
          error+=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);n+=3;
        }
        error/=n;if(error<best){best=error;bestX=x;bestY=y;bestWidth=marker.Width;bestHeight=marker.Height;}
      }
      }
      }
      if(best>18)return new object[0];
      return new object[]{new {id="dialog_next",label="Next dialogue",x=(bestX+bestWidth/2.0)/1200*1000,y=(bestY+bestHeight/2.0)/900*1000,matchError=best}};
    }
  }
  static string Encode(Bitmap bmp) {using(var s=new MemoryStream()){bmp.Save(s,ImageFormat.Png);return Convert.ToBase64String(s.ToArray());}}
  static string Crop(Bitmap bmp,double x,double y,double w,double h) {
    var r=new Rectangle((int)(bmp.Width*x),(int)(bmp.Height*y),Math.Max(1,(int)(bmp.Width*w)),Math.Max(1,(int)(bmp.Height*h)));
    using(var cropped=bmp.Clone(r,PixelFormat.Format24bppRgb))return Encode(cropped);
  }
  static object ReadText(Bitmap bmp) {
    var engine=OcrEngine.TryCreateFromUserProfileLanguages();
    if(engine==null)return new {available=false,text="",warning="Windows OCR language is unavailable."};
    double scale=Math.Min(1,2200.0/Math.Max(bmp.Width,bmp.Height));
    using(var scaled=new Bitmap(bmp,new Size((int)(bmp.Width*scale),(int)(bmp.Height*scale))))
    using(var ms=new MemoryStream())
    using(var stream=new InMemoryRandomAccessStream()) {
      scaled.Save(ms,ImageFormat.Png);
      using(var writer=new DataWriter(stream.GetOutputStreamAt(0))) {writer.WriteBytes(ms.ToArray());writer.StoreAsync().AsTask().GetAwaiter().GetResult();writer.DetachStream();}
      var decoder=BitmapDecoder.CreateAsync(stream).AsTask().GetAwaiter().GetResult();
      using(var pixels=decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied).AsTask().GetAwaiter().GetResult()) {
        var result=engine.RecognizeAsync(pixels).AsTask().GetAwaiter().GetResult();
        return new {available=true,text=result.Text,
          lines=result.Lines.Select(line=>new {text=line.Text,words=line.Words.Select(word=>new {text=word.Text,x=word.BoundingRect.X/scaled.Width*1000,y=word.BoundingRect.Y/scaled.Height*1000,w=word.BoundingRect.Width/scaled.Width*1000,h=word.BoundingRect.Height/scaled.Height*1000}).ToArray()}).ToArray()};
      }
    }
  }
  public static object Prepare(Bitmap bmp) {
    object ocr;
    try {ocr=ReadText(bmp);}catch {ocr=new {available=false,text="",warning="Windows OCR could not read this frame; use the image crops."};}
    return new {hud=Crop(bmp,.75,0,.25,.60),calendar=Crop(bmp,0,0,.24,.18),dialog=Crop(bmp,0,.68,.6,.32),ocr=ocr,controls=Controls(bmp)};
  }
  public static object FromPng(string png) {
    using(var stream=new MemoryStream(Convert.FromBase64String(png)))using(var bmp=new Bitmap(stream))return Prepare(bmp);
  }
}
