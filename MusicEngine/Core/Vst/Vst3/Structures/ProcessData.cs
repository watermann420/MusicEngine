// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;

namespace MusicEngine.Core.Vst.Vst3.Structures
{
    /// <summary>
    /// Processing setup structure containing configuration for audio processing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessSetup
    {
        /// <summary>
        /// The processing mode (realtime, prefetch, offline).
        /// </summary>
        public int ProcessMode;

        /// <summary>
        /// The symbolic sample size (32-bit or 64-bit).
        /// </summary>
        public int SymbolicSampleSize;

        /// <summary>
        /// Maximum number of samples per processing block.
        /// </summary>
        public int MaxSamplesPerBlock;

        /// <summary>
        /// The sample rate in Hz.
        /// </summary>
        public double SampleRate;
    }

    /// <summary>
    /// Main process data structure passed to the audio processor.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessData
    {
        /// <summary>
        /// The processing mode (realtime, prefetch, offline).
        /// </summary>
        public int ProcessMode;

        /// <summary>
        /// The symbolic sample size (32-bit or 64-bit).
        /// </summary>
        public int SymbolicSampleSize;

        /// <summary>
        /// Number of samples to process in this block.
        /// </summary>
        public int NumSamples;

        /// <summary>
        /// Number of input buses.
        /// </summary>
        public int NumInputs;

        /// <summary>
        /// Number of output buses.
        /// </summary>
        public int NumOutputs;

        /// <summary>
        /// Pointer to array of AudioBusBuffers for input buses.
        /// </summary>
        public IntPtr Inputs;

        /// <summary>
        /// Pointer to array of AudioBusBuffers for output buses.
        /// </summary>
        public IntPtr Outputs;

        /// <summary>
        /// Pointer to IParameterChanges interface for input parameter changes.
        /// </summary>
        public IntPtr InputParameterChanges;

        /// <summary>
        /// Pointer to IParameterChanges interface for output parameter changes.
        /// </summary>
        public IntPtr OutputParameterChanges;

        /// <summary>
        /// Pointer to IEventList interface for input events.
        /// </summary>
        public IntPtr InputEvents;

        /// <summary>
        /// Pointer to IEventList interface for output events.
        /// </summary>
        public IntPtr OutputEvents;

        /// <summary>
        /// Pointer to ProcessContext structure for transport and timing information.
        /// </summary>
        public IntPtr ProcessContext;
    }

    /// <summary>
    /// Audio bus buffers structure containing channel buffer pointers.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioBusBuffers
    {
        /// <summary>
        /// Number of channels in this bus.
        /// </summary>
        public int NumChannels;

        /// <summary>
        /// Bitmask indicating which channels contain silence.
        /// </summary>
        public ulong SilenceFlags;

        /// <summary>
        /// Pointer to array of 32-bit float channel buffers (float**).
        /// </summary>
        public IntPtr ChannelBuffers32;

        /// <summary>
        /// Pointer to array of 64-bit double channel buffers (double**).
        /// </summary>
        public IntPtr ChannelBuffers64;
    }
}
