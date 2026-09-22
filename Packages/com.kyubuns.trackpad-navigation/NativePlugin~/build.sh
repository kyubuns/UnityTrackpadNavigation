#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
if [[ "$(uname -s)" != Darwin || "$(uname -m)" != arm64 ]]; then
    echo 'Trackpad Navigation requires an Apple Silicon Mac.' >&2
    exit 1
fi
output="${1:-../Plugins/macOS/TrackpadBridge.bundle}"
sdk="$(xcrun --sdk macosx --show-sdk-path)"
version="$(plutil -extract version raw -o - ../package.json)"
mkdir -p "$(dirname "$output")"
staging="$(mktemp -d "$(dirname "$output")/.trackpad-build.XXXXXX")"
trap 'rm -rf "$staging"' EXIT
bundle="$staging/TrackpadBridge.bundle"
mkdir -p "$bundle/Contents/MacOS"
xcrun --sdk macosx clang++ -std=c++17 -arch arm64 -mmacosx-version-min=12.0 \
    -isysroot "$sdk" -fobjc-arc -fvisibility=hidden -Wall -Wextra -Werror -O2 \
    -bundle -framework AppKit TrackpadBridge.mm -o "$bundle/Contents/MacOS/TrackpadBridge"
cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>TrackpadBridge</string>
<key>CFBundleIdentifier</key><string>com.kyubuns.trackpad-navigation.bridge</string>
<key>CFBundlePackageType</key><string>BNDL</string>
<key>CFBundleVersion</key><string>1</string>
<key>CFBundleShortVersionString</key><string>$version</string>
<key>LSMinimumSystemVersion</key><string>12.0</string>
</dict></plist>
PLIST
codesign --force --sign - --timestamp=none "$bundle"
mkdir -p "$output/Contents/MacOS" "$output/Contents/_CodeSignature"
cp "$bundle/Contents/Info.plist" "$output/Contents/Info.plist"
cp "$bundle/Contents/_CodeSignature/CodeResources" "$output/Contents/_CodeSignature/CodeResources"
# Replace the inode: never truncate a Mach-O already mapped by a running Editor.
mv -f "$bundle/Contents/MacOS/TrackpadBridge" "$output/Contents/MacOS/TrackpadBridge"
file "$output/Contents/MacOS/TrackpadBridge"
