# Corbie’s Pocket Nest

An Android companion for Corbie, with the original raven shapes and his own little moonlit perch. Tap him to greet him, hold for scritches, or drag him around the nest. Offer treats, pocket shinies, wave, bow, and let him roost. Bond, shinies, perch and rest state are saved on the phone.

Pocket Corbie targets Android 15 and installs on Android 8.0 and newer. His tricks and remarks work offline.

## Try it

Transfer `Corbie-Pocket-v0.3.0.apk` to the phone and open it with Android’s package installer. Follow the phone’s install prompt. Open **Corbie** from the launcher. Install over an earlier Pocket Corbie build to keep your Nest progress.

## Let Corbie out

In the Nest, scroll to **Outside the Nest** and tap **Let Corbie out**. Allow **Display over other apps** for Corbie in Android Settings, return to the Nest, and tap **Let Corbie out** again. He starts near the bottom-right, idles, and takes the occasional little hop. Drag him to a perch and he’ll remember where you put him. Tap for a wave, or hold to open his perch menu.

From the perch menu, give him treats, shinies or scritches, let him roost, or use **Stay put** to turn off wandering. Care and roost state are shared with the Nest and widget. Choose **Bring him home** to stop floating mode.

**Say something** in the Nest or perch menu plays a short Corbie line and moves his beak. It uses an installed offline English voice from the phone, with British English preferred when available. His line still appears as text if an offline voice is unavailable.

Floating mode uses a small Android overlay and a foreground service. It starts when you ask, rests while the phone is locked, and stops when you send him home. Some apps hide overlays. If Android shows Corbie’s notification, it also has a **Bring him home** action.

Long-press an empty spot on the home screen, choose **Widgets**, and add **Corbie’s Pocket Nest**. The widget is a compact mood and shiny counter; tap it to open the animated nest. Widget updates happen when you interact with Corbie, without a polling service.

## Build on Windows

Use a JDK (17 or later), Android SDK platform 35 and Build Tools 35.0.0. The script uses the installed [AAPT2 resource tools](https://developer.android.com/tools/aapt2), D8, zipalign and [apksigner](https://developer.android.com/tools/apksigner), with no app libraries to download.

```powershell
.\android\Build-Android.ps1 -JavaHome 'C:\path\to\jdk'
```

Set `JAVA_HOME` to omit that argument. If the SDK is elsewhere, pass `-SdkPath` or set `ANDROID_SDK_ROOT`.

Output is `android/build/Corbie-Pocket-v0.3.0.apk`. The script checks the local state rules, APK signature, alignment, launcher and permissions. Keep the generated `android/.signing/development.p12` locally to install future development updates over this build. This is a local test build.
