package com.tonzhub.corbie;

import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioPlaybackCaptureConfiguration;
import android.media.AudioRecord;
import android.media.projection.MediaProjection;
import android.os.Handler;
import android.os.Looper;
import android.os.Process;
import android.os.SystemClock;
import java.util.function.Consumer;

/** Processes playback on a worker and exposes only animation levels. Requires Android 10. */
final class PlaybackDance implements AutoCloseable {
    private final MediaProjection projection;
    private final Consumer<PlaybackDance> stopped;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final MediaProjection.Callback projectionCallback = new MediaProjection.Callback() {
        @Override public void onStop() {
            stopped.accept(PlaybackDance.this);
        }
    };
    private volatile boolean closed;
    private volatile long lastRead;
    private volatile DanceEnergy.Levels levels = DanceEnergy.Levels.QUIET;

    PlaybackDance(MediaProjection projection, Consumer<PlaybackDance> stopped) {
        this.projection = projection;
        this.stopped = stopped;
    }

    void start() {
        projection.registerCallback(projectionCallback, main);
        AudioPlaybackCaptureConfiguration capture = new AudioPlaybackCaptureConfiguration.Builder(projection)
                .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
                .excludeUid(Process.myUid())
                .build();
        AudioRecord record = new AudioRecord.Builder()
                .setAudioFormat(new AudioFormat.Builder().setSampleRate(DanceEnergy.RATE)
                        .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                        .setChannelMask(AudioFormat.CHANNEL_IN_STEREO).build())
                .setAudioPlaybackCaptureConfig(capture)
                .build();
        try {
            record.startRecording();
            if (record.getRecordingState() != AudioRecord.RECORDSTATE_RECORDING) {
                throw new IllegalStateException("Playback capture didn't start");
            }
            new Thread(() -> analyze(record), "Corbie playback visualizer").start();
        } catch (RuntimeException e) {
            record.release();
            throw e;
        }
    }

    private void analyze(AudioRecord record) {
        DanceEnergy detector = new DanceEnergy();
        short[] samples = new short[DanceEnergy.RATE / 25 * 2];
        try {
            while (!closed) {
                int count = record.read(samples, 0, samples.length, AudioRecord.READ_NON_BLOCKING);
                if (count < 0) break;
                if (count == 0) {
                    SystemClock.sleep(12);
                    continue;
                }
                levels = detector.feed(samples, count);
                lastRead = SystemClock.uptimeMillis();
            }
        } finally {
            record.release();
            if (!closed) main.post(() -> stopped.accept(this));
        }
    }

    DanceEnergy.Levels levels() {
        return SystemClock.uptimeMillis() - lastRead < 200 ? levels : DanceEnergy.Levels.QUIET;
    }

    @Override public void close() {
        closed = true;
        projection.unregisterCallback(projectionCallback);
        projection.stop();
    }
}
