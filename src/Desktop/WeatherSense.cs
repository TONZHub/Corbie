using System;
using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace CorbiePet {
  public sealed class WeatherLocation {
    public bool Enabled { get; set; }
    public string Zip { get; set; }
    public string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool Resolved { get; set; }
  }
  public sealed class WeatherReading {
    public double Temperature,Wind,Precipitation,Humidity;
    public int Code;
    public DateTime ObservedUtc,ReceivedUtc;
    public bool Fresh(DateTime now) { return now>=ReceivedUtc && now-ReceivedUtc<TimeSpan.FromMinutes(45) && now>=ObservedUtc.AddMinutes(-5) && now-ObservedUtc<TimeSpan.FromMinutes(90); }
    public WeatherKind Kind { get { return Code>=95?WeatherKind.Storm:(Code>=71 && Code<=77 || Code==85 || Code==86)?WeatherKind.Snow:(Precipitation>0.1 || Code>=51 && Code<=67 || Code>=80 && Code<=82)?WeatherKind.Rain:WeatherKind.Clear; } }
    public string Description { get { return Code>=95?"thunderstorms":Kind==WeatherKind.Snow?"snow":Kind==WeatherKind.Rain?"rain":Code==45 || Code==48?"fog":Code==3?"overcast":Code==1 || Code==2?"partly cloudy":"clear"; } }
    public string Summary { get { return Description+" · "+Math.Round(Temperature*9/5+32)+" °F · wind "+Math.Round(Wind/1.609344)+" mph"; } }
  }
  // Designed weather drive, separate from the imported visual connectome.
  // Outdoor weather modulates a virtual creature; it is not an indoor sensor.
  public sealed class WeatherDrive {
    public double Shelter { get; private set; }
    public double Activity { get; private set; }
    public WeatherDrive() { Activity=1; }
    public double Sensitivity { get { return 1-0.3*Shelter; } }
    public bool Known { get; private set; }
    public void Step(double dt,WeatherReading reading,DateTime now) {
      if(dt<=0 || double.IsNaN(dt) || double.IsInfinity(dt)) return;
      Known=reading!=null && reading.Fresh(now);
      double shelter=0,activity=1;
      if(Known) {
        shelter=Math.Max(reading.Temperature<12?0.7:reading.Temperature>35?0.5:0,
          Math.Max(reading.Wind>=25?0.85:reading.Wind>=15?0.45:0,reading.Kind==WeatherKind.Storm?1:reading.Kind==WeatherKind.Rain || reading.Kind==WeatherKind.Snow?0.8:0));
        activity=shelter>0?1-0.55*shelter:reading.Temperature>=20 && reading.Temperature<=30?1.12:1;
      }
      double ease=1-Math.Exp(-Math.Min(dt,1)/3);
      Shelter+=(shelter-Shelter)*ease; Activity+=(activity-Activity)*ease;
    }
    public string Behavior { get { return !Known?"Weather unknown · neutral behavior":Shelter>0.55?"Sheltering · fewer flights":Shelter>0.15?"A little cautious":Activity>1.05?"Warm and restless":"Comfortable perch weather"; } }
    public static void Check() {
      var now=DateTime.UtcNow; var d=new WeatherDrive();
      var warm=new WeatherReading { Temperature=24,ObservedUtc=now,ReceivedUtc=now };
      for(int i=0;i<200;i++) d.Step(0.1,warm,now);
      if(d.Activity<1.1 || d.Shelter>0.01) throw new Exception("Warm dry weather did not raise activity.");
      var storm=new WeatherReading { Temperature=8,Wind=40,Code=95,ObservedUtc=now,ReceivedUtc=now };
      for(int i=0;i<200;i++) d.Step(0.1,storm,now);
      if(d.Shelter<0.9 || d.Activity>0.5 || d.Sensitivity<0.69) throw new Exception("Weather shelter drive incorrect.");
      foreach(var condition in new[]{new WeatherReading { Temperature=5 },new WeatherReading { Temperature=24,Wind=30 },new WeatherReading { Temperature=24,Code=61 }}) {
        condition.ObservedUtc=condition.ReceivedUtc=now; var isolated=new WeatherDrive();
        for(int i=0;i<200;i++) isolated.Step(0.1,condition,now);
        if(isolated.Shelter<0.65 || isolated.Activity>0.7) throw new Exception("Cold, wind or rain alone did not favor shelter.");
      }
      for(int i=0;i<300;i++) d.Step(0.1,storm,now.AddHours(2));
      if(d.Known || Math.Abs(d.Activity-1)>0.01 || d.Shelter>0.01) throw new Exception("Stale weather did not return to neutral.");
    }
  }
  public static class WeatherFeed {
    sealed class GeoResponse { public GeoPlace[] results { get; set; } }
    sealed class GeoPlace { public string name { get; set; } public string admin1 { get; set; } public string country_code { get; set; } public double latitude { get; set; } public double longitude { get; set; } }
    sealed class CurrentResponse { public Current current { get; set; } }
    sealed class Current { public double? time { get; set; } public double? temperature_2m { get; set; } public double? wind_speed_10m { get; set; } public double? precipitation { get; set; } public double? relative_humidity_2m { get; set; } public int? weather_code { get; set; } }
    public static bool ValidZip(string zip) { return zip!=null && System.Text.RegularExpressions.Regex.IsMatch(zip,"^[0-9]{5}$"); }
    static bool Finite(double n,double min,double max) { return !double.IsNaN(n) && !double.IsInfinity(n) && n>=min && n<=max; }
    static async Task<string> Get(HttpClient client,string url,CancellationToken cancel) {
      using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false)) {
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>65536) throw new InvalidDataException("Weather response too large.");
        using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        using(var data=new MemoryStream()) {
          var buffer=new byte[4096]; int count;
          while((count=await stream.ReadAsync(buffer,0,buffer.Length,cancel).ConfigureAwait(false))>0) { data.Write(buffer,0,count); if(data.Length>65536) throw new InvalidDataException("Weather response too large."); }
          return System.Text.Encoding.UTF8.GetString(data.ToArray());
        }
      }
    }
    public static async Task<WeatherLocation> Resolve(string zip,CancellationToken cancel) {
      if(!ValidZip(zip)) throw new ArgumentException("Use a five-digit US ZIP code.");
      using(var client=new HttpClient { Timeout=TimeSpan.FromSeconds(15) }) {
        string raw=await Get(client,"https://geocoding-api.open-meteo.com/v1/search?name="+zip+"&count=1&countryCode=US&language=en&format=json",cancel).ConfigureAwait(false);
        var geo=new JavaScriptSerializer().Deserialize<GeoResponse>(raw);
        if(geo.results==null || geo.results.Length==0) throw new InvalidDataException("No place found for that ZIP.");
        var p=geo.results[0]; if(p.country_code!="US" || !Finite(p.latitude,-90,90) || !Finite(p.longitude,-180,180)) throw new InvalidDataException("Invalid weather location.");
        return new WeatherLocation { Enabled=true,Resolved=true,Zip=zip,Name=p.name+", "+p.admin1,Latitude=p.latitude,Longitude=p.longitude };
      }
    }
    public static WeatherReading Parse(string raw,DateTime now) {
      var r=new JavaScriptSerializer().Deserialize<CurrentResponse>(raw); var c=r.current;
      if(c==null || !c.time.HasValue || !c.temperature_2m.HasValue || !c.wind_speed_10m.HasValue || !c.precipitation.HasValue || !c.relative_humidity_2m.HasValue || !c.weather_code.HasValue) throw new InvalidDataException("Incomplete current weather.");
      if(!Finite(c.temperature_2m.Value,-90,65) || !Finite(c.wind_speed_10m.Value,0,500) || !Finite(c.precipitation.Value,0,500) || !Finite(c.relative_humidity_2m.Value,0,100) || !Finite(c.time.Value,0,4102444800)) throw new InvalidDataException("Invalid weather readings.");
      int code=c.weather_code.Value;
      if(Array.IndexOf(new[]{0,1,2,3,45,48,51,53,55,56,57,61,63,65,66,67,71,73,75,77,80,81,82,85,86,95,96,99},code)<0) throw new InvalidDataException("Unknown weather code.");
      var reading=new WeatherReading { Temperature=c.temperature_2m.Value,Wind=c.wind_speed_10m.Value,Precipitation=c.precipitation.Value,Humidity=c.relative_humidity_2m.Value,Code=code,ObservedUtc=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(c.time.Value),ReceivedUtc=now };
      if(!reading.Fresh(now)) throw new InvalidDataException("Weather report is stale or future-dated.");
      return reading;
    }
    public static async Task<WeatherReading> Fetch(WeatherLocation location,CancellationToken cancel) {
      using(var client=new HttpClient { Timeout=TimeSpan.FromSeconds(15) }) {
        string url="https://api.open-meteo.com/v1/forecast?latitude="+location.Latitude.ToString("F5",CultureInfo.InvariantCulture)+"&longitude="+location.Longitude.ToString("F5",CultureInfo.InvariantCulture)+"&current=temperature_2m,relative_humidity_2m,precipitation,weather_code,wind_speed_10m&wind_speed_unit=kmh&temperature_unit=celsius&timeformat=unixtime&timezone=GMT&forecast_days=1";
        return Parse(await Get(client,url,cancel).ConfigureAwait(false),DateTime.UtcNow);
      }
    }
    public static void Check() {
      WeatherDrive.Check(); DateTime now=new DateTime(2026,9,27,12,0,0,DateTimeKind.Utc);
      double unix=(now-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
      string json="{\"current\":{\"time\":"+unix.ToString(CultureInfo.InvariantCulture)+",\"temperature_2m\":23,\"wind_speed_10m\":8,\"precipitation\":0.3,\"relative_humidity_2m\":70,\"weather_code\":61}}";
      if(Parse(json,now).Kind!=WeatherKind.Rain) throw new Exception("Rain mapping failed.");
      foreach(string bad in new[]{"{}",json.Replace("23","null"),json.Replace("70","101")}) { bool denied=false; try { Parse(bad,now); } catch { denied=true; } if(!denied) throw new Exception("Invalid weather accepted."); }
      bool stale=false; try { Parse(json,now.AddHours(2)); } catch { stale=true; } if(!stale || ValidZip("bad") || !ValidZip("10001")) throw new Exception("Weather freshness or ZIP validation failed.");
    }
  }
  public partial class Pet {
    readonly WeatherDrive weatherDrive=new WeatherDrive();
    WeatherLocation weatherLocation;
    WeatherReading weatherReading;
    CancellationTokenSource weatherRequest;
    DateTime weatherNextUtc;
    string weatherError;
    TextBlock weatherStatus;
    TextBox weatherZip;
    CheckBox weatherToggle;
    string WeatherPreference { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-weather.json"); } }
    void InitializeWeather() {
      weatherLocation=new WeatherLocation();
      if(!testing) try { if(File.Exists(WeatherPreference)) weatherLocation=new JavaScriptSerializer().Deserialize<WeatherLocation>(File.ReadAllText(WeatherPreference))??new WeatherLocation(); } catch { weatherError="Weather settings could not be read."; }
      if(weatherLocation.Enabled && !WeatherFeed.ValidZip(weatherLocation.Zip) || weatherLocation.Resolved && (double.IsNaN(weatherLocation.Latitude) || Math.Abs(weatherLocation.Latitude)>90 || double.IsNaN(weatherLocation.Longitude) || Math.Abs(weatherLocation.Longitude)>180)) { weatherLocation=new WeatherLocation(); weatherError="Weather location needs setting again."; }
    }
    void SaveWeather() { if(!testing) File.WriteAllText(WeatherPreference,new JavaScriptSerializer().Serialize(weatherLocation)); }
    void CancelWeather() { if(weatherRequest!=null) weatherRequest.Cancel(); }
    void SetWeather(bool enabled) {
      if(refreshingPanel) return;
      try { weatherLocation.Enabled=enabled; SaveWeather(); if(!enabled) { CancelWeather(); weatherReading=null; } weatherNextUtc=DateTime.MinValue; }
      catch(Exception ex) { weatherError="Couldn't save weather setting: "+ex.Message; }
      RefreshWeather();
    }
    async Task RefreshWeatherFeed() {
      if(testing || shuttingDown || !weatherLocation.Enabled || weatherRequest!=null) return;
      var cancel=new CancellationTokenSource(); cancel.CancelAfter(TimeSpan.FromSeconds(25)); weatherRequest=cancel;
      weatherNextUtc=DateTime.UtcNow.AddMinutes(20);
      try {
        var location=weatherLocation;
        if(!location.Resolved) { location=await WeatherFeed.Resolve(location.Zip,cancel.Token); if(cancel.IsCancellationRequested || shuttingDown) return; weatherLocation=location; SaveWeather(); }
        var reading=await WeatherFeed.Fetch(location,cancel.Token);
        if(!cancel.IsCancellationRequested && !shuttingDown && weatherLocation.Enabled) { weatherReading=reading; weatherError=null; }
      } catch(OperationCanceledException) { if(!shuttingDown && weatherLocation.Enabled) weatherError="Weather refresh cancelled or timed out."; }
      catch(Exception) { if(!cancel.IsCancellationRequested) { weatherError="Weather unavailable. Check your internet connection; I'll retry in five minutes."; weatherNextUtc=DateTime.UtcNow.AddMinutes(5); } }
      finally { if(weatherRequest==cancel) weatherRequest=null; cancel.Dispose(); if(!shuttingDown) RefreshWeather(); }
    }
    void StepWeather(double dt) {
      if(!testing && weatherLocation.Enabled && DateTime.UtcNow>=weatherNextUtc && weatherRequest==null) { var work=RefreshWeatherFeed(); }
      weatherDrive.Step(dt,weatherLocation.Enabled?weatherReading:null,DateTime.UtcNow);
      if(!paused && weatherDrive.Known && nervesEnabled && weatherDrive.Shelter>0.6 && birdFlight.Active) CancelBirdFlight(true);
    }
    string WeatherContext() {
      return weatherLocation.Enabled && weatherReading!=null && weatherReading.Fresh(DateTime.UtcNow)
        ? "Outdoor current model weather for "+weatherLocation.Name+": "+weatherReading.Summary+"; humidity "+Math.Round(weatherReading.Humidity)+"%. Source: Open-Meteo. This is not indoor temperature. Mention only when relevant, not in every response."
        : "No fresh outdoor weather available. Do not infer weather from the clock or season.";
    }
    string WeatherReport() {
      return weatherLocation.Enabled && weatherReading!=null && weatherReading.Fresh(DateTime.UtcNow)
        ? weatherLocation.Name+": "+weatherReading.Summary+". "+(weatherDrive.Shelter>0.55?"I'll be staying on my perch, cheers.":weatherDrive.Activity>1.05?"Decent weather for a little nuisance like me.":"I'll keep an eye on it.")
        : "I haven't got a fresh weather report. Check Weather senses in the Nest.";
    }
    void BuildWeatherControls(StackPanel body) {
      weatherZip=new TextBox { Text=weatherLocation.Zip??"",MaxLength=5,MinWidth=100,Margin=new Thickness(0,6,0,6) };
      weatherToggle=Toggle("Let outdoor weather influence Corbie",SetWeather);
      body.Children.Add(Section("Weather senses",weatherToggle,weatherStatus=Status(),new TextBlock { Text="US ZIP code",Foreground=MutedText },weatherZip,
        Actions(ActionButton("Use this ZIP",()=> {
          string zip=weatherZip.Text.Trim(); if(!WeatherFeed.ValidZip(zip)) { weatherError="Use a five-digit US ZIP code."; RefreshWeather(); return; }
          try { CancelWeather(); weatherLocation=new WeatherLocation { Zip=zip,Enabled=true }; weatherReading=null; SaveWeather(); weatherNextUtc=DateTime.MinValue; weatherError=null; } catch(Exception ex) { weatherError=ex.Message; } RefreshWeather();
        }),ActionButton("Refresh weather",()=> { weatherNextUtc=DateTime.MinValue; }),ActionButton("Weather report",()=>companion.Say(WeatherReport()))),
        new TextBlock { Text="Open-Meteo current model conditions, refreshed every 20 minutes. Only your chosen ZIP/city coordinates go to the weather provider. With the fly nervous system on, warm dry weather brings a little extra movement; cold, rain and wind favor a tucked perch and fewer flights. These are designed creature responses, not measured fly biology. Stale or missing reports return him to neutral.",TextWrapping=TextWrapping.Wrap,Foreground=MutedText }));
      RefreshWeather();
    }
    void RefreshWeather() {
      if(weatherStatus==null) return;
      bool prior=refreshingPanel; refreshingPanel=true; try { weatherToggle.IsChecked=weatherLocation.Enabled; } finally { refreshingPanel=prior; }
      string current=weatherReading!=null && weatherReading.Fresh(DateTime.UtcNow)?weatherReading.Summary+" · fetched "+Math.Max(0,(int)(DateTime.UtcNow-weatherReading.ReceivedUtc).TotalMinutes)+" min ago":"No fresh weather report";
      weatherStatus.Text=!weatherLocation.Enabled?"Weather senses off":(weatherLocation.Name??weatherLocation.Zip)+" · "+current+"\n"+weatherDrive.Behavior+(weatherRequest!=null?" · refreshing":"")+(weatherError!=null?"\n"+weatherError:"")+"\nSource: Open-Meteo / GeoNames";
    }
    void PoseWeather() {
      if(!nervesMayReact || !weatherDrive.Known || weatherDrive.Shelter<0.2 || walking || hop>0 || idle!=Idle.Quiet || scritches.Engaged || scritches.Celebrating || nervesStartleTimer>0 || NerveWariness>0.05 || thermalAlertTimer>0 || peckingCulpritTimer>0 || offendedTimer>0 || missionPoseTimer>0 || trinketInspectTimer>0 || mischiefChaseTimer>0 || isDragHovering) return;
      wingStretch.Angle+=5*weatherDrive.Shelter; RestingBody.Y+=weatherDrive.Shelter; squish.ScaleX+=0.025*weatherDrive.Shelter;
    }
    void CheckWeatherIntegration() {
      WeatherFeed.Check(); SetNerves(true); weatherLocation.Enabled=true;
      weatherReading=new WeatherReading { Temperature=5,Wind=35,Code=95,ReceivedUtc=DateTime.UtcNow,ObservedUtc=DateTime.UtcNow };
      for(int i=0;i<200;i++) StepWeather(0.1);
      if(FlightAllowed(false)) throw new Exception("Weather shelter did not suppress flight.");
      nervesMayReact=true; idle=Idle.Quiet; walking=false; hop=0; wingStretch.Angle=RestingBody.Y=0; PoseWeather();
      if(wingStretch.Angle<=0 || RestingBody.Y<=0) throw new Exception("Weather shelter pose absent.");
      paused=true; StepNerves(0.1,false); double wing=wingStretch.Angle; PoseWeather(); if(wingStretch.Angle!=wing) throw new Exception("Weather ignored pause."); paused=false;
      weatherReading=null; for(int i=0;i<300;i++) StepWeather(0.1);
      if(!FlightAllowed(false)) throw new Exception("Unknown weather did not restore normal flight.");
      CheckControlPanel(); if(weatherStatus==null) throw new Exception("Missing weather controls."); SetNerves(false);
    }
  }
}
