package com.tonzhub.corbie;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.provider.Settings;

public final class PerchTimer {
    static final String FINISHED_LINE = "Right, numpty. Have some water.";
    private static final int NOTIFICATION_ID = 7;
    private static final String SOUND_CHANNEL = "perch-timer";
    private static final String QUIET_CHANNEL = "perch-timer-quiet";
    private static PerchTimer instance;

    interface FinishListener {
        void finished(boolean silent);
    }

    private final Context context;
    private final AlarmManager alarms;
    private final NotificationManager notifications;
    private final SharedPreferences prefs;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final Runnable tick = this::refresh;
    private long deadline;
    private boolean silent;
    FinishListener nest, perch;

    // The activity, service, and alarm receiver all use this instance on the main thread.
    static PerchTimer get(Context context) {
        if (instance == null) instance = new PerchTimer(context.getApplicationContext());
        return instance;
    }

    private PerchTimer(Context context) {
        this.context = context;
        alarms = context.getSystemService(AlarmManager.class);
        notifications = context.getSystemService(NotificationManager.class);
        prefs = context.getSharedPreferences("perch-timer", Context.MODE_PRIVATE);
        silent = prefs.getBoolean("silent", false);
        if (prefs.getInt("boot", -1) == bootCount()) deadline = prefs.getLong("deadline", 0);
    }

    private int bootCount() {
        return Settings.Global.getInt(context.getContentResolver(), Settings.Global.BOOT_COUNT, -1);
    }

    boolean isRunning() { return deadline != 0; }
    boolean isSilent() { return silent; }
    boolean canNotify() { return notifications.areNotificationsEnabled(); }

    boolean canSchedulePrecisely() {
        return Build.VERSION.SDK_INT < 31 || alarms.canScheduleExactAlarms();
    }

    long remainingSeconds() {
        return Math.max(0, (deadline - SystemClock.elapsedRealtime() + 999) / 1000);
    }

    boolean start(int minutes, boolean silent) {
        if (isRunning()) return false;
        this.silent = silent;
        deadline = SystemClock.elapsedRealtime() + minutes * 60_000L;
        prefs.edit().putLong("deadline", deadline).putBoolean("silent", silent)
                .putInt("boot", bootCount()).apply();
        notifications.cancel(NOTIFICATION_ID);
        refresh();
        return true;
    }

    void cancel() {
        deadline = 0;
        prefs.edit().remove("deadline").apply();
        main.removeCallbacks(tick);
        PendingIntent scheduled = PendingIntent.getBroadcast(context, 0, alarmIntent(),
                PendingIntent.FLAG_NO_CREATE | PendingIntent.FLAG_IMMUTABLE);
        if (scheduled != null) {
            alarms.cancel(scheduled);
            scheduled.cancel();
        }
    }

    void refresh() {
        main.removeCallbacks(tick);
        if (!isRunning()) return;
        long remaining = deadline - SystemClock.elapsedRealtime();
        if (remaining <= 0) {
            boolean wasSilent = silent;
            cancel();
            FinishListener listener = perch != null ? perch : nest;
            if (listener != null) listener.finished(wasSilent);
            else notifyFinished(wasSilent);
            return;
        }

        main.postDelayed(tick, remaining);
        PendingIntent operation = PendingIntent.getBroadcast(context, 0, alarmIntent(),
                PendingIntent.FLAG_IMMUTABLE);
        if (canSchedulePrecisely()) {
            try {
                alarms.setExactAndAllowWhileIdle(AlarmManager.ELAPSED_REALTIME_WAKEUP, deadline, operation);
                return;
            } catch (SecurityException ignored) {
                // Access can change between the check and scheduling.
            }
        }
        alarms.setAndAllowWhileIdle(AlarmManager.ELAPSED_REALTIME_WAKEUP, deadline, operation);
    }

    private Intent alarmIntent() {
        return new Intent(context, Alarm.class);
    }

    private void notifyFinished(boolean silent) {
        if (!canNotify()) return;
        String channelId = silent ? QUIET_CHANNEL : SOUND_CHANNEL;
        NotificationChannel channel = new NotificationChannel(channelId,
                silent ? "Silent perch timers" : "Perch timers",
                silent ? NotificationManager.IMPORTANCE_LOW : NotificationManager.IMPORTANCE_HIGH);
        if (silent) {
            channel.setSound(null, null);
            channel.enableVibration(false);
        }
        notifications.createNotificationChannel(channel);
        PendingIntent openNest = PendingIntent.getActivity(context, 1,
                new Intent(context, PocketActivity.class).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP),
                PendingIntent.FLAG_IMMUTABLE);
        notifications.notify(NOTIFICATION_ID, new Notification.Builder(context, channelId)
                .setSmallIcon(R.drawable.corbie_badge)
                .setContentTitle("Corbie’s perch timer is up")
                .setContentText(FINISHED_LINE)
                .setCategory(Notification.CATEGORY_ALARM)
                .setContentIntent(openNest)
                .setAutoCancel(true)
                .build());
    }

    public static final class Alarm extends BroadcastReceiver {
        @Override public void onReceive(Context context, Intent intent) {
            PerchTimer.get(context).refresh();
        }
    }
}
