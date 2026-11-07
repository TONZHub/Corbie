# Corbie’s Pocket Nest

An Android companion for Corbie, with the original raven shapes and his own little moonlit perch. Tap him to greet him, hold for scritches, or drag him around the nest. Offer treats, pocket shinies, wave, bow, and let him roost. Bond, shinies, perch and rest state are saved on the phone.

Pocket Corbie targets Android 15 and installs on Android 8.0 and newer. His tricks and remarks work offline.

## Try it

Transfer `Corbie-Pocket-v0.4.2.apk` to the phone and open it with Android’s package installer. Follow the phone’s install prompt. Open **Corbie** from the launcher. Install over an earlier Pocket Corbie build signed with the same key to keep your Nest progress.

## Let Corbie out

In the Nest, scroll to **Outside the Nest** and tap **Let Corbie out**. Allow **Display over other apps** for Corbie in Android Settings, return to the Nest, and tap **Let Corbie out** again. He starts near the bottom-right and mixes little hops with short flights. Drag him to a perch and he’ll remember where you put him. Tap for a wave, or hold to open his perch menu and cue **Take flight**.

From the perch menu, give him treats, shinies or scritches, let him roost, or use **Stay put** to turn off wandering. When he roosts, his eyelid droops and his head nods. Care and roost state are shared with the Nest and widget. Choose **Bring him home** to stop floating mode.

**Say something** in the Nest or perch menu plays a short Corbie line and moves his beak. The six clips are bundled and use the Northern English Piper voice from Corbie’s desktop setup, with the pitch raised to match his Bird tone. They play offline as soon as the app is installed. See the [voice credits](res/raw/corbie_voice_credits.txt).

Floating mode uses a small Android overlay and a foreground service. It starts when you ask, rests while the phone is locked, and stops when you send him home. Some apps hide overlays. If Android shows Corbie’s notification, it also has a **Bring him home** action.

Long-press an empty spot on the home screen, choose **Widgets**, and add **Corbie’s Pocket Nest**. The widget is a compact mood and shiny counter; tap it to open the animated nest. Widget updates happen when you interact with Corbie, without a polling service.

## Keyboard perch

On Android 11 or newer, hold floating Corbie and tap **Snap to keyboard**. Open a text field with a docked on-screen keyboard. Corbie perches above it and pauses his wandering. Drag him left or right to move him along the top edge. When the keyboard closes, he returns to his usual perch height. **Unpin from keyboard** turns the mode off.

Keyboard perch uses the space reported by Android’s keyboard insets. A floating keyboard may leave Corbie at his usual perch.

## Dance to music

On Android 10 or newer, open **Outside the Nest → Dance to music**, approve the audio and screen-sharing prompts, then play music in another app. Corbie bobs and flaps to volume, bass, and treble. He settles when the music goes quiet. The feature reads playback levels in memory and uses no microphone input or screen images. Some media apps block playback sharing.

Hold Corbie and choose **Stop dancing** to end the session. It also ends when you send him home, let him roost, or lock the phone.

## TikTok stage

Tap **TikTok stage** in the Nest. Start your phone’s screen recording with device audio, then tap **Start take**. After a three-second countdown, Corbie waves, introduces himself with his bundled British voice, and bows. Tap **Another take** to repeat the routine.

## Perch timer

In the Nest, open **Phone Tools → Perch timer**, or hold floating Corbie and tap **Perch timer**. Pick **5 min**, **15 min**, or **25 min**. Both menus show the same countdown and **Cancel timer** stops it. **Silent** turns off the voice and the notification sound. When Corbie is visible, he waves a wing and gives you a reminder.

Use **Enable notifications** in the Nest’s timer to receive alerts when Corbie is away. If Android offers **Enable precise alerts**, allow it for on-time background alerts. Background delivery can be late while precise alerts are disabled. The timer continues when you leave the Nest or bring floating Corbie home. Restarting the phone ends an active timer.

## Clean app cache

Under **Phone Tools** in the Nest, tap **Clean app cache**. Search for an app and tap its name to open Android’s App info screen. Choose **Storage** (or **Storage & cache**), then **Clear cache**. Press Back to return to Corbie’s picker. Use **All apps** to find apps that don’t appear in the launcher. Android handles the cleanup, and **Clear cache** keeps saved app data.

## Build on Windows

Use a JDK (17 or later), Android SDK platform 35 and Build Tools 35.0.0. The script uses the installed [AAPT2 resource tools](https://developer.android.com/tools/aapt2), D8, zipalign and [apksigner](https://developer.android.com/tools/apksigner), with no app libraries to download.

```powershell
.\android\Build-Android.ps1 -JavaHome 'C:\path\to\jdk'
```

Set `JAVA_HOME` to omit that argument. If the SDK is elsewhere, pass `-SdkPath` or set `ANDROID_SDK_ROOT`.

Output is `android/build/Corbie-Pocket-v0.4.2.apk`. The script checks the local state rules, audio analysis, APK signature, alignment, launcher and permissions. Keep the generated `android/.signing/development.p12` locally to install future development updates over this build. This is a local test build.
