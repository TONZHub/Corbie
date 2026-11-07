# Corbie's articulated poses

Desktop v0.98 adds secondary neck, tail, wing-tip, torso and leg joints to the original vector raven. Python authors 48 samples for each of eight pose states; the desktop app loads the local JSON once, interpolates around the loop and smooths transitions. There is no per-frame Python process, network service or extra runtime dependency for these poses.

| State | Secondary movement |
| --- | --- |
| idle | Small neck and tail motion |
| walk | Counterbalancing torso/tail, wing follow-through and bent legs |
| talk | Gentle neck/torso gestures with a quieter half-cycle |
| stretch | Extended neck, wing tip and balancing tail |
| preen | Head lean and flexed wing tip |
| sleep | Tucked neck and subtle breathing |
| flight | Outward wing-tip follow-through and tucked legs |
| dance | Neck, torso and tail sway with leg bends |

The existing head tracking, shoulder movement, beak, flight, grounded resting feet and octopus remain. Talking gestures follow voice playback activity; this is not phoneme or audio-amplitude lip synchronization. Articulation freezes with pause; form switches/reset clear the joints. The wing is now two overlapping shapes joined at an elbow pivot. Toe positions remain fixed when secondary leg bending is applied; existing walking/hopping/flight movement still controls foot placement.

Keep **motion/avian-poses.json** beside the executable in this folder structure. Missing, oversized, malformed or out-of-range data falls back to zero secondary offsets and does not prevent startup.

## Authoring

Edit the curves in **author_poses.py**, then run from RowanPet:

```powershell
py motion/author_poses.py
py motion/author_poses.py --check
py -m unittest discover -s motion -p "test_*.py" -q
```

Restart the desktop pet to load revised poses. Standard Python only; no packages are required. `BirdArticulation.cs` applies the exported curves and enforces joint limits. **Corbie-v0.98.exe --articulation-test report.txt preview.png** runs the native rig/speech checks and renders an actual-artwork contact sheet. The sheet's columns are idle, talk, stretch, preen, sleep and flight; its two rows sample different phases. It is a pose fixture, not a recording of autonomous behavior.

## Speech

Mic conversation now has a 220-token response budget and can use up to four clear sentences for an explanation, while keeping casual remarks short. The former 300-character cutoff is replaced by a 700-character bound that prefers complete sentences and otherwise ends at a word boundary. Personality guidance allows thoughtful, warm and useful replies without mandatory jokes. The existing British Piper voice and its tone settings remain in use. Screenshots still receive short observations. No model was trained or replaced.

## Verification

Seven Python checks cover deterministic exports, dense joint bounds, loop seams, outward flight follow-through, quiet speech phases, subtle sleep breathing and unknown states. Native rig checks cover pose loading, eight states, limits, planted foot transforms, walking follow-through, pause, form reset and missing/invalid asset fallback. Native speech checks cover explanations longer than 300 characters and bounded sentence/word endings. The full desktop smoke check and dedicated flight checks passed, and the actual-artwork contact sheet was inspected. Live conversational quality and perceived animation quality still need a desktop try.
