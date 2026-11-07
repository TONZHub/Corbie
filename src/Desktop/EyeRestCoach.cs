using System;
using System.Windows;

namespace CorbiePet {
  public sealed class EyeRestCoach {
    public bool GlideActive { get; private set; }
    public double GlideTimeRemaining { get; private set; }
    public double GlideTotalDuration { get; private set; }
    public double GlideX { get; private set; }
    public double GlideY { get; private set; }
    public int GlideDirection { get; private set; }

    public bool PesterBossActive { get; private set; }
    public int ConsecutiveSnoozes { get; set; }
    public double PesterDizzyPhase { get; private set; }
    public double MouseStillDuration { get; private set; }
    double lastMouseX = double.NaN, lastMouseY;

    public EyeRestCoach() {
      GlideTotalDuration = 20.0;
      GlideDirection = 1;
    }

    public void StartGlide(double startX, double screenTop, double minX, double maxX) {
      GlideActive = true;
      GlideTimeRemaining = GlideTotalDuration;
      GlideX = startX;
      GlideY = screenTop + 12;
      GlideDirection = 1;
    }

    public void StopGlide() {
      GlideActive = false;
      GlideTimeRemaining = 0;
    }

    public bool StepGlide(double dt, double minX, double maxX, out double targetX, out double targetY, out int facingDirection) {
      targetX = GlideX;
      targetY = GlideY;
      facingDirection = GlideDirection;
      if (!GlideActive) return false;

      GlideTimeRemaining = Math.Max(0, GlideTimeRemaining - dt);
      if (GlideTimeRemaining <= 0) {
        StopGlide();
        return false;
      }

      // Smooth horizontal traversal from minX to maxX over total duration (one full round-trip in 20s)
      double progress = 1.0 - (GlideTimeRemaining / GlideTotalDuration);
      // Sine wave oscillation: 0 at start, 1 at half-time, 0 at end
      double cycle = (Math.Sin((progress * 2.0 * Math.PI) - (Math.PI / 2.0)) + 1.0) / 2.0;
      double newX = minX + cycle * (maxX - minX);

      // Determine movement direction for facing
      if (newX > GlideX + 0.5) GlideDirection = 1;
      else if (newX < GlideX - 0.5) GlideDirection = -1;

      GlideX = newX;
      targetX = GlideX;
      targetY = GlideY;
      facingDirection = GlideDirection;
      return true;
    }

    public void TriggerPesterBoss() {
      PesterBossActive = true;
      MouseStillDuration = 0;
      PesterDizzyPhase = 0;
    }

    public void DismissPesterBoss() {
      PesterBossActive = false;
      ConsecutiveSnoozes = 0;
      MouseStillDuration = 0;
    }

    public bool StepPester(double dt, double mouseX, double mouseY, out double targetX, out double targetY, out bool isDizzy) {
      targetX = mouseX;
      targetY = mouseY;
      isDizzy = false;
      if (!PesterBossActive) return false;

      double moveDist = double.IsNaN(lastMouseX) ? 0 : Math.Sqrt(Math.Pow(mouseX - lastMouseX, 2) + Math.Pow(mouseY - lastMouseY, 2));
      lastMouseX = mouseX;
      lastMouseY = mouseY;

      if (moveDist > 6.0) {
        // Mouse is being aggressively moved while Corbie is sitting on it
        MouseStillDuration = 0;
        PesterDizzyPhase += dt * (moveDist > 25.0 ? 14.0 : 8.0);
        isDizzy = true;
      } else {
        MouseStillDuration += dt;
        PesterDizzyPhase = Math.Max(0, PesterDizzyPhase - dt * 4.0);
      }

      // If user keeps mouse still for 8 seconds, pester boss grants reprieve
      if (MouseStillDuration >= 8.0) {
        DismissPesterBoss();
        return false;
      }

      targetX = mouseX - 90; // Center on Corbie (Width=180)
      targetY = mouseY - 145; // Sit talons right on top of pointer
      return true;
    }

    public static void SelfCheck() {
      var coach = new EyeRestCoach();
      coach.StartGlide(100, 0, 50, 800);
      if (!coach.GlideActive || coach.GlideTimeRemaining != 20.0) throw new Exception("EyeRestCoach: Glide start failed");

      double x, y; int dir;
      bool active = coach.StepGlide(5.0, 50, 800, out x, out y, out dir);
      if (!active || x <= 50 || x >= 800) throw new Exception("EyeRestCoach: Glide step calculation invalid");

      for (int i = 0; i < 20; i++) coach.StepGlide(1.0, 50, 800, out x, out y, out dir);
      if (coach.GlideActive) throw new Exception("EyeRestCoach: Glide should terminate after duration");

      // Test pester boss
      coach.TriggerPesterBoss();
      if (!coach.PesterBossActive) throw new Exception("EyeRestCoach: Pester boss activation failed");
      bool isDizzy;
      coach.StepPester(0.1, 500, 500, out x, out y, out isDizzy);
      coach.StepPester(0.1, 700, 700, out x, out y, out isDizzy);
      if (!isDizzy) throw new Exception("EyeRestCoach: Fast mouse movement should induce dizziness");
      // Test still timer completion
      for (int i = 0; i < 90; i++) coach.StepPester(0.1, 700, 700, out x, out y, out isDizzy);
      if (coach.PesterBossActive) throw new Exception("EyeRestCoach: Still mouse should complete pester boss");
    }
  }
}
