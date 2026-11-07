using System;

namespace CorbiePet {
  // Quiet circadian body language. Local clock only - no online weather fetch.
  public static class DayCycle {
    public static double Restlessness(CircadianPhase phase) {
      switch (phase) {
        case CircadianPhase.Morning: return 1.15;
        case CircadianPhase.Daylight: return 1.25;
        case CircadianPhase.Twilight: return 0.9;
        default: return 0.55;
      }
    }

    public static bool MuteChatter(CircadianPhase phase) {
      return phase == CircadianPhase.DeepNight;
    }

    public static void Check() {
      if (Restlessness(CircadianPhase.Daylight) <= Restlessness(CircadianPhase.DeepNight))
        throw new Exception("DayCycle: restlessness inverted");
      if (!MuteChatter(CircadianPhase.DeepNight) || MuteChatter(CircadianPhase.Morning))
        throw new Exception("DayCycle: mute phases wrong");
    }
  }
}
