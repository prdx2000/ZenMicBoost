# MicBoost

A Windows desktop app that gives you a reliable, system-wide microphone gain
control from **-30 dB to +30 dB** — for the many mic/driver combos where
Windows' built-in "Microphone Boost" slider is missing, capped too low, or
just doesn't work.

## Why this exists

Windows exposes microphone boost as an optional driver feature
(`IAudioEndpointVolume` / mixer "Boost" control). Whether it's available at
all, and how much boost it offers, is entirely up to the driver — a lot of
USB mics, headsets, and onboard audio chips either don't expose it, or cap it
well below what you'd actually want. There's no reliable, driver-independent
way to turn a quiet mic up (or a hot mic down) for every app on the system.

MicBoost solves this at the application layer instead of depending on the
driver:

```
Physical Mic (WASAPI capture)
   -> real-time gain stage (dB -> linear gain, with a soft limiter)
   -> rendered into a virtual audio cable's playback endpoint
        (the cable's matching recording endpoint is what Discord/Zoom/
         Teams/OBS/browsers/etc. select as their microphone)
```

Because the gain is applied by MicBoost itself and handed off through a
virtual audio cable, it works identically regardless of what your mic's
driver does or doesn't support.

## Prerequisites: installing a virtual audio cable

MicBoost needs a virtual audio cable driver installed to hand off the
boosted signal to other apps. The supported/default option is
**[VB-CABLE](https://vb-audio.com/Cable/)** (free, from VB-Audio):

1. Download VB-CABLE from https://vb-audio.com/Cable/
2. Run the installer as Administrator, then reboot if prompted.
3. That's it — no configuration needed. VB-CABLE installs two audio
   endpoints:
   - **CABLE Input (VB-Audio Virtual Cable)** — a playback device. This is
     what MicBoost renders your boosted mic audio into.
   - **CABLE Output (VB-Audio Virtual Cable)** — a recording device. This is
     what other apps select as their "microphone".

If MicBoost doesn't detect VB-CABLE, it shows a setup screen with a download
link and a "Recheck" button instead of failing silently — install the driver
and click Recheck.

The output stage sits behind an `IVirtualOutputDevice` interface
(`MicBoost.Audio/Output`), so a different virtual audio driver could be
swapped in later without touching capture, DSP, or UI code.

## Using MicBoost with Discord/Zoom/Teams/OBS/browsers

Once MicBoost is running with a mic selected:

1. Open your app's audio/microphone settings.
2. Set the input/microphone device to **CABLE Output (VB-Audio Virtual
   Cable)**.
3. Speak — the level you hear on the other end reflects your physical mic
   plus MicBoost's gain, not the raw driver signal.

You can leave MicBoost's own physical mic *unselected* everywhere else; only
MicBoost should read from it directly. Everything else should read from
CABLE Output.

## Features

- Lists every capture (microphone) device on the system, live — plugging or
  unplugging a mic updates the list automatically.
- Per-mic gain from **-30 dB to +30 dB** in 0.5 dB steps (slider, numeric
  entry, and +/- buttons), defaulting to 0 dB for any mic seen for the first
  time.
- Per-mic **bass** control (-30 dB to +30 dB, same step size), a low-shelf
  filter below ~200 Hz — independent of overall gain, so you can warm up or
  thin out your voice without changing its loudness.
- Gain is persisted **per physical device ID** (not by name, since names can
  duplicate or change) in `%AppData%/MicBoost/settings.json`, so switching
  mics recalls that mic's own saved gain.
- Real-time before/after level meters (input vs. output-after-gain), plus a
  visual + text cue when the limiter is actively clamping the signal.
- A soft-knee limiter (tanh saturation above ~-0.18 dBFS) so pushing gain
  hard never produces harsh digital clipping — it asymptotically approaches
  full scale instead of hard-clipping. Note that with up to +30 dB of gain
  available, extreme settings will engage the limiter heavily and compress
  dynamics — that's the safety net working as intended, not a bug.
- Mute toggle, from the main window or the tray icon's context menu, which
  silences the virtual mic without discarding your configured gain.
- **App Audio** tab: mirror one running app's playback (e.g. Spotify or a
  browser tab) into the same boosted stream, so people on the other end hear
  it mixed with your voice. Uses Windows 10 2004+'s per-process WASAPI
  loopback capture, so it picks up only that app (and its child processes,
  e.g. a browser's per-tab renderer processes) rather than everything playing
  on your speakers. A soft limiter on the combined signal prevents clipping
  when both sources are loud at once. When an app reports "now playing" info
  via the System Media Transport Controls (the same source as Windows' own
  volume flyout), its title/artist is shown next to it in the picker. Requires
  Windows 10 build 19041 or later; the tab explains if your OS is too old.
- Launch-on-startup and minimize-to-tray toggles; a system tray icon shows
  current gain and offers quick mute/show/exit actions.
- Fluent Design 2 UI (Mica backdrop, dark/light themes) built with
  [WPF-UI](https://github.com/lepoco/wpfui).
- Falls back gracefully if a previously-selected mic is no longer present at
  launch, prompting you to pick another.

## Project structure

```
MicBoost.sln
src/
  MicBoost.Audio/     Audio engine — no UI dependency, unit-testable
    Devices/          WASAPI capture device enumeration + hot-plug notifications
    Dsp/               dB<->linear gain math, gain/limiter sample provider, bass shelf EQ, level metering,
                       runtime sample-rate/channel adaptation so mismatched sources share one mix format
    Output/            Virtual cable abstraction (IVirtualOutputDevice) + VB-CABLE detection
    Engine/            Wires capture -> gain -> virtual output together
    Settings/          JSON settings persistence, keyed by device ID
  MicBoost.App/        WPF UI (MVVM via CommunityToolkit.Mvvm, WPF-UI Fluent controls)
tests/
  MicBoost.Tests/      xUnit tests for gain math, the gain/limiter DSP, and settings persistence
```

## Building and running

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
and Windows (WASAPI is Windows-only).

```powershell
dotnet build
dotnet run --project src/MicBoost.App/MicBoost.App.csproj
dotnet test
```

Or open `MicBoost.sln` in Visual Studio 2022+ and run/debug `MicBoost.App`.

### Publishing a standalone .exe

To produce a single self-contained executable that runs on any Windows
machine without installing the .NET runtime first:

```powershell
./publish.ps1
```

This writes `publish/win-x64/MicBoost.App.exe` (~72 MB, everything bundled —
just copy that one file and run it). Equivalent manual command:

```powershell
dotnet publish src/MicBoost.App/MicBoost.App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

## Tech stack

- .NET 8, C# 12, WPF
- [NAudio](https://github.com/naudio/NAudio) for WASAPI capture/render,
  device enumeration, and dB/gain sample processing
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) for
  MVVM (source-generated `ObservableObject`/`RelayCommand`)
- [WPF-UI](https://github.com/lepoco/wpfui) for Fluent Design 2 controls and
  the Mica window backdrop
- `Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection`
  for app startup/DI wiring
- `System.Text.Json` for settings persistence
- xUnit for tests

## Non-goals

- No custom kernel-mode audio driver — MicBoost relies on an existing
  user-mode virtual cable driver (VB-CABLE by default).
- Windows-only; WASAPI has no cross-platform equivalent.
