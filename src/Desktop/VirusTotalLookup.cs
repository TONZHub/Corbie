using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class VirusTotalReport {
    public string Sha256;
    public bool Known;
    public bool HasStats;
    public int Malicious,Suspicious,Harmless,Undetected;
    public DateTime? AnalyzedUtc;
    public string Url { get { return "https://www.virustotal.com/gui/file/"+Sha256; } }
    public string Summary {
      get {
        if(!Known) return "VirusTotal has no report for this hash. The file was not uploaded; its safety is unknown.";
        if(!HasStats) return "VirusTotal knows this hash but has no analysis counts to show. Check its report for details.";
        string counts=Malicious+" malicious, "+Suspicious+" suspicious, "+Harmless+" harmless, "+Undetected+" undetected";
        string date=AnalyzedUtc.HasValue?" · analyzed "+AnalyzedUtc.Value.ToLocalTime().ToString("g"):"";
        if(Malicious+Suspicious+Harmless+Undetected==0) return "VirusTotal has a record for this hash, but no engine results. Its safety is unknown.";
        if(Malicious>0 || Suspicious>0) return "VirusTotal flagged this known hash: "+counts+date+". Review the full report before deciding what to do.";
        return "No engines flagged this known hash in the latest VirusTotal report ("+counts+")"+date+". This does not prove the file is safe.";
      }
    }
  }
  public sealed class VirusTotalLookup {
    readonly string settingsPath;
    readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=8000000 };
    string apiKey;
    DateTime lastLookupUtc=DateTime.MinValue;
    public bool Configured { get { return !string.IsNullOrEmpty(apiKey); } }
    public VirusTotalLookup(string path=null) {
      settingsPath=path??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","virustotal.key");
      if(File.Exists(settingsPath)) {
        try {
          byte[] protectedKey=Convert.FromBase64String(File.ReadAllText(settingsPath).Trim());
          apiKey=Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedKey,null,DataProtectionScope.CurrentUser));
        } catch { apiKey=null; }
      }
    }
    public void SaveKey(string value) {
      value=(value??"").Trim();
      if(value.Length<20 || value.Length>512 || value.IndexOfAny(new [] { '\r','\n',' ','\t' })>=0)
        throw new ArgumentException("Paste your VirusTotal API key from your account. Do not paste it into chat.");
      byte[] protectedKey=ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser);
      string folder=Path.GetDirectoryName(settingsPath);Directory.CreateDirectory(folder);
      string temp=settingsPath+".tmp";
      try {
        File.WriteAllText(temp,Convert.ToBase64String(protectedKey),Encoding.ASCII);
        if(File.Exists(settingsPath)) File.Replace(temp,settingsPath,null); else File.Move(temp,settingsPath);
        apiKey=value;
      } finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public void ForgetKey() {
      if(File.Exists(settingsPath)) File.Delete(settingsPath);
      apiKey=null;
    }
    public async Task<VirusTotalReport> CheckFile(string path,CancellationToken token) {
      if(!Configured) throw new InvalidOperationException("Save a VirusTotal API key first.");
      if(string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("The selected file was not found.",path);
      if(DateTime.UtcNow-lastLookupUtc<TimeSpan.FromSeconds(15)) throw new InvalidOperationException("Wait 15 seconds before another VirusTotal lookup. Public keys have a rate limit.");
      string hash=await Task.Run(()=>HashFile(path,token),token).ConfigureAwait(false);
      token.ThrowIfCancellationRequested();
      lastLookupUtc=DateTime.UtcNow;
      using(var client=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,UseProxy=true }) { Timeout=TimeSpan.FromSeconds(35) })
      using(var request=new HttpRequestMessage(HttpMethod.Get,"https://www.virustotal.com/api/v3/files/"+hash)) {
        request.Headers.TryAddWithoutValidation("x-apikey",apiKey);
        using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false)) {
          if(response.StatusCode==HttpStatusCode.NotFound) return new VirusTotalReport { Sha256=hash,Known=false };
          if(response.StatusCode==HttpStatusCode.Unauthorized || response.StatusCode==HttpStatusCode.Forbidden)
            throw new InvalidOperationException("VirusTotal rejected the API key. Check it in your VirusTotal account.");
          if((int)response.StatusCode==429) throw new InvalidOperationException("VirusTotal's rate limit was reached. Try again later.");
          if(!response.IsSuccessStatusCode) throw new InvalidOperationException("VirusTotal lookup failed (HTTP "+(int)response.StatusCode+").");
          if(response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value>8000000)
            throw new InvalidOperationException("VirusTotal returned an unexpectedly large report.");
          string body=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
          token.ThrowIfCancellationRequested();
          if(body.Length>8000000) throw new InvalidOperationException("VirusTotal returned an unexpectedly large report.");
          return ParseReport(hash,body);
        }
      }
    }
    static string HashFile(string path,CancellationToken token) {
      var before=new FileInfo(path);
      long length=before.Length;DateTime written=before.LastWriteTimeUtc;
      using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920))
      using(var sha=SHA256.Create()) {
        byte[] buffer=new byte[81920];int count;
        while((count=stream.Read(buffer,0,buffer.Length))>0) {
          token.ThrowIfCancellationRequested();
          sha.TransformBlock(buffer,0,count,buffer,0);
        }
        sha.TransformFinalBlock(new byte[0],0,0);
        before.Refresh();
        if(before.Length!=length || before.LastWriteTimeUtc!=written) throw new IOException("The file changed while hashing. Try again.");
        return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
      }
    }
    VirusTotalReport ParseReport(string hash,string body) {
      var root=json.Deserialize<Dictionary<string,object>>(body);
      var data=GetObject(root,"data");var attributes=GetObject(data,"attributes");
      if(attributes==null) throw new InvalidOperationException("VirusTotal returned a report without file attributes.");
      var report=new VirusTotalReport { Sha256=hash,Known=true };
      var stats=GetObject(attributes,"last_analysis_stats");
      if(stats!=null) {
        report.HasStats=true;
        report.Malicious=Count(stats,"malicious");report.Suspicious=Count(stats,"suspicious");
        report.Harmless=Count(stats,"harmless");report.Undetected=Count(stats,"undetected");
      }
      object date;
      if(attributes.TryGetValue("last_analysis_date",out date) && date!=null)
        try { report.AnalyzedUtc=DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(date)).UtcDateTime; } catch { }
      return report;
    }
    static Dictionary<string,object> GetObject(Dictionary<string,object> source,string key) {
      object value;return source!=null && source.TryGetValue(key,out value)?value as Dictionary<string,object>:null;
    }
    static int Count(Dictionary<string,object> source,string key) {
      object value;
      if(!source.TryGetValue(key,out value) || value==null) return 0;
      try { return Math.Max(0,Convert.ToInt32(value)); } catch { return 0; }
    }
    public static void Check() {
      string path=Path.Combine(Path.GetTempPath(),"corbie-vt-check-"+Guid.NewGuid().ToString("N"));
      string keyPath=path+".key";
      try {
        File.WriteAllText(path,"abc",Encoding.ASCII);
        if(HashFile(path,CancellationToken.None)!="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")
          throw new Exception("VirusTotal file hashing failed.");
        var lookup=new VirusTotalLookup(keyPath);
        lookup.SaveKey(new string('a',64));
        if(File.ReadAllText(keyPath).Contains(new string('a',64)) || !new VirusTotalLookup(keyPath).Configured)
          throw new Exception("VirusTotal key protection failed.");
        var report=lookup.ParseReport(new string('b',64),"{\"data\":{\"attributes\":{\"last_analysis_stats\":{\"malicious\":2,\"suspicious\":1,\"harmless\":5,\"undetected\":9}}}}");
        if(report.Malicious!=2 || report.Suspicious!=1 || report.Summary.IndexOf("flagged",StringComparison.Ordinal)<0)
          throw new Exception("VirusTotal report parsing failed.");
      } finally {
        if(File.Exists(path)) File.Delete(path);
        if(File.Exists(keyPath)) File.Delete(keyPath);
      }
    }
  }
}
