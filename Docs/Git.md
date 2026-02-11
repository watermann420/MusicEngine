# Git URLs (Auto Cache)

MusicEngine can pull scripts and samples directly from GitHub URLs. The repo is cloned and cached automatically.

Cache location:
- `<project>/.gitcache/<owner>_<repo>/`

The cache auto-updates on refresh (fast-forward pull).

## Include a Script from Git

```csharp
Include "https://github.com/user/ME-Kits/scripts/synthInstance.cs";
var synth = Include.synthInstance.synth;
```

## Load Samples from Git

```csharp
var kick = GetSamples("https://github.com/user/ME-Kits#samples/909/Kick.wav");

var drums = GetSamples("https://github.com/user/ME-Kits#samples/909");
var clap = drums["Clap-01"]; // name-based lookup (file name without extension)
```

## Mount a Repo (optional)

```csharp
Include "https://github.com/user/ME-Kits"; // mount repo root
Include synthInstance; // resolves inside the mounted repo
```

## Notes

- URLs must be `https://github.com/...`.
- The repo root can be linked directly, or you can point to a specific file/folder.
- Use `#` to point to a subfolder path inside the repo.
- Missing files log a warning and stay silent.
