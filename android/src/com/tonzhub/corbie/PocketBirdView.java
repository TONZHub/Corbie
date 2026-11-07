package com.tonzhub.corbie;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.graphics.Shader;
import android.graphics.Typeface;
import android.os.SystemClock;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewConfiguration;
import android.view.accessibility.AccessibilityNodeInfo;
import android.widget.Button;
import java.util.function.BiConsumer;

final class PocketBirdView extends View {
    enum Pose { IDLE, HOP, FLIGHT, SCRITCH, WAVE, BOW, NIBBLE, SHINY }
    private static final int INK = 0xff29283e, FEATHER = 0xff25243b;
    private final NestState state;
    private final Runnable savePerch;
    private final BiConsumer<Float,Float> moveWindow;
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF card = new RectF();
    private final Path clip = new Path(), branch = new Path();
    private final Path tail = polygon(69,125,45,149,63,143,62,154,86,136);
    private final Path crest = polygon(91,51,88,34,101,45,108,32,115,46);
    private final Path lowerBeak = polygon(133,77,153,78,135,83);
    private final Path wing = new Path(), wingLine = new Path(), upperBeak = new Path(), feet = new Path();
    private final Typeface labelFont = Typeface.create("sans-serif-medium", Typeface.NORMAL);
    private final float slop;
    private Shader sky;
    private Pose pose = Pose.IDLE;
    private long start;
    private float flightProgress;
    private int facing = 1, pointer = -1;
    private boolean active, dragging, held, speaking;
    private float downX, downY, grabX, grabY, gaze;
    private float lastX, lastY;
    private final Runnable hold = () -> {
        if (pointer != -1 && !dragging) {
            held = performLongClick();
        }
    };

    PocketBirdView(Context context, NestState state, Runnable savePerch) {
        this(context,state,savePerch,null);
    }

    PocketBirdView(Context context, NestState state, Runnable savePerch, BiConsumer<Float,Float> moveWindow) {
        super(context);
        this.state = state;
        this.savePerch = savePerch;
        this.moveWindow = moveWindow;
        slop = ViewConfiguration.get(context).getScaledTouchSlop();
        setContentDescription(moveWindow==null
                ? "Corbie. Tap to greet him, hold for scritches, or drag to move his perch."
                : "Corbie. Tap for a wave, hold for his menu, or drag to move him.");
        setFocusable(true);
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_YES);
        wing.moveTo(74,86); wing.cubicTo(43,90,48,128,78,137);
        wing.cubicTo(69,120,94,107,74,86); wing.close();
        wingLine.moveTo(63,105); wingLine.quadTo(62,119,76,129);
        upperBeak.moveTo(130,61); upperBeak.quadTo(149,62,158,76); upperBeak.lineTo(133,77); upperBeak.close();
        feet.moveTo(92,136); feet.lineTo(90,147); feet.lineTo(81,148);
        feet.moveTo(90,147); feet.lineTo(99,149);
        feet.moveTo(110,136); feet.lineTo(109,147); feet.lineTo(103,150);
        feet.moveTo(109,147); feet.lineTo(119,149);
    }

    void play(Pose next) { pose = next; start = SystemClock.uptimeMillis(); invalidate(); }
    void fly(float progress) { pose = Pose.FLIGHT; flightProgress = progress; invalidate(); }
    void setActive(boolean value) { active = value; if (!value) endTouch(); invalidate(); }
    void setSpeaking(boolean value) { speaking=value; invalidate(); }
    boolean isInteracting() { return pointer != -1; }
    void face(int direction) { facing=direction; invalidate(); }
    private float dp(float value) { return value * getResources().getDisplayMetrics().density; }
    private float scale() {
        return moveWindow==null ? Math.min(getWidth()/230f,getHeight()/205f)
                : Math.min(getWidth()/152f,getHeight()/149f);
    }
    private float x() {
        if(moveWindow!=null) return getWidth()/2f;
        float s=scale();return bound(getWidth()*state.perchX,86*s,getWidth()-86*s);
    }
    private float y() {
        if(moveWindow!=null) return getHeight()-8*scale();
        float s=scale();return bound(getHeight()*state.perchY,143*s,getHeight()-19*s);
    }
    private static float bound(float value, float min, float max) { return Math.max(min, Math.min(max, value)); }
    private static float sin(double value) { return (float)Math.sin(value); }
    private static Path polygon(float... xy) {
        Path p = new Path(); p.moveTo(xy[0], xy[1]);
        for(int i=2;i<xy.length;i+=2) p.lineTo(xy[i],xy[i+1]);
        p.close(); return p;
    }

    @Override protected void onSizeChanged(int width, int height, int oldWidth, int oldHeight) {
        if(moveWindow!=null) return;
        card.set(0,0,width,height);
        clip.reset(); clip.addRoundRect(card,dp(24),dp(24),Path.Direction.CW);
        sky = new LinearGradient(0,0,0,Math.max(1,height),0xff6b587f,0xff947382,Shader.TileMode.CLAMP);
    }

    @Override protected void onDraw(Canvas c) {
        super.onDraw(c);
        if (getWidth()==0 || getHeight()==0) return;
        float s=scale(), cx=x(), cy=y();
        long now=SystemClock.uptimeMillis();
        float t=(now-start)/1000f;
        float duration=pose==Pose.HOP?.58f:pose==Pose.WAVE?1.6f:pose==Pose.BOW?2f:pose==Pose.SHINY?1.4f:1f;
        if(pose!=Pose.FLIGHT && t>=duration) pose=Pose.IDLE;
        float progress=pose==Pose.FLIGHT?flightProgress:pose==Pose.IDLE?0:t/duration;
        float bell=sin(Math.PI*progress), breathe=sin(now/590.0)*.6f;
        float hop=pose==Pose.HOP?bell*18:0;
        if(moveWindow!=null){
            drawAtPerch(c,cx,cy-hop*s,s,now,progress,bell,breathe);
            if(pose==Pose.SHINY || pose==Pose.SCRITCH){
                fill(0xfff4ce91);
                star(c,cx+facing*53*s,cy-(75+24*progress)*s,4*s);
            }
            if(active && isShown()) postInvalidateDelayed(state.resting && !speaking?80:33);
            return;
        }
        c.save();c.clipPath(clip);
        paint.setShader(sky);c.drawRect(card,paint);paint.setShader(null);
        fill(0x24f8d9a5);c.drawCircle(getWidth()*.78f,getHeight()*.22f,dp(42),paint);
        fill(0xfff6d9ae);c.drawCircle(getWidth()*.78f,getHeight()*.22f,dp(24),paint);
        for(int i=0;i<9;i++){
            fill(i%2==0?0x99ffeac8:0x55ffeac8);
            c.drawCircle(getWidth()*(.10f+((i*17)%77)/100f),getHeight()*(.18f+((i*11)%37)/100f),dp(i%3==0?1.4f:.85f),paint);
        }
        branch.reset();branch.moveTo(0,cy+dp(8));
        branch.cubicTo(getWidth()*.35f,cy+dp(8),getWidth()*.67f,cy-dp(7),getWidth(),cy+dp(3));
        branch.lineTo(getWidth(),getHeight());branch.lineTo(0,getHeight());branch.close();
        fill(0xff302331);c.drawPath(branch,paint);
        fill(0x44471928);c.drawOval(cx-47*s,cy-1*s,cx+37*s,cy+8*s,paint);
        stroke(0xff987864,2*s);c.drawLine(cx-55*s,cy+3*s,cx+43*s,cy+3*s,paint);
        fill(0xfff5d8ae);paint.setTypeface(labelFont);paint.setTextSize(dp(11));
        c.drawText(state.resting?"MOONLIGHT ROOST":"THE PERCH",dp(17),dp(27),paint);
        drawAtPerch(c,cx,cy-hop*s,s,now,progress,bell,breathe);
        if(pose==Pose.SHINY || pose==Pose.SCRITCH){
            fill(0xfff4ce91);float rise=progress*28*s;
            star(c,cx+facing*54*s,cy-82*s-rise,5*s);
            star(c,cx+facing*64*s,cy-71*s-rise,3*s);
        }
        if(state.resting){
            fill(0xfff5d8ae);paint.setTextSize(dp(15));paint.setTypeface(labelFont);
            c.drawText("z z z",bound(cx+35*s,dp(16),getWidth()-dp(53)),Math.max(dp(45),cy-119*s),paint);
        }
        c.restore();
        stroke(0xff856677,dp(1));c.drawRoundRect(.5f,.5f,getWidth()-.5f,getHeight()-.5f,dp(24),dp(24),paint);
        if(active && isShown()) postInvalidateDelayed(state.resting?80:33);
    }

    private void drawAtPerch(Canvas c,float x,float y,float scale,long now,float progress,float bell,float breathe) {
        c.save();c.translate(x,y);c.scale(scale*facing,scale);c.translate(-100,-149);
        drawBird(c,now,progress,bell,breathe);
        c.restore();
    }

    private void drawBird(Canvas c,long now,float progress,float bell,float breathe) {
        float tuck=pose==Pose.FLIGHT?bell:0;
        c.save();
        c.scale(1,1-.62f*tuck,100,136);
        stroke(0xffb99581,3);c.drawPath(feet,paint);
        c.restore();
        c.save();
        float nod=state.resting?sin(now/1230.0)*4:0;
        c.translate(0,state.resting?breathe+1.5f:breathe);
        c.rotate(13*tuck*(progress-.5f),98,110);
        float bow=pose==Pose.BOW?bell*bell:0;
        c.rotate(26*bow,97,136);
        fill(FEATHER);c.drawPath(tail,paint);c.drawOval(62,70,127,142,paint);
        fill(0xff32314b);c.drawOval(82,83,122,135,paint);
        c.save();
        float wingAngle=0;
        if(pose==Pose.WAVE) wingAngle=Math.min(1,bell*2.5f)*(123+17*sin(progress*20));
        if(pose==Pose.FLIGHT) wingAngle=Math.min(1,bell*2)*(49+35*sin(progress*8*Math.PI));
        c.rotate(wingAngle+bow*18,73,91);
        fill(0xff171829);c.drawPath(wing,paint);
        stroke(0xff45425d,2);c.drawPath(wingLine,paint);c.restore();
        c.save();c.rotate(24*bow+(state.resting?19+nod:0)+gaze+(pose==Pose.SCRITCH?-10*bell:0),105,83);
        fill(INK);c.drawOval(86,43,138,96,paint);c.drawPath(crest,paint);
        fill(0xffd3a575);c.drawPath(upperBeak,paint);
        c.save();
        if(speaking)c.rotate(17*Math.abs(sin(now/68.0)),133,77);
        else if(pose==Pose.NIBBLE)c.rotate(12*Math.abs(sin(progress*20)),133,77);
        fill(0xff986a57);c.drawPath(lowerBeak,paint);c.restore();
        boolean closed=state.resting?nod>2:now%5300>5130 || pose==Pose.SCRITCH && bell>.3 || bow>.45;
        fill(0xfff8ddab);
        c.drawOval(119,closed?66:state.resting?65:60,128,closed?68:71,paint);
        if(!closed){
            fill(Color.WHITE);
            if(state.resting)c.drawOval(123,66,125,68,paint);
            else c.drawOval(123,62,126,66,paint);
        }
        c.restore();c.restore();
    }
    private void star(Canvas c,float x,float y,float size) {
        c.drawOval(x-size/3,y-size,x+size/3,y+size,paint);
        c.drawOval(x-size,y-size/3,x+size,y+size/3,paint);
    }
    private void fill(int color) { paint.setStyle(Paint.Style.FILL);paint.setColor(color); }
    private void stroke(int color,float width) { paint.setStyle(Paint.Style.STROKE);paint.setColor(color);paint.setStrokeWidth(width);paint.setStrokeCap(Paint.Cap.ROUND);paint.setStrokeJoin(Paint.Join.ROUND); }

    @Override public boolean onTouchEvent(MotionEvent e) {
        if(!isEnabled()) return false;
        switch(e.getActionMasked()){
            case MotionEvent.ACTION_DOWN:
                float s=scale();
                if(s<=0 || Math.abs(e.getX()-x())>77*s || e.getY()<y()-143*s || e.getY()>y()+16*s) return false;
                pointer=e.getPointerId(0);downX=e.getX();downY=e.getY();grabX=x()-downX;grabY=y()-downY;
                if(moveWindow!=null){downX=lastX=e.getRawX();downY=lastY=e.getRawY();}
                dragging=false;held=false;gaze=bound((downX-x())/s*.12f,-8,8)*facing;
                if(moveWindow!=null) gaze=bound((e.getX()-x())/s*.12f,-8,8)*facing;
                getParent().requestDisallowInterceptTouchEvent(true);
                postDelayed(hold,ViewConfiguration.getLongPressTimeout());invalidate();return true;
            case MotionEvent.ACTION_MOVE:
                int i=e.findPointerIndex(pointer);if(i<0)return false;
                float px=e.getX(i),py=e.getY(i);
                if(moveWindow!=null){
                    px+=e.getRawX()-e.getX();py+=e.getRawY()-e.getY();
                }
                if(!held && !dragging && Math.hypot(px-downX,py-downY)>slop){
                    dragging=true;removeCallbacks(hold);getParent().requestDisallowInterceptTouchEvent(true);
                }
                if(dragging){
                    if(moveWindow!=null){
                        float dx=px-lastX;
                        if(Math.abs(dx)>dp(2))facing=dx>0?1:-1;
                        moveWindow.accept(dx,py-lastY);
                        lastX=px;lastY=py;
                    }else{
                        float old=x();state.perch((px+grabX)/getWidth(),(py+grabY)/getHeight());
                        if(Math.abs(x()-old)>dp(2))facing=x()>old?1:-1;
                    }
                    invalidate();
                }
                return true;
            case MotionEvent.ACTION_POINTER_UP:
                if(e.getPointerId(e.getActionIndex())==pointer)endTouch();return true;
            case MotionEvent.ACTION_UP:
                boolean click=pointer!=-1 && !dragging && !held;endTouch();if(click)performClick();return true;
            case MotionEvent.ACTION_CANCEL: endTouch();return true;
            default: return pointer!=-1;
        }
    }
    private void endTouch() {
        removeCallbacks(hold);
        if(dragging)savePerch.run();
        pointer=-1;dragging=false;held=false;gaze=0;
        if(getParent()!=null)getParent().requestDisallowInterceptTouchEvent(false);
    }
    @Override public boolean performClick(){return super.performClick();}
    @Override public void onInitializeAccessibilityNodeInfo(AccessibilityNodeInfo info){
        super.onInitializeAccessibilityNodeInfo(info);info.setClassName(Button.class.getName());
    }
    @Override protected void onDetachedFromWindow(){active=false;endTouch();super.onDetachedFromWindow();}
}
