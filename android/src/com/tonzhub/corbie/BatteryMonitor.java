package com.tonzhub.corbie;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.os.BatteryManager;
import android.os.Build;
import android.os.PowerManager;
import java.util.function.BiConsumer;

final class BatteryMonitor {
    static final class Reading {
        final int percent;
        final int plugged;
        final int status;
        final boolean saver;

        Reading(int level, int scale, int plugged, int status, boolean saver) {
            percent = scale > 0 && level >= 0 ? Math.min(100, Math.round(100f * level / scale)) : -1;
            this.plugged = plugged;
            this.status = status;
            this.saver = saver;
        }

        boolean low() { return plugged == 0 && percent >= 0 && percent <= 20; }
        boolean conserve() { return saver || low(); }

        String label() {
            String level = percent < 0 ? "Battery level unavailable" : "Battery " + percent + "%";
            String power = status == BatteryManager.BATTERY_STATUS_FULL ? " · Full"
                    : status == BatteryManager.BATTERY_STATUS_CHARGING ? " · Charging"
                    : plugged > 0 ? " · Plugged in"
                    : low() ? " · Low" : "";
            return level + power + (saver ? " · Battery Saver" : "");
        }
    }

    private final Context context;
    private final BiConsumer<Reading, Reading> changed;
    private boolean registered;
    private Intent battery;
    private Reading reading;
    private final BroadcastReceiver receiver = new BroadcastReceiver() {
        @Override public void onReceive(Context context, Intent intent) {
            if (Intent.ACTION_BATTERY_CHANGED.equals(intent.getAction())) battery = intent;
            update();
        }
    };

    BatteryMonitor(Context context, BiConsumer<Reading, Reading> changed) {
        this.context = context.getApplicationContext();
        this.changed = changed;
    }

    Reading current() { return reading; }

    void start() {
        if (registered) return;
        IntentFilter filter = new IntentFilter(Intent.ACTION_BATTERY_CHANGED);
        filter.addAction(PowerManager.ACTION_POWER_SAVE_MODE_CHANGED);
        battery = Build.VERSION.SDK_INT >= 33
                ? context.registerReceiver(receiver, filter, Context.RECEIVER_NOT_EXPORTED)
                : context.registerReceiver(receiver, filter);
        registered = true;
        update();
    }

    void stop() {
        if (!registered) return;
        registered = false;
        context.unregisterReceiver(receiver);
    }

    private void update() {
        if (!registered) return;
        Intent intent = battery;
        Reading next = new Reading(intent == null ? -1 : intent.getIntExtra(BatteryManager.EXTRA_LEVEL, -1),
                intent == null ? -1 : intent.getIntExtra(BatteryManager.EXTRA_SCALE, -1),
                intent == null ? -1 : intent.getIntExtra(BatteryManager.EXTRA_PLUGGED, -1),
                intent == null ? BatteryManager.BATTERY_STATUS_UNKNOWN
                        : intent.getIntExtra(BatteryManager.EXTRA_STATUS, BatteryManager.BATTERY_STATUS_UNKNOWN),
                context.getSystemService(PowerManager.class).isPowerSaveMode());
        Reading previous = reading;
        if (previous != null && previous.percent == next.percent && previous.plugged == next.plugged
                && previous.status == next.status && previous.saver == next.saver) return;
        reading = next;
        changed.accept(previous, next);
    }
}
