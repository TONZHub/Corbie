package com.tonzhub.corbie;

import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.widget.RemoteViews;

public final class NestWidget extends AppWidgetProvider {
    @Override public void onUpdate(Context context, AppWidgetManager manager, int[] ids) {
        update(context, manager, ids, NestStore.load(context));
    }

    @Override public void onAppWidgetOptionsChanged(Context context, AppWidgetManager manager, int id, Bundle options) {
        manager.updateAppWidget(id, render(context, NestStore.load(context), options));
    }

    static void refresh(Context context, NestState state) {
        AppWidgetManager manager = AppWidgetManager.getInstance(context);
        update(context, manager, manager.getAppWidgetIds(new ComponentName(context, NestWidget.class)), state);
    }

    private static void update(Context context, AppWidgetManager manager, int[] ids, NestState state) {
        for (int id : ids) manager.updateAppWidget(id, render(context, state, manager.getAppWidgetOptions(id)));
    }

    private static RemoteViews render(Context context, NestState state, Bundle options) {
        boolean wide = options.getInt(AppWidgetManager.OPTION_APPWIDGET_MIN_WIDTH) >= 180;
        RemoteViews views = new RemoteViews(context.getPackageName(), wide ? R.layout.nest_widget_wide : R.layout.nest_widget);
        views.setTextViewText(R.id.widget_mood, state.mood());
        Shiny item = state.carried;
        views.setViewVisibility(R.id.widget_shiny, item == null ? View.GONE : View.VISIBLE);
        if (item == null) {
            String count = context.getResources().getQuantityString(R.plurals.widget_shinies, state.shinies, state.shinies);
            views.setTextViewText(R.id.widget_hoard, count);
            views.setContentDescription(R.id.widget_hoard, count);
        } else {
            String carrying = context.getString(R.string.widget_carrying, item.label);
            views.setImageViewResource(R.id.widget_shiny, ShinyArt.icon(item));
            views.setTextViewText(R.id.widget_hoard, wide ? carrying : item.label);
            views.setContentDescription(R.id.widget_hoard, carrying);
        }
        Intent open = new Intent(context, PocketActivity.class).addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        views.setOnClickPendingIntent(R.id.widget_root, PendingIntent.getActivity(context, 0, open,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE));
        return views;
    }
}
