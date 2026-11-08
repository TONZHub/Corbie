package com.tonzhub.corbie;

import android.os.BatteryManager;
import android.os.PowerManager;

final class BatteryReadingCheck {
    public static void main(String[] args) {
        int unavailable = BatteryMonitor.THERMAL_UNAVAILABLE;
        BatteryMonitor.Reading low = new BatteryMonitor.Reading(40, 200, 0, BatteryManager.BATTERY_STATUS_DISCHARGING, false, unavailable);
        BatteryMonitor.Reading normal = new BatteryMonitor.Reading(21, 100, 0, BatteryManager.BATTERY_STATUS_DISCHARGING, false, unavailable);
        BatteryMonitor.Reading charging = new BatteryMonitor.Reading(15, 100, 1, BatteryManager.BATTERY_STATUS_CHARGING, false, unavailable);
        BatteryMonitor.Reading saver = new BatteryMonitor.Reading(97, 100, 0, BatteryManager.BATTERY_STATUS_DISCHARGING, true, unavailable);
        BatteryMonitor.Reading unknown = new BatteryMonitor.Reading(-1, 0, -1, BatteryManager.BATTERY_STATUS_UNKNOWN, false, unavailable);
        if (low.percent != 20 || !low.low() || !low.conserve() || normal.conserve()
                || charging.low() || charging.conserve() || !charging.label().endsWith("Charging")
                || saver.low() || !saver.conserve() || unknown.conserve() || unknown.percent != -1) {
            throw new AssertionError("Low battery, charging, saver, and missing battery data must stay distinct");
        }
        BatteryMonitor.Reading cool = thermal(PowerManager.THERMAL_STATUS_NONE);
        BatteryMonitor.Reading light = thermal(PowerManager.THERMAL_STATUS_LIGHT);
        BatteryMonitor.Reading moderate = thermal(PowerManager.THERMAL_STATUS_MODERATE);
        BatteryMonitor.Reading severe = thermal(PowerManager.THERMAL_STATUS_SEVERE);
        if (cool.conserve() || light.conserve() || !moderate.hot() || !moderate.conserve() || !severe.conserve()
                || unknown.label().contains("Thermal:")
                || !moderate.label().contains("Charging\nThermal: moderate throttling")) {
            throw new AssertionError("Only moderate heat and above should trigger thermal conservation, even when charging");
        }
        System.out.println("PASS: battery scale, low threshold, charging, Battery Saver, and thermal levels");
    }

    private static BatteryMonitor.Reading thermal(int severity) {
        return new BatteryMonitor.Reading(70, 100, 1, BatteryManager.BATTERY_STATUS_CHARGING, false, severity);
    }
}
