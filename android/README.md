# Corbie’s Pocket Nest

An Android companion for Corbie, with the original raven shapes and his own little moonlit perch. Tap him to greet him, hold for scritches, or drag him around the nest. Offer treats, pocket shinies, wave, bow, and let him roost. Bond, shinies, perch and rest state are saved on the phone.

Pocket Corbie targets Android 15 and installs on Android 8.0 and newer. His tricks and remarks work offline.

## Try it

Transfer `Corbie-Pocket-v0.3.4.apk` to the phone and open it with Android’s package installer. Follow the phone’s install prompt. Open **Corbie** from the launcher. Install over an earlier Pocket Corbie build to keep your Nest progress.

## Let Corbie out

In the Nest, scroll to **Outside the Nest** and tap **Let Corbie out**. Allow **Display over other apps** for Corbie in Android Settings, return to the Nest, and tap **Let Corbie out** again. He starts near the bottom-right, idles, and takes the occasional little hop. Drag him to a perch and he’ll remember where you put him. Tap for a wave, or hold to open his perch menu.

From the perch menu, give him treats, shinies or scritches, let him roost, or use **Stay put** to turn off wandering. Care and roost state are shared with the Nest and widget. Choose **Bring him home** to stop floating mode.

**Say something** in the Nest or perch menu plays a short Corbie line and moves his beak. The six clips are bundled and use the Northern English Piper voice from Corbie’s desktop setup, with the pitch raised to match his Bird tone. They play offline as soon as the app is installed. See the [voice credits](res/raw/corbie_voice_credits.txt).

Floating mode uses a small Android overlay and a foreground service. It starts when you ask, rests while the phone is locked, and stops when you send him home. Some apps hide overlays. If Android shows Corbie’s notification, it also has a **Bring him home** action.

Long-press an empty spot on the home screen, choose **Widgets**, and add **Corbie’s Pocket Nest**. The widget is a compact mood and shiny counter; tap it to open the animated nest. Widget updates happen when you interact with Corbie, without a polling service.

## Perch timer

In the Nest, open **Phone Tools → Perch timer**, or hold floating Corbie and tap **Perch timer**. Pick **5 min**, **15 min**, or **25 min**. Both menus show the same countdown and **Cancel timer** stops it. **Silent** turns off the voice and the notification sound. When Corbie is visible, he waves a wing and gives you a reminder.

Use **Enable notifications** in the Nest’s timer to receive alerts when Corbie is away. If Android offers **Enable precise alerts**, allow it for on-time background alerts. Background delivery can be late while precise alerts are disabled. The timer continues when you leave the Nest or bring floating Corbie home. Restarting the phone ends an active timer.

## Music visualizer

In the Nest, open **Phone Tools → Music visualizer** to visit **Moonlight Mix**. Play something in another app and tap **Use phone audio**, or tap **Play audio file** to choose a track. Corbie dances and the bars show frequencies from bass to treble. **Stop** ends the session.

For floating Corbie, hold him and choose **Dance to music** or **Stop dancing**. Grant audio access in Moonlight Mix first. Floating dance listens while the perch is visible and pauses when the phone locks.

Android requires the permission labeled **Microphone** for its playback visualizer. Corbie uses the playback signal for animation and keeps it in memory. Other apps and audio routes may leave the bars flat, in which case you can try a saved audio file in Moonlight Mix.

## Clean app cache

Under **Phone Tools** in the Nest, tap **Clean app cache**. Search for an app and tap its name to open Android’s App info screen. Choose **Storage** (or **Storage & cache**), then **Clear cache**. Press Back to return to Corbie’s picker. Use **All apps** to find apps that don’t appear in the launcher. Android handles the cleanup, and **Clear cache** keeps saved app data.

## Build on Windows

Use a JDK (17 or later), Android SDK platform 35 and Build Tools 35.0.0. The script uses the installed [AAPT2 resource tools](https://developer.android.com/tools/aapt2), D8, zipalign and [apksigner](https://developer.android.com/tools/apksigner), with no app libraries to download.

```powershell
.\android\Build-Android.ps1 -JavaHome 'C:\path\to\jdk'
```

Set `JAVA_HOME` to omit that argument. If the SDK is elsewhere, pass `-SdkPath` or set `ANDROID_SDK_ROOT`.

Output is `android/build/Corbie-Pocket-v0.3.4.apk`. The script checks the local state rules, APK signature, alignment, launcher and permissions. Keep the generated `android/.signing/development.p12` locally to install future development updates over this build. This is a local test build.
