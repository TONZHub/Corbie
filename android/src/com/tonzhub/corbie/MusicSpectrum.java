package com.tonzhub.corbie;

import java.util.Arrays;

final class MusicSpectrum {
    private static final int[] EDGES = {
            40, 85, 130, 185, 260, 370, 520, 740, 1050, 1500, 2100, 3000, 4200, 6000, 8500, 12000, 16500
    };
    private static final float LOG_SCALE = (float) Math.log1p(65);
    final float[] levels = new float[EDGES.length - 1];
    float motion, kick;
    boolean hasSignal;
    private float bassAverage;
    private long lastKick;

    void update(byte[] fft, int rateMilliHz, long now) {
        float binHz = rateMilliHz / (1000f * fft.length);
        float bass = 0, strongest = 0;
        hasSignal = false;
        for (int band = 0; band < levels.length; band++) {
            int first = Math.max(1, Math.round(EDGES[band] / binHz));
            int end = Math.min(fft.length / 2, Math.max(first + 1, Math.round(EDGES[band + 1] / binHz)));
            int peak = 0;
            for (int bin = first; bin < end; bin++) {
                int real = fft[2 * bin], imaginary = fft[2 * bin + 1];
                peak = Math.max(peak, real * real + imaginary * imaginary);
            }
            hasSignal |= peak >= 4;
            float value = Math.min(1f, (float) Math.log1p(Math.sqrt(peak)) / LOG_SCALE);
            levels[band] += (value - levels[band]) * (value > levels[band] ? .65f : .26f);
            if (band < 3) bass = Math.max(bass, value);
            strongest = Math.max(strongest, value);
        }
        motion += (strongest - motion) * .28f;
        kick *= .76f;
        if (bass > .28f && bass > bassAverage + .16f && now - lastKick >= 160) {
            kick = bass;
            lastKick = now;
        }
        bassAverage += (bass - bassAverage) * .15f;
    }

    void reset() {
        Arrays.fill(levels, 0);
        motion = kick = bassAverage = 0;
        hasSignal = false;
        lastKick = 0;
    }
}
