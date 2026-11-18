# Typewriter for Windows 11

A native WPF port of the Mac app, packaged as a self-contained portable executable. It has the same close spotlight, moving carriage, monospaced writing surface, paper themes, local library, recoverable Trash, import/export, and optional synthesized key sounds.

## Use the portable build

Download `Typewriter-Windows-x64.zip` from the **Windows portable build** workflow's artifacts, extract it into a writable folder, and double-click `Typewriter.exe`. There is no installation, administrator prompt, separate .NET download, browser, or account requirement.

The application creates `TypewriterData` beside the executable, using `AppContext.BaseDirectory` rather than the shortcut's working directory. Close the app and move the executable and this folder together to move your library, including settings and Trash. To update, replace only `Typewriter.exe`. To back up, copy `TypewriterData` while the app is closed.

```text
Any writable folder/
  Typewriter.exe
  TypewriterData/             # created on first launch
    settings.json
    settings.json.bak         # previous saved preferences
    .open.lock                # prevents simultaneous writers to this library
    Notes/
      <note UUID>.json
      <note UUID>.json.bak    # previous saved version
```

Each note is written atomically and flushed to disk. Notes use the same field names and ISO-8601 date format as the Mac library. Files and previous versions are preserved on load errors. A protected/read-only folder produces an error instead of silently putting the library somewhere else. The bundled runtime may extract its native components to Windows' temporary cache at first launch; writing and settings remain beside the executable.

The x64 build targets standard 64-bit Windows 11 PCs. The first build is not code-signed, so Windows may display a publisher or SmartScreen notice when it is downloaded. Run the extracted application from a writable folder; running it from inside an archive preview will not keep your library with the downloaded ZIP.

See [PORTABLE-README.txt](PORTABLE-README.txt) for shortcuts and moving/updating instructions.

## Build on Windows

Install the .NET 10 SDK for development, then run from the repository root:

```powershell
dotnet run --project windows/Typewriter.Checks -c Release
dotnet run --project windows/Typewriter.WindowsChecks -c Release -- dist/windows-validation
./scripts/build-windows.ps1
./scripts/test-windows-portable.ps1 -Executable dist/windows/Typewriter-win-x64/Typewriter.exe
```

The output is `dist/windows/Typewriter-Windows-x64.zip`, containing just `Typewriter.exe` and the readme. The executable contains the .NET desktop runtime and native dependencies; it does not use an installed .NET runtime on the recipient's PC. `build-windows.ps1 -Runtime win-arm64` can produce a native ARM64 archive, but the default build and executable smoke test use x64.

The application can be cross-compiled on macOS with `dotnet build windows/Typewriter/Typewriter.csproj`. It can only run on Windows. Native UI checks and the shipped executable's startup tests run on a Windows GitHub Actions runner. No third-party runtime packages are used.

## Checks

- Ten portable storage checks cover Unicode, autosave on switching notes, Trash recovery, moving the folder, independence from working directory, concurrent instances, corruption, atomic backups, Mac date compatibility, and preference validation.
- Nine native editor checks cover empty/long notes, soft wrapping, Unicode line breaks, undo/redo, type-size changes, narrow windows, editing earlier lines, and separation of undo history between notes. The Windows job also renders native previews for visual inspection.
- The actual self-contained EXE is copied into a fresh folder and launched with an unrelated working directory. The test creates a note and preferences, moves the entire folder to a path with spaces and Unicode characters, and relaunches the same EXE to verify the saved library and native editor. Test files never enter the downloadable package.
