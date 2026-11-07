package com.tonzhub.corbie;

import android.content.Context;
import android.content.SharedPreferences;

final class NestStore {
    static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences("pocket-nest", Context.MODE_PRIVATE);
    }

    static NestState load(Context context) {
        SharedPreferences p = prefs(context);
        NestState state = new NestState(p.getInt("bond", 0), p.getInt("shinies", 0), p.getInt("treats", 0),
                p.getBoolean("resting", false), p.getFloat("perch_x", .47f), p.getFloat("perch_y", .78f));
        state.carry(Shiny.restore(p.getString("carried_shiny", null)));
        return state;
    }

    static NestState act(Context context, NestState.Action action) {
        NestState state = load(context);
        state.apply(action);
        prefs(context).edit().putInt("bond", state.bond).putInt("shinies", state.shinies)
                .putInt("treats", state.treats).putBoolean("resting", state.resting)
                .putString("carried_shiny", carriedId(state)).apply();
        NestWidget.refresh(context, state);
        return state;
    }

    static boolean carry(Context context, Shiny item) {
        NestState state = load(context);
        if (!state.carry(item)) return false;
        prefs(context).edit().putString("carried_shiny", carriedId(state)).apply();
        NestWidget.refresh(context, state);
        return true;
    }

    private static String carriedId(NestState state) {
        return state.carried == null ? null : state.carried.name();
    }

    static void savePerch(Context context, NestState state) {
        prefs(context).edit().putFloat("perch_x", state.perchX).putFloat("perch_y", state.perchY).apply();
    }
}
