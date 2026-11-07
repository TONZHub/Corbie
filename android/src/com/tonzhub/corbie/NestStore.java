package com.tonzhub.corbie;

import android.content.Context;
import android.content.SharedPreferences;

final class NestStore {
    static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences("pocket-nest", Context.MODE_PRIVATE);
    }

    static NestState load(Context context) {
        SharedPreferences p = prefs(context);
        return new NestState(p.getInt("bond", 0), p.getInt("shinies", 0), p.getInt("treats", 0),
                p.getBoolean("resting", false), p.getFloat("perch_x", .47f), p.getFloat("perch_y", .78f));
    }

    static void act(Context context, NestState.Action action) {
        NestState state = load(context);
        state.apply(action);
        prefs(context).edit().putInt("bond", state.bond).putInt("shinies", state.shinies)
                .putInt("treats", state.treats).putBoolean("resting", state.resting).apply();
        NestWidget.refresh(context, state);
    }

    static void savePerch(Context context, NestState state) {
        prefs(context).edit().putFloat("perch_x", state.perchX).putFloat("perch_y", state.perchY).apply();
    }
}
