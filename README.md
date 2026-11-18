# Typewriter

A quiet, local writing app for macOS and Windows 11, inspired by a typewriter: warm paper, a moving pool of light, a carriage that follows the cursor, and optional mechanical key sounds.

Native Mac app built with SwiftUI/AppKit, plus a standalone Windows WPF port. No accounts or server. The Mac app requires macOS 14 or later.

## Windows 11 — portable

Extract `Typewriter-Windows-x64.zip` into a writable folder and double-click `Typewriter.exe`. No installer or separate runtime download is required. Notes and preferences live in `TypewriterData` beside the executable, so moving both together moves your library. The Windows build is available as a downloadable artifact from the **Windows portable build** GitHub Actions workflow.

See [Windows instructions and checks](windows/README.md). The first portable build is unsigned; Windows may show a publisher notice.

## Run on macOS

```sh
./scripts/build.sh
open dist/Typewriter.app
```

You can also open `Package.swift` in Xcode or use `swift run Typewriter` during development. Copy `dist/Typewriter.app` to Applications if desired. This local build is ad-hoc signed; distribution to other Macs would require Developer ID signing and notarization.

## Your writing on macOS

Notes save automatically on this Mac in `~/Library/Application Support/Typewriter/Notes`, one atomic JSON file per note. The app never uploads your writing. Export plain text or Markdown through the File menu. Deleted notes stay in Trash and can be restored. Source-code pushes to GitHub do not include your notes.

## Development

```sh
./scripts/test.sh
./scripts/build.sh
```

## Writing

- **⌘N** starts a new note; click the title to rename it.
- **⇧⌘L** opens the library. Search covers titles and note text.
- **⌘O** imports UTF-8 `.txt` or `.md` files. **⇧⌘E** exports the current note.
- The expand button hides the controls for focus mode; **Escape** brings them back. **⌃⌘F** toggles full screen.
- The bottom controls toggle key sounds and the spotlight. Writing preferences offer Linen, Parchment, and Midnight paper, type size, and sound volume.
- Move notes to Trash through the Note menu or a library item's context menu. Open Trash, select a note, and choose Restore to recover it.
- Normal Mac selection, copy/paste, and undo/redo are supported. The active line stays above the rail as you write; scroll to read earlier text.

## Validation

`./scripts/test.sh` runs five storage checks without requiring Xcode's test frameworks. `./scripts/test-layout.sh` exercises the actual AppKit editor: empty pages, large pastes, wrapping, Unicode, type size, small windows, and editing earlier lines. Layout checks require a logged-in macOS desktop session. GitHub Actions checks storage and builds the app on macOS.

The local app was also checked interactively for note creation, titles, undo/redo, search, themes, focus mode, text import/export, Trash restoration, and persistence across relaunches. Test notes use a temporary directory via `TYPEWRITER_DATA_DIR`; normal launches use the private Application Support directory.

## Structure

- `Sources/Typewriter`: SwiftUI shell, AppKit editor and paper/carriage rendering, synthesized audio.
- `Sources/TypewriterCore`: note model and atomic file storage.
- `Tests`: storage and native layout regression checks.
- `scripts/build.sh`: creates the icon, release executable, and ad-hoc signed `dist/Typewriter.app`.

The Windows implementation, portable storage, and Windows-specific checks are in `windows/`; packaging is handled by `scripts/build-windows.ps1`.
