package com.tonzhub.corbie;

import android.Manifest;
import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Canvas;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Shader;
import android.graphics.Typeface;
import android.media.AudioAttributes;
import android.media.AudioFocusRequest;
import android.media.AudioManager;
import android.media.MediaPlayer;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.SystemClock;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import java.io.IOException;
import java.util.function.Consumer;

public final class MusicActivity extends Activity {
    private static final int AUDIO_PERMISSION = 1, AUDIO_FILE = 2;
    private static final int PHONE = 1, FILE = 2;
    private static final int GOLD = 0xffeac28a, CREAM = 0xfff8eedb, MUTED = 0xffd0b8c7;
    private static final String READY = "Play music on your phone, then tap Use phone audio.";
    private static final AudioAttributes MUSIC = new AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_MEDIA)
            .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
            .build();

    private final Consumer<MusicSpectrum> frame = this::onSpectrum;
    private PocketBirdView bird;
    private SpectrumView bars;
    private TextView status;
    private Button stopButton;
    private MusicCapture capture;
    private MediaPlayer player;
    private AudioManager audio;
    private AudioFocusRequest focus;
    private long lastSignal;
    private String waiting;
    private int pendingSource;
    private Uri pendingFile;
    private boolean resumed;

    @Override protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        if (saved != null) pendingSource = saved.getInt("pendingSource");
        audio = getSystemService(AudioManager.class);

        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        if (Build.VERSION.SDK_INT >= 30) {
            getWindow().setDecorFitsSystemWindows(false);
            scroll.setOnApplyWindowInsetsListener((view, insets) -> {
                android.graphics.Insets safe = insets.getInsets(
                        WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout());
                view.setPadding(safe.left, safe.top, safe.right, safe.bottom);
                return WindowInsets.CONSUMED;
            });
        }
        LinearLayout stage = new LinearLayout(this);
        stage.setOrientation(LinearLayout.VERTICAL);
        stage.setPadding(dp(20), dp(18), dp(20), dp(20));
        scroll.addView(stage);
        setContentView(scroll);

        TextView eyebrow = label("CORBIE  /  MUSIC VISUALIZER", 11, GOLD);
        eyebrow.setLetterSpacing(.16f);
        add(stage, eyebrow, 0, -2);
        TextView title = label("Moonlight Mix", 36, CREAM);
        title.setTypeface(Typeface.create("serif", Typeface.BOLD));
        add(stage, title, 4, -2);
        bird = new PocketBirdView(this, new NestState(0, 0, 0, false, .52f, .82f), () -> {});
        bird.useMusicStage();
        add(stage, bird, 14, dp(250));
        bars = new SpectrumView();
        add(stage, bars, 12, dp(96));
        add(stage, label("BASS TO TREBLE", 10, GOLD), 5, -2);
        status = label(READY, 14, MUTED);
        add(stage, status, 14, -2);

        LinearLayout sources = new LinearLayout(this);
        add(stage, sources, 10, -2);
        sources.addView(button("Use phone audio", () -> selectSource(PHONE)), new LinearLayout.LayoutParams(0, -2, 1));
        sources.addView(button("Play audio file", () -> selectSource(FILE)), new LinearLayout.LayoutParams(0, -2, 1));
        LinearLayout navigation = new LinearLayout(this);
        add(stage, navigation, 2, -2);
        stopButton = button("Stop", () -> reset(READY));
        stopButton.setEnabled(false);
        navigation.addView(stopButton, new LinearLayout.LayoutParams(0, -2, 1));
        navigation.addView(button("Back to Nest", this::finish), new LinearLayout.LayoutParams(0, -2, 1));
        add(stage, label("Android asks for microphone permission for its playback visualizer. Corbie uses the playback signal.", 12, MUTED), 10, -2);
    }

    private void selectSource(int source) {
        reset(READY);
        pendingSource = source;
        if (MusicCapture.hasPermission(this)) usePendingSource();
        else requestPermissions(new String[]{Manifest.permission.RECORD_AUDIO}, AUDIO_PERMISSION);
    }

    private void usePendingSource() {
        if (!resumed || !MusicCapture.hasPermission(this)) return;
        int source = pendingSource;
        pendingSource = 0;
        if (source == PHONE) {
            if (!startCapture(MusicCapture.PHONE_AUDIO, "Waiting for phone audio. If the bars stay flat, try a saved audio file.")) {
                reset("Phone audio is unavailable here. Try a saved audio file.");
            }
        } else if (source == FILE) {
            try {
                startActivityForResult(new Intent(Intent.ACTION_OPEN_DOCUMENT)
                        .addCategory(Intent.CATEGORY_OPENABLE).setType("audio/*"), AUDIO_FILE);
            } catch (ActivityNotFoundException e) {
                message("This phone couldn’t open its audio file picker.");
            }
        }
    }

    @Override public void onRequestPermissionsResult(int request, String[] permissions, int[] grants) {
        super.onRequestPermissionsResult(request, permissions, grants);
        if (request != AUDIO_PERMISSION) return;
        if (grants.length > 0 && grants[0] == PackageManager.PERMISSION_GRANTED) usePendingSource();
        else {
            pendingSource = 0;
            message("Allow audio access to visualize playback. You can enable it in Android’s app settings.");
        }
    }

    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request == AUDIO_FILE && result == RESULT_OK && data != null) {
            pendingFile = data.getData();
            playPendingFile();
        }
    }

    private void playPendingFile() {
        if (!resumed || pendingFile == null) return;
        Uri uri = pendingFile;
        pendingFile = null;
        if (!MusicCapture.hasPermission(this)) {
            message("Allow audio access before playing a file.");
            return;
        }
        MediaPlayer requested = new MediaPlayer();
        player = requested;
        stopButton.setEnabled(true);
        message("Opening audio file…");
        requested.setOnPreparedListener(this::playPrepared);
        requested.setOnCompletionListener(finished -> {
            if (player == finished) reset("Track ended. Pick another audio file or use phone audio.");
        });
        requested.setOnErrorListener((failed, what, extra) -> {
            if (player == failed) reset("Corbie couldn’t play that audio file.");
            return true;
        });
        try {
            requested.setAudioAttributes(MUSIC);
            requested.setDataSource(this, uri);
            requested.prepareAsync();
        } catch (IOException | RuntimeException failure) {
            reset("Corbie couldn’t open that audio file.");
        }
    }

    private void playPrepared(MediaPlayer prepared) {
        if (player != prepared) return;
        focus = new AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN)
                .setAudioAttributes(MUSIC)
                .setOnAudioFocusChangeListener(change -> {
                    if (change < 0 && player == prepared) reset("Track stopped because another app took audio focus.");
                })
                .build();
        if (audio.requestAudioFocus(focus) != AudioManager.AUDIOFOCUS_REQUEST_GRANTED) {
            reset("The phone couldn’t give Corbie audio focus.");
            return;
        }
        if (!startCapture(new MusicCapture(prepared.getAudioSessionId()), "Waiting for a signal from this track.")) {
            reset("The audio visualizer couldn’t open for this track.");
            return;
        }
        try {
            prepared.start();
        } catch (IllegalStateException e) {
            reset("Corbie couldn’t start that audio file.");
        }
    }

    private boolean startCapture(MusicCapture source, String waitingMessage) {
        if (!source.attach(frame)) return false;
        capture = source;
        lastSignal = 0;
        waiting = waitingMessage;
        message(waiting);
        stopButton.setEnabled(true);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        return true;
    }

    private void onSpectrum(MusicSpectrum spectrum) {
        if (spectrum == null) {
            reset("The playback signal was interrupted. Start the visualizer again.");
            return;
        }
        long now = SystemClock.elapsedRealtime();
        if (spectrum.hasSignal) lastSignal = now;
        boolean playing = lastSignal != 0 && now - lastSignal < 1000;
        message(playing ? (player == null ? "Reacting to phone audio" : "Reacting to your track") : waiting);
        bird.setMusicMotion(spectrum.motion, spectrum.kick);
        bars.show(spectrum.levels);
    }

    private void reset(String text) {
        if (capture != null) capture.detach(frame);
        capture = null;
        if (player != null) {
            player.release();
            player = null;
        }
        if (focus != null) {
            audio.abandonAudioFocusRequest(focus);
            focus = null;
        }
        bird.setMusicMotion(0, 0);
        bars.show(null);
        stopButton.setEnabled(false);
        getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        message(text);
    }

    @Override protected void onResume() {
        super.onResume();
        resumed = true;
        bird.setActive(true);
        usePendingSource();
        playPendingFile();
    }

    @Override protected void onPause() {
        resumed = false;
        bird.setActive(false);
        reset(READY);
        super.onPause();
    }

    @Override protected void onSaveInstanceState(Bundle state) {
        super.onSaveInstanceState(state);
        state.putInt("pendingSource", pendingSource);
    }

    private void message(String text) {
        if (!text.contentEquals(status.getText())) status.setText(text);
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private TextView label(String text, int size, int color) {
        TextView view = new TextView(this);
        view.setText(text);
        view.setTextSize(size);
        view.setTextColor(color);
        return view;
    }

    private Button button(String text, Runnable action) {
        Button view = new Button(this);
        view.setText(text);
        view.setAllCaps(false);
        view.setMinHeight(dp(48));
        view.setOnClickListener(ignored -> action.run());
        return view;
    }

    private void add(LinearLayout parent, View view, int top, int height) {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(-1, height);
        params.topMargin = dp(top);
        parent.addView(view, params);
    }

    private final class SpectrumView extends View {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private Shader gradient;
        private float[] levels;

        SpectrumView() {
            super(MusicActivity.this);
            setContentDescription("Frequency spectrum, bass to treble");
        }

        void show(float[] values) {
            levels = values;
            invalidate();
        }

        @Override protected void onSizeChanged(int width, int height, int oldWidth, int oldHeight) {
            gradient = new LinearGradient(0, height, width, 0, 0xffab87ee, 0xffffd49a, Shader.TileMode.CLAMP);
        }

        @Override protected void onDraw(Canvas canvas) {
            int count = 16;
            float width = getWidth() / (float) count;
            float gap = Math.min(dp(5), width / 4);
            float bottom = getHeight() - dp(2);
            for (int i = 0; i < count; i++) {
                float left = i * width + gap;
                float right = (i + 1) * width - gap;
                paint.setColor(0xff372a45);
                canvas.drawRoundRect(left, 0, right, bottom, gap, gap, paint);
                float value = levels == null ? 0 : levels[i];
                float height = dp(3) + value * (bottom - dp(3));
                paint.setShader(gradient);
                canvas.drawRoundRect(left, bottom - height, right, bottom, gap, gap, paint);
                paint.setShader(null);
            }
        }
    }
}
