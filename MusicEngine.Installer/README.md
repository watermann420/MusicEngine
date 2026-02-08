# MusicEngine Installer

This project builds a simple console installer (`MusicEngine.Installer.exe`).

How it works:
- The installer expects a `payload` folder next to the installer EXE.
- It copies the payload contents to the chosen install directory.
- It creates a desktop shortcut to `MusicEngine.exe` (unless `--no-shortcut` is used).

Packaging layout:
```
MusicEngine.Installer.exe
payload/
  MusicEngine.exe
  MusicEngine.dll
  (all other build output files)
```

Usage:
```
MusicEngine.Installer.exe
MusicEngine.Installer.exe --install-dir "D:\Apps\MusicEngine"
MusicEngine.Installer.exe --no-shortcut
```
