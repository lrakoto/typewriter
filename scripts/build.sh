#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
export CLANG_MODULE_CACHE_PATH="$PWD/.build/clang-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="$PWD/.build/module-cache"
swift build --product Typewriter -c release --cache-path "$PWD/.build/cache" --disable-sandbox
APP="$PWD/dist/Typewriter.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" .build/AppIcon.iconset
cp .build/release/Typewriter "$APP/Contents/MacOS/Typewriter"
swift scripts/draw-icon.swift .build/icon-1024.png
for dimension in 16 32 128 256 512; do
    sips -z "$dimension" "$dimension" .build/icon-1024.png --out ".build/AppIcon.iconset/icon_${dimension}x${dimension}.png" >/dev/null
    doubled=$((dimension * 2))
    sips -z "$doubled" "$doubled" .build/icon-1024.png --out ".build/AppIcon.iconset/icon_${dimension}x${dimension}@2x.png" >/dev/null
done
iconutil -c icns .build/AppIcon.iconset -o "$APP/Contents/Resources/AppIcon.icns"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>Typewriter</string>
<key>CFBundleIdentifier</key><string>dev.lrakoto.typewriter</string>
<key>CFBundleName</key><string>Typewriter</string>
<key>CFBundleDisplayName</key><string>Typewriter</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>0.1.0</string>
<key>CFBundleVersion</key><string>1</string>
<key>CFBundleIconFile</key><string>AppIcon</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSPrincipalClass</key><string>NSApplication</string>
<key>NSHumanReadableCopyright</key><string>Typewriter</string>
</dict></plist>
PLIST
codesign --force --deep --sign - "$APP"
echo "Built $APP"
