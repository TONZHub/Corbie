package com.tonzhub.corbie;

import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.speech.tts.Voice;
import android.widget.Toast;
import java.util.Locale;
import java.util.Random;
import java.util.Set;
import java.util.function.Consumer;

final class PocketVoice {
    private static final String[] LINES = {
            "Oh, good. My favourite nuisance.",
            "I have a hoard. We can both pretend it's a collection.",
            "You've done enough for five minutes. Go have some water.",
            "If you see anything shiny, I saw it first.",
            "Go on. Do the small bit first. You can sulk afterwards.",
            "Look at you. Carrying on despite my supervision."
    };

    private final Context context;
    private final Consumer<Boolean> speaking;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final Random random = new Random();
    private TextToSpeech engine;
    private String pending;
    private String utterance;
    private int lastLine = -1, generation, sequence;
    private boolean ready;

    PocketVoice(Context context, Consumer<Boolean> speaking) {
        this.context = context.getApplicationContext();
        this.speaking = speaking;
    }

    String saySomething() {
        int index = random.nextInt(LINES.length - (lastLine < 0 ? 0 : 1));
        if (lastLine >= 0 && index >= lastLine) index++;
        lastLine = index;
        String line = LINES[index];
        pending = line;
        if (ready) {
            speakPending();
        } else if (engine == null) {
            int attempt = ++generation;
            engine = new TextToSpeech(context, status -> main.post(() -> initialized(attempt, status)));
        }
        return line;
    }

    private void initialized(int attempt, int status) {
        if (attempt != generation) return;
        Voice voice = status == TextToSpeech.SUCCESS ? localVoice(engine.getVoices()) : null;
        if (voice == null || engine.setVoice(voice) != TextToSpeech.SUCCESS) {
            close();
            toast("Add an offline English voice in Android's text-to-speech settings.");
            return;
        }
        engine.setOnUtteranceProgressListener(new UtteranceProgressListener() {
            @Override public void onStart(String id) { progress(id, true, false); }
            @Override public void onDone(String id) { progress(id, false, false); }
            @Override public void onError(String id) { progress(id, false, true); }
            @Override public void onStop(String id, boolean interrupted) { progress(id, false, false); }
        });
        ready = true;
        speakPending();
    }

    private static Voice localVoice(Set<Voice> voices) {
        Voice best = null;
        int bestScore = -1;
        if (voices == null) return null;
        for (Voice voice : voices) {
            if (voice.isNetworkConnectionRequired() || !"en".equals(voice.getLocale().getLanguage())
                    || (voice.getFeatures() != null
                        && voice.getFeatures().contains(TextToSpeech.Engine.KEY_FEATURE_NOT_INSTALLED))) continue;
            int score = voice.getQuality() + (Locale.UK.equals(voice.getLocale()) ? 1000 : 0);
            if (score > bestScore) {
                best = voice;
                bestScore = score;
            }
        }
        return best;
    }

    private void speakPending() {
        if (pending == null) return;
        utterance = "corbie-" + ++sequence;
        String line = pending;
        pending = null;
        speaking.accept(false);
        if (engine.speak(line, TextToSpeech.QUEUE_FLUSH, null, utterance) == TextToSpeech.ERROR) {
            utterance = null;
            toast("Corbie's offline voice is unavailable.");
        }
    }

    private void progress(String id, boolean talking, boolean failed) {
        main.post(() -> {
            if (!id.equals(utterance)) return;
            if (!talking) utterance = null;
            speaking.accept(talking);
            if (failed) toast("Corbie's offline voice is unavailable.");
        });
    }

    private void toast(String message) {
        Toast.makeText(context, message, Toast.LENGTH_LONG).show();
    }

    void close() {
        generation++;
        pending = null;
        utterance = null;
        ready = false;
        speaking.accept(false);
        if (engine != null) {
            engine.stop();
            engine.shutdown();
            engine = null;
        }
    }
}
