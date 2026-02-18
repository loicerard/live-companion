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

## Project Phases

| Phase | Scope                                          | Status      |
|-------|-------------------------------------------------|-------------|
| 1     | **Core** — Models, state machine, tick engine, JSON persistence, tests | Current |
| 2     | **Audio ASIO** — High-precision timer via ASIO callback, sample playback on dedicated outputs, click track | Planned |
| 3     | **MIDI** — Real MIDI output to Quad Cortex x2 and Roland SSPD | Planned |
| 4     | **UI** — WPF/Avalonia interface: setlist editor, live view, emergency stop button | Planned |
| 5     | **Integration SSPD** — Full Roland SSPD scene/footswitch integration | Planned |

## Directory Structure

```
/src/LiveCompanion.Core/
  Models/         — Domain entities (Song, Setlist, events, presets)
  Engine/         — MetronomeEngine, SetlistPlayer, SetlistRepository
/src/LiveCompanion.Core.Tests/
                  — xUnit tests (navigation, dispatch, serialization)
/data/setlists/   — JSON setlist files
/data/samples/    — Audio sample files (WAV)
/docs/            — This document
```
