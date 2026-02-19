# LiveCompanion — Architecture

## Overview

LiveCompanion is a live concert management application for bands. It automates sound effect triggers, MIDI preset changes, and metronome control so the musicians can focus entirely on playing.

## Hardware Setup

```
                     ┌──────────────┐
                     │   PC Laptop  │  ◄── LiveCompanion
                     │  (brain)     │
                     └──┬───┬───┬───┘
                        │   │   │
               USB/MIDI │   │   │ ASIO
          ┌─────────────┘   │   └─────────────────┐
          │                 │                     │
  ┌───────▼───────┐ ┌──────▼───────┐   ┌─────────▼─────────┐
  │ Roland SSPD   │ │ Neural DSP   │   │  Audio Interface  │
  │ (scene +      │ │ Quad Cortex  │   │  (ASIO)           │
  │  footswitch)  │ │ x2 (guitars) │   │  - FX on ch 1-2   │
  └───────────────┘ └──────────────┘   │  - Click on ch 3-4 │
                                       └───────────────────┘
```

- **Roland SSPD**: scene and footswitch control via MIDI
- **Neural DSP Quad Cortex x2**: guitar preset changes and BPM sync via MIDI
- **ASIO audio interface**: sound effects on one stereo pair, click track on another

## Timeline & Tick System

All timing is expressed in **ticks** using a PPQN (Pulses Per Quarter Note) resolution of **480** by default.

```
Tick 0                    Tick 480                  Tick 960
|── quarter note (1 beat) ──|── quarter note (1 beat) ──|
```

At 120 BPM:
- 1 quarter note = 500 ms
- 1 tick = 500 / 480 ≈ 1.04 ms

The tick counter is the single source of truth. Every event in a song is positioned at a specific tick. The `MetronomeEngine` advances the counter; the `SetlistPlayer` dispatches events when their tick is reached.

## Domain Model

```
Setlist
 ├── Name, PPQN
 └── Songs[]
      ├── Title, Artist, DurationTicks
      └── Events[] (polymorphic)
           ├── SectionChangeEvent  → BPM, TimeSignature, MidiPreset[]
           └── AudioCueEvent       → SampleFileName, GainDb
```

**MidiPreset** targets a `DeviceTarget` (Quad1, Quad2, SSPD) with a MIDI channel, program change number, and optional list of CC (Control Change) messages.

## Engine Components

### MetronomeEngine

- Maintains the current tick counter
- Calculates tick interval: `60_000 ms / (BPM × PPQN)`
- Fires `TickAdvanced` on every tick
- Fires `Beat` on every quarter-note boundary (every PPQN ticks)
- Phase 1: uses `Task.Delay` for timing
- Phase 2: will use high-precision ASIO audio callback

### SetlistPlayer

State machine with three states:

```
  ┌──────┐   Play()    ┌─────────┐   song ends / Stop()   ┌─────────┐
  │ Idle │ ──────────► │ Playing │ ──────────────────────► │ Stopped │
  └──────┘             └─────────┘                         └─────────┘
       ▲                                                        │
       └────────────── Load(newSetlist) ◄───────────────────────┘
```

**Events emitted:**

| Event              | Payload                        | When                                    |
|--------------------|--------------------------------|-----------------------------------------|
| `SongStarted`      | Song, index                    | A new song begins                       |
| `SongEnded`        | Song, index                    | A song's duration is reached            |
| `SetlistCompleted`  | —                              | All songs have been played              |
| `BeatFired`        | beat (in bar), bar number      | Every quarter-note boundary             |
| `SectionChanged`    | SectionChangeEvent             | A section change event tick is reached  |
| `AudioCueFired`     | AudioCueEvent                  | An audio cue event tick is reached      |
| `MidiPresetChanged` | MidiPreset                     | For each preset in a section change     |

### SetlistRepository

- Serializes/deserializes `Setlist` as JSON using `System.Text.Json`
- Polymorphic support via `[JsonDerivedType]` on `SongEvent`
- Provides both sync (string) and async (file) APIs

## Phase 2 — Audio Engine (LiveCompanion.Audio)

### Architecture

The audio engine replaces the `Task.Delay`-based timing of Phase 1 with a high-precision ASIO callback-driven clock. The ASIO buffer callback is the single source of timing truth.

```
┌─────────────────────────────────────────────────────────┐
│                      AsioService                        │
│  (driver lifecycle, fault handling, auto-reconnect)     │
│                                                         │
│   ┌──────────────────────────────────────────────────┐  │
│   │              AsioOutputRouter                    │  │
│   │  (multi-channel ISampleProvider)                 │  │
│   │                                                  │  │
│   │   Ch 0-1 ◄── MetronomeWaveProvider               │  │
│   │               (click tones + tick counter)       │  │
│   │                                                  │  │
│   │   Ch 2-3 ◄── MixingSampleProvider                │  │
│   │               (concurrent sample playback)       │  │
│   └──────────────────────────────────────────────────┘  │
│                         │                               │
│                    AsioOut.Init()                        │
└─────────────────────────────────────────────────────────┘
```

### Key Design Decisions

**ASIO callback = clock**: `MetronomeWaveProvider.Read()` is called by the ASIO driver on a high-priority audio thread. For each audio frame, the provider accumulates fractional samples and advances the tick counter when the accumulation reaches one tick's worth of samples (`SampleRate × 60 / (BPM × PPQN)`). This gives sub-sample timing accuracy without a separate timing thread.

**Multi-channel routing**: `AsioOutputRouter` is a multi-channel `ISampleProvider` that takes stereo sources and places them at configurable channel offsets in the output buffer. Default: metronome on channels 0-1, samples on channels 2-3.

**Testability**: `IAsioOut` and `IAsioOutFactory` abstract the NAudio ASIO layer. Tests use `FakeAsioOut` which allows manually pumping audio buffers to verify tick advancement and event dispatch deterministically.

**Pre-loaded samples**: `SamplePlayer` loads all audio files into memory (`float[]`) at setlist load time. No disk I/O during live performance.

### Components

#### AudioConfiguration
POCO serializable to JSON. Contains:
- ASIO driver name, buffer size, sample rate
- Metronome channel offset, sample channel offset
- Volume levels (master metronome, strong beat, weak beat)
- Auto-reconnect settings

#### AsioService
Manages the ASIO driver lifecycle:
- Lists available ASIO drivers
- Initializes `AsioOut` with the configured driver
- Creates the `AsioOutputRouter` audio pipeline
- Emits `AudioFault` on driver errors without crashing
- Automatic reconnection loop (configurable)

#### MetronomeAudioEngine
Drop-in replacement for the Phase 1 `MetronomeEngine`. Same public API:
- `CurrentTick`, `IsRunning`
- `Start()`, `Stop()`, `Reset()`
- `ChangeTempo(bpm, timeSignature)`
- Events: `TickAdvanced`, `Beat`

Internally delegates to `MetronomeWaveProvider` which generates stereo click audio (sine wave, 1000 Hz strong beat / 800 Hz weak beat, linear fade envelope) and advances ticks in its `Read()` method.

#### SamplePlayer
- Subscribes to `SetlistPlayer.AudioCueFired`
- Pre-loads audio files (WAV, MP3, OGG) into memory via NAudio's `AudioFileReader`
- Mixes concurrent samples via `MixingSampleProvider`
- Applies per-sample gain (dB → linear conversion)
- Routes output to configurable ASIO channel pair

### Error Handling

- **Driver disappearance**: `AsioService` catches `PlaybackStopped` with a non-null exception, emits `AudioFault`, and optionally starts a reconnection loop
- **Missing sample files**: `SamplePlayer` silently skips unknown files during `OnAudioCueFired` (logged but no crash)
- **No ASIO driver configured**: `AsioService.Initialize()` throws `InvalidOperationException` with a clear message

## Phase 3 — MIDI Engine (LiveCompanion.Midi)

### Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     LiveCompanion.Midi                       │
│                                                              │
│  ┌─────────────┐  MidiPresetChanged   ┌──────────────────┐  │
│  │ SetlistPlayer│ ──────────────────► │   MidiRouter     │  │
│  └─────────────┘                      │  (PC + CC route) │  │
│                                       └────────┬─────────┘  │
│  ┌─────────────────────┐                       │            │
│  │ MetronomeAudioEngine │  TickAdvanced         │            │
│  │  (ASIO callback)    │ ──────────┐           │            │
│  └─────────────────────┘          ▼            ▼            │
│                          ┌──────────────┐  ┌────────────┐   │
│                          │MidiClockEngine│  │MidiService │   │
│                          │ 24 clk/beat  │  │(port mgmt) │   │
│                          └──────────────┘  └─────┬──────┘   │
│  ┌────────────────┐                              │           │
│  │MidiInputHandler│     IMidiOutput / IMidiInput  │           │
│  │ (SSPD→actions) │     NAudio MidiOut / MidiIn  ◄┘          │
│  └────────────────┘                                          │
└──────────────────────────────────────────────────────────────┘
```

### MIDI Routing Table

| DeviceTarget | Default port name  | Direction | Content                        |
|--------------|--------------------|-----------|---------------------------------|
| Quad1        | "Quad Cortex 1"    | OUT       | Program Change, CC, MIDI Clock  |
| Quad2        | "Quad Cortex 2"    | OUT       | Program Change, CC, MIDI Clock  |
| SSPD         | "Roland SSPD"      | OUT + IN  | Program Change, CC; IN: footswitch |

Port names are configurable in `MidiConfiguration.OutputDevices`.

### MIDI Clock — Key Design Decision

**The MIDI Clock is driven by the ASIO audio callback, not an independent timer.**

`MidiClockEngine` subscribes to `MetronomeAudioEngine.TickAdvanced`, which fires on the
high-priority ASIO audio thread from inside `MetronomeWaveProvider.Read()`. This guarantees
that the MIDI clock and the audio metronome click are derived from the exact same tick counter
with sub-sample accuracy — no drift, no jitter from competing timers.

```
ASIO driver callback
  └─► MetronomeWaveProvider.Read()
        ├─► TickAdvanced fires (on ASIO audio thread)
        │     └─► MidiClockEngine.OnTickAdvanced()
        │           └─► every 20 ticks: midiOutShortMsg(0xF8)  ← MIDI Clock pulse
        └─► Beat fires every 480 ticks = 1 quarter note
```

**MIDI Clock math (PPQN = 480):**
- MIDI spec: 24 timing clock messages per quarter note
- Ticks per pulse: 480 / 24 = **20 ticks**
- At 120 BPM: 1 clock every ≈ 0.833 ms (sub-millisecond lock with audio)

**BPM changes** are handled implicitly. When `SetlistPlayer.SectionChanged` fires, the
`MetronomeAudioEngine` calls `ChangeTempo()`, which changes the ASIO callback rate. The
`TickAdvanced` events then arrive faster or slower, and the MIDI clock interval tracks
the new BPM automatically with no explicit action in `MidiClockEngine`.

**Clock messages use `SendImmediate`** (no queue). Stale timing pulses are dropped
rather than replayed in a burst on reconnect.

### MIDI Message Encoding (NAudio `MidiOut.Send(int)`)

```
bits  0– 7: status byte   (e.g. 0xC0 = Program Change ch 0)
bits  8–15: data byte 1   (program number, controller number)
bits 16–23: data byte 2   (value — 0 for PC, 0–127 for CC)
```

| Message         | Encoding example                        |
|-----------------|-----------------------------------------|
| Program Change  | `0xC0 \| (prog << 8)`                   |
| Control Change  | `0xB0 \| (ctrl << 8) \| (val << 16)`    |
| MIDI Clock      | `0xF8`                                  |
| MIDI Start      | `0xFA`                                  |
| MIDI Continue   | `0xFB`                                  |
| MIDI Stop       | `0xFC`                                  |

### Error Handling — MIDI Port Loss

| Scenario                     | Behaviour                                              |
|------------------------------|--------------------------------------------------------|
| Port not found on open       | `MidiFault` event fired; reconnect loop started        |
| `Send()` throws              | `MidiFault` fired; message queued for replay on reconnect |
| `SendImmediate()` throws     | Message silently dropped; `MidiFault` fired            |
| Reconnect interval           | Every `ReconnectDelayMs` ms (default: 5 000 ms)        |
| Reconnect success            | `PortReconnected` fired; pending queue drained         |

### Components

#### MidiConfiguration
Serializable POCO (JSON). Contains:
- `OutputDevices`: `DeviceTarget` → `(PortName, Channel)`
- `MidiInputPortName`: MIDI IN port for footswitch messages
- `InputMappings`: `(StatusType, Channel, Data1, Data2) → MidiAction` rules
- `ClockTargets`: devices receiving MIDI Clock (default: Quad1, Quad2)
- `ReconnectDelayMs`: auto-reconnect interval (default: 5 000 ms)

#### MidiService
- Opens output ports on first use (lazy)
- `Send()`: delivers to port or queues if unavailable
- `SendImmediate()`: delivers or drops (for MIDI Clock — no queue)
- Auto-reconnect loop drains pending queue on success

#### MidiRouter
- Subscribes to `SetlistPlayer.MidiPresetChanged`
- Looks up port via `MidiConfiguration.OutputDevices[DeviceTarget]`
- Sends Program Change then all CCs in order on the correct channel

#### MidiClockEngine
- Subscribes to `MetronomeAudioEngine.TickAdvanced` (ASIO thread)
- Every `PPQN / 24` ticks sends `0xF8` to all `ClockTargets`
- `Start()` → `0xFA`; `Stop()` → `0xFC`; `Continue()` → `0xFB`

#### MidiInputHandler
- Opens the configured MIDI IN port
- `ProcessMessage()`: first-match rule fires `ActionTriggered`
- Matching: StatusType (nibble-masked), Channel (-1=any), Data1/Data2 (-1=any)
- Auto-reconnect on port loss

### Testability

All NAudio MIDI types are hidden behind `IMidiOutput`, `IMidiInput`, `IMidiPortFactory`.
Test project provides:
- `FakeMidiOutput`: records `Send()` calls; `ThrowOnSend` simulates faults
- `FakeMidiInput`: injects messages via `SimulateMessage()`
- `FakeMidiPortFactory`: returns fakes by name; `ThrowOnOpen` simulates unavailability

MIDI clock tests pump `MetronomeWaveProvider` frames deterministically, verifying that
exactly 24 clock pulses are emitted per PPQN-worth of ticks (same pattern as Phase 2
audio engine tests).

## Phase 4 — WPF UI (LiveCompanion.App)

### Pattern: MVVM with CommunityToolkit.Mvvm

All ViewModels derive from `ObservableObject` (CommunityToolkit). Properties use `[ObservableProperty]`
source generators; commands use `[RelayCommand]`. No logic in code-behind — XAML code-behind is
limited to thin event bridges (e.g. TreeView selection, ListBox double-click).

**ViewModel testability:** ViewModels depend on `IDispatcher` (not `System.Windows.Threading.Dispatcher`)
so they can be instantiated and tested without a running WPF application.

### Navigation

```
MainWindow
  └── Grid
       ├── Left sidebar (64 px, hidden in Performance mode)
       │     4 nav buttons → NavigationService.NavigateTo(ViewKey)
       └── ContentControl bound to MainWindowViewModel.CurrentView
             (WPF DataTemplate resolves ViewModel type → View UserControl)
```

`NavigationService` holds the current `ViewKey` and fires `NavigatedTo`. `MainWindowViewModel`
subscribes and sets `CurrentView` to the matching ViewModel instance. WPF's `DataTemplate` registry
in `MainWindow.Resources` maps each ViewModel type to its corresponding `UserControl`.

### Service Wiring (AppServices)

```
App.OnStartup()
  ├── Load AudioConfiguration  (from %APPDATA%\LiveCompanion\audio_config.json)
  ├── Load MidiConfiguration   (from %APPDATA%\LiveCompanion\midi_config.json)
  └── AppServices
        ├── MetronomeEngine + SetlistPlayer   (Core — Task.Delay timing)
        ├── AsioService                       (Audio — driver lifecycle)
        ├── MetronomeAudioEngine              (Audio — ASIO click + tick source)
        ├── SamplePlayer                      (Audio — cue playback)
        ├── MidiService + MidiRouter          (MIDI — preset dispatch)
        ├── MidiClockEngine                   (MIDI — ASIO-driven clock)
        └── MidiInputHandler                  (MIDI — footswitch actions)
```

`AppServices.InitializeAudio()` is called only when an ASIO driver is configured.
If no config exists on first launch, the app redirects to the Setup view.

### Fault Notifications

`AsioService.AudioFault` and `MidiService.MidiFault` are wired in `MainWindowViewModel` to
`NotificationViewModel.ShowError()`. The notification overlay appears as a top-edge banner
over the content area, auto-dismissed after 5 seconds or on click.

### The 4 Views

| View | Key | Description |
|------|-----|-------------|
| Performance | `ViewKey.Performance` | Stage display: song title (72 pt), BPM (56 pt), beat ellipse flash, Play/Stop, song nav bar. Nav sidebar hidden. |
| Setlist | `ViewKey.Setlist` | Scrollable song list with number, title, artist, BPM, estimated duration. Current song highlighted in green. |
| Config | `ViewKey.Config` | Two-panel editor: left tree (Setlist → Songs → Sections), right detail panel for the selected node. Saves via `SetlistRepository`. |
| Setup | `ViewKey.Setup` | ASIO driver picker, buffer size, volume sliders, MIDI port pickers per device, clock targets, input-action mapping table. |

## Project Phases

| Phase | Scope                                          | Status      |
|-------|-------------------------------------------------|-------------|
| 1     | **Core** — Models, state machine, tick engine, JSON persistence, tests | Done |
| 2     | **Audio ASIO** — High-precision timer via ASIO callback, sample playback on dedicated outputs, click track | Done |
| 3     | **MIDI** — Program Change + CC routing, MIDI Clock sync via ASIO callback, MIDI input handling | Done |
| 4     | **UI** — WPF MVVM interface: setlist editor, live stage view, hardware setup | Done |
| 5     | **Integration SSPD** — Full Roland SSPD scene/footswitch integration | Planned |

## Directory Structure

```
/src/LiveCompanion.Core/
  Models/             — Domain entities (Song, Setlist, events, presets)
  Engine/             — MetronomeEngine, SetlistPlayer, SetlistRepository
/src/LiveCompanion.Core.Tests/
                      — xUnit tests (navigation, dispatch, serialization)
/src/LiveCompanion.Audio/
  Abstractions/       — IAsioOut, IAsioOutFactory, NAudioAsioOut, NAudioAsioOutFactory
  Providers/          — MetronomeWaveProvider, AsioOutputRouter
  AsioService.cs      — ASIO driver lifecycle management
  MetronomeAudioEngine.cs — ASIO-callback-driven metronome
  SamplePlayer.cs     — Audio cue playback with pre-loading and mixing
  AudioConfiguration.cs — Serializable audio config POCO
/src/LiveCompanion.Audio.Tests/
  Fakes/              — FakeAsioOut, FakeAsioOutFactory
                      — xUnit tests (config, ASIO service, metronome, sample player)
/src/LiveCompanion.Midi/
  Abstractions/       — IMidiOutput, IMidiInput, IMidiPortFactory, NAudio implementations
  MidiConfiguration.cs — Serializable MIDI config POCO
  MidiService.cs      — Port lifecycle, fault handling, message queueing, auto-reconnect
  MidiRouter.cs       — Program Change + CC dispatch per DeviceTarget
  MidiClockEngine.cs  — MIDI Clock tied to ASIO tick callback (24 pulses/quarter note)
  MidiInputHandler.cs — MIDI IN message → MidiAction mapping
/src/LiveCompanion.Midi.Tests/
  Fakes/              — FakeMidiOutput, FakeMidiInput, FakeMidiPortFactory
                      — xUnit tests (config, service, router, clock engine, input handler)
/src/LiveCompanion.App/
  App.xaml / App.xaml.cs       — WPF application entry point; composition root
  MainWindow.xaml / .cs         — Shell window with sidebar navigation
  Themes/Dark.xaml              — Shared dark-mode resource dictionary (colors, brushes, styles)
  Services/
    IDispatcher.cs / WpfDispatcher.cs  — UI thread abstraction for ViewModel testability
    INavigationService.cs / NavigationService.cs — View-key-based navigation service
    ConfigPaths.cs              — %APPDATA%\LiveCompanion config file paths
    AppServices.cs              — Composition root: wires all engine services together
  ViewModels/
    MainWindowViewModel.cs      — Shell: navigation state, notification wiring
    NotificationViewModel.cs    — Non-blocking fault notification overlay (auto-dismiss 5 s)
    PerformanceViewModel.cs     — Stage view: subscribes to SetlistPlayer events
    SetlistViewModel.cs         — Setlist view: load/navigate songs
    SongItemViewModel.cs        — Song row: title, artist, BPM, estimated duration
    ConfigViewModel.cs          — Setlist/song/section editor with tree nodes
    SetupViewModel.cs           — ASIO + MIDI hardware configuration form
    MidiActionValues.cs         — Enum helper for ComboBox binding
  Views/
    PerformanceView.xaml / .cs  — Stage display (hero title, BPM, beat indicator, Play/Stop)
    SetlistView.xaml / .cs      — Scrollable song list with double-click navigation
    ConfigView.xaml / .cs       — Two-panel tree editor (structure + detail)
    SetupView.xaml / .cs        — Audio driver + MIDI port setup form
  Converters/
    ValueConverters.cs          — NotNull, NotNullToVisible, BoolToVisible, BoolToCollapsed
/data/setlists/       — JSON setlist files
/data/samples/        — Audio sample files (WAV, MP3, OGG)
/docs/                — This document
```
