package com.tonzhub.corbie;

import android.content.Context;
import android.graphics.Color;
import android.view.Gravity;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.LinearLayout;
import android.widget.TextView;
import java.util.Locale;

final class PerchTimerPanel extends LinearLayout {
    private final PerchTimer timer;
    private final TextView status;
    private final LinearLayout durations;
    private final CheckBox silent;
    private final Button cancel, notifications, exact;
    private final TextView help;
    private final Runnable update = this::update;

    PerchTimerPanel(Context context, Runnable allowNotifications, Runnable allowExact) {
        super(context);
        timer = PerchTimer.get(context);
        setOrientation(VERTICAL);
        status = text();
        status.setTextSize(20);

        silent = new CheckBox(context);
        silent.setText("Silent");
        silent.setTextColor(Color.WHITE);
        silent.setChecked(timer.isSilent());

        durations = new LinearLayout(context);
        addView(durations);
        for (int minutes : new int[]{5, 15, 25}) {
            Button duration = button(minutes + " min", () -> {
                if (timer.start(minutes, silent.isChecked())) update();
            });
            durations.addView(duration, new LayoutParams(0, LayoutParams.WRAP_CONTENT, 1));
        }
        addView(silent);

        cancel = button("Cancel timer", () -> {
            timer.cancel();
            update();
        });
        addView(cancel);
        help = text();
        help.setTextSize(13);
        if (allowNotifications == null) {
            notifications = null;
            exact = null;
        } else {
            notifications = button("Enable notifications", allowNotifications);
            exact = button("Enable precise alerts", allowExact);
            addView(notifications);
            addView(exact);
        }
    }

    private TextView text() {
        TextView view = new TextView(getContext());
        view.setTextColor(Color.WHITE);
        view.setGravity(Gravity.CENTER_HORIZONTAL);
        addView(view);
        return view;
    }

    private Button button(String label, Runnable action) {
        Button button = new Button(getContext());
        button.setText(label);
        button.setAllCaps(false);
        button.setOnClickListener(view -> action.run());
        return button;
    }

    @Override protected void onAttachedToWindow() {
        super.onAttachedToWindow();
        update();
    }

    @Override protected void onDetachedFromWindow() {
        removeCallbacks(update);
        super.onDetachedFromWindow();
    }

    private void update() {
        removeCallbacks(update);
        boolean running = timer.isRunning();
        long seconds = timer.remainingSeconds();
        status.setText(running ? String.format(Locale.getDefault(), "%d:%02d%s",
                seconds / 60, seconds % 60, timer.isSilent() ? " · Silent" : "") : "Choose a duration");
        durations.setVisibility(running ? GONE : VISIBLE);
        silent.setVisibility(running ? GONE : VISIBLE);
        cancel.setVisibility(running ? VISIBLE : GONE);

        boolean notify = timer.canNotify();
        boolean precise = timer.canSchedulePrecisely();
        if (notifications != null) {
            notifications.setVisibility(notify ? GONE : VISIBLE);
            exact.setVisibility(precise ? GONE : VISIBLE);
        }
        if (!notify) {
            help.setText(notifications == null ? "For alerts away from Corbie, open the Nest’s timer."
                    : "Enable notifications for alerts away from Corbie.");
        } else if (!precise) {
            help.setText(exact == null ? "Background alerts may be late. Set up precise alerts in the Nest."
                    : "Background alerts may be late without precise alerts.");
        }
        help.setVisibility(notify && precise ? GONE : VISIBLE);
        if (isAttachedToWindow()) postDelayed(update, running ? 500 : 1000);
    }
}
