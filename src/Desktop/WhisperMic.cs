using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using NAudio.Wave;

namespace CorbiePet {
  // Records a single sentence with a small local voice-activity detector, then
  // transcribes it using whisper.cpp. The temporary WAV is deleted after use.
  public static class WhisperMic {
    sealed class Settings { public string Cli { get; set; } public string Model { get; set; } public string Prompt { get; set; } }
    const string DefaultPrompt="Corbie, trinket, raven, octopus, Ollama, Piper, Blackboard, Spotify, Libre Hardware Monitor.";
    static readonly string Base=AppDomain.CurrentDomain.BaseDirectory;
    static Settings ReadSettings() {
      string file=Path.Combine(Base,"whisper.json");
      if(!File.Exists(file)) return null;
      try { return new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(file)); }
      catch { return null; }
    }
    static string Resolve(string path) {
      if(string.IsNullOrWhiteSpace(path)) return null;
      return Path.IsPathRooted(path)?path:Path.GetFullPath(Path.Combine(Base,path));
    }
    public static bool Configured {
      get {
        var s=ReadSettings();
        return s!=null && File.Exists(Resolve(s.Cli)) && File.Exists(Resolve(s.Model));
      }
    }
    public static string EngineName {
      get {
        if(!Configured) return "Windows speech fallback";
        var s=ReadSettings();
        string name=s==null?null:Path.GetFileNameWithoutExtension(s.Model??"");
        if(!string.IsNullOrEmpty(name) && name.StartsWith("ggml-",StringComparison.OrdinalIgnoreCase)) name=name.Substring(5);
        return "local Whisper"+(string.IsNullOrEmpty(name)?"":" ("+name+")");
      }
    }
    public static async Task<string> Listen(CancellationToken token,bool handsFree,Action<MicListener.Activity> report) {
      if(!Configured) throw new InvalidOperationException("Local Whisper is not configured. Run Setup-Whisper.ps1.");
      string wav=Path.Combine(Path.GetTempPath(),"corbie-mic-"+Guid.NewGuid().ToString("N")+".wav");
      string output=Path.Combine(Path.GetTempPath(),"corbie-whisper-"+Guid.NewGuid().ToString("N"));
      try {
        bool speech=await RecordSentence(wav,handsFree,token,report).ConfigureAwait(false);
        if(!speech) return null;
        if(report!=null) report(new MicListener.Activity { Level=-1,Message="transcribing locally" });
        return await Transcribe(wav,output,token).ConfigureAwait(false);
      } finally {
        TryDelete(wav); TryDelete(output+".txt"); TryDelete(output+".json");
      }
    }
    // Lesson recordings are kept on disk; only Whisper's temporary output is removed.
    public static async Task<string> TranscribeFile(string wav,string vocabulary,CancellationToken token,Action<int,int> progress=null) {
      if(!Configured) throw new InvalidOperationException("Local Whisper is not configured. Run Setup-Whisper.ps1.");
      if(string.IsNullOrWhiteSpace(wav) || !File.Exists(wav)) throw new FileNotFoundException("Lesson recording was not found.",wav);
      const int chunkSeconds=180;
      var transcript=new StringBuilder();
      using(var reader=new WaveFileReader(wav)) {
        if(reader.WaveFormat.Encoding!=WaveFormatEncoding.Pcm || reader.WaveFormat.Channels!=1 || reader.WaveFormat.SampleRate!=16000 || reader.WaveFormat.BitsPerSample!=16)
          throw new InvalidOperationException("Lesson recording must be a 16 kHz mono WAV.");
        int chunkBytes=reader.WaveFormat.AverageBytesPerSecond*chunkSeconds;
        int total=(int)Math.Max(1,(reader.Length+chunkBytes-1)/chunkBytes);
        byte[] buffer=new byte[81920];
        for(int index=0;index<total;index++) {
          token.ThrowIfCancellationRequested();
          string chunk=Path.Combine(Path.GetTempPath(),"corbie-lesson-"+Guid.NewGuid().ToString("N")+".wav");
          string output=Path.Combine(Path.GetTempPath(),"corbie-whisper-"+Guid.NewGuid().ToString("N"));
          try {
            using(var writer=new WaveFileWriter(chunk,reader.WaveFormat)) {
              int remaining=chunkBytes;
              while(remaining>0) {
                int count=reader.Read(buffer,0,Math.Min(buffer.Length,remaining));
                if(count<=0) break;
                writer.Write(buffer,0,count);remaining-=count;
              }
            }
            if(progress!=null) progress(index+1,total);
            string text=await Transcribe(chunk,output,token,vocabulary).ConfigureAwait(false);
            if(!string.IsNullOrWhiteSpace(text)) {
              if(transcript.Length>0) transcript.AppendLine().AppendLine();
              transcript.Append('[').Append(TimeSpan.FromSeconds(index*chunkSeconds).ToString(@"hh\:mm\:ss")).Append("] ").Append(text);
            }
          } finally {
            TryDelete(chunk);TryDelete(output+".txt");TryDelete(output+".json");
          }
        }
      }
      return transcript.ToString();
    }
    static async Task<bool> RecordSentence(string path,bool handsFree,CancellationToken token,Action<MicListener.Activity> report) {
      var format=new WaveFormat(16000,16,1);
      var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      var started=Stopwatch.StartNew();
      var lastVoice=Stopwatch.StartNew();
      bool heard=false,stopping=false;
      double noise=0.003;
      int peak=0;
      object gate=new object();
      using(var input=new WaveInEvent { DeviceNumber=0,WaveFormat=format,BufferMilliseconds=50,NumberOfBuffers=3 })
      using(var writer=new WaveFileWriter(path,format)) {
        input.DataAvailable+=(s,e)=> {
          lock(gate) {
            if(stopping) return;
            writer.Write(e.Buffer,0,e.BytesRecorded);
            double sum=0; int count=e.BytesRecorded/2;
            for(int i=0;i+1<e.BytesRecorded;i+=2) { short sample=(short)(e.Buffer[i]|(e.Buffer[i+1]<<8)); double v=sample/32768.0; sum+=v*v; }
            double rms=count>0?Math.Sqrt(sum/count):0;
            int level=Math.Min(100,(int)Math.Round(rms*350));
            peak=Math.Max(peak,level);
            if(report!=null) report(new MicListener.Activity { Level=level });
            if(!heard && started.ElapsedMilliseconds<600) noise=noise*0.88+rms*0.12;
            double threshold=Math.Max(0.007,noise*2.8);
            if(rms>=threshold) {
              if(!heard && started.ElapsedMilliseconds>180) {
                heard=true;
                if(report!=null) report(new MicListener.Activity { Level=-1,Message="speech detected" });
              }
              lastVoice.Restart();
            }
            bool initialTimeout=!heard && started.Elapsed>TimeSpan.FromSeconds(handsFree?15:10);
            bool phraseDone=heard && lastVoice.Elapsed>TimeSpan.FromMilliseconds(950) && started.Elapsed>TimeSpan.FromMilliseconds(700);
            bool tooLong=started.Elapsed>TimeSpan.FromSeconds(25);
            if(initialTimeout || phraseDone || tooLong) { stopping=true; try { input.StopRecording(); } catch { } }
          }
        };
        input.RecordingStopped+=(s,e)=> {
          if(e.Exception!=null) done.TrySetException(e.Exception);
          else done.TrySetResult(heard);
        };
        using(token.Register(()=> { lock(gate) { if(stopping) return; stopping=true; } try { input.StopRecording(); } catch { done.TrySetCanceled(); } })) {
          token.ThrowIfCancellationRequested();
          input.StartRecording();
          bool result=await done.Task.ConfigureAwait(false);
          token.ThrowIfCancellationRequested();
          if(!result && report!=null) report(new MicListener.Activity { Level=-1,Message=peak<2?"no sound from selected microphone":"audio heard, but no sentence detected" });
          return result;
        }
      }
    }
    static async Task<string> Transcribe(string wav,string output,CancellationToken token,string vocabulary=null) {
      var s=ReadSettings(); string cli=Resolve(s.Cli),model=Resolve(s.Model);
      string prompt=string.IsNullOrWhiteSpace(s.Prompt)?DefaultPrompt:s.Prompt.Trim();
      if(!string.IsNullOrWhiteSpace(vocabulary)) prompt+=" "+vocabulary.Trim().Substring(0,Math.Min(500,vocabulary.Trim().Length));
      string args="-m "+Quote(model)+" -f "+Quote(wav)+" -l en -t 8 -nt -np --prompt "+Quote(prompt)+" -otxt -of "+Quote(output);
      var info=new ProcessStartInfo(cli,args) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(cli) };
      using(var process=new Process { StartInfo=info }) {
        process.Start();
        string stdoutTaskResult=null,stderrTaskResult=null;
        var stdout=Task.Run(()=>process.StandardOutput.ReadToEnd());
        var stderr=Task.Run(()=>process.StandardError.ReadToEnd());
        using(token.Register(()=> { try { if(!process.HasExited) process.Kill(); } catch { } }))
          await Task.Run(()=>process.WaitForExit(),token).ConfigureAwait(false);
        stdoutTaskResult=await stdout.ConfigureAwait(false); stderrTaskResult=await stderr.ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if(process.ExitCode!=0) throw new InvalidOperationException("Whisper failed: "+CleanError(stderrTaskResult));
        string text=File.Exists(output+".txt")?File.ReadAllText(output+".txt"):stdoutTaskResult;
        text=CleanTranscript(text);
        return string.IsNullOrWhiteSpace(text)?null:text;
      }
    }
    static string CleanTranscript(string text) {
      if(string.IsNullOrWhiteSpace(text)) return null;
      var lines=text.Replace("\r","").Split('\n'); var result=new StringBuilder();
      foreach(string raw in lines) {
        string line=raw.Trim();
        if(line.Length==0 || (line.StartsWith("[") && line.EndsWith("]")) || (line.StartsWith("(") && line.EndsWith(")"))) continue;
        if(result.Length>0) result.Append(' '); result.Append(line);
      }
      string cleaned=result.ToString().Trim();
      if(cleaned.Length==0) return null;
      string simple=cleaned.Trim(' ','.',',','!','?','"','\'').ToLowerInvariant();
      if(simple=="you" || simple=="thank you" || simple=="thanks for watching" || simple=="bye") return null;
      return cleaned;
    }
    static string CleanError(string value) {
      if(string.IsNullOrWhiteSpace(value)) return "unknown local transcription error";
      value=value.Replace("\r"," ").Replace("\n"," ").Trim(); return value.Length>180?value.Substring(0,180):value;
    }
    static string Quote(string value) { return "\""+value.Replace("\"","\\\"")+"\""; }
    static void TryDelete(string path) { try { if(File.Exists(path)) File.Delete(path); } catch { } }
    public static void Check() {
      if(CleanTranscript("[BLANK_AUDIO]\n")!=null) throw new Exception("Whisper blank-audio cleanup failed.");
      if(CleanTranscript(" you\n")!=null) throw new Exception("Whisper silence hallucination cleanup failed.");
      if(CleanTranscript("Hello Corbie.")!="Hello Corbie.") throw new Exception("Whisper transcript cleanup changed real speech.");
      if(string.IsNullOrWhiteSpace(DefaultPrompt) || DefaultPrompt.IndexOf("trinket",StringComparison.OrdinalIgnoreCase)<0)
        throw new Exception("Whisper vocabulary prompt is missing Corbie terms.");
      var s=ReadSettings();
      if(s!=null && (!File.Exists(Resolve(s.Cli)) || !File.Exists(Resolve(s.Model))))
        throw new Exception("whisper.json points to a missing runtime or model.");
    }
  }
}
