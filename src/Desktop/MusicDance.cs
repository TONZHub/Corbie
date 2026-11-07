using System;
using System.Diagnostics;
using NAudio.Wave;

namespace CorbiePet {
  // A short, smoothed volume envelope for three bands. No tempo lock or audio storage.
  public sealed class AudioEnergy {
    readonly int block;
    int count;
    double power,lowPower,highPower,average=0.025;
    public double Level,Low,High;
    public AudioEnergy(int rate) { block=Math.Max(1,rate/100); }
    static double Smooth(double old,double target) { return old+(target-old)*(target>old?0.4:0.055); }
    public void Feed(double full,double low,double high) {
      power+=full; lowPower+=low; highPower+=high;
      if(++count<block) return;
      double rms=Math.Sqrt(power/count), bass=Math.Sqrt(lowPower/count), treble=Math.Sqrt(highPower/count);
      average+=(rms-average)*(rms>average?0.004:0.001);
      double scale=Math.Max(0.025,average*2.5);
      Level=Smooth(Level,Map(rms,scale)); Low=Smooth(Low,Map(bass,scale)); High=Smooth(High,Map(treble,scale));
      count=0; power=lowPower=highPower=0;
    }
    static double Map(double value,double scale) { return Math.Sqrt(Math.Max(0,Math.Min(1,(value-0.0008)/scale))); }
  }
  public sealed class MusicDance : IDisposable {
    readonly object gate=new object();
    readonly Stopwatch clock=Stopwatch.StartNew();
    WasapiLoopbackCapture capture;
    AudioEnergy detector;
    MelodyPitch melody;
    int channels,bytesPerSample,frameSize;
    bool floatSamples;
    double[] lows,highs;
    double lowAlpha,highAlpha,receivedAt=-100;
    string error;
    public struct State { public bool Active; public double Level,Low,High; public int PitchMidi,PitchSerial; public double PitchConfidence; public string Error; }
    void Configure(int rate,int channelCount) {
      channels=channelCount; detector=new AudioEnergy(rate); melody=new MelodyPitch(rate); lows=new double[channels]; highs=new double[channels];
      lowAlpha=1-Math.Exp(-2*Math.PI*180/rate); highAlpha=1-Math.Exp(-2*Math.PI*2000/rate);
    }
    public void Start() {
      Stop(); var next=new WasapiLoopbackCapture();
      try {
        var format=next.WaveFormat;
        bytesPerSample=format.BitsPerSample/8; frameSize=format.BlockAlign;
        var extended=format as WaveFormatExtensible;
        floatSamples=format.Encoding==WaveFormatEncoding.IeeeFloat || (extended!=null && extended.SubFormat==new Guid("00000003-0000-0010-8000-00aa00389b71"));
        bool pcm=format.Encoding==WaveFormatEncoding.Pcm || (extended!=null && extended.SubFormat==new Guid("00000001-0000-0010-8000-00aa00389b71"));
        if((floatSamples && bytesPerSample!=4) || (!floatSamples && (!pcm || bytesPerSample<2 || bytesPerSample>4))) throw new InvalidOperationException("Unsupported output audio format");
        lock(gate) { Configure(format.SampleRate,format.Channels); error=null; receivedAt=-100; }
        next.DataAvailable+=Audio; next.RecordingStopped+=Stopped;
        capture=next; next.StartRecording();
      } catch { next.Dispose(); capture=null; throw; }
    }
    void Audio(object sender,WaveInEventArgs args) {
      lock(gate) {
        if(detector==null) return;
        for(int frame=0;frame+frameSize<=args.BytesRecorded;frame+=frameSize) {
          double full=0,low=0,high=0,melodicSample=0;
          for(int channel=0;channel<channels;channel++) {
            int offset=frame+channel*bytesPerSample; double value;
            if(floatSamples) value=BitConverter.ToSingle(args.Buffer,offset);
            else if(bytesPerSample==2) value=BitConverter.ToInt16(args.Buffer,offset)/32768.0;
            else if(bytesPerSample==4) value=BitConverter.ToInt32(args.Buffer,offset)/2147483648.0;
            else { int raw=args.Buffer[offset]|args.Buffer[offset+1]<<8|args.Buffer[offset+2]<<16; if((raw&0x800000)!=0) raw|=unchecked((int)0xff000000); value=raw/8388608.0; }
            if(double.IsNaN(value)||double.IsInfinity(value)) value=0;
            value=Math.Max(-1,Math.Min(1,value));
            if(channel==0) melodicSample=value;
            lows[channel]+=(value-lows[channel])*lowAlpha;
            highs[channel]+=(value-highs[channel])*highAlpha;
            // Filter signed samples per channel before combining their energies.
            full+=value*value; low+=lows[channel]*lows[channel]; high+=(value-highs[channel])*(value-highs[channel]);
          }
          detector.Feed(full/channels,low/channels,high/channels);
          melody.Feed(melodicSample);
        }
        receivedAt=clock.Elapsed.TotalSeconds;
      }
    }
    void Stopped(object sender,StoppedEventArgs args) { lock(gate) error="Audio output changed or stopped. Toggle Dance to music to reconnect."; }
    public State Read() {
      lock(gate) {
        var state=new State { Error=error }; if(detector==null) return state;
        // Loopback sends no buffers on some devices during silence: decay stale data too.
        double lag=clock.Elapsed.TotalSeconds-receivedAt;
        double decay=Math.Exp(-Math.Max(0,lag-0.1)*8);
        state.Level=detector.Level*decay; state.Low=detector.Low*decay; state.High=detector.High*decay;
        if(lag<0.25 && melody!=null) { state.PitchMidi=melody.Midi; state.PitchConfidence=melody.Confidence; state.PitchSerial=melody.Serial; }
        state.Active=state.Level>0.025; return state;
      }
    }
    public void Stop() {
      var old=capture; capture=null;
      if(old!=null) { old.DataAvailable-=Audio; old.RecordingStopped-=Stopped; old.StopRecording(); old.Dispose(); }
      lock(gate) { detector=null; melody=null; error=null; }
    }
    public void Dispose() { Stop(); }
    public static string Probe() {
      using(var listener=new MusicDance()) { listener.Start(); System.Threading.Thread.Sleep(1200); var state=listener.Read(); if(state.Error!=null) throw new Exception(state.Error); return "Windows output capture opened and closed successfully; audio analyzed only in memory."; }
    }
    void TestTone(double frequency,double amplitude,int batches) {
      var packet=new byte[480*8];
      for(int batch=0;batch<batches;batch++) {
        for(int frame=0;frame<480;frame++) {
          double time=(batch*480+frame)/48000.0;
          float value=(float)(Math.Sin(time*2*Math.PI*frequency)*amplitude);
          Array.Copy(BitConverter.GetBytes(value),0,packet,frame*8,4); Array.Copy(BitConverter.GetBytes(-value),0,packet,frame*8+4,4);
        }
        Audio(null,new WaveInEventArgs(packet,packet.Length));
      }
    }
    public static void Check() {
      MelodyPitch.Check();
      using(var listener=new MusicDance()) {
        listener.bytesPerSample=4; listener.frameSize=8; listener.floatSamples=true; listener.Configure(48000,2);
        listener.TestTone(80,0.008,40); var soft=listener.Read();
        if(!soft.Active || soft.Low<=soft.High) throw new Exception("Soft sustained bass did not animate");
        listener.TestTone(80,0.08,40); var loud=listener.Read();
        if(loud.Level<=soft.Level) throw new Exception("Louder audio did not increase movement");
        listener.TestTone(6000,0.02,80); var bright=listener.Read();
        if(!bright.Active || bright.High<=bright.Low) throw new Exception("Treble band did not respond");
        listener.TestTone(100,0,200); if(listener.Read().Active) throw new Exception("Silence did not settle");
        listener.TestTone(100,0.0002,80); if(listener.Read().Active) throw new Exception("Noise floor triggered movement");
        listener.TestTone(220,0.02,40); listener.receivedAt=-100;
        if(listener.Read().Active) throw new Exception("Missing audio packets did not settle");
      }
      using(var listener=new MusicDance()) {
        listener.bytesPerSample=4; listener.frameSize=8; listener.floatSamples=true; listener.Configure(48000,2);
        listener.TestTone(440,.06,45); var a=listener.Read();
        if(Math.Abs(a.PitchMidi-69)>1 || a.PitchSerial<=0) throw new Exception("Loopback pitch path missed A4.");
        listener.TestTone(523.25,.06,45); var b=listener.Read();
        if(Math.Abs(b.PitchMidi-72)>1 || b.PitchSerial<=a.PitchSerial) throw new Exception("Loopback pitch path missed C5.");
      }
      using(var listener=new MusicDance()) {
        listener.bytesPerSample=4; listener.frameSize=8; listener.floatSamples=true; listener.Configure(48000,2);
        var ear=new BirdTuneEar(); double[] phrase={440,523.25,659.25};
        for(int i=0;i<90;i++) { listener.TestTone(phrase[(i/5)%3],.06,10); ear.Observe(.1,listener.Read()); }
        if(ear.Remembered==null || ear.Remembered.Notes.Length<3) throw new Exception("Captured tones did not become a bird motif.");
      }
    }
  }
}
