package com.sketch.sdk.ads;

import android.animation.Animator;
import android.animation.AnimatorListenerAdapter;
import android.animation.ValueAnimator;
import android.app.Activity;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.PixelFormat;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.view.animation.LinearInterpolator;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import com.google.android.gms.ads.AdError;
import com.google.android.gms.ads.AdListener;
import com.google.android.gms.ads.AdapterResponseInfo;
import com.google.android.gms.ads.ResponseInfo;
import com.google.android.gms.ads.AdLoader;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.MediaContent;
import com.google.android.gms.ads.VideoController;
import com.google.android.gms.ads.nativead.MediaView;
import com.google.android.gms.ads.nativead.NativeAd;
import com.google.android.gms.ads.nativead.NativeAdOptions;
import com.google.android.gms.ads.nativead.NativeAdView;
import com.unity3d.player.UnityPlayer;

import java.util.ArrayList;
import java.util.List;

// Sketch SDK custom native layout for Android (in-rect mode). Same player-facing behaviour as the
// Knife Tower / Bounce native renderer: a white panel at an exact pixel rect in its own sub-window,
// media + icon/headline/advertiser/"Ad" badge + body + CTA, and a round countdown close (26 dp,
// 6 dp inside the top-right or top-left corner). The close ignores touches until the countdown ends,
// then only the user closes the ad. Unity talks to it through SketchAndroidNative (C#) via JNI;
// callbacks go back with UnityPlayer.UnitySendMessage. MobileAds must already be initialized.
@SuppressWarnings("deprecation")
public final class SketchAdmobNative {
    private static final String TAG = "SketchAdmobNative";
    private static final int CLOSE_SIZE_DP = 26;
    private static final int CLOSE_MARGIN_DP = 6;

    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static final Handler LOADER = new Handler(Looper.getMainLooper());
    private static final List<VideoController> VIDEOS = new ArrayList<>();

    private static volatile NativeAd ready;
    private static volatile NativeAd showing;
    private static volatile FrameLayout panel;
    private static volatile boolean loading;
    private static String unitId;
    private static String receiver;
    private static int failures;
    private static long nextLoadAt;

    private static View closeHit;
    private static CloseRing closeRing;
    private static ValueAnimator countdown;
    private static boolean closeEnabled;
    private static boolean playbackReleased;

    private SketchAdmobNative() {}

    // ---- Unity entry points ----

    public static void setReceiver(String name) {
        if (name != null && name.length() > 0) receiver = name;
    }

    public static void load(String adUnitId) {
        if (adUnitId != null && adUnitId.length() > 0) unitId = adUnitId;
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            LOADER.removeCallbacksAndMessages(null);
            loadNow(activity);
        });
    }

    public static boolean isReady() {
        return ready != null && panel == null;
    }

    public static boolean isShowing() {
        return panel != null;
    }

    public static String status() {
        return "ready=" + (ready != null) + " showing=" + (panel != null) + " loading=" + loading
                + " failures=" + failures + " unit=" + (unitId != null);
    }

    // x/y: top-left corner in Android window pixels; width/height in pixels.
    public static boolean showInRect(final int x, final int y, final int width, final int height, final String placement) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null || ready == null || panel != null) return false;
        activity.runOnUiThread(() -> showInRectNow(activity, x, y, width, height, placement));
        return true;
    }

    // Countdown on the close (seconds; 0 = enabled at once). showCountdown=false hides the close until it is enabled.
    // closeOnLeft moves the close to the top-left corner (Unity alternates 3 right : 1 left).
    public static void startCloseCountdown(final int seconds, final boolean showCountdown, final boolean closeOnLeft) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) return;
        activity.runOnUiThread(() -> beginCountdown(Math.max(0, seconds), showCountdown, closeOnLeft));
    }

    // Native video stays paused until the panel is really on screen; then the first video may play with audio.
    public static void releasePlayback() {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            playbackReleased = true;
            boolean audio = true;
            for (VideoController video : new ArrayList<>(VIDEOS)) {
                if (video == null) continue;
                video.mute(!audio);
                audio = false;
                try { video.play(); } catch (Exception e) { Log.w(TAG, "video play failed", e); }
            }
        });
    }

    // Programmatic hide (full-screen ad, teardown, pause): no OnNativeClosed callback, the caller already knows.
    public static void hide() {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) return;
        activity.runOnUiThread(() -> removePanel(activity, false));
    }

    // ---- loading ----

    private static void loadNow(final Activity activity) {
        if (unitId == null || loading || ready != null) return;
        long wait = nextLoadAt - SystemClock.elapsedRealtime();
        if (wait > 0) { scheduleLoad(wait); return; }
        loading = true;
        NativeAdOptions options = new NativeAdOptions.Builder()
                .setAdChoicesPlacement(NativeAdOptions.ADCHOICES_TOP_LEFT)
                .setMediaAspectRatio(NativeAdOptions.NATIVE_MEDIA_ASPECT_RATIO_ANY)
                .build();
        new AdLoader.Builder(activity, unitId)
                .forNativeAd(ad -> {
                    loading = false;
                    failures = 0;
                    nextLoadAt = 0L;
                    if (ready != null) ready.destroy();
                    ready = ad;
                    bindPaid(ad, "");
                    send("OnNativeLoaded", "");
                })
                .withNativeAdOptions(options)
                .withAdListener(new AdListener() {
                    @Override public void onAdFailedToLoad(LoadAdError error) {
                        loading = false;
                        failures++;
                        long delay = Math.min(60000L, 10000L * (1L << (Math.max(1, Math.min(failures, 4)) - 1)));
                        nextLoadAt = SystemClock.elapsedRealtime() + delay;
                        send("OnNativeLoadFailed", error.getCode() + "|" + clean(error.getMessage()) + "|" + clean(error.getDomain())
                                + "|" + responseIdOf(error) + "|" + adaptersOf(error));
                        scheduleLoad(delay);
                    }
                    @Override public void onAdClicked() {
                        send("OnNativeClicked", "");
                    }
                })
                .build()
                .loadAd(new AdRequest.Builder().build());
    }

    private static void scheduleLoad(long delayMs) {
        LOADER.removeCallbacksAndMessages(null);
        LOADER.postDelayed(() -> {
            Activity activity = UnityPlayer.currentActivity;
            if (activity != null) activity.runOnUiThread(() -> loadNow(activity));
        }, Math.max(1L, delayMs));
    }

    private static void bindPaid(final NativeAd ad, final String placement) {
        ad.setOnPaidEventListener(value -> {
            String responseId = "";
            String source = "AdMob";
            try {
                if (ad.getResponseInfo() != null) {
                    responseId = ad.getResponseInfo().getResponseId();
                    if (ad.getResponseInfo().getLoadedAdapterResponseInfo() != null
                            && ad.getResponseInfo().getLoadedAdapterResponseInfo().getAdSourceName() != null)
                        source = ad.getResponseInfo().getLoadedAdapterResponseInfo().getAdSourceName();
                }
            } catch (Exception e) { Log.w(TAG, "response info", e); }
            send("OnNativePaid", value.getValueMicros() + "|" + value.getCurrencyCode() + "|" + value.getPrecisionType()
                    + "|" + clean(responseId) + "|" + clean(source) + "|" + clean(placement));
        });
    }

    // ---- show ----

    private static void showInRectNow(Activity activity, int x, int y, int width, int height, String placement) {
        if (ready == null || panel != null) return;
        showing = ready;
        ready = null;
        bindPaid(showing, placement);
        loadNow(activity); // fetch the next one while this one is on screen

        playbackReleased = false;
        VIDEOS.clear();

        FrameLayout root = new FrameLayout(activity);
        root.setBackgroundColor(Color.WHITE);
        panel = root;
        root.addView(buildAdView(activity, showing), new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        int size = dp(activity, CLOSE_SIZE_DP);
        View close = buildClose(activity, size);
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(size, size);
        lp.gravity = Gravity.TOP | Gravity.END;
        lp.topMargin = dp(activity, CLOSE_MARGIN_DP);
        lp.rightMargin = dp(activity, CLOSE_MARGIN_DP);
        root.addView(close, lp);

        addWindow(activity, root, Math.max(1, width), Math.max(1, height), x, y);
        send("OnNativeShown", "1");
    }

    // Own sub-window on the Activity token: the media surface composites reliably above Unity's surface, no
    // overlay permission needed; NOT_TOUCH_MODAL lets touches outside the panel reach the game.
    private static void addWindow(Activity activity, View view, int width, int height, int x, int y) {
        WindowManager.LayoutParams lp = new WindowManager.LayoutParams();
        lp.type = WindowManager.LayoutParams.TYPE_APPLICATION_PANEL;
        lp.token = activity.getWindow().getDecorView().getWindowToken();
        lp.width = width;
        lp.height = height;
        lp.gravity = Gravity.TOP | Gravity.START;
        lp.x = x;
        lp.y = y;
        lp.format = PixelFormat.TRANSLUCENT;
        if (Build.VERSION.SDK_INT >= 28) lp.layoutInDisplayCutoutMode = Build.VERSION.SDK_INT >= 30 ? 3 : 1;
        lp.flags = WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN
                | WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                | WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL
                | WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED;
        activity.getWindowManager().addView(view, lp);
    }

    private static NativeAdView buildAdView(Activity activity, final NativeAd ad) {
        final NativeAdView adView = new NativeAdView(activity);
        adView.setBackgroundColor(Color.WHITE);

        LinearLayout column = new LinearLayout(activity);
        column.setOrientation(LinearLayout.VERTICAL);
        int pad = dp(activity, 8);
        column.setPadding(pad, pad, pad, pad);
        adView.addView(column, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        final MediaView media = new MediaView(activity);
        media.setBackgroundColor(Color.WHITE);
        media.setImageScaleType(ImageView.ScaleType.CENTER_CROP);
        column.addView(media, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));
        adView.setMediaView(media);

        LinearLayout info = new LinearLayout(activity);
        info.setOrientation(LinearLayout.HORIZONTAL);
        info.setGravity(Gravity.CENTER_VERTICAL);
        info.setPadding(0, dp(activity, 8), 0, dp(activity, 4));
        column.addView(info, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        ImageView icon = new ImageView(activity);
        icon.setScaleType(ImageView.ScaleType.CENTER_CROP);
        info.addView(icon, new LinearLayout.LayoutParams(dp(activity, 64), dp(activity, 64)));
        adView.setIconView(icon);
        if (ad.getIcon() != null) icon.setImageDrawable(ad.getIcon().getDrawable());
        else icon.setVisibility(View.GONE);

        LinearLayout texts = new LinearLayout(activity);
        texts.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams textsLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
        textsLp.leftMargin = dp(activity, 10);
        info.addView(texts, textsLp);

        TextView headline = label(activity, 20, Color.DKGRAY, Typeface.BOLD);
        headline.setText(ad.getHeadline());
        texts.addView(headline);
        adView.setHeadlineView(headline);

        TextView advertiser = label(activity, 14, Color.GRAY, Typeface.NORMAL);
        advertiser.setText(ad.getAdvertiser());
        texts.addView(advertiser);
        adView.setAdvertiserView(advertiser);
        if (ad.getAdvertiser() == null) advertiser.setVisibility(View.GONE);

        int green = Color.rgb(30, 110, 40);
        TextView badge = label(activity, 12, green, Typeface.BOLD);
        badge.setText("Ad");
        badge.setGravity(Gravity.CENTER);
        GradientDrawable outline = new GradientDrawable();
        outline.setColor(Color.TRANSPARENT);
        outline.setStroke(dp(activity, 1), green);
        outline.setCornerRadius(dp(activity, 4));
        badge.setBackground(outline);
        info.addView(badge, new LinearLayout.LayoutParams(dp(activity, 34), dp(activity, 26)));

        TextView body = label(activity, 16, Color.DKGRAY, Typeface.NORMAL);
        body.setText(ad.getBody());
        body.setPadding(0, dp(activity, 4), 0, dp(activity, 8));
        column.addView(body, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        adView.setBodyView(body);
        if (ad.getBody() == null) body.setVisibility(View.GONE);

        Button cta = new Button(activity);
        cta.setText(ad.getCallToAction());
        cta.setTextColor(Color.WHITE);
        cta.setTextSize(18);
        cta.setBackgroundColor(Color.rgb(66, 133, 244));
        column.addView(cta, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(activity, 58)));
        adView.setCallToActionView(cta);
        if (ad.getCallToAction() == null) cta.setVisibility(View.GONE);

        MediaContent content = ad.getMediaContent();
        final VideoController video = content != null ? content.getVideoController() : null;
        if (video != null && content.hasVideoContent()) {
            VIDEOS.add(video);
            video.mute(true);
            video.setVideoLifecycleCallbacks(new VideoController.VideoLifecycleCallbacks() {
                @Override public void onVideoStart() { if (!playbackReleased) video.pause(); }
                @Override public void onVideoPlay() { if (!playbackReleased) video.pause(); }
            });
        }

        // Bind after the MediaView is attached and measured, then force one layout pass (black video surface on some
        // vendor/Vulkan combinations otherwise).
        adView.post(() -> {
            if (panel == null || adView.getWindowToken() == null) return;
            adView.setNativeAd(ad);
            media.postDelayed(() -> {
                if (media.getWindowToken() == null) return;
                media.setVisibility(View.GONE);
                media.post(() -> {
                    if (media.getWindowToken() == null) return;
                    media.setVisibility(View.VISIBLE);
                    media.requestLayout();
                    media.invalidate();
                });
            }, 150L);
        });
        return adView;
    }

    // ---- close ----

    private static View buildClose(final Activity activity, int size) {
        FrameLayout hit = new FrameLayout(activity);
        closeRing = new CloseRing(activity);
        hit.addView(closeRing, new FrameLayout.LayoutParams(size, size, Gravity.CENTER));
        closeEnabled = false;
        hit.setOnClickListener(v -> { if (closeEnabled) removePanel(activity, true); });
        // Not clickable while waiting: touches fall through to the ad underneath.
        hit.setClickable(false);
        closeHit = hit;
        return hit;
    }

    private static void beginCountdown(int seconds, boolean showCountdown, boolean onLeft) {
        if (closeHit == null || panel == null) return;
        MAIN.removeCallbacksAndMessages(null);
        if (countdown != null) countdown.cancel();
        closeEnabled = false;
        closeHit.setClickable(false);
        placeClose(onLeft);
        if (seconds == 0) { enableClose(); return; }
        if (!showCountdown) {
            closeHit.setVisibility(View.INVISIBLE);
            MAIN.postDelayed(() -> {
                if (closeHit == null || panel == null) return;
                closeHit.setVisibility(View.VISIBLE);
                enableClose();
            }, seconds * 1000L);
            return;
        }
        closeRing.setReady(false);
        closeRing.setRemaining(1f);
        countdown = ValueAnimator.ofFloat(1f, 0f);
        countdown.setDuration(seconds * 1000L);
        countdown.setInterpolator(new LinearInterpolator());
        countdown.addUpdateListener(a -> { if (closeRing != null) closeRing.setRemaining((float) a.getAnimatedValue()); });
        countdown.addListener(new AnimatorListenerAdapter() {
            boolean cancelled;
            @Override public void onAnimationCancel(Animator a) { cancelled = true; }
            @Override public void onAnimationEnd(Animator a) {
                countdown = null;
                if (!cancelled && closeHit != null && panel != null) enableClose();
            }
        });
        countdown.start();
    }

    private static void placeClose(boolean onLeft) {
        ViewGroup.LayoutParams raw = closeHit.getLayoutParams();
        if (!(raw instanceof FrameLayout.LayoutParams)) return;
        FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) raw;
        int margin = Math.max(lp.leftMargin, lp.rightMargin);
        lp.gravity = Gravity.TOP | (onLeft ? Gravity.START : Gravity.END);
        lp.leftMargin = onLeft ? margin : 0;
        lp.rightMargin = onLeft ? 0 : margin;
        closeHit.setLayoutParams(lp);
    }

    private static void enableClose() {
        if (closeRing != null) {
            closeRing.setRemaining(0f);
            closeRing.setReady(true);
        }
        closeEnabled = true;
        closeHit.setClickable(true);
    }

    private static void removePanel(Activity activity, boolean byUser) {
        MAIN.removeCallbacksAndMessages(null);
        if (countdown != null) { countdown.cancel(); countdown = null; }
        closeHit = null;
        closeRing = null;
        closeEnabled = false;
        boolean wasShowing = panel != null;
        if (panel != null) {
            try { activity.getWindowManager().removeViewImmediate(panel); }
            catch (Exception e) { Log.w(TAG, "panel already detached", e); }
            panel = null;
        }
        if (showing != null) { showing.destroy(); showing = null; }
        playbackReleased = false;
        VIDEOS.clear();
        if (byUser && wasShowing) send("OnNativeClosed", "user");
        loadNow(activity);
    }

    // ---- helpers ----

    private static void send(String method, String payload) {
        if (receiver != null && receiver.length() > 0) UnityPlayer.UnitySendMessage(receiver, method, payload);
    }

    private static String clean(String value) {
        return value == null ? "" : value.replace("|", "/");
    }

    private static String responseIdOf(LoadAdError error) {
        ResponseInfo info = error.getResponseInfo();
        return info == null ? "" : clean(info.getResponseId());
    }

    // "Source:code/latencyms;..." (ok when the source loaded), same shape as the C# SketchAdLoadDiagnostics.AdapterSummary.
    private static String adaptersOf(LoadAdError error) {
        ResponseInfo info = error.getResponseInfo();
        if (info == null) return "";
        StringBuilder sb = new StringBuilder();
        for (AdapterResponseInfo adapter : info.getAdapterResponses()) {
            if (sb.length() > 0) sb.append(';');
            AdError adapterError = adapter.getAdError();
            sb.append(clean(adapter.getAdSourceName()).replace(";", ",")).append(':')
              .append(adapterError == null ? "ok" : String.valueOf(adapterError.getCode()))
              .append('/').append(adapter.getLatencyMillis()).append("ms");
        }
        return sb.toString();
    }

    private static TextView label(Activity activity, float sp, int color, int style) {
        TextView view = new TextView(activity);
        view.setTextSize(sp);
        view.setTextColor(color);
        view.setTypeface(Typeface.DEFAULT, style);
        return view;
    }

    private static int dp(Activity activity, int value) {
        return Math.round(value * activity.getResources().getDisplayMetrics().density);
    }

    // Round close: white ring with a thin dark edge and an X. Waiting: dimmed ring + X and a bright arc that grows
    // clockwise from 12 o'clock as time passes; ready: fully bright.
    private static final class CloseRing extends View {
        private static final int TRACK_ALPHA_WAITING = 80;
        private static final int GLYPH_ALPHA_WAITING = 110;
        private final Paint edge = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint ring = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint arcEdge = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint arc = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint glyphEdge = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint glyph = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF bounds = new RectF();
        private float remaining = 1f;
        private boolean isReady;

        CloseRing(Activity activity) {
            super(activity);
            stroke(edge, dp(activity, 3), Color.rgb(20, 20, 20), false);
            stroke(ring, dp(activity, 2), Color.WHITE, false);
            stroke(arcEdge, dp(activity, 3), Color.argb(200, 20, 20, 20), true);
            stroke(arc, dp(activity, 2), Color.WHITE, true);
            stroke(glyphEdge, dp(activity, 5), Color.rgb(20, 20, 20), true);
            stroke(glyph, dp(activity, 2), Color.WHITE, true);
        }

        private static void stroke(Paint paint, float width, int color, boolean round) {
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(width);
            paint.setColor(color);
            if (round) paint.setStrokeCap(Paint.Cap.ROUND);
        }

        void setRemaining(float value) { remaining = Math.max(0f, Math.min(1f, value)); invalidate(); }

        void setReady(boolean value) { isReady = value; invalidate(); }

        @Override protected void onDraw(Canvas canvas) {
            super.onDraw(canvas);
            float cx = getWidth() * 0.5f;
            float cy = getHeight() * 0.5f;
            float inset = edge.getStrokeWidth() * 0.5f;
            float radius = Math.min(getWidth(), getHeight()) * 0.5f - inset;

            int track = isReady ? 255 : TRACK_ALPHA_WAITING;
            edge.setAlpha(track);
            ring.setAlpha(track);
            canvas.drawCircle(cx, cy, radius, edge);
            canvas.drawCircle(cx, cy, radius, ring);

            if (!isReady) {
                float sweep = 360f * (1f - remaining);
                if (sweep > 0f) {
                    bounds.set(inset, inset, getWidth() - inset, getHeight() - inset);
                    canvas.drawArc(bounds, -90f, sweep, false, arcEdge);
                    canvas.drawArc(bounds, -90f, sweep, false, arc);
                }
            }

            glyph.setAlpha(isReady ? 255 : GLYPH_ALPHA_WAITING);
            glyphEdge.setAlpha(isReady ? 255 : GLYPH_ALPHA_WAITING / 2);
            float half = Math.min(getWidth(), getHeight()) * 0.14f;
            canvas.drawLine(cx - half, cy - half, cx + half, cy + half, glyphEdge);
            canvas.drawLine(cx + half, cy - half, cx - half, cy + half, glyphEdge);
            canvas.drawLine(cx - half, cy - half, cx + half, cy + half, glyph);
            canvas.drawLine(cx + half, cy - half, cx - half, cy + half, glyph);
        }
    }
}
