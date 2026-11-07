using System;

namespace CorbiePet {
  // Small streaming pitch sketch for the local output analyzer. It keeps one
  // 2048-sample mono window and exposes only note estimates, never audio clips.
  public sealed class MelodyPitch {
    const int Size=2048;
    readonly double[] ring=new double[Size];
    readonly int stride,rate;
    int skip,write,filled,hop;
    public int Midi { get; private set; }
    public double Confidence { get; private set; }
    public int Serial { get; private set; }
    public MelodyPitch(int inputRate) {
      stride=Math.Max(1,inputRate/11025);
      rate=inputRate/stride;
    }
    public void Feed(double sample) {
      if(++skip<stride) return;
      skip=0;
      ring[write]=sample; write=(write+1)%Size;
      if(filled<Size) { filled++; if(filled==Size) Estimate(); return; }
      if(++hop>=Size/2) { hop=0; Estimate(); }
    }
    void Estimate() {
      Serial++; Midi=0; Confidence=0;
      var real=new double[Size]; var imaginary=new double[Size];
      double power=0;
      for(int i=0;i<Size;i++) {
        double sample=ring[(write+i)%Size]; power+=sample*sample;
        real[i]=sample*(0.5-0.5*Math.Cos(2*Math.PI*i/(Size-1)));
      }
      if(Math.Sqrt(power/Size)<0.004) return;
      int j=0;
      for(int i=1;i<Size;i++) {
        int bit=Size>>1;
        while((j&bit)!=0) { j^=bit; bit>>=1; }
        j^=bit;
        if(i<j) { double r=real[i]; real[i]=real[j]; real[j]=r; }
      }
      for(int length=2;length<=Size;length<<=1) {
        int half=length/2;
        for(int k=0;k<half;k++) {
          double angle=-2*Math.PI*k/length,c=Math.Cos(angle),s=Math.Sin(angle);
          for(int start=0;start<Size;start+=length) {
            double r=real[start+k+half]*c-imaginary[start+k+half]*s;
            double im=real[start+k+half]*s+imaginary[start+k+half]*c;
            real[start+k+half]=real[start+k]-r; imaginary[start+k+half]=imaginary[start+k]-im;
            real[start+k]+=r; imaginary[start+k]+=im;
          }
        }
      }
      var amplitude=new double[Size/2];
      for(int k=1;k<amplitude.Length;k++) amplitude[k]=Math.Sqrt(real[k]*real[k]+imaginary[k]*imaginary[k]);
      int min=Math.Max(2,(int)Math.Ceiling(170.0*Size/rate));
      int max=Math.Min(amplitude.Length/3-2,(int)Math.Floor(1000.0*Size/rate));
      double average=0; int count=0;
      for(int k=min;k<=Math.Min(amplitude.Length-1,(int)(3000.0*Size/rate));k++) { average+=amplitude[k]; count++; }
      average/=Math.Max(1,count);
      double best=0; int winner=0;
      for(int k=min;k<=max;k++) {
        double score=amplitude[k]+.46*amplitude[2*k]+.22*amplitude[3*k];
        if(score>best) { best=score; winner=k; }
      }
      if(winner<2 || best<average*7 || amplitude[winner]<average*3) return;
      double left=amplitude[winner-1],middle=amplitude[winner],right=amplitude[winner+1];
      double denominator=left-2*middle+right;
      double shift=Math.Abs(denominator)<1e-9?0:Math.Max(-.5,Math.Min(.5,.5*(left-right)/denominator));
      double frequency=(winner+shift)*rate/(double)Size;
      int note=(int)Math.Round(69+12*Math.Log(frequency/440,2));
      if(note<48 || note>84) return;
      Midi=note; Confidence=Math.Min(1,best/Math.Max(1e-9,average*15));
    }
    public static void Check() {
      var detector=new MelodyPitch(48000);
      int[] notes={69,72,76};
      foreach(int note in notes) {
        double frequency=440*Math.Pow(2,(note-69)/12.0);
        for(int i=0;i<48000*.45;i++) detector.Feed(.08*Math.Sin(2*Math.PI*frequency*i/48000));
        if(Math.Abs(detector.Midi-note)>1 || detector.Confidence<.2) throw new Exception("Pitch detector missed note "+note+" (got "+detector.Midi+").");
      }
      for(int i=0;i<24000;i++) detector.Feed(0);
      if(detector.Midi!=0) throw new Exception("Silence retained a pitch.");
    }
  }
}
