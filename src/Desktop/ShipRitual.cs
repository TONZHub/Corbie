using System;
using System.IO;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class NestEgg {
    public int Version { get; set; }
    public string Build { get; set; }
    public DateTime BuiltUtc { get; set; }
    public bool SmokeOk { get; set; }
    public bool Celebrated { get; set; }
  }

  // Local victory stamp after a successful build.ps1 / smoke-test.
  // Lives only under %LOCALAPPDATA%\Corbie; never reads source trees or CI logs.
  public static class ShipRitual {
    public static string DefaultPath {
      get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","nest-egg.json"); }
    }

    public static void Stamp(string path, string build, bool smokeOk) {
      if(string.IsNullOrEmpty(path)) return;
      var egg=new NestEgg {
        Version=1,
        Build=string.IsNullOrEmpty(build)?"unknown":build,
        BuiltUtc=DateTime.UtcNow,
        SmokeOk=smokeOk,
        Celebrated=false
      };
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      string temp=path+".tmp";
      File.WriteAllText(temp, new JavaScriptSerializer().Serialize(egg));
      if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    }

    public static NestEgg PeekFresh(string path, double maxAgeHours) {
      try {
        if(string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        if(new FileInfo(path).Length>64*1024) return null;
        string raw=File.ReadAllText(path);
        var egg=new JavaScriptSerializer().Deserialize<NestEgg>(raw);
        if(egg==null || egg.Celebrated) return null;
        if(egg.BuiltUtc==DateTime.MinValue) {
          // PowerShell ConvertTo-Json writes ISO-8601; JavaScriptSerializer may leave MinValue.
          int key=raw.IndexOf("BuiltUtc", StringComparison.OrdinalIgnoreCase);
          if(key>=0) {
            int colon=raw.IndexOf(':', key);
            int q1=colon>=0?raw.IndexOf('"', colon+1):-1;
            int q2=q1>=0?raw.IndexOf('"', q1+1):-1;
            if(q1>=0 && q2>q1) {
              DateTime parsed;
              if(DateTime.TryParse(raw.Substring(q1+1, q2-q1-1), null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed))
                egg.BuiltUtc=parsed.ToUniversalTime();
            }
          }
        }
        if(egg.BuiltUtc==DateTime.MinValue) return null;
        double hours=(DateTime.UtcNow-egg.BuiltUtc).TotalHours;
        if(hours<0 || hours>maxAgeHours) return null;
        return egg;
      } catch { return null; }
    }

    public static void MarkCelebrated(string path) {
      try {
        if(string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var egg=new JavaScriptSerializer().Deserialize<NestEgg>(File.ReadAllText(path));
        if(egg==null) return;
        egg.Celebrated=true;
        string temp=path+".tmp";
        File.WriteAllText(temp, new JavaScriptSerializer().Serialize(egg));
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
      } catch { }
    }

    public static string VictoryLine(NestEgg egg, bool octopus) {
      if(egg==null) return octopus?"Ship complete. Tentacles salute the nest egg.":"Ship complete. Nest egg stamped.";
      string build=string.IsNullOrEmpty(egg.Build)?"build":egg.Build;
      if(egg.SmokeOk) {
        return octopus
          ? "Ship ritual! "+build+" smoked clean. Nest egg stamped. Eight arms of triumph."
          : "Ship ritual! "+build+" smoked clean. Nest egg stamped. Victory hop engaged.";
      }
      return octopus
        ? "Ship ritual! "+build+" compiled. Nest egg warm. Smoke still pending, but the arms celebrate anyway."
        : "Ship ritual! "+build+" compiled. Nest egg warm. Victory hop now; smoke can wait.";
    }

    public static void Check() {
      string folder=Path.Combine(Path.GetTempPath(),"Corbie-ship-"+Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(folder);
      try {
        string file=Path.Combine(folder,"nest-egg.json");
        Stamp(file,"v0.47",true);
        var egg=PeekFresh(file,4);
        if(egg==null || egg.Build!="v0.47" || !egg.SmokeOk || egg.Celebrated) throw new Exception("ShipRitual: peek failed");
        string line=VictoryLine(egg,false);
        if(line.IndexOf("Victory", StringComparison.OrdinalIgnoreCase)<0) throw new Exception("ShipRitual: victory line missing");
        MarkCelebrated(file);
        if(PeekFresh(file,4)!=null) throw new Exception("ShipRitual: celebrated egg resurfaced");
        Stamp(file,"old",false);
        var raw=new JavaScriptSerializer().Deserialize<NestEgg>(File.ReadAllText(file));
        raw.BuiltUtc=DateTime.UtcNow.AddHours(-10);
        File.WriteAllText(file, new JavaScriptSerializer().Serialize(raw));
        if(PeekFresh(file,4)!=null) throw new Exception("ShipRitual: stale egg accepted");
      } finally {
        foreach(string path in Directory.GetFiles(folder)) File.Delete(path);
        Directory.Delete(folder);
      }
    }
  }
}
