// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

/// <summary>
/// VST3 interface GUIDs from the official Steinberg VST3 SDK.
/// These are used for COM-style QueryInterface calls.
/// </summary>
public static class Vst3Guids
{
    // Base interfaces (FUnknown)
    public static readonly Guid FUnknown = new("00000000-0000-0000-C000-000000000046");

    // Plugin Factory interfaces
    public static readonly Guid IPluginFactory = new("7A4D811C-5211-4A1F-AED9-D2EE0B43BF9F");
    public static readonly Guid IPluginFactory2 = new("0007B650-F24B-4C0B-A464-EDB9F00B2ABB");
    public static readonly Guid IPluginFactory3 = new("4555A2AB-C123-4E57-9B12-291036878931");

    // Plugin Base interfaces
    public static readonly Guid IPluginBase = new("22888DDB-156E-45AE-8358-B34808190625");

    // Component interfaces
    public static readonly Guid IComponent = new("E831FF31-F2D5-4301-928E-BBEE25697802");

    // Audio Processor interfaces
    public static readonly Guid IAudioProcessor = new("42043F99-B7DA-453C-A569-E79D9AAEC33D");

    // Edit Controller interfaces
    public static readonly Guid IEditController = new("DCD7BBE3-7742-448D-A874-AACC979C759E");
    public static readonly Guid IEditController2 = new("7F4EFE59-F320-4967-AC27-A3AEAFB63038");
    public static readonly Guid IComponentHandler = new("93A0BEA3-0BD0-45DB-8E89-0B0CC1E46AC6");
    public static readonly Guid IComponentHandler2 = new("F040B4B3-A360-45EC-ABCD-C045B4D5A2CC");
    public static readonly Guid IComponentHandler3 = new("69F11617-D26B-400D-A4B6-B9647B6EBBAB");
    public static readonly Guid IComponentHandlerBusActivation = new("067D02C1-5B4E-274D-A92D-90FD6EAF7240");

    // Connection interfaces
    public static readonly Guid IConnectionPoint = new("70A4156F-6E6E-4026-9891-48BFAA60D8D1");

    // Unit interfaces
    public static readonly Guid IUnitInfo = new("3D4BD6B5-913A-4FD2-A886-E768A5EB92C1");
    public static readonly Guid IUnitData = new("6C389611-D391-455D-B870-B83394A0EFDD");
    public static readonly Guid IProgramListData = new("8683B01F-7B35-4F70-A265-1DEC353AF4FF");

    // Plug View interfaces
    public static readonly Guid IPlugView = new("5BC32507-D060-49EA-A615-1B522B755B29");
    public static readonly Guid IPlugFrame = new("367FAF01-AFA9-4693-8D4D-A2A0ED0882A3");

    // Parameter interfaces
    public static readonly Guid IParameterChanges = new("A4779663-0BB6-4A56-B443-84A8466FEB9D");
    public static readonly Guid IParamValueQueue = new("01263A18-ED07-4F6F-98C9-D3564686F9BA");
    public static readonly Guid IParameterFinder = new("0F618302-215D-4587-A512-073C77B9D383");

    // Event interfaces
    public static readonly Guid IEventList = new("3A2C4214-3463-49FE-B2C4-F397B9695A44");

    // Note Expression interfaces
    public static readonly Guid INoteExpressionController = new("B7F8F859-4123-4872-9116-95814F3721A3");
    public static readonly Guid IKeyswitchController = new("1F2F76D3-BFFF-4B96-B996-5AB4EBD87A13");
    public static readonly Guid INoteExpressionPhysicalUIMapping = new("B03078FF-94D2-4AC8-90CC-D303D4133324");

    // MIDI interfaces
    public static readonly Guid IMidiMapping = new("DF0FF9F7-49B7-4669-B63A-B7327ADBF5E5");
    public static readonly Guid IMidiLearn = new("6B2449CC-4197-40B5-AB3C-79DAC5FE5C86");

    // Host Application interfaces
    public static readonly Guid IHostApplication = new("58E595CC-DB2D-4969-8B6A-AF8C36A664E5");
    public static readonly Guid IAttributeList = new("1E5F0AEB-CC7F-4533-A254-401138AD5EE4");
    public static readonly Guid IMessage = new("936F033B-C6C0-47DB-BB08-82F813C1E613");

    // Context Menu interfaces
    public static readonly Guid IContextMenu = new("2E93C863-0C9C-4588-97DB-ECF5AD17817D");
    public static readonly Guid IContextMenuTarget = new("3CDF2E75-85D3-4144-BF86-D36BD7C4894D");

    // Process Context interfaces
    public static readonly Guid IProcessContextRequirements = new("2A654303-EF76-4E3D-95B5-FE83730EF6D0");

    // Prefetch Support interfaces
    public static readonly Guid IPrefetchableSupport = new("8AE54FDA-E930-46B9-A285-55BCDC98E21E");

    // Automation State interfaces
    public static readonly Guid IAutomationState = new("B4E8287F-1BB3-46AA-83A4-666768937BAB");

    // Info Listener interfaces
    public static readonly Guid IInfoListener = new("0F194781-8D98-4ADA-BBA0-C1EFC011D8D0");

    // Plugin interface ID (for createInstance)
    public static readonly Guid kAudioEffectClass = new("E831FF31-F2D5-4301-928E-BBEE25697802");
    public static readonly Guid kInstrumentClass = new("E831FF31-F2D5-4301-928E-BBEE25697802");
}

/// <summary>
/// VST3 category strings for plugin classification
/// </summary>
public static class Vst3Categories
{
    public const string Fx = "Fx";
    public const string Instrument = "Instrument";
    public const string Spatial = "Spatial";
    public const string Generator = "Generator";
    public const string Analyzer = "Analyzer";

    // Effect sub-categories
    public const string FxDelay = "Fx|Delay";
    public const string FxDistortion = "Fx|Distortion";
    public const string FxDynamics = "Fx|Dynamics";
    public const string FxEQ = "Fx|EQ";
    public const string FxFilter = "Fx|Filter";
    public const string FxModulation = "Fx|Modulation";
    public const string FxPitchShift = "Fx|Pitch Shift";
    public const string FxReverb = "Fx|Reverb";
    public const string FxSurround = "Fx|Surround";
    public const string FxTools = "Fx|Tools";

    // Instrument sub-categories
    public const string InstrumentDrum = "Instrument|Drum";
    public const string InstrumentExternal = "Instrument|External";
    public const string InstrumentPiano = "Instrument|Piano";
    public const string InstrumentSampler = "Instrument|Sampler";
    public const string InstrumentSynth = "Instrument|Synth";
    public const string InstrumentSynthSampler = "Instrument|Synth|Sampler";
}
