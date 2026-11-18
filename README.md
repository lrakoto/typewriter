# Typewriter

A quiet, local macOS writing app inspired by a typewriter: warm paper, a moving pool of light, a carriage that follows the cursor, and optional mechanical key sounds.

Built with SwiftUI and AppKit. No accounts, server, or package dependencies. Requires macOS 14 or later.

## Run

```sh
./scripts/build.sh
open dist/Typewriter.app
```

You can also open `Package.swift` in Xcode or use `swift run Typewriter` during development. Copy `dist/Typewriter.app` to Applications if desired. This local build is ad-hoc signed; distribution to other Macs would require Developer ID signing and notarization.

## Your writing

Notes save automatically on this Mac in `~/Library/Application Support/Typewriter/Notes`, one atomic JSON file per note. The app never uploads your writing. Export plain text or Markdown through the File menu. Deleted notes stay in Trash and can be restored. Source-code pushes to GitHub do not include your notes.

## Development

```sh
./scripts/test.sh
./scripts/build.sh
```
