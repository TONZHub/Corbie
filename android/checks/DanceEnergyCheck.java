package com.tonzhub.corbie;

final class DanceEnergyCheck {
    public static void main(String[] args) {
        DanceEnergy energy = new DanceEnergy();
        DanceEnergy.Levels quiet = tone(energy, 80, .0002, 80);
        DanceEnergy.Levels soft = tone(energy, 80, .008, 40);
        DanceEnergy.Levels loud = tone(energy, 80, .08, 40);
        DanceEnergy.Levels bright = tone(energy, 6000, .02, 90);
        DanceEnergy.Levels stopped = tone(energy, 80, 0, 200);
        if (quiet.volume > .001 || soft.volume < .025 || soft.bass <= soft.treble
                || loud.volume <= soft.volume || bright.treble <= bright.bass || stopped.volume >= .001) {
            throw new AssertionError("Dance must react to soft stereo playback, distinguish bands, and settle in silence");
        }
        System.out.println("PASS: music dance tracks soft and loud stereo, bass, treble, and silence");
    }

    private static DanceEnergy.Levels tone(DanceEnergy energy, int hz, double amplitude, int blocks) {
        short[] stereo = new short[DanceEnergy.RATE / 100 * 2];
        DanceEnergy.Levels levels = DanceEnergy.Levels.QUIET;
        for (int block = 0; block < blocks; block++) {
            for (int frame = 0; frame < stereo.length / 2; frame++) {
                double time = (block * stereo.length / 2 + frame) / (double) DanceEnergy.RATE;
                stereo[2 * frame] = (short) (Math.sin(time * 2 * Math.PI * hz) * amplitude * 32767);
                stereo[2 * frame + 1] = (short) -stereo[2 * frame];
            }
            levels = energy.feed(stereo, stereo.length);
        }
        return levels;
    }
}
