using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using Moq;
using Xunit;

namespace MusicEngine.Tests.Core;

public class PresetTests
{
    #region Preset Constructor Tests

    [Fact]
    public void Preset_Constructor_SetsDefaults()
    {
        var preset = new Preset();

        preset.Id.Should().NotBeNullOrEmpty();
        preset.Name.Should().BeEmpty();
        preset.Author.Should().BeEmpty();
        preset.Description.Should().BeEmpty();
        preset.Category.Should().BeEmpty();
        preset.Tags.Should().BeEmpty();
        preset.TargetType.Should().Be(PresetTargetType.Synth);
        preset.TargetClassName.Should().BeEmpty();
        preset.Parameters.Should().BeEmpty();
        preset.Version.Should().Be(1);
        preset.IsFavorite.Should().BeFalse();
        preset.Rating.Should().Be(0);
        preset.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Preset_Id_IsUniqueForEachInstance()
    {
        var preset1 = new Preset();
        var preset2 = new Preset();

        preset1.Id.Should().NotBe(preset2.Id);
    }

    [Fact]
    public void Preset_CreatedDate_IsSetToNow()
    {
        var before = DateTime.UtcNow;
        var preset = new Preset();
        var after = DateTime.UtcNow;

        preset.CreatedDate.Should().BeOnOrAfter(before);
        preset.CreatedDate.Should().BeOnOrBefore(after);
    }

    #endregion

    #region Preset Clone Tests

    [Fact]
    public void Preset_Clone_CreatesNewInstanceWithSameValues()
    {
        var original = new Preset
        {
            Name = "Test Preset",
            Author = "Test Author",
            Description = "Test Description",
            Category = "Bass",
            Tags = ["tag1", "tag2"],
            TargetType = PresetTargetType.Effect,
            TargetClassName = "ReverbEffect",
            Parameters = new Dictionary<string, float>
            {
                ["Mix"] = 0.5f,
                ["RoomSize"] = 0.7f
            },
            IsFavorite = true,
            Rating = 4
        };

        var clone = original.Clone();

        clone.Id.Should().NotBe(original.Id);
        clone.Name.Should().Be(original.Name);
        clone.Author.Should().Be(original.Author);
        clone.Description.Should().Be(original.Description);
        clone.Category.Should().Be(original.Category);
        clone.Tags.Should().BeEquivalentTo(original.Tags);
        clone.TargetType.Should().Be(original.TargetType);
        clone.TargetClassName.Should().Be(original.TargetClassName);
        clone.Parameters.Should().BeEquivalentTo(original.Parameters);
        clone.IsFavorite.Should().BeFalse();
        clone.Rating.Should().Be(0);
    }

    [Fact]
    public void Preset_Clone_CreatesIndependentTagsList()
    {
        var original = new Preset { Tags = ["tag1", "tag2"] };

        var clone = original.Clone();
        clone.Tags.Add("tag3");

        original.Tags.Should().HaveCount(2);
        clone.Tags.Should().HaveCount(3);
    }

    [Fact]
    public void Preset_Clone_CreatesIndependentParameters()
    {
        var original = new Preset
        {
            Parameters = new Dictionary<string, float> { ["Mix"] = 0.5f }
        };

        var clone = original.Clone();
        clone.Parameters["Mix"] = 0.8f;

        original.Parameters["Mix"].Should().Be(0.5f);
        clone.Parameters["Mix"].Should().Be(0.8f);
    }

    #endregion

    #region Preset ApplyTo Tests

    [Fact]
    public void Preset_ApplyToSynth_SetsAllParameters()
    {
        var preset = new Preset
        {
            TargetType = PresetTargetType.Synth,
            Parameters = new Dictionary<string, float>
            {
                ["Attack"] = 0.1f,
                ["Decay"] = 0.2f,
                ["Sustain"] = 0.7f,
                ["Release"] = 0.3f
            }
        };
        var synth = new MockSynth();

        preset.ApplyTo(synth);

        synth.ParameterChanges.Should().HaveCount(4);
        synth.ParameterChanges.Should().Contain(("Attack", 0.1f));
        synth.ParameterChanges.Should().Contain(("Decay", 0.2f));
        synth.ParameterChanges.Should().Contain(("Sustain", 0.7f));
        synth.ParameterChanges.Should().Contain(("Release", 0.3f));
    }

    [Fact]
    public void Preset_ApplyToSynth_ThrowsIfNotSynthPreset()
    {
        var preset = new Preset { TargetType = PresetTargetType.Effect };
        var synth = new MockSynth();

        var action = () => preset.ApplyTo(synth);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Preset_ApplyToEffect_SetsAllParameters()
    {
        var preset = new Preset
        {
            TargetType = PresetTargetType.Effect,
            Parameters = new Dictionary<string, float>
            {
                ["Mix"] = 0.5f,
                ["RoomSize"] = 0.7f
            }
        };
        var effect = new Mock<IEffect>();
        effect.Setup(e => e.SetParameter(It.IsAny<string>(), It.IsAny<float>()));

        preset.ApplyTo(effect.Object);

        effect.Verify(e => e.SetParameter("Mix", 0.5f), Times.Once);
        effect.Verify(e => e.SetParameter("RoomSize", 0.7f), Times.Once);
    }

    [Fact]
    public void Preset_ApplyToEffect_ThrowsIfNotEffectPreset()
    {
        var preset = new Preset { TargetType = PresetTargetType.Synth };
        var effect = new Mock<IEffect>();

        var action = () => preset.ApplyTo(effect.Object);

        action.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Preset JSON Serialization Tests

    [Fact]
    public void Preset_ToJson_SerializesCorrectly()
    {
        var preset = new Preset
        {
            Name = "Test Preset",
            Category = "Bass",
            Parameters = new Dictionary<string, float> { ["Mix"] = 0.5f }
        };

        var json = preset.ToJson();

        json.Should().Contain("\"name\"");
        json.Should().Contain("Test Preset");
        json.Should().Contain("\"category\"");
        json.Should().Contain("Bass");
        json.Should().Contain("\"parameters\"");
        json.Should().Contain("0.5");
    }

    [Fact]
    public void Preset_FromJson_DeserializesCorrectly()
    {
        var original = new Preset
        {
            Name = "Test Preset",
            Author = "Test Author",
            Category = "Bass",
            Tags = ["tag1", "tag2"],
            TargetType = PresetTargetType.Effect,
            TargetClassName = "ReverbEffect",
            Parameters = new Dictionary<string, float>
            {
                ["Mix"] = 0.5f,
                ["RoomSize"] = 0.7f
            }
        };
        var json = original.ToJson();

        var deserialized = Preset.FromJson(json);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(original.Name);
        deserialized.Author.Should().Be(original.Author);
        deserialized.Category.Should().Be(original.Category);
        deserialized.Tags.Should().BeEquivalentTo(original.Tags);
        deserialized.TargetType.Should().Be(original.TargetType);
        deserialized.TargetClassName.Should().Be(original.TargetClassName);
        deserialized.Parameters.Should().BeEquivalentTo(original.Parameters);
    }

    [Fact]
    public void Preset_FromJson_ReturnsNullForInvalidJson()
    {
        var result = Preset.FromJson("invalid json");

        result.Should().BeNull();
    }

    [Fact]
    public void Preset_FromJson_ReturnsNullForEmptyString()
    {
        var result = Preset.FromJson("");

        result.Should().BeNull();
    }

    [Fact]
    public void Preset_RoundTrip_PreservesAllData()
    {
        var original = new Preset
        {
            Name = "Complex Preset",
            Author = "Author Name",
            Description = "A complex preset with many settings",
            Category = "Lead",
            Tags = ["bright", "analog", "vintage"],
            TargetType = PresetTargetType.Synth,
            TargetClassName = "FMSynth",
            Parameters = new Dictionary<string, float>
            {
                ["Operator1Level"] = 1.0f,
                ["Operator2Level"] = 0.8f,
                ["ModulationIndex"] = 5.0f,
                ["Attack"] = 0.01f,
                ["Release"] = 0.5f
            },
            Version = 2,
            IsFavorite = true,
            Rating = 5,
            Metadata = new Dictionary<string, string>
            {
                ["Genre"] = "Synthwave",
                ["Tempo"] = "120"
            }
        };

        var json = original.ToJson();
        var deserialized = Preset.FromJson(json);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(original.Name);
        deserialized.Version.Should().Be(original.Version);
        deserialized.Metadata.Should().BeEquivalentTo(original.Metadata);
    }

    #endregion

    #region PresetBank Constructor Tests

    [Fact]
    public void PresetBank_Constructor_SetsDefaults()
    {
        var bank = new PresetBank();

        bank.Id.Should().NotBeNullOrEmpty();
        bank.Name.Should().BeEmpty();
        bank.Description.Should().BeEmpty();
        bank.Author.Should().BeEmpty();
        bank.Version.Should().Be("1.0.0");
        bank.Presets.Should().BeEmpty();
        bank.Count.Should().Be(0);
    }

    #endregion

    #region PresetBank AddPreset Tests

    [Fact]
    public void PresetBank_AddPreset_IncreasesCount()
    {
        var bank = new PresetBank();
        var preset = new Preset { Name = "Test" };

        bank.AddPreset(preset);

        bank.Count.Should().Be(1);
    }

    [Fact]
    public void PresetBank_AddPreset_ThrowsOnNull()
    {
        var bank = new PresetBank();

        var action = () => bank.AddPreset(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region PresetBank RemovePreset Tests

    [Fact]
    public void PresetBank_RemovePreset_DecreasesCount()
    {
        var bank = new PresetBank();
        var preset = new Preset { Name = "Test" };
        bank.AddPreset(preset);

        var result = bank.RemovePreset(preset);

        result.Should().BeTrue();
        bank.Count.Should().Be(0);
    }

    [Fact]
    public void PresetBank_RemovePreset_ReturnsFalseIfNotFound()
    {
        var bank = new PresetBank();
        var preset = new Preset { Name = "Test" };

        var result = bank.RemovePreset(preset);

        result.Should().BeFalse();
    }

    [Fact]
    public void PresetBank_RemovePresetById_RemovesCorrectPreset()
    {
        var bank = new PresetBank();
        var preset1 = new Preset { Name = "Preset1" };
        var preset2 = new Preset { Name = "Preset2" };
        bank.AddPreset(preset1);
        bank.AddPreset(preset2);

        var result = bank.RemovePresetById(preset1.Id);

        result.Should().BeTrue();
        bank.Count.Should().Be(1);
        bank.Presets[0].Name.Should().Be("Preset2");
    }

    #endregion

    #region PresetBank GetPreset Tests

    [Fact]
    public void PresetBank_GetPresetById_ReturnsCorrectPreset()
    {
        var bank = new PresetBank();
        var preset = new Preset { Name = "Test" };
        bank.AddPreset(preset);

        var result = bank.GetPresetById(preset.Id);

        result.Should().BeSameAs(preset);
    }

    [Fact]
    public void PresetBank_GetPresetById_ReturnsNullIfNotFound()
    {
        var bank = new PresetBank();

        var result = bank.GetPresetById("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void PresetBank_GetPresetByName_ReturnsCorrectPreset()
    {
        var bank = new PresetBank();
        var preset = new Preset { Name = "Test Preset" };
        bank.AddPreset(preset);

        var result = bank.GetPresetByName("Test Preset");

        result.Should().BeSameAs(preset);
    }

    [Fact]
    public void PresetBank_GetPresetByName_IsCaseInsensitive()
    {
        var bank = new PresetBank();
        var preset = new Preset { Name = "Test Preset" };
        bank.AddPreset(preset);

        var result = bank.GetPresetByName("test preset");

        result.Should().BeSameAs(preset);
    }

    #endregion

    #region PresetBank Filtering Tests

    [Fact]
    public void PresetBank_GetPresetsByCategory_ReturnsMatchingPresets()
    {
        var bank = new PresetBank();
        bank.AddPreset(new Preset { Name = "Bass1", Category = "Bass" });
        bank.AddPreset(new Preset { Name = "Lead1", Category = "Lead" });
        bank.AddPreset(new Preset { Name = "Bass2", Category = "Bass" });

        var result = bank.GetPresetsByCategory("Bass").ToList();

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.Category.Should().Be("Bass"));
    }

    [Fact]
    public void PresetBank_GetPresetsByCategory_IsCaseInsensitive()
    {
        var bank = new PresetBank();
        bank.AddPreset(new Preset { Name = "Bass1", Category = "Bass" });

        var result = bank.GetPresetsByCategory("bass").ToList();

        result.Should().HaveCount(1);
    }

    [Fact]
    public void PresetBank_GetPresetsByTag_ReturnsMatchingPresets()
    {
        var bank = new PresetBank();
        bank.AddPreset(new Preset { Name = "Preset1", Tags = ["bright", "analog"] });
        bank.AddPreset(new Preset { Name = "Preset2", Tags = ["dark", "digital"] });
        bank.AddPreset(new Preset { Name = "Preset3", Tags = ["bright", "digital"] });

        var result = bank.GetPresetsByTag("bright").ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void PresetBank_GetPresetsByTag_IsCaseInsensitive()
    {
        var bank = new PresetBank();
        bank.AddPreset(new Preset { Name = "Preset1", Tags = ["Bright"] });

        var result = bank.GetPresetsByTag("bright").ToList();

        result.Should().HaveCount(1);
    }

    [Fact]
    public void PresetBank_GetPresetsByTargetType_ReturnsMatchingPresets()
    {
        var bank = new PresetBank();
        bank.AddPreset(new Preset { Name = "Synth1", TargetType = PresetTargetType.Synth });
        bank.AddPreset(new Preset { Name = "Effect1", TargetType = PresetTargetType.Effect });
        bank.AddPreset(new Preset { Name = "Synth2", TargetType = PresetTargetType.Synth });

        var result = bank.GetPresetsByTargetType(PresetTargetType.Synth).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void PresetBank_GetCategories_ReturnsUniqueCategories()
    {
        var bank = new PresetBank();
        bank.AddPreset(new Preset { Category = "Bass" });
        bank.AddPreset(new Preset { Category = "Lead" });
        bank.AddPreset(new Preset { Category = "Bass" });
        bank.AddPreset(new Preset { Category = "Pad" });

        var result = bank.GetCategories().ToList();

        result.Should().HaveCount(3);
        result.Should().Contain("Bass");
        result.Should().Contain("Lead");
        result.Should().Contain("Pad");
    }

    [Fact]
    public void PresetBank_GetCategories_ReturnsSorted()
    {
        var bank = new PresetBank();
        bank.AddPreset(new Preset { Category = "Pad" });
        bank.AddPreset(new Preset { Category = "Bass" });
        bank.AddPreset(new Preset { Category = "Lead" });

        var result = bank.GetCategories().ToList();

        result.Should().ContainInOrder("Bass", "Lead", "Pad");
    }

    [Fact]
    public void PresetBank_GetAllTags_ReturnsUniqueTags()
    {
        var bank = new PresetBank();
        bank.AddPreset(new Preset { Tags = ["tag1", "tag2"] });
        bank.AddPreset(new Preset { Tags = ["tag2", "tag3"] });

        var result = bank.GetAllTags().ToList();

        result.Should().HaveCount(3);
        result.Should().Contain("tag1");
        result.Should().Contain("tag2");
        result.Should().Contain("tag3");
    }

    #endregion

    #region PresetBank File Operations Tests

    [Fact]
    public void PresetBank_SaveToFile_CreatesFile()
    {
        var bank = new PresetBank
        {
            Name = "Test Bank",
            Author = "Test Author"
        };
        bank.AddPreset(new Preset { Name = "Preset1" });
        var tempFile = Path.GetTempFileName();

        try
        {
            bank.SaveToFile(tempFile);

            File.Exists(tempFile).Should().BeTrue();
            bank.FilePath.Should().Be(tempFile);
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    [Fact]
    public void PresetBank_LoadFromFile_LoadsCorrectly()
    {
        var bank = new PresetBank
        {
            Name = "Test Bank",
            Author = "Test Author"
        };
        bank.AddPreset(new Preset { Name = "Preset1", Category = "Bass" });
        bank.AddPreset(new Preset { Name = "Preset2", Category = "Lead" });
        var tempFile = Path.GetTempFileName();

        try
        {
            bank.SaveToFile(tempFile);
            var loaded = PresetBank.LoadFromFile(tempFile);

            loaded.Should().NotBeNull();
            loaded!.Name.Should().Be("Test Bank");
            loaded.Author.Should().Be("Test Author");
            loaded.Count.Should().Be(2);
            loaded.FilePath.Should().Be(tempFile);
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    [Fact]
    public void PresetBank_LoadFromFile_ReturnsNullForNonExistentFile()
    {
        var result = PresetBank.LoadFromFile("nonexistent.json");

        result.Should().BeNull();
    }

    [Fact]
    public void PresetBank_SaveToDirectory_CreatesStructure()
    {
        var bank = new PresetBank
        {
            Name = "Test Bank",
            Author = "Test Author"
        };
        bank.AddPreset(new Preset { Name = "Bass1", Category = "Bass" });
        bank.AddPreset(new Preset { Name = "Lead1", Category = "Lead" });
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            bank.SaveToDirectory(tempDir);

            Directory.Exists(tempDir).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "bank.json")).Should().BeTrue();
            Directory.Exists(Path.Combine(tempDir, "Bass")).Should().BeTrue();
            Directory.Exists(Path.Combine(tempDir, "Lead")).Should().BeTrue();
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PresetBank_LoadFromDirectory_LoadsCorrectly()
    {
        var bank = new PresetBank
        {
            Name = "Test Bank",
            Author = "Test Author"
        };
        bank.AddPreset(new Preset { Name = "Bass1", Category = "Bass" });
        bank.AddPreset(new Preset { Name = "Lead1", Category = "Lead" });
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            bank.SaveToDirectory(tempDir);
            var loaded = PresetBank.LoadFromDirectory(tempDir);

            loaded.Should().NotBeNull();
            loaded!.Name.Should().Be("Test Bank");
            loaded.Author.Should().Be("Test Author");
            loaded.Count.Should().Be(2);
            loaded.DirectoryPath.Should().Be(tempDir);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PresetBank_LoadFromDirectory_ReturnsNullForNonExistentDirectory()
    {
        var result = PresetBank.LoadFromDirectory("nonexistent_directory");

        result.Should().BeNull();
    }

    [Fact]
    public void PresetBank_SaveToDirectory_HandlesUncategorizedPresets()
    {
        var bank = new PresetBank { Name = "Test" };
        bank.AddPreset(new Preset { Name = "NoCategory", Category = "" });
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            bank.SaveToDirectory(tempDir);

            Directory.Exists(Path.Combine(tempDir, "Uncategorized")).Should().BeTrue();
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    #endregion

    #region Preset FromEffect Tests

    [Fact]
    public void Preset_FromEffect_CapturesParameters()
    {
        var mockEffect = new Mock<IEffect>();
        mockEffect.Setup(e => e.GetParameter("Mix")).Returns(0.5f);
        mockEffect.Setup(e => e.GetParameter("RoomSize")).Returns(0.7f);

        var preset = Preset.FromEffect(mockEffect.Object, "Reverb Preset", ["Mix", "RoomSize"]);

        preset.Name.Should().Be("Reverb Preset");
        preset.TargetType.Should().Be(PresetTargetType.Effect);
        preset.Parameters["Mix"].Should().Be(0.5f);
        preset.Parameters["RoomSize"].Should().Be(0.7f);
    }

    #endregion

    #region Preset Metadata Tests

    [Fact]
    public void Preset_Metadata_CanStoreCustomData()
    {
        var preset = new Preset
        {
            Metadata = new Dictionary<string, string>
            {
                ["CustomKey1"] = "Value1",
                ["CustomKey2"] = "Value2"
            }
        };

        preset.Metadata["CustomKey1"].Should().Be("Value1");
        preset.Metadata["CustomKey2"].Should().Be("Value2");
    }

    [Fact]
    public void Preset_Metadata_SurvivesRoundTrip()
    {
        var original = new Preset
        {
            Name = "Test",
            Metadata = new Dictionary<string, string>
            {
                ["Genre"] = "Electronic",
                ["BPM"] = "128"
            }
        };

        var json = original.ToJson();
        var loaded = Preset.FromJson(json);

        loaded!.Metadata["Genre"].Should().Be("Electronic");
        loaded.Metadata["BPM"].Should().Be("128");
    }

    #endregion

    #region Helper Methods

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion
}
