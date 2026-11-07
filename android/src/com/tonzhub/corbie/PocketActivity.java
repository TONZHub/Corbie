package com.tonzhub.corbie;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.RippleDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.view.Gravity;
import android.view.HapticFeedbackConstants;
import android.view.View;
import android.view.WindowInsets;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;
import java.util.Random;

public final class PocketActivity extends Activity {
    private static final int BACKGROUND=0xff211a27, CREAM=0xfff8eedb, GOLD=0xffeac28a, MUTED=0xffd0b8c7;
    private final Random random=new Random();
    private NestState state;
    private PocketBirdView bird;
    private TextView mood, counts, remark;
    private Button rest;
    private String lastLine="";
    private PocketVoice voice;
    private AlertDialog timerDialog;
    private AlertDialog showDialog;
    private ScrollView nestScroll;
    private final SharedPreferences.OnSharedPreferenceChangeListener careChanged = (prefs,key) -> refreshCare();

    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        state=NestStore.load(this);
        voice=new PocketVoice(this, speaking -> bird.setSpeaking(speaking));
        getWindow().setStatusBarColor(BACKGROUND);
        getWindow().setNavigationBarColor(BACKGROUND);
        ScrollView scroll=new ScrollView(this);
        nestScroll=scroll;
        scroll.setFillViewport(true);scroll.setBackgroundColor(BACKGROUND);scroll.setClipToPadding(false);
        if(Build.VERSION.SDK_INT>=30){
            getWindow().setDecorFitsSystemWindows(false);
            scroll.setOnApplyWindowInsetsListener((v,insets)-> {
                android.graphics.Insets bars=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());
                v.setPadding(bars.left,bars.top,bars.right,bars.bottom);
                return WindowInsets.CONSUMED;
            });
        }
        LinearLayout outer=new LinearLayout(this);outer.setOrientation(LinearLayout.VERTICAL);outer.setGravity(Gravity.TOP|Gravity.CENTER_HORIZONTAL);
        outer.setPadding(dp(19),dp(14),dp(19),dp(20));scroll.addView(outer);
        LinearLayout page=new LinearLayout(this);page.setOrientation(LinearLayout.VERTICAL);
        int available=getResources().getDisplayMetrics().widthPixels-dp(38);
        outer.addView(page,new LinearLayout.LayoutParams(Math.max(dp(220),Math.min(dp(500),available)),-2));
        setContentView(scroll);

        LinearLayout header=new LinearLayout(this);header.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout title=new LinearLayout(this);title.setOrientation(LinearLayout.VERTICAL);
        TextView eyebrow=text("POCKET NEST",11,GOLD);eyebrow.setLetterSpacing(.19f);
        title.addView(eyebrow);
        TextView name=text("Corbie",42,CREAM);name.setTypeface(Typeface.create("serif",Typeface.BOLD));name.setIncludeFontPadding(false);
        title.addView(name);
        header.addView(title,new LinearLayout.LayoutParams(0,-2,1));
        ImageView badge=new ImageView(this);badge.setImageResource(R.drawable.corbie_badge);
        badge.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);header.addView(badge,new LinearLayout.LayoutParams(dp(57),dp(57)));
        add(page,header,0,-2);
        add(page,text("Small raven. Enormous opinions.",15,MUTED),5,-2);
        LinearLayout stats=new LinearLayout(this);stats.setGravity(Gravity.CENTER_VERTICAL);
        mood=text("",13,GOLD);counts=text("",13,MUTED);counts.setGravity(Gravity.END);
        stats.addView(mood,new LinearLayout.LayoutParams(0,-2,1));stats.addView(counts,new LinearLayout.LayoutParams(-2,-2));
        add(page,stats,15,-2);
        bird=new PocketBirdView(this,state,()->NestStore.savePerch(this,state));
        bird.setOnClickListener(v->act(NestState.Action.GREET,PocketBirdView.Pose.HOP,
                "Oh, hello. I was very busy doing absolutely nothing.",
                "Yes, it is me. Smaller screen, same enormous talent.",
                "Oi. I am available for admiration."));
        bird.setOnLongClickListener(v->{act(NestState.Action.SCRITCH,PocketBirdView.Pose.SCRITCH,
                "Fine. You may continue. For strictly scientific reasons.",
                "Oh. That’s the spot. This information is classified.",
                "You saw nothing. Especially not me enjoying that.");return true;});
        add(page,bird,10,dp(263));
        add(page,text("Tap to greet · Hold for scritches · Drag to perch",12,MUTED),7,-2);
        remark=text("Oh. You found the pocket nest.",17,0xff342638);
        remark.setPadding(dp(13),dp(10),dp(13),dp(10));remark.setBackground(shape(CREAM,Color.TRANSPARENT,15));
        remark.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);add(page,remark,10,-2);
        LinearLayout treats=row(page,9);
        button(treats,"Offer treat",true,()->act(NestState.Action.TREAT,PocketBirdView.Pose.NIBBLE,
                "A treat! I accept this entirely unbiased tribute.",
                "Excellent. Your services to raven-kind have been noted.",
                "Bribery? I prefer to call it diplomacy."));
        button(treats,"Pocket a shiny",false,()->act(NestState.Action.SHINY,PocketBirdView.Pose.SHINY,
                "Mine. In the warm, community-minded sense of the word.",
                "Ooh. Shiny. My very sophisticated collection grows.",
                "Acquired! We will speak no further of its provenance."));
        LinearLayout tricks=row(page,2);
        button(tricks,"Wave",false,()->trick(PocketBirdView.Pose.WAVE,"Hello, tiny audience. Yes, the wing is entirely natural."));
        button(tricks,"Take a bow",false,()->trick(PocketBirdView.Pose.BOW,"Thank you. I accept applause, shiny objects, and absolutely none of your constructive criticism."));
        button(row(page,2),"TikTok stage",false,this::showTikTokStage);
        LinearLayout quiet=row(page,2);
        rest=button(quiet,"Rest",false,()-> {
            boolean waking=state.resting;
            act(waking?NestState.Action.WAKE:NestState.Action.REST,PocketBirdView.Pose.IDLE,
                    waking?"I'm awake. The world may resume being mildly disappointing.":"Shh. Even legends need to roost.");
        });
        button(quiet,"Say something",false,()-> {
            if(state.resting)NestStore.act(this,NestState.Action.WAKE);
            remark.setText(voice.saySomething());
        });
        add(page,text("OUTSIDE THE NEST",11,GOLD),18,-2);
        LinearLayout floating=row(page,6);
        button(floating,"Let Corbie out",true,this::letCorbieOut);
        button(floating,"Bring him home",false,()->stopService(new Intent(this,FloatingCorbieService.class)));
        add(page,text("Drag him to a perch, tap for a wave, or hold for his menu.",12,MUTED),7,-2);
        add(page,text("PHONE TOOLS",11,GOLD),18,-2);
        LinearLayout phoneTools = row(page,6);
        button(phoneTools,"Perch timer",false,this::showPerchTimer);
        button(phoneTools,"Clean app cache",false,()->AppCacheCleanup.show(this));
        refresh();
    }

    private void showTikTokStage() {
        voice.close();
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setPadding(dp(16), 0, dp(16), dp(12));
        PocketBirdView stage = new PocketBirdView(this, new NestState(0, 0, 0, false, .5f, .8f), () -> {});
        stage.setEnabled(false);
        stage.setFocusable(false);
        stage.setContentDescription("Corbie on his moonlit stage");
        add(panel, stage, 0, dp(224));
        TextView cue = text("Start your recording, then cue Corbie.", 16, CREAM);
        cue.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        add(panel, cue, 10, -2);
        LinearLayout controls = row(panel, 8);
        PocketVoice showVoice = new PocketVoice(this, stage::setSpeaking);
        Button take = button(controls, "Start take", true, null);
        Runnable enableTake = () -> take.setEnabled(true);
        Runnable countdown = new Runnable() {
            private int remaining = 3;

            @Override public void run() {
                if (showDialog == null || !showDialog.isShowing()) return;
                if (remaining > 0) {
                    cue.setText(Integer.toString(remaining--));
                    stage.postDelayed(this, 1000);
                    return;
                }
                remaining = 3;
                cue.setText(PocketVoice.TIKTOK_GREETING);
                stage.play(PocketBirdView.Pose.WAVE);
                showVoice.sayTikTokGreeting(() -> {
                    stage.play(PocketBirdView.Pose.BOW);
                    take.setText("Another take");
                    stage.postDelayed(enableTake, 2000);
                });
            }
        };
        take.setOnClickListener(view -> {
            take.setEnabled(false);
            countdown.run();
        });
        ScrollView scroll = new ScrollView(this);
        scroll.addView(panel);
        showDialog = new AlertDialog.Builder(this).setTitle("Hello, TikTok")
                .setView(scroll).setNegativeButton("Close", null).create();
        showDialog.setOnDismissListener(dialog -> {
            stage.removeCallbacks(countdown);
            stage.removeCallbacks(enableTake);
            stage.setActive(false);
            showVoice.close();
            showDialog = null;
        });
        showDialog.show();
        stage.setActive(true);
    }

    private void showPerchTimer() {
        PerchTimerPanel panel = new PerchTimerPanel(this,
                () -> openTimerSettings(new Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS)
                        .putExtra(Settings.EXTRA_APP_PACKAGE, getPackageName())),
                () -> openTimerSettings(new Intent(Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM,
                        Uri.fromParts("package", getPackageName(), null))));
        int padding = dp(20);
        panel.setPadding(padding, padding, padding, 0);
        ScrollView scroll = new ScrollView(this);
        scroll.addView(panel);
        timerDialog = new AlertDialog.Builder(this).setTitle("Perch timer")
                .setView(scroll).setNegativeButton("Close", null).create();
        timerDialog.setOnDismissListener(dialog -> timerDialog = null);
        timerDialog.show();
    }

    private void openTimerSettings(Intent intent) {
        try {
            startActivity(intent);
        } catch (ActivityNotFoundException | SecurityException e) {
            Toast.makeText(this, "Couldn't open Android's timer settings.", Toast.LENGTH_SHORT).show();
        }
    }

    private void timerFinished(boolean silent) {
        if (timerDialog != null) timerDialog.dismiss();
        nestScroll.smoothScrollTo(0, 0);
        bird.play(PocketBirdView.Pose.WAVE);
        remark.setText(PerchTimer.FINISHED_LINE);
        if (!silent) voice.sayTimerFinished();
    }

    private void letCorbieOut(){
        if(!Settings.canDrawOverlays(this)){
            Toast.makeText(this,"Allow Corbie to display over other apps, then come back and tap Let Corbie out.",Toast.LENGTH_LONG).show();
            try {
                startActivity(new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                        Uri.fromParts("package",getPackageName(),null)));
            } catch (ActivityNotFoundException e) {
                Toast.makeText(this,"This phone couldn't open its overlay settings.",Toast.LENGTH_LONG).show();
            }
            return;
        }
        try {
            startForegroundService(new Intent(this,FloatingCorbieService.class));
        } catch (IllegalStateException | SecurityException e) {
            Toast.makeText(this,"Corbie couldn't leave the Nest.",Toast.LENGTH_SHORT).show();
        }
    }

    private void act(NestState.Action action,PocketBirdView.Pose pose,String... lines){
        NestStore.act(this,action);bird.play(pose);
        if(action!=NestState.Action.REST)bird.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK);
        String line=lines[random.nextInt(lines.length)];
        if(lines.length>1 && line.equals(lastLine))for(String alternate:lines)if(!alternate.equals(lastLine)){line=alternate;break;}
        lastLine=line;remark.setText(line);
    }
    private void trick(PocketBirdView.Pose pose,String line){act(NestState.Action.WAKE,pose,line);}
    private void refreshCare(){
        state.updateCare(NestStore.load(this));
        bird.invalidate();
        refresh();
    }
    private void refresh(){mood.setText(state.mood());counts.setText(state.shinies+(state.shinies==1?" shiny":" shinies")+"  ·  Bond "+state.bond);rest.setText(state.resting?"Wake Corbie":"Let him roost");}
    @Override protected void onResume(){
        super.onResume();
        NestStore.prefs(this).registerOnSharedPreferenceChangeListener(careChanged);
        refreshCare();
        bird.setActive(true);
        PerchTimer timer = PerchTimer.get(this);
        timer.nest = this::timerFinished;
        timer.refresh();
    }
    @Override protected void onPause(){
        if(showDialog!=null)showDialog.dismiss();
        PerchTimer.get(this).nest = null;
        NestStore.prefs(this).unregisterOnSharedPreferenceChangeListener(careChanged);
        bird.setActive(false);
        voice.close();
        super.onPause();
    }

    private int dp(float v){return Math.round(v*getResources().getDisplayMetrics().density);}
    private TextView text(String value,int size,int color){TextView v=new TextView(this);v.setText(value);v.setTextSize(size);v.setTextColor(color);return v;}
    private void add(LinearLayout parent,View view,int top,int height){LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,height);p.topMargin=dp(top);parent.addView(view,p);}
    private LinearLayout row(LinearLayout parent,int top){LinearLayout row=new LinearLayout(this);row.setGravity(Gravity.CENTER_VERTICAL);add(parent,row,top,-2);return row;}
    private GradientDrawable shape(int color,int border,int radius){
        GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(radius));d.setStroke(dp(1),border);return d;
    }
    private Button button(LinearLayout row,String label,boolean primary,Runnable action){
        Button v=new Button(this);
        v.setText(label);v.setAllCaps(false);v.setTextSize(14);v.setTextColor(primary?0xff312233:CREAM);
        v.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));
        v.setMinHeight(dp(48));v.setPadding(dp(8),dp(6),dp(8),dp(6));
        v.setStateListAnimator(null);
        v.setBackground(new RippleDrawable(ColorStateList.valueOf(0x307a5069),shape(primary?GOLD:0xff403045,0xff856677,13),null));
        LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(0,-2,1);if(row.getChildCount()>0)p.leftMargin=dp(7);
        row.addView(v,p);if(action!=null)v.setOnClickListener(w->action.run());return v;
    }
}
