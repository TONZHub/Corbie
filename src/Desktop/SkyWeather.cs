using System;

namespace CorbiePet {
  public enum CircadianPhase {
    DeepNight,
    Morning,
    Daylight,
    Twilight
  }

  public enum WeatherKind {
    Clear,
    Rain,
    Snow,
    Storm
  }

  public sealed class SkyState {
    public CircadianPhase Phase { get; set; }
    public string PhaseName { get; set; }
    public bool SleepRoost { get; set; }
    public WeatherKind Weather { get; set; }
    public string WeatherRemark { get; set; }
  }

  public static class SkyWeather {
    public static CircadianPhase GetPhase(int hour) {
      if (hour >= 23 || hour < 6) return CircadianPhase.DeepNight;
      if (hour >= 6 && hour < 11) return CircadianPhase.Morning;
      if (hour >= 11 && hour < 18) return CircadianPhase.Daylight;
      return CircadianPhase.Twilight;
    }

    public static SkyState Evaluate(DateTime localTime, WeatherKind weather = WeatherKind.Clear) {
      int hour = localTime.Hour;
      var phase = GetPhase(hour);
      bool sleep = (phase == CircadianPhase.DeepNight);
      string phaseName;
      switch (phase) {
        case CircadianPhase.DeepNight: phaseName = "Deep Night Slumber"; break;
        case CircadianPhase.Morning: phaseName = "Morning Perch"; break;
        case CircadianPhase.Daylight: phaseName = "High Daylight"; break;
        case CircadianPhase.Twilight: phaseName = "Twilight Roost"; break;
        default: phaseName = "Roost"; break;
      }

      string remark = null;
      switch (weather) {
        case WeatherKind.Rain:
          remark = "Drizzle outside. My feathers are waterproof, but my mood certainly isn't.";
          break;
        case WeatherKind.Snow:
          remark = "Freezing snow out there. The only thing keeping my talons thawed is your CPU exhaust.";
          break;
        case WeatherKind.Storm:
          remark = "Thunder rumbles in the distance. Excellent brooding weather for a raven.";
          break;
        default:
          if (phase == CircadianPhase.DeepNight) remark = "Zzz... why are you still typing? The sun gave up hours ago.";
          else if (phase == CircadianPhase.Twilight) remark = "Dusk is settling. Keep that focus sharp before nightfall.";
          break;
      }

      return new SkyState {
        Phase = phase,
        PhaseName = phaseName,
        SleepRoost = sleep,
        Weather = weather,
        WeatherRemark = remark
      };
    }

    public static void SelfCheck() {
      // Test hour ranges
      if (GetPhase(23) != CircadianPhase.DeepNight) throw new Exception("SkyWeather: 23:00 should be DeepNight");
      if (GetPhase(2) != CircadianPhase.DeepNight) throw new Exception("SkyWeather: 02:00 should be DeepNight");
      if (GetPhase(7) != CircadianPhase.Morning) throw new Exception("SkyWeather: 07:00 should be Morning");
      if (GetPhase(14) != CircadianPhase.Daylight) throw new Exception("SkyWeather: 14:00 should be Daylight");
      if (GetPhase(20) != CircadianPhase.Twilight) throw new Exception("SkyWeather: 20:00 should be Twilight");

      var night = Evaluate(new DateTime(2026, 9, 20, 2, 0, 0));
      if (!night.SleepRoost || night.Phase != CircadianPhase.DeepNight) throw new Exception("SkyWeather: Night evaluation failed");

      var rain = Evaluate(new DateTime(2026, 9, 20, 14, 0, 0), WeatherKind.Rain);
      if (rain.SleepRoost || !rain.WeatherRemark.Contains("waterproof")) throw new Exception("SkyWeather: Rain evaluation failed");
    }
  }
}
