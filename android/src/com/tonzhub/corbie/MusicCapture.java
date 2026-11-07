package com.tonzhub.corbie;

import android.Manifest;
import android.content.Context;
import android.content.pm.PackageManager;
import android.media.audiofx.Visualizer;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import java.util.LinkedHashSet;
import java.util.Set;
import java.util.function.Consumer;

final class MusicCapture {
    // A single output-mix effect can serve both the stage and the floating bird.
    static final MusicCapture PHONE_AUDIO = new MusicCapture(0);
    private final int session;
    private final MusicSpectrum spectrum = new MusicSpectrum();
    private final Handler main = new Handler(Looper.getMainLooper());
    private final Set<Consumer<MusicSpectrum>> listeners = new LinkedHashSet<>();
    private final Runnable tick = this::poll;
    private Visualizer effect;
    private byte[] fft;
    private int sampleRate;

    MusicCapture(int session) {
        this.session = session;
    }

    static boolean hasPermission(Context context) {
        return context.checkSelfPermission(Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED;
    }

    boolean attach(Consumer<MusicSpectrum> listener) {
        if (effect == null && !start()) return false;
        listeners.add(listener);
        return true;
    }

    void detach(Consumer<MusicSpectrum> listener) {
        listeners.remove(listener);
        if (listeners.isEmpty()) stop();
    }

    private boolean start() {
        try {
            effect = new Visualizer(session);
            if (!effect.getEnabled()) {
                int[] range = Visualizer.getCaptureSizeRange();
                effect.setCaptureSize(Math.max(range[0], Math.min(1024, range[1])));
                if (effect.setEnabled(true) != Visualizer.SUCCESS) {
                    stop();
                    return false;
                }
            }
            sampleRate = effect.getSamplingRate();
            if (sampleRate <= 0) {
                stop();
                return false;
            }
            fft = new byte[effect.getCaptureSize()];
            main.post(tick);
            return true;
        } catch (RuntimeException failure) {
            stop();
            return false;
        }
    }

    private void poll() {
        try {
            if (effect.getFft(fft) == Visualizer.SUCCESS) {
                spectrum.update(fft, sampleRate, SystemClock.elapsedRealtime());
                for (Consumer<MusicSpectrum> listener : listeners) listener.accept(spectrum);
                main.postDelayed(tick, 45);
                return;
            }
        } catch (IllegalStateException | SecurityException ignored) {
            // The output or access can disappear during capture.
        }
        Set<Consumer<MusicSpectrum>> failed = new LinkedHashSet<>(listeners);
        listeners.clear();
        stop();
        for (Consumer<MusicSpectrum> listener : failed) listener.accept(null);
    }

    private void stop() {
        main.removeCallbacks(tick);
        if (effect != null) {
            effect.release();
            effect = null;
        }
        fft = null;
        spectrum.reset();
    }
}
