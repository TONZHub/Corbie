using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace CorbiePet {
  public enum VoiceTone {
    Bird,
    Normal,
    Chirpy,
    Croak,
    Grump
  }

  public sealed class PiperVoice : IDisposable {
    public sealed class Settings { public string Python { get; set; } public string Model { get; set; } public string Voice { get; set; } public string Tone { get; set; } public object Speaker { get; set; } }
    public static VoiceTone Tone { get; set; }
    public static string VoiceId { get; private set; }
    readonly Settings settings;
    CancellationTokenSource request;
    WaveOutEvent output;
    WaveFileReader reader;
    MemoryStream audio;
    bool disposed;
    public bool Busy { get { return request!=null || IsSpeaking; } }
    public bool IsSpeaking { get { return output!=null && output.PlaybackState==PlaybackState.Playing; } }
    public static bool Configured { get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"piper.json")); } }
    static PiperVoice() {
      Tone=VoiceTone.Bird;
      VoiceId="";
      try {
        string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"piper.json");
        if(File.Exists(file)) {
          var s=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(file));
          if(s!=null && !string.IsNullOrEmpty(s.Tone)) {
            Tone=(VoiceTone)Enum.Parse(typeof(VoiceTone),s.Tone,true);
          }
          if(s!=null) VoiceId=ResolveVoiceId(s);
        }
      } catch { }
    }
    public PiperVoice() {
      settings=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"piper.json")));
      if(settings!=null && !string.IsNullOrEmpty(settings.Voice)) {
        string selected=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"voices",settings.Voice+".onnx");
        if(File.Exists(selected) && File.Exists(selected+".json")) settings.Model=selected;
      }
      if(settings==null || !File.Exists(settings.Python) || !File.Exists(settings.Model) || !File.Exists(settings.Model+".json")) throw new InvalidOperationException("Run Setup-Piper.ps1 to configure a Python interpreter and voice model.");
      VoiceId=ResolveVoiceId(settings);
      if(!string.IsNullOrEmpty(settings.Tone)) {
        try { Tone=(VoiceTone)Enum.Parse(typeof(VoiceTone),settings.Tone,true); }
        catch { Tone=VoiceTone.Bird; }
      } else {
        Tone=VoiceTone.Bird;
      }
    }
    public static void SaveTone(VoiceTone tone) {
      Tone=tone;
      try {
        string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"piper.json");
        if(File.Exists(file)) {
          var s=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(file));
          if(s!=null) {
            s.Tone=tone.ToString();
            File.WriteAllText(file,new JavaScriptSerializer().Serialize(s));
          }
        }
      } catch { }
    }
    static string ResolveVoiceId(Settings value) {
      if(value==null) return "";
      if(!string.IsNullOrEmpty(value.Voice)) return value.Voice;
      return string.IsNullOrEmpty(value.Model)?"":Path.GetFileNameWithoutExtension(value.Model);
    }
    public static List<string> InstalledVoices() {
      var result=new List<string>(); string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"voices");
      try { if(Directory.Exists(folder)) foreach(string model in Directory.GetFiles(folder,"*.onnx")) if(File.Exists(model+".json")) result.Add(Path.GetFileNameWithoutExtension(model)); } catch { }
      if(!string.IsNullOrEmpty(VoiceId) && !result.Contains(VoiceId,StringComparer.OrdinalIgnoreCase)) result.Add(VoiceId);
      return result.OrderBy(FriendlyVoiceName).ToList();
    }
    public static string FriendlyVoiceName(string id) {
      switch((id??"").ToLowerInvariant()) {
        case "en_us-ryan-medium": return "Ryan -- warm American";
        case "en_gb-northern_english_male-medium": return "Northern -- dry British";
        case "en_gb-semaine-medium": return "Semaine -- multi-speaker British (Spike is speaker 1)";
        case "en_gb-alan-medium": return "Alan -- measured British";
        case "en_us-lessac-medium": return "Lessac -- clear American";
        default: return (id??"Unknown voice").Replace('_',' ').Replace('-',' ');
      }
    }
    public static void SaveVoice(string id) {
      string model=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"voices",id+".onnx");
      if(!File.Exists(model) || !File.Exists(model+".json")) throw new InvalidOperationException("That voice is not installed. Run Setup-Piper.ps1 to download the Corbie voice pack.");
      string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"piper.json");
      var s=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(file)); if(s==null) throw new InvalidOperationException("Piper settings are missing.");
      // Speaker ids are per-model; drop a stale one when switching to a different voice.
      if(!string.Equals(s.Voice,id,StringComparison.OrdinalIgnoreCase)) s.Speaker=null;
      s.Voice=id; s.Model=model; if(string.IsNullOrEmpty(s.Tone)) s.Tone=Tone.ToString(); File.WriteAllText(file,new JavaScriptSerializer().Serialize(s)); VoiceId=id;
    }
    public void SaveSettings() {
      SaveTone(Tone);
    }
    // Optional multi-speaker id from piper.json ("Speaker"). Missing/empty/non-numeric keeps the old behaviour.
    public static string SpeakerArgument(object value) {
      int id; string speaker=value==null?null:Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);
      if(string.IsNullOrWhiteSpace(speaker) || !int.TryParse(speaker.Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out id)) return "";
      return " -s "+id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    public static void CheckSpeaker() {
      if(SpeakerArgument(null)!="" || SpeakerArgument("")!="" || SpeakerArgument("abc")!="" || SpeakerArgument("-1")!="") throw new Exception("PiperVoice: empty/invalid speaker must add no argument");
      if(SpeakerArgument("1")!=" -s 1" || SpeakerArgument(" 3 ")!=" -s 3" || SpeakerArgument(2)!=" -s 2") throw new Exception("PiperVoice: speaker argument");
      var s=new JavaScriptSerializer().Deserialize<Settings>("{\"Voice\":\"x\",\"Speaker\":1,\"Tone\":\"Grump\"}");
      if(SpeakerArgument(s.Speaker)!=" -s 1" || (VoiceTone)Enum.Parse(typeof(VoiceTone),s.Tone,true)!=VoiceTone.Grump) throw new Exception("PiperVoice: settings round-trip");
    }
    static string Quote(string text) { return "\""+text.Replace("\"", "\\\"")+"\""; }
    public static byte[] ApplyPitchShift(byte[] wav, double pitch) {
      if(wav==null || wav.Length<36 || Math.Abs(pitch-1.0)<0.01) return wav;
      try {
        int sampleRate=BitConverter.ToInt32(wav,24);
        short channels=BitConverter.ToInt16(wav,22);
        short bits=BitConverter.ToInt16(wav,34);
        int newRate=(int)(sampleRate*pitch);
        int newByteRate=newRate*channels*(bits/8);
        byte[] rateBytes=BitConverter.GetBytes(newRate);
        byte[] byteRateBytes=BitConverter.GetBytes(newByteRate);
        Array.Copy(rateBytes,0,wav,24,4);
        Array.Copy(byteRateBytes,0,wav,28,4);
      } catch { }
      return wav;
    }
    public async Task Speak(string text) {
      Stop(); if(disposed) return;
      var current=new CancellationTokenSource(); request=current;
      try {
        byte[] wav=await Task.Run(()=>Synthesize(text,current.Token));
        if(disposed || current.IsCancellationRequested || request!=current) return;
        audio=new MemoryStream(wav,false); reader=new WaveFileReader(audio);
        output=new WaveOutEvent(); output.Init(reader); output.Volume=0.75f; output.Play();
      } catch(Exception) {
        // Killing the Piper process can surface as a pipe error or an exit error.
        // A requested stop is a normal interruption, not a broken voice.
        if(!current.IsCancellationRequested) throw;
      } finally { if(request==current) request=null; current.Dispose(); }
    }
    byte[] Synthesize(string text,CancellationToken token) {
      string path=Path.Combine(Path.GetTempPath(),"corbie-voice-"+Guid.NewGuid().ToString("N")+".wav");
      try {
        double lengthScale=0.94;
        double noiseScale=0.667;
        double pitch=1.0;

        switch(Tone) {
          case VoiceTone.Bird:
            pitch=1.08; lengthScale=1.05; noiseScale=0.72; break;
          case VoiceTone.Chirpy:
            pitch=1.16; lengthScale=1.12; noiseScale=0.70; break;
          case VoiceTone.Croak:
            pitch=0.92; lengthScale=0.90; noiseScale=0.78; break;
          case VoiceTone.Grump:
            // Tuned for en_GB-semaine-medium speaker 1 (spike): ~98 Hz natural -> ~115-125 Hz median.
            pitch=1.25; lengthScale=1.12; noiseScale=0.80; break;
          case VoiceTone.Normal:
          default:
            pitch=1.0; lengthScale=0.94; noiseScale=0.667; break;
        }

        var info=new ProcessStartInfo(settings.Python,"-m piper -m "+Quote(settings.Model)+" -f "+Quote(path)+" --length-scale "+lengthScale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" --noise-scale "+noiseScale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+SpeakerArgument(settings.Speaker)) {
          UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
          StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
        };
        info.EnvironmentVariables["PYTHONUTF8"]="1";
        using(var process=Process.Start(info)) {
          var errors=process.StandardError.ReadToEndAsync(); var stdout=process.StandardOutput.ReadToEndAsync();
          using(token.Register(()=> { try { if(!process.HasExited) process.Kill(); } catch(InvalidOperationException) { } })) {
            using(var input=new StreamWriter(process.StandardInput.BaseStream,new UTF8Encoding(false))) input.WriteLine(text);
            if(!process.WaitForExit(60000)) { process.Kill(); throw new TimeoutException("Piper took too long."); }
            token.ThrowIfCancellationRequested();
            Task.WaitAll(errors,stdout);
            if(process.ExitCode!=0) throw new InvalidOperationException("Piper could not generate speech. Check the configured model and installation.");
          }
        }
        byte[] wav=File.ReadAllBytes(path);
        return ApplyPitchShift(wav,pitch);
      } finally { try { File.Delete(path); } catch(IOException) { } }
    }
    public void Stop() {
      if(request!=null) { request.Cancel(); request=null; }
      if(output!=null) { output.Stop(); output.Dispose(); output=null; }
      if(reader!=null) { reader.Dispose(); reader=null; }
      if(audio!=null) { audio.Dispose(); audio=null; }
    }
    public static void Probe(string path) {
      using(var voice=new PiperVoice()) {
        byte[] wav=voice.Synthesize("Oh, a treat. Compensation for my working conditions.",CancellationToken.None);
        using(var stream=new MemoryStream(wav)) using(var reader=new WaveFileReader(stream)) {
          if(reader.TotalTime.TotalSeconds<0.5) throw new Exception("Piper produced no speech.");
        }
        File.WriteAllBytes(path,wav);
      }
    }
    public void Dispose() { disposed=true; Stop(); }
  }
}
