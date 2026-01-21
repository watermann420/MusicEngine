namespace MusicEngine.Core.Extensions;

/// <summary>
/// Marks a class as a synth extension.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class SynthExtensionAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }

    public SynthExtensionAttribute(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

/// <summary>
/// Marks a class as an effect extension.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class EffectExtensionAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string? Category { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }

    public EffectExtensionAttribute(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

/// <summary>
/// Marks a property as an extension parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class ExtensionParameterAttribute : Attribute
{
    public string Name { get; }
    public string? DisplayName { get; set; }
    public float MinValue { get; set; } = 0f;
    public float MaxValue { get; set; } = 1f;
    public float DefaultValue { get; set; } = 0f;
    public string? Unit { get; set; }

    public ExtensionParameterAttribute(string name)
    {
        Name = name;
    }
}
