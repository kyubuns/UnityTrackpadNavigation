# Trackpad Navigation

English | [日本語](README.ja.md)

Trackpad pan, zoom, and rotation for the Unity Editor on Apple Silicon Macs.

https://github.com/user-attachments/assets/7dfbf874-a19e-457c-acfa-09c6d326cd9f

## Install

Add this URL with **Package Manager > Install package from git URL**:

```text
https://github.com/kyubuns/UnityTrackpadNavigation.git?path=Packages/com.kyubuns.trackpad-navigation
```

If Scene View navigation feels choppy after the first installation, restart the Unity Editor.

## Controls

| Gesture | Action |
|---|---|
| Two-finger slide | Pan |
| Pinch | Zoom around the cursor |
| Two-finger double-tap | Select and focus the object under the cursor |
| Option + two-finger slide | Orbit around the target |
| Command + two-finger slide | Look around from the camera position |

Adjust sensitivity, inversion, momentum, and enable/disable the plugin in `Preferences > Trackpad Navigation`.\
The same page also exposes Unity's **Shader Graph > Zoom Step Size** for standard scroll zoom.\
This changes Unity's own preference; Trackpad Navigation's **Restore defaults** does not reset it.\
The **VFX Graph > Zoom Step Size** and **UI Builder > Zoom Step Size** settings on the same page are Trackpad Navigation overrides for standard scroll zoom (including Control + two-finger scrolling), separate from pinch sensitivity.\
Lower values zoom more slowly.\
They are reset by **Restore defaults**; turning off **Enable** or **Graph / Timeline integration** restores each window's original scroll step.\
Inspect input in `Window > Trackpad Navigation > Diagnostics`.\
Use `Copy diagnostic report` to copy details for a bug report.

## Supported windows and limitations

Tested with macOS 26.6.2 and Unity 6000.3.23f1 on Apple Silicon.

- Scene View
- Game View
- Sprite Editor
- Profiler (CPU Usage → Timeline)
- Timeline
- Animator
- Animation (Dope Sheet / Curves)
- Curve Editor (opened from an Inspector curve field)
- Shader Graph
- VFX Graph
- UI Builder

Orbit and look are available in Scene View.\
Double-tap selects the object under the cursor and frames the selection, just like pressing F, in Scene View, GraphView, UI Builder, and Animator. Empty space leaves the selection and view unchanged.

Game View supports pan and zoom while stopped or paused.\
During playback, game input takes priority; zoom and image bounds follow Unity’s standard limits.

In the Profiler CPU Timeline, slide to pan time and scroll threads, and pinch to zoom time.\
Overview charts, Hierarchy, and other detail views keep Unity’s standard scrolling.

## Development

Example scenes, animation clips, and graphs are in `Assets/TrackpadExamples`.\
Open them from `Tools > Trackpad Navigation > Open ...`.\
The example Shader Graph and VFX Graph assets use Unity templates; their licenses are included in that folder.\
For recordings, open `Window > Trackpad Navigation > Live View` in this project to show finger positions and modifier keys.\
This tool lives in `Assets/Editor/TrackpadLiveView` and is not included in the UPM package.\
Its native build script is `Assets/Editor/TrackpadLiveView/Native~/build.sh`.\
Run `TrackpadNavigation.Tests` in the EditMode Test Runner.\
To run the tests from another project, add the package name to `testables` in that project's manifest.

To rebuild the native plugin, run these commands from the repository root on an Apple Silicon Mac with Xcode Command Line Tools, then restart Unity:

```sh
./Packages/com.kyubuns.trackpad-navigation/NativePlugin~/build.sh
./Packages/com.kyubuns.trackpad-navigation/NativePlugin~/test.sh
```

## FAQ

### Pinching no longer maximizes Unity windows

While the plugin is enabled, Unity's built-in pinch gestures are disabled throughout the Editor, including unsupported windows and views whose individual integration is turned off.\
To restore them, turn off **Enable** in `Preferences > Trackpad Navigation`.

### Pinch zoom doesn't work

Check that **System Settings > Trackpad > Scroll & Zoom > Zoom in or out** is enabled in macOS.

### Two-finger double-tap doesn't focus

Check that **System Settings > Trackpad > Scroll & Zoom > Smart zoom** is enabled in macOS.

[MIT License](LICENSE)
