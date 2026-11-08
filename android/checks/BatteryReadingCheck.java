package com.tonzhub.corbie;

import android.os.BatteryManager;

final class BatteryReadingCheck {
    public static void main(String[] args) {
        BatteryMonitor.Reading low = new BatteryMonitor.Reading(40, 200, 0, BatteryManager.BATTERY_STATUS_DISCHARGING, false);
        BatteryMonitor.Reading normal = new BatteryMonitor.Reading(21, 100, 0, BatteryManager.BATTERY_STATUS_DISCHARGING, false);
        BatteryMonitor.Reading charging = new BatteryMonitor.Reading(15, 100, 1, BatteryManager.BATTERY_STATUS_CHARGING, false);
        BatteryMonitor.Reading saver = new BatteryMonitor.Reading(97, 100, 0, BatteryManager.BATTERY_STATUS_DISCHARGING, true);
        BatteryMonitor.Reading unknown = new BatteryMonitor.Reading(-1, 0, -1, BatteryManager.BATTERY_STATUS_UNKNOWN, false);
        if (low.percent != 20 || !low.low() || !low.conserve() || normal.conserve()
                || charging.low() || charging.conserve() || !charging.label().endsWith("Charging")
                || saver.low() || !saver.conserve() || unknown.conserve() || unknown.percent != -1) {
            throw new AssertionError("Low battery, charging, saver, and missing battery data must stay distinct");
        }
        System.out.println("PASS: battery scale, low threshold, charging, and Battery Saver");
    }
}
