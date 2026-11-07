package com.tonzhub.corbie;

/** The desktop visualizer's adaptive envelopes, applied to stereo PCM. */
final class DanceEnergy {
    static final int RATE = 48000;
    private static final int BLOCK = RATE / 100;
    private static final double BASS = 1 - Math.exp(-2 * Math.PI * 180 / RATE);
    private static final double TREBLE = 1 - Math.exp(-2 * Math.PI * 2000 / RATE);

    static final class Levels {
        static final Levels QUIET = new Levels(0, 0, 0);
        final float volume, bass, treble;

        Levels(float volume, float bass, float treble) {
            this.volume = volume;
            this.bass = bass;
            this.treble = treble;
        }
    }

    private final double[] low = new double[2], mid = new double[2];
    private double fullPower, lowPower, highPower, average = .025;
    private int frames;
    private Levels levels = Levels.QUIET;

    Levels feed(short[] stereo, int count) {
        for (int i = 0; i + 1 < count; i += 2) {
            for (int channel = 0; channel < 2; channel++) {
                double sample = stereo[i + channel] / 32768.0;
                low[channel] += (sample - low[channel]) * BASS;
                mid[channel] += (sample - mid[channel]) * TREBLE;
                double high = sample - mid[channel];
                fullPower += sample * sample;
                lowPower += low[channel] * low[channel];
                highPower += high * high;
            }
            if (++frames == BLOCK) update();
        }
        return levels;
    }

    private void update() {
        double rms = Math.sqrt(fullPower / (2 * BLOCK));
        average += (rms - average) * (rms > average ? .004 : .001);
        double scale = Math.max(.025, average * 2.5);
        levels = new Levels(smooth(levels.volume, rms, scale),
                smooth(levels.bass, Math.sqrt(lowPower / (2 * BLOCK)), scale),
                smooth(levels.treble, Math.sqrt(highPower / (2 * BLOCK)), scale));
        frames = 0;
        fullPower = lowPower = highPower = 0;
    }

    private static float smooth(float old, double rms, double scale) {
        double target = Math.sqrt(Math.max(0, Math.min(1, (rms - .0008) / scale)));
        return (float) (old + (target - old) * (target > old ? .4 : .055));
    }
}
