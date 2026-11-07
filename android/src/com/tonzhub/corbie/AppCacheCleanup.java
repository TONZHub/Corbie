package com.tonzhub.corbie;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.net.Uri;
import android.provider.Settings;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.widget.ArrayAdapter;
import android.widget.LinearLayout;
import android.widget.SearchView;
import android.widget.TextView;
import android.widget.Toast;
import java.text.Collator;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

final class AppCacheCleanup {
    static void show(Activity activity) {
        ArrayAdapter<App> apps = new ArrayAdapter<>(activity, android.R.layout.simple_list_item_1,
                installedApps(activity.getPackageManager()));

        LinearLayout header = new LinearLayout(activity);
        header.setOrientation(LinearLayout.VERTICAL);
        int inset = Math.round(24 * activity.getResources().getDisplayMetrics().density);
        header.setPadding(inset, inset, inset, 0);
        header.setFocusableInTouchMode(true);

        TextView title = new TextView(activity);
        title.setText("Clean app cache");
        title.setTextAppearance(android.R.style.TextAppearance_Material_Title);
        header.addView(title);

        TextView directions = new TextView(activity);
        directions.setText("Choose an app, then tap Storage and Clear cache in Android Settings.");
        directions.setTextAppearance(android.R.style.TextAppearance_Material_Body1);
        header.addView(directions);

        SearchView search = new SearchView(activity);
        search.setIconifiedByDefault(false);
        search.setQueryHint("Find an app");
        header.addView(search);

        TextView empty = new TextView(activity);
        empty.setText("No matching apps. Try All apps for more.");
        empty.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        empty.setVisibility(apps.isEmpty() ? View.VISIBLE : View.GONE);
        header.addView(empty);

        search.setOnQueryTextListener(new SearchView.OnQueryTextListener() {
            @Override public boolean onQueryTextSubmit(String query) {
                search.clearFocus();
                return true;
            }

            @Override public boolean onQueryTextChange(String query) {
                apps.getFilter().filter(query.trim(), count ->
                        empty.setVisibility(count == 0 ? View.VISIBLE : View.GONE));
                return true;
            }
        });

        AlertDialog dialog = new AlertDialog.Builder(activity)
                .setCustomTitle(header)
                .setAdapter(apps, null)
                .setNeutralButton("All apps", (ignored, which) ->
                        openSettings(activity, new Intent(Settings.ACTION_MANAGE_APPLICATIONS_SETTINGS)))
                .setNegativeButton("Close", null)
                .create();
        Window window = dialog.getWindow();
        window.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_HIDDEN
                | WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        dialog.show();
        // AlertDialog checks its body for text editors, but the search field lives in the title.
        window.clearFlags(WindowManager.LayoutParams.FLAG_ALT_FOCUSABLE_IM);
        header.requestFocus();
        dialog.getListView().setOnItemClickListener((list, view, position, id) -> {
            App app = apps.getItem(position);
            if (app == null) return;
            search.clearFocus();
            openSettings(activity, new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                    Uri.fromParts("package", app.packageName, null)));
        });
    }

    private static List<App> installedApps(PackageManager packages) {
        Intent launcher = new Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_LAUNCHER);
        Map<String, App> byPackage = new LinkedHashMap<>();
        for (ResolveInfo match : packages.queryIntentActivities(launcher, 0)) {
            ApplicationInfo info = match.activityInfo.applicationInfo;
            byPackage.computeIfAbsent(info.packageName, name ->
                    new App(name, info.loadLabel(packages).toString()));
        }
        List<App> apps = new ArrayList<>(byPackage.values());
        Collator collator = Collator.getInstance();
        apps.sort((left, right) -> {
            int order = collator.compare(left.label, right.label);
            return order == 0 ? left.packageName.compareTo(right.packageName) : order;
        });
        return apps;
    }

    private static void openSettings(Activity activity, Intent intent) {
        try {
            activity.startActivity(intent);
        } catch (ActivityNotFoundException | SecurityException e) {
            Toast.makeText(activity, "Couldn’t open Android’s app settings.", Toast.LENGTH_SHORT).show();
        }
    }

    private static final class App {
        final String packageName;
        final String label;

        App(String packageName, String label) {
            this.packageName = packageName;
            this.label = label;
        }

        @Override public String toString() {
            return label;
        }
    }
}
