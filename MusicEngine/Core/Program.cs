//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: The main program entry point for the MusicEngine application.


using MusicEngine.Scripting;


// Parse command line arguments for safe mode (--safe or --no-audio)
EngineLauncher.ParseArguments(args);

await EngineLauncher.LaunchAsync(); // Start the Music Engine application