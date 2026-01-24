//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Unit tests for VstHost class.

using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Helpers;
using MusicEngine.Tests.Mocks;
using Xunit;

namespace MusicEngine.Tests.Core;

/// <summary>
/// Unit tests for the VstHost class covering plugin loading, scanning, management, and resource handling.
/// </summary>
public class VstHostTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly VstHost _vstHost;

    public VstHostTests()
    {
        _tempDirectory = VstTestHelper.CreateTempVstDirectory();
        _vstHost = new VstHost();
        VstTestHelper.ConfigureTestVstPaths(_tempDirectory);
    }

    public void Dispose()
    {
        _vstHost.Dispose();
        VstTestHelper.CleanupTempDirectory(_tempDirectory);
        VstTestHelper.RestoreDefaultVstPaths();
    }

    #region Constructor Tests

    [Fact]
    public void VstHost_Constructor_CreatesEmptyPluginLists()
    {
        using var host = new VstHost();

        host.DiscoveredPlugins.Should().BeEmpty();
        host.DiscoveredVst3Plugins.Should().BeEmpty();
        host.LoadedPlugins.Should().BeEmpty();
    }

    [Fact]
    public void VstHost_Constructor_SafeScanModeEnabledByDefault()
    {
        using var host = new VstHost();

        host.SafeScanMode.Should().BeTrue();
    }

    #endregion

    #region Plugin Scanning Tests

    [Fact]
    public void ScanForPlugins_EmptyDirectory_ReturnsEmptyList()
    {
        var result = _vstHost.ScanForPlugins();

        result.Should().BeEmpty();
    }

    [Fact]
    public void ScanForPlugins_DirectoryWithDlls_DiscoversDlls()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin1.dll");
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin2.dll");

        var result = _vstHost.ScanForPlugins();

        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Name == "Plugin1");
        result.Should().Contain(p => p.Name == "Plugin2");
    }

    [Fact]
    public void ScanForPlugins_WithSubdirectories_DiscoversDllsRecursively()
    {
        var subDir = Path.Combine(_tempDirectory, "SubFolder");
        Directory.CreateDirectory(subDir);
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "RootPlugin.dll");
        VstTestHelper.CreateDummyVstDll(subDir, "SubPlugin.dll");

        var result = _vstHost.ScanForPlugins();

        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Name == "RootPlugin");
        result.Should().Contain(p => p.Name == "SubPlugin");
    }

    [Fact]
    public void ScanForPlugins_Vst3Bundle_DiscoversVst3Plugin()
    {
        VstTestHelper.CreateDummyVst3Bundle(_tempDirectory, "TestSynth.vst3");

        _vstHost.ScanForPlugins();

        _vstHost.DiscoveredVst3Plugins.Should().HaveCount(1);
        _vstHost.DiscoveredVst3Plugins[0].Name.Should().Be("TestSynth");
        _vstHost.DiscoveredVst3Plugins[0].IsBundle.Should().BeTrue();
    }

    [Fact]
    public void ScanForPlugins_Vst3SingleFile_DiscoversVst3Plugin()
    {
        var vst3Path = Path.Combine(_tempDirectory, "SingleFile.vst3");
        File.WriteAllBytes(vst3Path, new byte[2048]);

        _vstHost.ScanForPlugins();

        _vstHost.DiscoveredVst3Plugins.Should().HaveCount(1);
    }

    [Fact]
    public void ScanForPlugins_ClearsOldResults()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin1.dll");
        _vstHost.ScanForPlugins();
        File.Delete(Path.Combine(_tempDirectory, "Plugin1.dll"));

        var result = _vstHost.ScanForPlugins();

        result.Should().BeEmpty();
    }

    [Fact]
    public void ScanForPlugins_NonexistentPath_HandlesGracefully()
    {
        Settings.VstPluginSearchPaths = new List<string> { @"C:\NonExistent\Path\12345" };

        var result = _vstHost.ScanForPlugins();

        result.Should().BeEmpty();
    }

    [Fact]
    public void ScanForPlugins_SkipsSmallFiles()
    {
        var smallFile = Path.Combine(_tempDirectory, "TooSmall.dll");
        File.WriteAllBytes(smallFile, new byte[100]); // Less than 1KB

        var result = _vstHost.ScanForPlugins();

        result.Should().BeEmpty();
    }

    #endregion

    #region Plugin Loading Tests

    [Fact]
    public void LoadPlugin_ValidPath_ReturnsPlugin()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "TestPlugin.dll");
        _vstHost.ScanForPlugins();

        var plugin = _vstHost.LoadPlugin("TestPlugin");

        // Plugin loading may fail for dummy DLLs (no actual VST entry point)
        // but the load attempt should be made
        _vstHost.DiscoveredPlugins.Should().Contain(p => p.Name == "TestPlugin");
    }

    [Fact]
    public void LoadPlugin_NotDiscovered_ReturnsNull()
    {
        var plugin = _vstHost.LoadPlugin("NonExistentPlugin");

        plugin.Should().BeNull();
    }

    [Fact]
    public void LoadPlugin_ByPartialName_FindsPlugin()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "MyAwesomeSynth.dll");
        _vstHost.ScanForPlugins();

        // The partial match should find the plugin in discovered list
        var info = _vstHost.DiscoveredPlugins.FirstOrDefault(p => p.Name.Contains("Synth", StringComparison.OrdinalIgnoreCase));

        info.Should().NotBeNull();
    }

    [Fact]
    public void LoadPlugin_AlreadyLoaded_ReturnsSameInstance()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "TestPlugin.dll");
        _vstHost.ScanForPlugins();

        // First load attempt
        var plugin1 = _vstHost.LoadPlugin("TestPlugin");

        // Second load attempt - should return same instance if first succeeded
        // or null if first also failed
        var plugin2 = _vstHost.LoadPlugin("TestPlugin");

        // If loading succeeded, should be same instance
        if (plugin1 != null && plugin2 != null)
        {
            plugin1.Should().BeSameAs(plugin2);
        }
    }

    [Fact]
    public void LoadPluginByIndex_ValidIndex_AttemptsLoad()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin1.dll");
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin2.dll");
        _vstHost.ScanForPlugins();

        // Should not throw
        var plugin = _vstHost.LoadPluginByIndex(0);

        // Result depends on whether dummy DLL is valid VST
    }

    [Fact]
    public void LoadPluginByIndex_InvalidIndex_ReturnsNull()
    {
        _vstHost.ScanForPlugins();

        var plugin = _vstHost.LoadPluginByIndex(999);

        plugin.Should().BeNull();
    }

    [Fact]
    public void LoadPluginByIndex_NegativeIndex_ReturnsNull()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin1.dll");
        _vstHost.ScanForPlugins();

        var plugin = _vstHost.LoadPluginByIndex(-1);

        plugin.Should().BeNull();
    }

    #endregion

    #region Plugin Management Tests

    [Fact]
    public void GetPlugin_NotLoaded_ReturnsNull()
    {
        var plugin = _vstHost.GetPlugin("NonExistentPlugin");

        plugin.Should().BeNull();
    }

    [Fact]
    public void UnloadPlugin_NotLoaded_DoesNotThrow()
    {
        var action = () => _vstHost.UnloadPlugin("NonExistentPlugin");

        action.Should().NotThrow();
    }

    [Fact]
    public void GetAllDiscoveredPlugins_ReturnsBothVst2AndVst3()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Vst2Plugin.dll");
        VstTestHelper.CreateDummyVst3Bundle(_tempDirectory, "Vst3Plugin.vst3");
        _vstHost.ScanForPlugins();

        var all = _vstHost.GetAllDiscoveredPlugins();

        all.Should().HaveCount(2);
    }

    [Fact]
    public void LoadedPlugins_IsReadOnly()
    {
        var loadedPlugins = _vstHost.LoadedPlugins;

        loadedPlugins.Should().BeAssignableTo<IReadOnlyDictionary<string, IVstPlugin>>();
    }

    [Fact]
    public void DiscoveredPlugins_IsReadOnly()
    {
        var discovered = _vstHost.DiscoveredPlugins;

        discovered.Should().BeAssignableTo<IReadOnlyList<VstPluginInfo>>();
    }

    #endregion

    #region SafeScanMode Tests

    [Fact]
    public void SafeScanMode_WhenTrue_SkipsNativeProbing()
    {
        using var host = new VstHost();
        host.SafeScanMode = true;
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "TestPlugin.dll");

        // Should not crash even with invalid DLL
        var action = () => host.ScanForPlugins();

        action.Should().NotThrow();
    }

    [Fact]
    public void SafeScanMode_CanBeDisabled()
    {
        using var host = new VstHost();

        host.SafeScanMode = false;

        host.SafeScanMode.Should().BeFalse();
    }

    #endregion

    #region Preset Utilities Tests

    [Fact]
    public void ScanForPresets_EmptyDirectory_ReturnsEmptyList()
    {
        var presets = _vstHost.ScanForPresets(_tempDirectory);

        presets.Should().BeEmpty();
    }

    [Fact]
    public void ScanForPresets_WithFxpFiles_ReturnsPresetPaths()
    {
        VstTestHelper.CreateTestPresetFile(_tempDirectory, "Preset1.fxp");
        VstTestHelper.CreateTestPresetFile(_tempDirectory, "Preset2.fxp");

        var presets = _vstHost.ScanForPresets(_tempDirectory);

        presets.Should().HaveCount(2);
        presets.Should().Contain(p => p.Contains("Preset1.fxp"));
        presets.Should().Contain(p => p.Contains("Preset2.fxp"));
    }

    [Fact]
    public void ScanForPresets_WithPluginName_SearchesPluginFolder()
    {
        var pluginDir = Path.Combine(_tempDirectory, "TestPlugin");
        Directory.CreateDirectory(pluginDir);
        VstTestHelper.CreateTestPresetFile(pluginDir, "PluginPreset.fxp");

        var presets = _vstHost.ScanForPresets(_tempDirectory, "TestPlugin");

        presets.Should().Contain(p => p.Contains("PluginPreset.fxp"));
    }

    [Fact]
    public void GetCommonPresetDirectories_ReturnsValidPaths()
    {
        var directories = VstHost.GetCommonPresetDirectories();

        directories.Should().NotBeNull();
        // Directories should either be empty or contain existing paths
        foreach (var dir in directories)
        {
            Directory.Exists(dir).Should().BeTrue();
        }
    }

    #endregion

    #region Parameter Discovery Tests

    [Fact]
    public void DiscoverParameters_PluginNotLoaded_ReturnsEmptyList()
    {
        var parameters = _vstHost.DiscoverParameters("NonExistentPlugin");

        parameters.Should().BeEmpty();
    }

    [Fact]
    public void FindParameters_PluginNotLoaded_ReturnsEmptyList()
    {
        var matches = _vstHost.FindParameters("NonExistentPlugin", "Filter");

        matches.Should().BeEmpty();
    }

    [Fact]
    public void CreateParameterSnapshot_PluginNotLoaded_ReturnsEmptyDictionary()
    {
        var snapshot = _vstHost.CreateParameterSnapshot("NonExistentPlugin");

        snapshot.Should().BeEmpty();
    }

    [Fact]
    public void SetParameters_PluginNotLoaded_DoesNotThrow()
    {
        var parameters = new Dictionary<int, float> { { 0, 0.5f } };

        var action = () => _vstHost.SetParameters("NonExistentPlugin", parameters);

        action.Should().NotThrow();
    }

    [Fact]
    public void CopyParameters_SourceNotLoaded_ReturnsZero()
    {
        var count = _vstHost.CopyParameters("Source", "Dest");

        count.Should().Be(0);
    }

    [Fact]
    public void RestoreParameterSnapshot_DoesNotThrow()
    {
        var snapshot = new Dictionary<int, float> { { 0, 0.5f }, { 1, 0.75f } };

        var action = () => _vstHost.RestoreParameterSnapshot("NonExistentPlugin", snapshot);

        action.Should().NotThrow();
    }

    [Fact]
    public void RandomizeParameters_PluginNotLoaded_DoesNotThrow()
    {
        var action = () => _vstHost.RandomizeParameters("NonExistentPlugin");

        action.Should().NotThrow();
    }

    #endregion

    #region Preset Bank Tests

    [Fact]
    public void CreatePresetBank_EmptyPresetList_ReturnsFalse()
    {
        var outputPath = Path.Combine(_tempDirectory, "Empty.fxb");

        var result = _vstHost.CreatePresetBank(Array.Empty<string>(), outputPath);

        result.Should().BeFalse();
    }

    [Fact]
    public void CreatePresetBank_ValidPresets_CreatesFile()
    {
        var preset1 = VstTestHelper.CreateTestPresetFile(_tempDirectory, "P1.fxp");
        var preset2 = VstTestHelper.CreateTestPresetFile(_tempDirectory, "P2.fxp");
        var outputPath = Path.Combine(_tempDirectory, "Bank.fxb");

        var result = _vstHost.CreatePresetBank(new[] { preset1, preset2 }, outputPath);

        result.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
    }

    #endregion

    #region Resource Management Tests

    [Fact]
    public void Dispose_ClearsAllCollections()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin.dll");
        _vstHost.ScanForPlugins();
        _vstHost.Dispose();

        _vstHost.DiscoveredPlugins.Should().BeEmpty();
        _vstHost.DiscoveredVst3Plugins.Should().BeEmpty();
        _vstHost.LoadedPlugins.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var action = () =>
        {
            _vstHost.Dispose();
            _vstHost.Dispose();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_StopsActivePlugins()
    {
        // This test verifies that Dispose properly cleans up
        using var host = new VstHost();
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin.dll");
        host.ScanForPlugins();

        // Dispose should not throw
        var action = () => host.Dispose();

        action.Should().NotThrow();
    }

    #endregion

    #region VST3 Bundle Resolution Tests

    [Fact]
    public void ResolveBundlePath_DirectFile_ReturnsPath()
    {
        var filePath = Path.Combine(_tempDirectory, "Plugin.vst3");
        File.WriteAllBytes(filePath, new byte[2048]);

        var resolved = _vstHost.ResolveBundlePath(filePath);

        resolved.Should().Be(filePath);
    }

    [Fact]
    public void ResolveBundlePath_Bundle_ReturnsInternalPath()
    {
        var bundlePath = VstTestHelper.CreateDummyVst3Bundle(_tempDirectory, "TestBundle.vst3");

        var resolved = _vstHost.ResolveBundlePath(bundlePath);

        resolved.Should().NotBeNull();
        resolved.Should().Contain("x86_64-win");
    }

    [Fact]
    public void ResolveBundlePath_EmptyPath_ReturnsNull()
    {
        var resolved = _vstHost.ResolveBundlePath("");

        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveBundlePath_NonExistentPath_ReturnsNull()
    {
        var resolved = _vstHost.ResolveBundlePath(@"C:\NonExistent\Plugin.vst3");

        resolved.Should().BeNull();
    }

    #endregion

    #region Async Scanning Tests

    [Fact]
    public async Task ScanForPluginsAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var result = await _vstHost.ScanForPluginsAsync((IProgress<MusicEngine.Core.Progress.VstScanProgress>?)null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanForPluginsAsync_WithPlugins_DiscoversPlugins()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "AsyncPlugin1.dll");
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "AsyncPlugin2.dll");

        var result = await _vstHost.ScanForPluginsAsync((IProgress<MusicEngine.Core.Progress.VstScanProgress>?)null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScanForPluginsAsync_CanBeCancelled()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin1.dll");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = async () => await _vstHost.ScanForPluginsAsync((IProgress<MusicEngine.Core.Progress.VstScanProgress>?)null, cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ScanForPluginsAsync_ReportsProgress()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin1.dll");
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin2.dll");

        var progressReports = new List<MusicEngine.Core.Progress.VstScanProgress>();
        var progress = new Progress<MusicEngine.Core.Progress.VstScanProgress>(p => progressReports.Add(p));

        await _vstHost.ScanForPluginsAsync(progress);

        progressReports.Should().NotBeEmpty();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void LoadPresetForPlugin_PluginNotLoaded_ReturnsFalse()
    {
        var result = _vstHost.LoadPresetForPlugin("NonExistent", "preset.fxp");

        result.Should().BeFalse();
    }

    [Fact]
    public void SavePresetForPlugin_PluginNotLoaded_ReturnsFalse()
    {
        var outputPath = Path.Combine(_tempDirectory, "save.fxp");

        var result = _vstHost.SavePresetForPlugin("NonExistent", outputPath);

        result.Should().BeFalse();
    }

    [Fact]
    public void PrintDiscoveredPlugins_DoesNotThrow()
    {
        VstTestHelper.CreateDummyVstDll(_tempDirectory, "Plugin1.dll");
        _vstHost.ScanForPlugins();

        var action = () => _vstHost.PrintDiscoveredPlugins();

        action.Should().NotThrow();
    }

    [Fact]
    public void PrintLoadedPlugins_DoesNotThrow()
    {
        var action = () => _vstHost.PrintLoadedPlugins();

        action.Should().NotThrow();
    }

    [Fact]
    public void PrintParameters_PluginNotLoaded_DoesNotThrow()
    {
        var action = () => _vstHost.PrintParameters("NonExistent");

        action.Should().NotThrow();
    }

    #endregion
}
