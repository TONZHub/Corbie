using System;
using System.IO;
using System.Threading;
using NAudio.Wave;

namespace CorbiePet {
  public enum FoleyKind {
    Caw,
    Clack,
    Whoosh,
    SuctionPop
  }

  public static class CreatureFoley {
    static readonly byte[] cachedCaw;
    static readonly byte[] cachedClack;
    static readonly byte[] cachedWhoosh;
    static readonly byte[] cachedPop;

    static CreatureFoley() {
      Enabled = true;
      cachedCaw = SynthesizeCaw();
      cachedClack = SynthesizeClack();
      cachedWhoosh = SynthesizeWhoosh();
      cachedPop = SynthesizeSuctionPop();
    }

    public static bool Enabled { get; set; }

    public static void Play(FoleyKind kind, bool quiet = false) {
      if (!Enabled || quiet) return;
      byte[] data;
      switch (kind) {
        case FoleyKind.Caw: data = cachedCaw; break;
        case FoleyKind.Clack: data = cachedClack; break;
        case FoleyKind.Whoosh: data = cachedWhoosh; break;
        case FoleyKind.SuctionPop: data = cachedPop; break;
        default: return;
      }

      if (data == null || data.Length == 0) return;
      ThreadPool.QueueUserWorkItem(_ => {
        try {
          using (var ms = new MemoryStream(data, false))
          using (var reader = new WaveFileReader(ms))
          using (var waveOut = new WaveOutEvent()) {
            waveOut.Init(reader);
            waveOut.Play();
            while (waveOut.PlaybackState == PlaybackState.Playing) {
              Thread.Sleep(15);
            }
          }
        } catch { }
      });
    }

    public static byte[] BuildWav(short[] samples, int sampleRate) {
      int dataSize = samples.Length * 2;
      byte[] wav = new byte[44 + dataSize];
      using (var ms = new MemoryStream(wav))
      using (var bw = new BinaryWriter(ms)) {
        // RIFF header
        bw.Write(new char[] { 'R', 'I', 'F', 'F' });
        bw.Write(36 + dataSize);
        bw.Write(new char[] { 'W', 'A', 'V', 'E' });
        // fmt chunk
        bw.Write(new char[] { 'f', 'm', 't', ' ' });
        bw.Write(16); // subchunk 1 size (16 for PCM)
        bw.Write((short)1); // AudioFormat: 1 = PCM
        bw.Write((short)1); // NumChannels: 1 (mono)
        bw.Write(sampleRate); // SampleRate
        bw.Write(sampleRate * 2); // ByteRate = SampleRate * NumChannels * BitsPerSample/8
        bw.Write((short)2); // BlockAlign = NumChannels * BitsPerSample/8
        bw.Write((short)16); // BitsPerSample: 16
        // data chunk
        bw.Write(new char[] { 'd', 'a', 't', 'a' });
        bw.Write(dataSize);
        for (int i = 0; i < samples.Length; i++) {
          bw.Write(samples[i]);
        }
      }
      return wav;
    }

    // Synthesizes an expressive raven squawk/caw
    static byte[] SynthesizeCaw() {
      int rate = 22050;
      double duration = 0.35;
      int count = (int)(rate * duration);
      short[] samples = new short[count];
      for (int i = 0; i < count; i++) {
        double t = (double)i / rate;
        // Pitch drop from 1150Hz down to 620Hz
        double f = 1150.0 - 530.0 * Math.Pow(t / duration, 1.2);
        // Throat flutter amplitude modulation (AM at 42Hz)
        double am = 0.65 + 0.35 * Math.Sin(2.0 * Math.PI * 42.0 * t);
        // Harmonics with slight clipping/roughness
        double s1 = Math.Sin(2.0 * Math.PI * f * t);
        double s2 = 0.5 * Math.Sin(2.0 * Math.PI * f * 2.02 * t);
        double s3 = 0.25 * Math.Sin(2.0 * Math.PI * f * 3.05 * t);
        double raw = (s1 + s2 + s3) * am;
        // Envelope: fast attack, steady sustain, smooth release
        double env;
        if (t < 0.025) env = t / 0.025;
        else if (t < 0.24) env = 1.0;
        else env = Math.Max(0.0, 1.0 - (t - 0.24) / (duration - 0.24));

        double val = raw * env * 26000.0;
        val = Math.Max(-32000.0, Math.Min(32000.0, val));
        samples[i] = (short)val;
      }
      return BuildWav(samples, rate);
    }

    // Synthesizes a snappy double beak-clack / click train
    static byte[] SynthesizeClack() {
      int rate = 22050;
      double duration = 0.09;
      int count = (int)(rate * duration);
      short[] samples = new short[count];
      for (int i = 0; i < count; i++) {
        double t = (double)i / rate;
        double val = 0.0;
        // Click 1 at t = 0.004s
        double t1 = t - 0.004;
        if (t1 >= 0 && t1 < 0.028) {
          double decay = Math.Exp(-t1 * 340.0);
          val += Math.Sin(2.0 * Math.PI * 1950.0 * t1) * decay;
        }
        // Click 2 at t = 0.036s
        double t2 = t - 0.036;
        if (t2 >= 0 && t2 < 0.035) {
          double decay = Math.Exp(-t2 * 280.0);
          val += 0.9 * Math.Sin(2.0 * Math.PI * 1700.0 * t2) * decay;
        }
        samples[i] = (short)Math.Max(-32000.0, Math.Min(32000.0, val * 29000.0));
      }
      return BuildWav(samples, rate);
    }

    // Synthesizes a soft wing flutter whoosh
    static byte[] SynthesizeWhoosh() {
      int rate = 22050;
      double duration = 0.28;
      int count = (int)(rate * duration);
      short[] samples = new short[count];
      var rand = new Random(1337);
      double filtered = 0.0;
      for (int i = 0; i < count; i++) {
        double t = (double)i / rate;
        // White noise sample
        double noise = rand.NextDouble() * 2.0 - 1.0;
        // Low-pass filter at ~850Hz
        filtered += (noise - filtered) * 0.22;
        // Wing beat AM modulation (13 Hz)
        double am = 0.35 + 0.65 * Math.Sin(2.0 * Math.PI * 13.0 * t);
        // Smooth swell envelope
        double env = Math.Sin(Math.PI * (t / duration));
        double val = filtered * am * env * 48000.0;
        samples[i] = (short)Math.Max(-32000.0, Math.Min(32000.0, val));
      }
      return BuildWav(samples, rate);
    }

    // Synthesizes an octopus tentacle suction-cup pop
    static byte[] SynthesizeSuctionPop() {
      int rate = 22050;
      double duration = 0.12;
      int count = (int)(rate * duration);
      short[] samples = new short[count];
      for (int i = 0; i < count; i++) {
        double t = (double)i / rate;
        // Pitch upward sweep 240Hz -> 850Hz
        double f = 240.0 + 610.0 * Math.Pow(t / duration, 1.8);
        double decay = Math.Exp(-t * 36.0);
        double s = Math.Sin(2.0 * Math.PI * f * t) * decay;
        // Add tiny high click at the very start
        if (t < 0.015) {
          s += 0.35 * Math.Sin(2.0 * Math.PI * 2200.0 * t) * (1.0 - t / 0.015);
        }
        samples[i] = (short)Math.Max(-32000.0, Math.Min(32000.0, s * 29000.0));
      }
      return BuildWav(samples, rate);
    }

    public static void SelfCheck() {
      if (cachedCaw == null || cachedCaw.Length < 100) throw new Exception("CreatureFoley: Caw synthesis failed");
      if (cachedClack == null || cachedClack.Length < 100) throw new Exception("CreatureFoley: Clack synthesis failed");
      if (cachedWhoosh == null || cachedWhoosh.Length < 100) throw new Exception("CreatureFoley: Whoosh synthesis failed");
      if (cachedPop == null || cachedPop.Length < 100) throw new Exception("CreatureFoley: Pop synthesis failed");
      // Check RIFF headers
      if (cachedCaw[0] != 'R' || cachedCaw[1] != 'I' || cachedCaw[2] != 'F' || cachedCaw[3] != 'F')
        throw new Exception("CreatureFoley: Invalid RIFF header in generated audio");
    }
  }
}
