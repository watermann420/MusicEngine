//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Unit tests for the Session class.

using FluentAssertions;
using MusicEngine.Core;
using Xunit;

namespace MusicEngine.Tests.Core;

public class SessionTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _createdFiles = [];

    public SessionTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "MusicEngineTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up test files
        foreach (var file in _createdFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    private string GetTestFilePath(string filename = "test_session.json")
    {
        var path = Path.Combine(_testDirectory, filename);
        _createdFiles.Add(path);
        return path;
    }

    #region Save and Load Roundtrip Tests

    [Fact]
    public void SaveAndLoad_ShouldPreserveBasicProperties()
    {
        // Arrange
        var session = new Session();
        session.Data.BPM = 140f;
        session.Data.SampleRate = 48000;
        session.Data.MasterVolume = 0.8f;
        session.Data.TimeSignatureNumerator = 3;
        session.Data.TimeSignatureDenominator = 4;
        var filePath = GetTestFilePath();

        // Act
        session.Save(filePath);
        var loadedSession = new Session();
        loadedSession.Load(filePath);

        // Assert
        loadedSession.Data.BPM.Should().Be(140f);
        loadedSession.Data.SampleRate.Should().Be(48000);
        loadedSession.Data.MasterVolume.Should().Be(0.8f);
        loadedSession.Data.TimeSignatureNumerator.Should().Be(3);
        loadedSession.Data.TimeSignatureDenominator.Should().Be(4);
    }

    [Fact]
    public void SaveAndLoad_ShouldPreserveMetadata()
    {
        // Arrange
        var session = new Session();
        session.Data.Metadata.Name = "My Test Project";
        session.Data.Metadata.Author = "Test Author";
        session.Data.Metadata.Description = "A test description";
        session.Data.Metadata.Version = "2.0";
        session.Data.Metadata.Tags.Add("Electronic");
        session.Data.Metadata.Tags.Add("Ambient");
        var filePath = GetTestFilePath();

        // Act
        session.Save(filePath);
        var loadedSession = new Session();
        loadedSession.Load(filePath);

        // Assert
        loadedSession.Data.Metadata.Name.Should().Be("My Test Project");
        loadedSession.Data.Metadata.Author.Should().Be("Test Author");
        loadedSession.Data.Metadata.Description.Should().Be("A test description");
        loadedSession.Data.Metadata.Version.Should().Be("2.0");
        loadedSession.Data.Metadata.Tags.Should().Contain("Electronic");
        loadedSession.Data.Metadata.Tags.Should().Contain("Ambient");
    }

    [Fact]
    public void SaveAndLoad_ShouldPreservePatterns()
    {
        // Arrange
        var session = new Session();
        var pattern = new PatternConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Bass Pattern",
            LoopLength = 8.0,
            IsLooping = true,
            Enabled = true,
            InstrumentId = "synth1"
        };
        pattern.Events.Add(new NoteEventConfig { Note = 60, Beat = 0, Duration = 1.0, Velocity = 100 });
        pattern.Events.Add(new NoteEventConfig { Note = 64, Beat = 1, Duration = 0.5, Velocity = 80 });
        session.Data.Patterns.Add(pattern);
        var filePath = GetTestFilePath();

        // Act
        session.Save(filePath);
        var loadedSession = new Session();
        loadedSession.Load(filePath);

        // Assert
        loadedSession.Data.Patterns.Should().HaveCount(1);
        var loadedPattern = loadedSession.Data.Patterns[0];
        loadedPattern.Name.Should().Be("Bass Pattern");
        loadedPattern.LoopLength.Should().Be(8.0);
        loadedPattern.IsLooping.Should().BeTrue();
        loadedPattern.Events.Should().HaveCount(2);
        loadedPattern.Events[0].Note.Should().Be(60);
        loadedPattern.Events[1].Velocity.Should().Be(80);
    }

    [Fact]
    public async Task SaveAsyncAndLoadAsync_ShouldWork()
    {
        // Arrange
        var session = new Session();
        session.Data.BPM = 128f;
        session.Data.Metadata.Name = "Async Test";
        var filePath = GetTestFilePath();

        // Act
        await session.SaveAsync(filePath, null, CancellationToken.None);
        var loadedSession = new Session();
        await loadedSession.LoadAsync(filePath, null, CancellationToken.None);

        // Assert
        loadedSession.Data.BPM.Should().Be(128f);
        loadedSession.Data.Metadata.Name.Should().Be("Async Test");
    }

    #endregion

    #region HasUnsavedChanges Tests

    [Fact]
    public void HasUnsavedChanges_ShouldBeFalseOnNewSession()
    {
        // Arrange & Act
        var session = new Session();

        // Assert
        session.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void HasUnsavedChanges_ShouldBeTrueAfterMarkChanged()
    {
        // Arrange
        var session = new Session();

        // Act
        session.MarkChanged();

        // Assert
        session.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void HasUnsavedChanges_ShouldBeFalseAfterSave()
    {
        // Arrange
        var session = new Session();
        session.MarkChanged();
        var filePath = GetTestFilePath();

        // Act
        session.Save(filePath);

        // Assert
        session.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void HasUnsavedChanges_ShouldBeFalseAfterLoad()
    {
        // Arrange
        var session = new Session();
        session.Save(GetTestFilePath("original.json"));

        var newSession = new Session();
        newSession.MarkChanged();
        newSession.HasUnsavedChanges.Should().BeTrue();

        // Act
        newSession.Load(GetTestFilePath("original.json"));

        // Assert
        newSession.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void MarkChanged_ShouldFireSessionChangedEvent()
    {
        // Arrange
        var session = new Session();
        var eventFired = false;
        session.SessionChanged += (_, _) => eventFired = true;

        // Act
        session.MarkChanged();

        // Assert
        eventFired.Should().BeTrue();
    }

    #endregion

    #region Pattern Serialization Tests

    [Fact]
    public void Patterns_ShouldSerializeMultiplePatterns()
    {
        // Arrange
        var session = new Session();
        for (int i = 0; i < 5; i++)
        {
            session.Data.Patterns.Add(new PatternConfig
            {
                Name = $"Pattern {i + 1}",
                LoopLength = (i + 1) * 2
            });
        }
        var filePath = GetTestFilePath();

        // Act
        session.Save(filePath);
        var loadedSession = new Session();
        loadedSession.Load(filePath);

        // Assert
        loadedSession.Data.Patterns.Should().HaveCount(5);
        loadedSession.Data.Patterns[2].Name.Should().Be("Pattern 3");
        loadedSession.Data.Patterns[4].LoopLength.Should().Be(10);
    }

    [Fact]
    public void Patterns_ShouldSerializeComplexNoteEvents()
    {
        // Arrange
        var session = new Session();
        var pattern = new PatternConfig { Name = "Complex Pattern" };

        // Add chord
        pattern.Events.Add(new NoteEventConfig { Note = 60, Beat = 0, Duration = 2.0, Velocity = 100 });
        pattern.Events.Add(new NoteEventConfig { Note = 64, Beat = 0, Duration = 2.0, Velocity = 100 });
        pattern.Events.Add(new NoteEventConfig { Note = 67, Beat = 0, Duration = 2.0, Velocity = 100 });

        // Add melody notes at various positions
        pattern.Events.Add(new NoteEventConfig { Note = 72, Beat = 0.5, Duration = 0.25, Velocity = 80 });
        pattern.Events.Add(new NoteEventConfig { Note = 74, Beat = 1.0, Duration = 0.5, Velocity = 90 });

        session.Data.Patterns.Add(pattern);
        var filePath = GetTestFilePath();

        // Act
        session.Save(filePath);
        var loadedSession = new Session();
        loadedSession.Load(filePath);

        // Assert
        var loadedPattern = loadedSession.Data.Patterns[0];
        loadedPattern.Events.Should().HaveCount(5);
        loadedPattern.Events.Where(e => e.Beat == 0).Should().HaveCount(3);
    }

    #endregion

    #region Invalid Data Handling Tests

    [Fact]
    public void Load_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var session = new Session();
        var invalidPath = Path.Combine(_testDirectory, "nonexistent.json");

        // Act
        var act = () => session.Load(invalidPath);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Load_WithEmptyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var session = new Session();

        // Act
        var act = () => session.Load("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Load_WithNullPath_ShouldThrowArgumentException()
    {
        // Arrange
        var session = new Session();

        // Act
        var act = () => session.Load(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Save_WithEmptyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var session = new Session();

        // Act
        var act = () => session.Save("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_WithInvalidBpm_ShouldReturnError()
    {
        // Arrange
        var session = new Session();
        session.Data.BPM = -10f;

        // Act
        var errors = session.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("BPM"));
    }

    [Fact]
    public void Validate_WithInvalidSampleRate_ShouldReturnError()
    {
        // Arrange
        var session = new Session();
        session.Data.SampleRate = 500; // Too low

        // Act
        var errors = session.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("Sample rate"));
    }

    [Fact]
    public void Validate_WithInvalidMasterVolume_ShouldReturnError()
    {
        // Arrange
        var session = new Session();
        session.Data.MasterVolume = 10f; // Too high

        // Act
        var errors = session.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("Master volume"));
    }

    [Fact]
    public void Validate_WithInvalidNoteNumber_ShouldReturnError()
    {
        // Arrange
        var session = new Session();
        session.Data.Patterns.Add(new PatternConfig
        {
            Name = "Invalid Pattern",
            Events = [new NoteEventConfig { Note = 200, Beat = 0, Duration = 1, Velocity = 100 }]
        });

        // Act
        var errors = session.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("MIDI note"));
    }

    [Fact]
    public void Validate_WithInvalidVelocity_ShouldReturnError()
    {
        // Arrange
        var session = new Session();
        session.Data.Patterns.Add(new PatternConfig
        {
            Name = "Invalid Pattern",
            Events = [new NoteEventConfig { Note = 60, Beat = 0, Duration = 1, Velocity = 200 }]
        });

        // Act
        var errors = session.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("velocity"));
    }

    [Fact]
    public void Validate_WithValidSession_ShouldReturnEmptyList()
    {
        // Arrange
        var session = new Session();
        session.Data.BPM = 120f;
        session.Data.SampleRate = 44100;
        session.Data.MasterVolume = 1.0f;

        // Act
        var errors = session.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    #endregion

    #region Empty Session Tests

    [Fact]
    public void Save_EmptySession_ShouldSucceed()
    {
        // Arrange
        var session = new Session();
        var filePath = GetTestFilePath();

        // Act
        var act = () => session.Save(filePath);

        // Assert
        act.Should().NotThrow();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public void Load_EmptySession_ShouldSucceed()
    {
        // Arrange
        var session = new Session();
        var filePath = GetTestFilePath();
        session.Save(filePath);

        // Act
        var loadedSession = new Session();
        var act = () => loadedSession.Load(filePath);

        // Assert
        act.Should().NotThrow();
        loadedSession.Data.Should().NotBeNull();
    }

    [Fact]
    public void EmptySession_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var session = new Session();

        // Assert
        session.Data.BPM.Should().Be(120f);
        session.Data.SampleRate.Should().Be(44100);
        session.Data.MasterVolume.Should().Be(1.0f);
        session.Data.TimeSignatureNumerator.Should().Be(4);
        session.Data.TimeSignatureDenominator.Should().Be(4);
        session.Data.Patterns.Should().BeEmpty();
        session.Data.InstrumentConfigs.Should().BeEmpty();
        session.Data.EffectConfigs.Should().BeEmpty();
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Save_ShouldFireSessionSavedEvent()
    {
        // Arrange
        var session = new Session();
        var eventFired = false;
        session.SessionSaved += (_, _) => eventFired = true;
        var filePath = GetTestFilePath();

        // Act
        session.Save(filePath);

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void Load_ShouldFireSessionLoadedEvent()
    {
        // Arrange
        var session = new Session();
        session.Save(GetTestFilePath("for_load.json"));

        var loadSession = new Session();
        var eventFired = false;
        loadSession.SessionLoaded += (_, _) => eventFired = true;

        // Act
        loadSession.Load(GetTestFilePath("for_load.json"));

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void Save_ShouldUpdateFilePath()
    {
        // Arrange
        var session = new Session();
        var filePath = GetTestFilePath();
        session.FilePath.Should().BeNull();

        // Act
        session.Save(filePath);

        // Assert
        session.FilePath.Should().Be(filePath);
    }

    [Fact]
    public void Save_ShouldUpdateModifiedDate()
    {
        // Arrange
        var session = new Session();
        var beforeSave = DateTime.Now.AddSeconds(-1);
        var filePath = GetTestFilePath();

        // Act
        session.Save(filePath);

        // Assert
        session.Data.Metadata.ModifiedDate.Should().BeAfter(beforeSave);
    }

    #endregion

    #region Template Tests

    [Fact]
    public void CreateFromTemplate_WithValidTemplate_ShouldApplySettings()
    {
        // Act
        var session = Session.CreateFromTemplate("EDM");

        // Assert
        session.Data.BPM.Should().Be(128f);
        session.Data.SampleRate.Should().Be(44100);
    }

    [Fact]
    public void CreateFromTemplate_WithInvalidTemplate_ShouldThrow()
    {
        // Act
        var act = () => Session.CreateFromTemplate("NonExistentTemplate");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Templates_ShouldContainDefaultTemplates()
    {
        // Assert
        Session.Templates.Should().Contain(t => t.Name == "Default");
        Session.Templates.Should().Contain(t => t.Name == "EDM");
        Session.Templates.Should().Contain(t => t.Name == "Hip Hop");
    }

    [Fact]
    public void SaveAsTemplate_ShouldAddToTemplateList()
    {
        // Arrange
        var session = new Session();
        session.Data.BPM = 175f;
        var initialCount = Session.Templates.Count;

        // Act
        var template = session.SaveAsTemplate("DnB Template", "Drum and Bass template");

        // Assert
        Session.Templates.Should().HaveCount(initialCount + 1);
        template.BPM.Should().Be(175f);
        template.Name.Should().Be("DnB Template");
        template.Description.Should().Be("Drum and Bass template");

        // Cleanup
        Session.Templates.Remove(template);
    }

    #endregion

    #region GetSessionInfo Tests

    [Fact]
    public void GetSessionInfo_ShouldReturnMetadataWithoutFullLoad()
    {
        // Arrange
        var session = new Session();
        session.Data.Metadata.Name = "Quick Info Test";
        session.Data.Metadata.Author = "Test User";
        var filePath = GetTestFilePath();
        session.Save(filePath);

        // Act
        var info = Session.GetSessionInfo(filePath);

        // Assert
        info.Should().NotBeNull();
        info!.Name.Should().Be("Quick Info Test");
        info.Author.Should().Be("Test User");
    }

    [Fact]
    public void GetSessionInfo_WithNonExistentFile_ShouldReturnNull()
    {
        // Act
        var info = Session.GetSessionInfo(Path.Combine(_testDirectory, "nonexistent.json"));

        // Assert
        info.Should().BeNull();
    }

    #endregion
}
