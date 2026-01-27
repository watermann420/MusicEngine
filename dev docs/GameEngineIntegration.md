# Game Engine Integration - Developer Guide

This guide explains how to integrate MusicEngine into game engines like Unity, Godot, and Unreal Engine using the ExternalControlService.

## Overview

The `ExternalControlService` provides a simple API for game engines to:

- **Control playback**: Play, pause, stop, and seek
- **Set variables**: BPM, volume, intensity, custom parameters
- **Trigger events**: Transitions, cues, dynamic music changes
- **Receive callbacks**: React to music events in your game

## Quick Start

```csharp
using MusicEngineEditor.Services;

// Get the service instance
var control = ExternalControlService.Instance;

// Set music parameters based on game state
control.SetVariable("IntensityLevel", player.DangerLevel);
control.SetVariable("PlayerHealth", player.Health);

// Trigger music events
control.TriggerEvent("TransitionTo", "Combat");
```

## Built-in Variables

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `BPM` | Float | 20-300 | Tempo in beats per minute |
| `MasterVolume` | Float | 0-1 | Master output volume |
| `IntensityLevel` | Float | 0-1 | Music intensity/energy |
| `DangerLevel` | Float | 0-1 | Threat/danger level |
| `PlayerHealth` | Float | 0-1 | Player health percentage |
| `TimeOfDay` | Float | 0-24 | In-game time (hours) |
| `IsUnderwater` | Bool | - | Underwater state |
| `IsIndoors` | Bool | - | Indoor/outdoor state |
| `BiomeType` | Int | 0-10 | Current biome/area type |

## Built-in Events

| Event | Parameters | Description |
|-------|------------|-------------|
| `Play` | - | Start playback |
| `Stop` | - | Stop playback |
| `Pause` | - | Pause playback |
| `Resume` | - | Resume from pause |
| `TransitionTo` | string state | Transition to music state |
| `PlayStinger` | string name | Play one-shot stinger |
| `SetLayer` | string layer, bool active | Enable/disable layer |
| `FadeTo` | float volume, float time | Fade master volume |
| `CrossfadeTo` | string state, float time | Crossfade to state |

## Unity Integration

### Basic Setup

```csharp
using UnityEngine;
using MusicEngineEditor.Services;

public class MusicController : MonoBehaviour
{
    private ExternalControlService _music;

    void Awake()
    {
        _music = ExternalControlService.Instance;

        // Register custom variables
        _music.RegisterVariable("PlayerHealth", VariableType.Float, 1.0f);
        _music.RegisterVariable("CombatIntensity", VariableType.Float, 0.0f);

        // Register event callbacks
        _music.RegisterEvent("OnBeatHit", OnBeatHit);
        _music.RegisterEvent("OnBarComplete", OnBarComplete);
    }

    void Start()
    {
        _music.TriggerEvent("Play");
    }

    void Update()
    {
        // Update music based on game state
        UpdateMusicState();
    }

    private void UpdateMusicState()
    {
        // Get player health
        float health = PlayerManager.Instance.Health / PlayerManager.Instance.MaxHealth;
        _music.SetVariable("PlayerHealth", health);

        // Get combat intensity from AI system
        float intensity = CombatManager.Instance.GetIntensity();
        _music.SetVariable("CombatIntensity", intensity);

        // Time of day
        _music.SetVariable("TimeOfDay", GameTime.Instance.Hour);
    }

    private void OnBeatHit(object[] args)
    {
        // Pulse UI elements on beat
        UIManager.Instance.PulseBeatIndicator();
    }

    private void OnBarComplete(object[] args)
    {
        // Potential state change point
        CheckMusicTransition();
    }

    private void CheckMusicTransition()
    {
        if (CombatManager.Instance.IsInCombat && _currentState != "Combat")
        {
            _music.TriggerEvent("TransitionTo", "Combat");
            _currentState = "Combat";
        }
        else if (!CombatManager.Instance.IsInCombat && _currentState == "Combat")
        {
            _music.TriggerEvent("CrossfadeTo", "Exploration", 2.0f);
            _currentState = "Exploration";
        }
    }

    private string _currentState = "Exploration";
}
```

### Audio Source Integration

```csharp
using UnityEngine;
using MusicEngineEditor.Services;

[RequireComponent(typeof(AudioSource))]
public class MusicEngineAudioSource : MonoBehaviour
{
    private AudioSource _audioSource;
    private ExternalControlService _music;
    private float[] _audioBuffer;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _music = ExternalControlService.Instance;
        _audioBuffer = new float[1024];

        // Create audio clip for streaming
        _audioSource.clip = AudioClip.Create(
            "MusicEngine",
            44100 * 60,  // 60 seconds buffer
            2,           // Stereo
            44100,
            true,        // Stream
            OnAudioRead
        );

        _audioSource.loop = true;
        _audioSource.Play();
    }

    void OnAudioRead(float[] data)
    {
        // Get audio from MusicEngine
        _music.ProcessAudio(data, data.Length / 2, 44100);
    }
}
```

### ScriptableObject for Music Zones

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "MusicZone", menuName = "Audio/Music Zone")]
public class MusicZoneData : ScriptableObject
{
    public string stateName;
    public float transitionTime = 1.0f;
    public float intensity = 0.5f;
    [Range(0, 1)] public float volume = 1.0f;
    public bool isIndoors = false;
}

public class MusicZone : MonoBehaviour
{
    public MusicZoneData zoneData;
    private ExternalControlService _music;

    void Awake()
    {
        _music = ExternalControlService.Instance;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _music.SetVariable("IntensityLevel", zoneData.intensity);
            _music.SetVariable("IsIndoors", zoneData.isIndoors);
            _music.TriggerEvent("CrossfadeTo", zoneData.stateName, zoneData.transitionTime);
        }
    }
}
```

## Godot Integration

### GDScript Setup

```gdscript
extends Node

var music_engine

func _ready():
    # Load MusicEngine (assumes C# interop or native plugin)
    music_engine = MusicEngineEditor.Services.ExternalControlService.Instance

    # Register variables
    music_engine.RegisterVariable("PlayerHealth", 0, 1.0)  # Float type = 0
    music_engine.RegisterVariable("CombatActive", 2, false)  # Bool type = 2

    # Start playback
    music_engine.TriggerEvent("Play")

func _process(delta):
    update_music_state()

func update_music_state():
    var player = get_node("/root/Main/Player")

    # Update health
    var health_percent = player.health / player.max_health
    music_engine.SetVariable("PlayerHealth", health_percent)

    # Update combat state
    var in_combat = get_node("/root/Main/CombatManager").is_in_combat
    music_engine.SetVariable("CombatActive", in_combat)

func on_enemy_spotted():
    music_engine.TriggerEvent("TransitionTo", "Tension")
    music_engine.SetVariable("IntensityLevel", 0.7)

func on_combat_started():
    music_engine.TriggerEvent("TransitionTo", "Combat")
    music_engine.SetVariable("IntensityLevel", 1.0)

func on_combat_ended():
    music_engine.TriggerEvent("CrossfadeTo", "Exploration", 3.0)
    music_engine.SetVariable("IntensityLevel", 0.3)

func on_boss_phase_change(phase: int):
    music_engine.TriggerEvent("SetLayer", "BossPhase" + str(phase), true)
    if phase > 1:
        music_engine.TriggerEvent("SetLayer", "BossPhase" + str(phase - 1), false)
```

### AudioStreamPlayer Integration

```gdscript
extends AudioStreamPlayer

var music_engine
var audio_buffer: PackedFloat32Array

func _ready():
    music_engine = MusicEngineEditor.Services.ExternalControlService.Instance
    audio_buffer = PackedFloat32Array()
    audio_buffer.resize(1024)

    # Create audio stream
    var generator = AudioStreamGenerator.new()
    generator.mix_rate = 44100
    generator.buffer_length = 0.1
    stream = generator
    play()

func _process(delta):
    var playback = get_stream_playback()
    var frames_available = playback.get_frames_available()

    if frames_available > 0:
        # Get audio from MusicEngine
        music_engine.ProcessAudio(audio_buffer, frames_available, 44100)

        # Push to stream
        for i in range(frames_available):
            playback.push_frame(Vector2(audio_buffer[i * 2], audio_buffer[i * 2 + 1]))
```

## Unreal Engine Integration

### C++ Setup

```cpp
// MusicEngineComponent.h
#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "MusicEngineComponent.generated.h"

UCLASS(ClassGroup=(Audio), meta=(BlueprintSpawnableComponent))
class MYGAME_API UMusicEngineComponent : public UActorComponent
{
    GENERATED_BODY()

public:
    UMusicEngineComponent();

    virtual void BeginPlay() override;
    virtual void TickComponent(float DeltaTime, ELevelTick TickType,
        FActorComponentTickFunction* ThisTickFunction) override;

    UFUNCTION(BlueprintCallable, Category = "Music")
    void SetMusicVariable(FString Name, float Value);

    UFUNCTION(BlueprintCallable, Category = "Music")
    void TriggerMusicEvent(FString EventName, FString Parameter = "");

    UFUNCTION(BlueprintCallable, Category = "Music")
    void TransitionToState(FString StateName, float TransitionTime = 1.0f);

private:
    void* MusicEngineInstance;
    void UpdateMusicState();
};
```

```cpp
// MusicEngineComponent.cpp
#include "MusicEngineComponent.h"

// Assumes MusicEngine is loaded as a DLL
typedef void* (*GetInstanceFunc)();
typedef void (*SetVariableFunc)(void*, const char*, float);
typedef void (*TriggerEventFunc)(void*, const char*, const char*);

static GetInstanceFunc GetInstance = nullptr;
static SetVariableFunc SetVariable = nullptr;
static TriggerEventFunc TriggerEvent = nullptr;

UMusicEngineComponent::UMusicEngineComponent()
{
    PrimaryComponentTick.bCanEverTick = true;
}

void UMusicEngineComponent::BeginPlay()
{
    Super::BeginPlay();

    // Load MusicEngine DLL
    void* DllHandle = FPlatformProcess::GetDllHandle(TEXT("MusicEngine.dll"));
    if (DllHandle)
    {
        GetInstance = (GetInstanceFunc)FPlatformProcess::GetDllExport(
            DllHandle, TEXT("GetExternalControlService"));
        SetVariable = (SetVariableFunc)FPlatformProcess::GetDllExport(
            DllHandle, TEXT("SetVariable"));
        TriggerEvent = (TriggerEventFunc)FPlatformProcess::GetDllExport(
            DllHandle, TEXT("TriggerEvent"));

        if (GetInstance)
        {
            MusicEngineInstance = GetInstance();
            TriggerMusicEvent("Play");
        }
    }
}

void UMusicEngineComponent::TickComponent(float DeltaTime, ELevelTick TickType,
    FActorComponentTickFunction* ThisTickFunction)
{
    Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
    UpdateMusicState();
}

void UMusicEngineComponent::SetMusicVariable(FString Name, float Value)
{
    if (SetVariable && MusicEngineInstance)
    {
        SetVariable(MusicEngineInstance, TCHAR_TO_ANSI(*Name), Value);
    }
}

void UMusicEngineComponent::TriggerMusicEvent(FString EventName, FString Parameter)
{
    if (TriggerEvent && MusicEngineInstance)
    {
        TriggerEvent(MusicEngineInstance,
            TCHAR_TO_ANSI(*EventName),
            TCHAR_TO_ANSI(*Parameter));
    }
}

void UMusicEngineComponent::TransitionToState(FString StateName, float TransitionTime)
{
    TriggerMusicEvent("CrossfadeTo", StateName);
}

void UMusicEngineComponent::UpdateMusicState()
{
    // Get player state
    ACharacter* Player = UGameplayStatics::GetPlayerCharacter(GetWorld(), 0);
    if (Player)
    {
        // Update health
        UHealthComponent* Health = Player->FindComponentByClass<UHealthComponent>();
        if (Health)
        {
            SetMusicVariable("PlayerHealth", Health->GetHealthPercent());
        }
    }

    // Update time of day
    AGameState* GameState = GetWorld()->GetGameState<AGameState>();
    if (GameState)
    {
        SetMusicVariable("TimeOfDay", GameState->GetTimeOfDay());
    }
}
```

### Blueprint Integration

```cpp
// BlueprintFunctionLibrary for easier Blueprint access
UCLASS()
class MYGAME_API UMusicEngineBPLibrary : public UBlueprintFunctionLibrary
{
    GENERATED_BODY()

public:
    UFUNCTION(BlueprintCallable, Category = "Music Engine")
    static void SetMusicIntensity(float Intensity);

    UFUNCTION(BlueprintCallable, Category = "Music Engine")
    static void TransitionToMusicState(FString StateName);

    UFUNCTION(BlueprintCallable, Category = "Music Engine")
    static void PlayMusicStinger(FString StingerName);
};
```

## Custom Engine Integration

### Minimal C API

```c
// musicengine_api.h
#ifndef MUSICENGINE_API_H
#define MUSICENGINE_API_H

#ifdef __cplusplus
extern "C" {
#endif

// Initialization
void* MusicEngine_Create();
void MusicEngine_Destroy(void* engine);
int MusicEngine_LoadProject(void* engine, const char* projectPath);

// Playback control
void MusicEngine_Play(void* engine);
void MusicEngine_Stop(void* engine);
void MusicEngine_Pause(void* engine);
void MusicEngine_SetPosition(void* engine, double seconds);

// Variables
void MusicEngine_SetFloat(void* engine, const char* name, float value);
void MusicEngine_SetInt(void* engine, const char* name, int value);
void MusicEngine_SetBool(void* engine, const char* name, int value);
float MusicEngine_GetFloat(void* engine, const char* name);

// Events
void MusicEngine_TriggerEvent(void* engine, const char* eventName, const char* param);

// Audio processing
void MusicEngine_ProcessAudio(void* engine, float* buffer, int sampleCount, int sampleRate);

// Callbacks
typedef void (*BeatCallback)(int beatNumber, void* userData);
typedef void (*EventCallback)(const char* eventName, void* userData);
void MusicEngine_SetBeatCallback(void* engine, BeatCallback callback, void* userData);
void MusicEngine_SetEventCallback(void* engine, EventCallback callback, void* userData);

#ifdef __cplusplus
}
#endif

#endif // MUSICENGINE_API_H
```

### Example: Custom Game Engine Integration

```c
#include "musicengine_api.h"
#include "game.h"

static void* g_musicEngine = NULL;

void Game_InitMusic()
{
    g_musicEngine = MusicEngine_Create();
    MusicEngine_LoadProject(g_musicEngine, "data/music/main.meproj");

    // Set callbacks
    MusicEngine_SetBeatCallback(g_musicEngine, OnBeat, NULL);

    // Start playback
    MusicEngine_Play(g_musicEngine);
}

void Game_UpdateMusic(float deltaTime)
{
    // Update music parameters from game state
    MusicEngine_SetFloat(g_musicEngine, "PlayerHealth", g_player.health / g_player.maxHealth);
    MusicEngine_SetFloat(g_musicEngine, "IntensityLevel", GetCombatIntensity());
    MusicEngine_SetBool(g_musicEngine, "IsIndoors", g_player.isIndoors);
}

void Game_ProcessMusicAudio(float* buffer, int sampleCount)
{
    MusicEngine_ProcessAudio(g_musicEngine, buffer, sampleCount, 44100);
}

void OnBeat(int beatNumber, void* userData)
{
    // Sync visual effects to beat
    FlashBeatIndicator();
}

void OnEnterCombat()
{
    MusicEngine_TriggerEvent(g_musicEngine, "TransitionTo", "Combat");
}

void OnExitCombat()
{
    MusicEngine_TriggerEvent(g_musicEngine, "CrossfadeTo", "Exploration");
}

void Game_ShutdownMusic()
{
    MusicEngine_Stop(g_musicEngine);
    MusicEngine_Destroy(g_musicEngine);
    g_musicEngine = NULL;
}
```

## Advanced: Music State Machine

```csharp
using MusicEngineEditor.Services;

public class MusicStateMachine
{
    private readonly ExternalControlService _music;
    private readonly Dictionary<string, MusicState> _states = new();
    private MusicState _currentState;

    public MusicStateMachine()
    {
        _music = ExternalControlService.Instance;
        DefineStates();
    }

    private void DefineStates()
    {
        // Define music states
        AddState(new MusicState
        {
            Name = "MainMenu",
            Layers = new[] { "Ambient", "Melody" },
            BPM = 90,
            Intensity = 0.3f
        });

        AddState(new MusicState
        {
            Name = "Exploration",
            Layers = new[] { "Ambient", "Percussion", "Bass" },
            BPM = 100,
            Intensity = 0.5f,
            TransitionRules = new Dictionary<string, string>
            {
                { "EnemyNearby", "Tension" },
                { "EnterCombat", "Combat" },
                { "EnterBoss", "BossIntro" }
            }
        });

        AddState(new MusicState
        {
            Name = "Tension",
            Layers = new[] { "Ambient", "Percussion", "Bass", "TensionStrings" },
            BPM = 110,
            Intensity = 0.7f,
            TransitionRules = new Dictionary<string, string>
            {
                { "EnemyGone", "Exploration" },
                { "EnterCombat", "Combat" }
            }
        });

        AddState(new MusicState
        {
            Name = "Combat",
            Layers = new[] { "Percussion", "Bass", "CombatMelody", "CombatStrings" },
            BPM = 140,
            Intensity = 1.0f,
            TransitionRules = new Dictionary<string, string>
            {
                { "CombatEnded", "CombatVictory" },
                { "PlayerDied", "Death" }
            }
        });

        AddState(new MusicState
        {
            Name = "CombatVictory",
            Layers = new[] { "VictoryFanfare" },
            Duration = 4.0f,  // One-shot, then transition
            NextState = "Exploration"
        });
    }

    public void TransitionTo(string stateName, float transitionTime = 1.0f)
    {
        if (!_states.TryGetValue(stateName, out var newState))
            return;

        // Disable old layers
        if (_currentState != null)
        {
            foreach (var layer in _currentState.Layers)
            {
                if (!newState.Layers.Contains(layer))
                {
                    _music.TriggerEvent("SetLayer", layer, false);
                }
            }
        }

        // Enable new layers
        foreach (var layer in newState.Layers)
        {
            _music.TriggerEvent("SetLayer", layer, true);
        }

        // Update parameters
        _music.SetVariable("BPM", newState.BPM);
        _music.SetVariable("IntensityLevel", newState.Intensity);

        _currentState = newState;

        // Handle one-shot states
        if (newState.Duration > 0)
        {
            ScheduleTransition(newState.NextState, newState.Duration);
        }
    }

    public void HandleGameEvent(string eventName)
    {
        if (_currentState?.TransitionRules == null)
            return;

        if (_currentState.TransitionRules.TryGetValue(eventName, out var nextState))
        {
            TransitionTo(nextState);
        }
    }

    private void AddState(MusicState state)
    {
        _states[state.Name] = state;
    }

    private async void ScheduleTransition(string stateName, float delay)
    {
        await Task.Delay(TimeSpan.FromSeconds(delay));
        TransitionTo(stateName);
    }
}

public class MusicState
{
    public string Name { get; set; }
    public string[] Layers { get; set; }
    public float BPM { get; set; }
    public float Intensity { get; set; }
    public Dictionary<string, string> TransitionRules { get; set; }
    public float Duration { get; set; }  // 0 = indefinite
    public string NextState { get; set; }
}
```

## Performance Tips

1. **Batch variable updates**: Update multiple variables in a single frame rather than scattered throughout
2. **Use events for one-shots**: Use `PlayStinger` for impact sounds rather than creating new tracks
3. **Transition on bar boundaries**: Use `OnBarComplete` callback to time transitions
4. **Preload states**: Load all music states at level start to avoid hitches
5. **Use audio threading**: Process audio on a separate thread to avoid frame drops
