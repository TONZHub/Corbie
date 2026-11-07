package com.tonzhub.corbie;

import android.content.Context;
import android.media.AudioAttributes;
import android.media.AudioManager;
import android.media.MediaPlayer;
import android.widget.Toast;
import java.util.Random;
import java.util.function.Consumer;

final class PocketVoice {
    private static final Clip[] CLIPS = {
            new Clip(R.raw.corbie_nuisance, "Oh, good. My favourite nuisance."),
            new Clip(R.raw.corbie_hoard, "I have a hoard. We can both pretend it's a collection."),
            new Clip(R.raw.corbie_water, "You've done enough for five minutes. Go have some water."),
            new Clip(R.raw.corbie_shiny, "If you see anything shiny, I saw it first."),
            new Clip(R.raw.corbie_small_bit, "Go on. Do the small bit first. You can sulk afterwards."),
            new Clip(R.raw.corbie_supervision, "Look at you. Carrying on despite my supervision.")
    };
    private static final AudioAttributes AUDIO = new AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_MEDIA)
            .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
            .build();

    private static final class Clip {
        final int resource;
        final String line;

        Clip(int resource, String line) {
            this.resource = resource;
            this.line = line;
        }
    }

    private final Context context;
    private final Consumer<Boolean> speaking;
    private final Random random = new Random();
    private MediaPlayer player;
    private int lastLine = -1;

    PocketVoice(Context context, Consumer<Boolean> speaking) {
        this.context = context.getApplicationContext();
        this.speaking = speaking;
    }

    String saySomething() {
        int index = random.nextInt(CLIPS.length - (lastLine < 0 ? 0 : 1));
        if (lastLine >= 0 && index >= lastLine) index++;
        lastLine = index;
        Clip clip = CLIPS[index];
        play(clip.resource);
        return clip.line;
    }

    void sayTimerFinished() {
        play(R.raw.corbie_timer);
    }

    private void play(int resource) {
        close();
        player = MediaPlayer.create(context, resource, AUDIO, AudioManager.AUDIO_SESSION_ID_GENERATE);
        if (player == null) {
            unavailable();
            return;
        }
        player.setOnCompletionListener(finished -> {
            if (player == finished) close();
        });
        player.setOnErrorListener((failed, what, extra) -> {
            if (player == failed) unavailable();
            return true;
        });
        try {
            player.start();
            speaking.accept(true);
        } catch (IllegalStateException e) {
            unavailable();
        }
    }

    private void unavailable() {
        close();
        Toast.makeText(context, "Corbie's voice couldn't play.", Toast.LENGTH_SHORT).show();
    }

    void close() {
        if (player != null) {
            player.release();
            player = null;
        }
        speaking.accept(false);
    }
}
