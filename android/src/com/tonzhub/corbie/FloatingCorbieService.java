package com.tonzhub.corbie;

import android.animation.Animator;
import android.animation.AnimatorListenerAdapter;
import android.animation.ValueAnimator;
import android.app.KeyguardManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.SharedPreferences;
import android.content.res.Configuration;
import android.content.pm.ServiceInfo;
import android.graphics.PixelFormat;
import android.graphics.Point;
import android.graphics.drawable.GradientDrawable;
import android.hardware.display.DisplayManager;
import android.os.Build;
import android.os.IBinder;
import android.os.PowerManager;
import android.provider.Settings;
import android.view.Display;
import android.view.Gravity;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.view.WindowMetrics;
import android.view.animation.LinearInterpolator;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

public final class FloatingCorbieService extends Service {
    private static final String CHANNEL = "floating-corbie";
    private static final String RETURN_HOME = "com.tonzhub.corbie.RETURN_HOME";
    private static final int NOTIFICATION_ID = 6;
    private static final long PERCH_TIME_MS = 16000;
    private static final int BIRD_WIDTH_DP = 118;

    private WindowManager windows;
    private WindowManager.LayoutParams position;
    private FrameLayout window;
    private PocketBirdView bird;
    private ScrollView menu;
    private NestState state;
    private PocketVoice voice;
    private SharedPreferences perch;
    private ValueAnimator motion;
    private int wanderCount;
    private int roomWidth, roomHeight, maxX, maxY, perchX, perchY;
    private boolean windowAdded, receiverRegistered, careRegistered;
    private boolean visible, wandering;
    private final Runnable wander = this::wander;
    private final SharedPreferences.OnSharedPreferenceChangeListener careChanged = (prefs, key) -> {
        boolean wasResting = state.resting;
        state.updateCare(NestStore.load(this));
        if (state.resting != wasResting) {
            cancelMotion();
            bird.play(PocketBirdView.Pose.IDLE);
            scheduleWander();
        }
    };
    private final BroadcastReceiver screen = new BroadcastReceiver() {
        @Override public void onReceive(Context context, Intent intent) {
            updateVisibility();
        }
    };

    @Override public IBinder onBind(Intent intent) { return null; }

    @Override public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent == null || RETURN_HOME.equals(intent.getAction())) {
            stopSelf();
            return START_NOT_STICKY;
        }
        if (bird != null) return START_NOT_STICKY;
        try {
            Notification notification = notification();
            if (Build.VERSION.SDK_INT >= 34) {
                startForeground(NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);
            } else {
                startForeground(NOTIFICATION_ID, notification);
            }
            if (!Settings.canDrawOverlays(this)) {
                stopSelf();
                return START_NOT_STICKY;
            }
            attachBird();
        } catch (WindowManager.BadTokenException | SecurityException | IllegalStateException e) {
            Toast.makeText(this, "Corbie couldn't open his floating perch.", Toast.LENGTH_SHORT).show();
            stopSelf();
        }
        return START_NOT_STICKY;
    }

    private Notification notification() {
        NotificationManager manager = getSystemService(NotificationManager.class);
        manager.createNotificationChannel(new NotificationChannel(CHANNEL, "Floating Corbie",
                NotificationManager.IMPORTANCE_LOW));
        PendingIntent openNest = PendingIntent.getActivity(this, 0, new Intent(this, PocketActivity.class),
                PendingIntent.FLAG_IMMUTABLE);
        PendingIntent returnHome = PendingIntent.getService(this, 0,
                new Intent(this, FloatingCorbieService.class).setAction(RETURN_HOME),
                PendingIntent.FLAG_IMMUTABLE);
        return new Notification.Builder(this, CHANNEL)
                .setSmallIcon(R.drawable.corbie_badge)
                .setContentTitle("Corbie is out of the Nest")
                .setContentText("Tap to open the Nest.")
                .setContentIntent(openNest)
                .setOngoing(true)
                .addAction(new Notification.Action.Builder(null, "Bring him home", returnHome).build())
                .build();
    }

    private void attachBird() {
        int type = WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY;
        Context overlayContext = this;
        if (Build.VERSION.SDK_INT >= 30) {
            Display display = getSystemService(DisplayManager.class).getDisplay(Display.DEFAULT_DISPLAY);
            if (display == null) throw new IllegalStateException("No default display");
            overlayContext = createDisplayContext(display).createWindowContext(type, null);
        }
        windows = overlayContext.getSystemService(WindowManager.class);
        position = new WindowManager.LayoutParams(dp(BIRD_WIDTH_DP), dp(116), type,
                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE | WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
                PixelFormat.TRANSLUCENT);
        position.gravity = Gravity.TOP | Gravity.LEFT;
        if (Build.VERSION.SDK_INT >= 30) {
            position.setFitInsetsTypes(perchInsets());
            position.setFitInsetsIgnoringVisibility(true);
        }
        measureRoom();
        perch = getSharedPreferences("floating-perch", MODE_PRIVATE);
        wandering = perch.getBoolean("wandering", true);
        perchX = restoreAxis("x", maxX, dp(12));
        perchY = restoreAxis("y", maxY, dp(24));
        position.x = perchX;
        position.y = perchY;

        window = new FrameLayout(overlayContext);
        state = NestStore.load(this);
        bird = new PocketBirdView(overlayContext, state, this::savePerch, this::drag);
        voice = new PocketVoice(this, bird::setSpeaking);
        bird.face(-1);
        bird.setOnClickListener(view -> {
            cancelMotion();
            if (state.resting) react(NestState.Action.WAKE, PocketBirdView.Pose.WAVE, "Yes, I'm up.");
            else bird.play(PocketBirdView.Pose.WAVE);
            scheduleWander();
        });
        bird.setOnLongClickListener(view -> { openMenu(); return true; });
        window.addView(bird, new FrameLayout.LayoutParams(-1, -1));
        windows.addView(window, position);
        windowAdded = true;
        NestStore.prefs(this).registerOnSharedPreferenceChangeListener(careChanged);
        careRegistered = true;

        IntentFilter filter = new IntentFilter(Intent.ACTION_SCREEN_OFF);
        filter.addAction(Intent.ACTION_SCREEN_ON);
        filter.addAction(Intent.ACTION_USER_PRESENT);
        if (Build.VERSION.SDK_INT >= 33) registerReceiver(screen, filter, Context.RECEIVER_NOT_EXPORTED);
        else registerReceiver(screen, filter);
        receiverRegistered = true;
        updateVisibility();
    }

    private void openMenu() {
        openMenu(false);
    }

    private void openMenu(boolean showTimer) {
        if (menu != null) return;
        cancelMotion();
        voice.close();
        Context context = window.getContext();
        menu = new ScrollView(context);
        LinearLayout panel = new LinearLayout(context);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setPadding(dp(12), dp(10), dp(12), dp(10));
        GradientDrawable background = new GradientDrawable();
        background.setColor(0xff302439);
        background.setCornerRadius(dp(18));
        background.setStroke(dp(1), 0xffa38699);
        menu.setBackground(background);
        menu.setFillViewport(true);
        menu.addView(panel);
        TextView title = new TextView(context);
        title.setText(showTimer ? "Perch timer" : "Corbie's perch");
        title.setTextColor(0xfff3ca94);
        title.setTextSize(18);
        title.setPadding(dp(4), dp(4), dp(4), dp(6));
        panel.addView(title);

        if (showTimer) {
            panel.addView(new PerchTimerPanel(context, null, null));
            menuButton(menuRow(panel), "Back to perch menu", this::openMenu);
        } else {
            populatePerchMenu(panel);
        }
        bird.setActive(false);
        bird.setVisibility(View.GONE);
        window.addView(menu, new FrameLayout.LayoutParams(-1, -1));
        layoutWindow();
        scheduleWander();
    }

    private void populatePerchMenu(LinearLayout panel) {
        LinearLayout snacks = menuRow(panel);
        menuButton(snacks, "Treat", () -> react(NestState.Action.TREAT, PocketBirdView.Pose.NIBBLE,
                "Finally. A sensible use of your time."));
        menuButton(snacks, "Shiny", () -> react(NestState.Action.SHINY, PocketBirdView.Pose.SHINY,
                "It's going straight in the hoard."));
        LinearLayout attention = menuRow(panel);
        menuButton(attention, "Scritches", () -> react(NestState.Action.SCRITCH, PocketBirdView.Pose.SCRITCH,
                "I'll allow it. Mind the feathers."));
        menuButton(attention, "Say something", () -> {
            if (state.resting) NestStore.act(this, NestState.Action.WAKE);
            showLine(voice.saySomething());
        });
        LinearLayout quiet = menuRow(panel);
        menuButton(quiet, state.resting ? "Wake up" : "Roost", () -> {
            if (state.resting) react(NestState.Action.WAKE, PocketBirdView.Pose.HOP, "I was merely thinking.");
            else react(NestState.Action.REST, PocketBirdView.Pose.IDLE, "Wake me for something shiny.");
        });
        menuButton(quiet, wandering ? "Stay put" : "Wander", () -> {
            wandering = !wandering;
            savePerch();
        });
        LinearLayout activities = menuRow(panel);
        menuButton(activities, "Perch timer", () -> openMenu(true));
        menuButton(activities, "Take flight", () -> {
            if (state.resting) NestStore.act(this, NestState.Action.WAKE);
            travel(true);
            scheduleWander();
        });
        LinearLayout done = menuRow(panel);
        menuButton(done, "Close", null);
        menuButton(done, "Bring him home", this::stopSelf);
    }

    private void timerFinished(boolean silent) {
        closeMenu();
        cancelMotion();
        bird.play(PocketBirdView.Pose.WAVE);
        showLine(PerchTimer.FINISHED_LINE);
        if (!silent) voice.sayTimerFinished();
        scheduleWander();
    }

    private static LinearLayout menuRow(LinearLayout panel) {
        LinearLayout row = new LinearLayout(panel.getContext());
        panel.addView(row, new LinearLayout.LayoutParams(-1, -2));
        return row;
    }

    private void menuButton(LinearLayout row, String label, Runnable action) {
        Button button = new Button(row.getContext());
        button.setAllCaps(false);
        button.setText(label);
        button.setTextSize(14);
        button.setMinHeight(dp(48));
        button.setMinimumHeight(dp(48));
        row.addView(button, new LinearLayout.LayoutParams(0, -2, 1));
        button.setOnClickListener(view -> {
            closeMenu();
            if (action != null) action.run();
        });
    }

    private void closeMenu() {
        if (menu == null) return;
        window.removeView(menu);
        menu = null;
        layoutWindow();
        bird.setVisibility(View.VISIBLE);
        bird.setActive(visible);
        scheduleWander();
    }

    private void react(NestState.Action action, PocketBirdView.Pose pose, String line) {
        NestStore.act(this, action);
        bird.play(pose);
        showLine(line);
    }

    private void showLine(String line) {
        Toast.makeText(this, line, Toast.LENGTH_SHORT).show();
    }

    private static int perchInsets() {
        return WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout();
    }

    private void measureRoom() {
        Point size = new Point();
        if (Build.VERSION.SDK_INT >= 30) {
            WindowMetrics metrics = windows.getCurrentWindowMetrics();
            android.graphics.Insets insets = metrics.getWindowInsets().getInsetsIgnoringVisibility(perchInsets());
            size.set(metrics.getBounds().width() - insets.left - insets.right,
                    metrics.getBounds().height() - insets.top - insets.bottom);
        } else {
            windows.getDefaultDisplay().getSize(size);
        }
        roomWidth = Math.max(1, size.x);
        roomHeight = Math.max(1, size.y);
        position.width = Math.min(dp(BIRD_WIDTH_DP), roomWidth);
        position.height = Math.min(dp(116), roomHeight);
        maxX = roomWidth - position.width;
        maxY = roomHeight - position.height;
    }

    @Override public void onConfigurationChanged(Configuration config) {
        super.onConfigurationChanged(config);
        if (bird == null) return;
        cancelMotion();
        float x = fraction(perchX, maxX);
        float y = fraction(perchY, maxY);
        measureRoom();
        moveTo(Math.round(x * maxX), Math.round(y * maxY));
        scheduleWander();
    }

    private void updateVisibility() {
        visible = getSystemService(PowerManager.class).isInteractive()
                && !getSystemService(KeyguardManager.class).isKeyguardLocked();
        if (!visible) {
            cancelMotion();
            voice.close();
            closeMenu();
        }
        window.setVisibility(visible ? View.VISIBLE : View.INVISIBLE);
        bird.setActive(visible && menu == null);
        PerchTimer timer = PerchTimer.get(this);
        timer.perch = visible ? this::timerFinished : null;
        if (visible) timer.refresh();
        scheduleWander();
    }

    private void scheduleWander() {
        if (bird == null) return;
        bird.removeCallbacks(wander);
        if (visible && wandering && menu == null && !state.resting) bird.postDelayed(wander, PERCH_TIME_MS);
    }

    private void wander() {
        scheduleWander();
        if (!bird.isShown() || !wandering || state.resting || bird.isInteracting() || maxX == 0) return;
        travel(++wanderCount % 3 == 0);
    }

    private void travel(boolean flight) {
        if (maxX == 0) return;
        cancelMotion();
        int from = perchX;
        int ground = perchY;
        int stride = Math.min(maxX, dp(flight ? 136 : 24));
        int direction = from + stride > maxX ? -1 : from < stride ? 1 : (Math.random() < .5 ? -1 : 1);
        int to = Math.max(0, Math.min(maxX, from + direction * stride));
        if (to == from) to = direction < 0 ? maxX : 0;
        int destination = to;
        bird.face(destination < from ? -1 : 1);
        if (!ValueAnimator.areAnimatorsEnabled()) {
            moveTo(destination, ground);
            return;
        }
        int lift = Math.min(ground, dp(58));
        if (flight) bird.fly(0);
        else bird.play(PocketBirdView.Pose.HOP);
        motion = ValueAnimator.ofFloat(0, 1);
        motion.setInterpolator(new LinearInterpolator());
        motion.setDuration(flight ? 1060 : 580);
        motion.addUpdateListener(animation -> {
            if (bird.isInteracting()) {
                cancelMotion();
                return;
            }
            float phase = animation.getAnimatedFraction();
            float eased = phase * phase * (3 - 2 * phase);
            if (flight) bird.fly(phase);
            int y = flight ? ground - (int) Math.round(lift * Math.sin(Math.PI * phase)) : ground;
            moveTo(from + Math.round((destination - from) * eased), y);
        });
        motion.addListener(new AnimatorListenerAdapter() {
            @Override public void onAnimationEnd(Animator finished) {
                if (motion != finished) return;
                motion = null;
                bird.play(PocketBirdView.Pose.IDLE);
            }
        });
        motion.start();
    }

    private void drag(float dx, float dy) {
        cancelMotion();
        moveTo(perchX + Math.round(dx), perchY + Math.round(dy));
    }

    private void moveTo(int x, int y) {
        perchX = Math.max(0, Math.min(x, maxX));
        perchY = Math.max(0, Math.min(y, maxY));
        layoutWindow();
    }

    private void layoutWindow() {
        position.width = Math.min(dp(menu == null ? BIRD_WIDTH_DP : 272), roomWidth);
        position.height = Math.min(dp(menu == null ? 116 : 318), roomHeight);
        position.x = Math.min(perchX, roomWidth - position.width);
        position.y = Math.min(perchY, roomHeight - position.height);
        try {
            windows.updateViewLayout(window, position);
        } catch (IllegalArgumentException | SecurityException e) {
            cancelMotion();
            stopSelf();
        }
    }

    private int restoreAxis(String key, int max, int margin) {
        float fallback = fraction(Math.max(0, max - margin), max);
        return Math.round(max * NestState.safeFraction(perch.getFloat(key, fallback), fallback));
    }

    private static float fraction(int coordinate, int max) {
        return max == 0 ? 1f : coordinate / (float) max;
    }

    private void savePerch() {
        SharedPreferences.Editor saved = perch.edit().putBoolean("wandering", wandering);
        if (maxX > 0) saved.putFloat("x", fraction(perchX, maxX));
        if (maxY > 0) saved.putFloat("y", fraction(perchY, maxY));
        saved.apply();
        scheduleWander();
    }

    private void cancelMotion() {
        if (motion == null) return;
        ValueAnimator previous = motion;
        motion = null;
        previous.cancel();
        bird.play(PocketBirdView.Pose.IDLE);
    }

    @Override public void onDestroy() {
        if (receiverRegistered) unregisterReceiver(screen);
        if (careRegistered) NestStore.prefs(this).unregisterOnSharedPreferenceChangeListener(careChanged);
        cancelMotion();
        if (bird != null) {
            PerchTimer.get(this).perch = null;
            visible = false;
            voice.close();
            bird.setActive(false);
            bird.removeCallbacks(wander);
            if (windowAdded) windows.removeViewImmediate(window);
            bird = null;
        }
        stopForeground(STOP_FOREGROUND_REMOVE);
        super.onDestroy();
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
