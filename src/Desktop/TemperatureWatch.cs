using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class TemperatureReading {
    public string Name, Id;
    public double Celsius;
  }
  public sealed class TemperatureWatch : IDisposable {
    readonly HttpClient client=new HttpClient(new HttpClientHandler { UseProxy=false,AllowAutoRedirect=false });
    public TemperatureWatch() { client.Timeout=TimeSpan.FromSeconds(2); client.MaxResponseContentBufferSize=2097152; }
    public async Task<TemperatureReading> Read(CancellationToken token) {
      using(var response=await client.GetAsync("http://127.0.0.1:8085/data.json",token).ConfigureAwait(false)) {
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
      }
    }
    public static TemperatureReading Parse(string json) {
      var root=new JavaScriptSerializer().DeserializeObject(json);
      TemperatureReading best=null; int rank=-1;
      Visit(root,ref best,ref rank);
      if(best==null) throw new InvalidOperationException("CPU package temperature unavailable.");
      return best;
    }
    static string Text(Dictionary<string,object> node,string key) { object value; return node.TryGetValue(key,out value)?Convert.ToString(value,CultureInfo.InvariantCulture):""; }
    static void Visit(object value,ref TemperatureReading best,ref int rank) {
      var node=value as Dictionary<string,object>; if(node==null) return;
      string id=Text(node,"SensorId"), name=Text(node,"Text"), lower=name.ToLowerInvariant();
      if((id.StartsWith("/amdcpu/") || id.StartsWith("/intelcpu/")) && Text(node,"Type")=="Temperature") {
        int score=lower.Contains("tctl/tdie")?3:lower.Contains("package")?2:lower=="core (tdie)"?1:0;
        object raw; double c;
        if(score>0 && node.TryGetValue(node.ContainsKey("RawValue")?"RawValue":"Value",out raw) && TryCelsius(raw,out c) && (score>rank || (score==rank && best!=null && c>best.Celsius))) {
          rank=score; best=new TemperatureReading { Name=name,Id=id,Celsius=c };
        }
      }
      object children;
      if(node.TryGetValue("Children",out children) && children is IEnumerable)
        foreach(object child in (IEnumerable)children) Visit(child,ref best,ref rank);
    }
    static bool TryCelsius(object raw,out double c) {
      c=0; if(raw==null) return false;
      string text=raw as string;
      if(text!=null) {
        text=text.Trim(); bool fahrenheit=text.EndsWith("°F",StringComparison.OrdinalIgnoreCase);
        if(!fahrenheit && !text.EndsWith("°C",StringComparison.OrdinalIgnoreCase)) return false;
        if(!double.TryParse(text.Substring(0,text.Length-2).Trim().Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out c)) return false;
        if(fahrenheit) c=(c-32)*5/9;
      } else { try { c=Convert.ToDouble(raw,CultureInfo.InvariantCulture); } catch { return false; } }
      return !double.IsNaN(c) && !double.IsInfinity(c) && c>0 && c<=150;
    }
    public void Dispose() { client.Dispose(); }
    public static void Check() {
      const string prefix="{\"Children\":[{\"SensorId\":\"/gpu-nvidia/0/temperature/0\",\"Text\":\"CPU Package\",\"Type\":\"Temperature\",\"RawValue\":110},{\"SensorId\":\"/amdcpu/0/temperature/2\",\"Text\":\"Core (Tctl/Tdie)\",\"Type\":\"Temperature\",\"RawValue\":";
      foreach(string raw in new[]{"78.5","\"78,5 °C\"","\"173.3 °F\""})
        if(Math.Abs(Parse(prefix+raw+"}]}").Celsius-78.5)>0.01) throw new Exception("Temperature parsing failed");
      foreach(string raw in new[]{"null","\"NaN °C\"","\"0 °C\"","\"unavailable\""}) {
        bool rejected=false; try { Parse(prefix+raw+"}]}"); } catch(InvalidOperationException) { rejected=true; }
        if(!rejected) throw new Exception("Missing temperature accepted");
      }
      TemperatureTrend.Check();
    }
  }
  public sealed class TemperatureTrend {
    double hot,cool,last=double.NaN,lastNotice=double.NegativeInfinity;
    bool warned;
    public void BreakContinuity() { hot=cool=0; last=double.NaN; }
    public string Observe(double c,double now,bool notify) {
      return Observe(c,now,notify,null);
    }
    public string Observe(double c,double now,bool notify,CulpritInfo culprit) {
      double dt=now-last; last=now;
      if(double.IsNaN(dt) || dt<=0 || dt>6 || double.IsNaN(c)) { hot=cool=0; return null; }
      hot=c>=90?hot+dt:0; cool=c<=75?cool+dt:0;
      if(!warned && hot>=30 && now-lastNotice>=600 && notify) {
        warned=true; lastNotice=now;
        if(culprit!=null) {
          if(culprit.IsPerched) {
            return "I like warm perches, but your CPU is "+Math.Round(c)+"°C and "+culprit.Name+" is roasting my claws right on this window! Ramp up your fans!";
          } else {
            return "I like warm perches, but your CPU is "+Math.Round(c)+"°C! It's not this window—'"+culprit.Name+"' is runaway in the background cooking my nest!";
          }
        }
        return "I like warm perches, but your CPU is "+Math.Round(c)+"°C and cooking above 90°C for 30 seconds. Ramp up your fans before you melt my feathers.";
      }
      if(warned && cool>=120 && notify) {
        warned=false;
        return "Crisis averted. CPU has stayed below 75°C for two minutes. You can switch to a quieter fan mode unless you enjoy the sound of an airport runway.";
      }
      return null;
    }
    public static void Check() {
      var t=new TemperatureTrend(); double now=0; t.Observe(95,now,true);
      for(int i=0;i<9;i++) if(t.Observe(95,now+=3,true)!=null) throw new Exception("Heat alert too early");
      if(t.Observe(95,now+=3,true)==null) throw new Exception("Heat alert missing");
      for(int i=0;i<20;i++) if(t.Observe(95,now+=3,true)!=null) throw new Exception("Heat alert repeated");
      for(int i=0;i<39;i++) if(t.Observe(70,now+=3,true)!=null) throw new Exception("Cooling alert too early");
      if(t.Observe(70,now+=3,true)==null) throw new Exception("Cooling alert missing");
      t.BreakContinuity();
      for(int i=0;i<15;i++) if(t.Observe(95,now+=3,true)!=null) throw new Exception("Cooldown lost after disconnect");
      t=new TemperatureTrend(); now=0;
      for(int i=0;i<20;i++) if(t.Observe(95,now+=3,false)!=null) throw new Exception("Quiet ignored");
      t.BreakContinuity(); if(t.Observe(95,now+=3,true)!=null) throw new Exception("Disconnect counted as heat");
      if(t.Observe(95,now+=60,true)!=null) throw new Exception("Sleep counted as heat");

      // Test culprit awareness
      t=new TemperatureTrend(); now=0; t.Observe(95,now,true);
      for(int i=0;i<9;i++) t.Observe(95,now+=3,true);
      var perchedCulprit=new CulpritInfo { Pid=100, Name="Visual Studio", IsPerched=true };
      var perchedNotice=t.Observe(95,now+=3,true,perchedCulprit);
      if(perchedNotice==null || !perchedNotice.Contains("Visual Studio") || !perchedNotice.Contains("roasting my claws")) throw new Exception("Perched temperature culprit failed");

      t=new TemperatureTrend(); now=0; t.Observe(95,now,true);
      for(int i=0;i<9;i++) t.Observe(95,now+=3,true);
      var bgCulprit=new CulpritInfo { Pid=200, Name="Node.js", IsPerched=false };
      var bgNotice=t.Observe(95,now+=3,true,bgCulprit);
      if(bgNotice==null || !bgNotice.Contains("Node.js") || !bgNotice.Contains("cooking my nest")) throw new Exception("Background temperature culprit failed");
    }
  }
}
