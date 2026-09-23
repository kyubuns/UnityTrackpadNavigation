#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
output="${1:-../Plugins/macOS/TrackpadLiveView.bundle}"
mkdir -p "$(dirname "$output")"
staging=$(mktemp -d "$(dirname "$output")/.live-view-build.XXXXXX")
trap 'rm -rf "$staging"' EXIT
bundle="$staging/TrackpadLiveView.bundle"
mkdir -p "$bundle/Contents/MacOS"
xcrun clang++ -std=c++17 -arch arm64 -mmacosx-version-min=12.0 -fobjc-arc \
    -fvisibility=hidden -Wall -Wextra -Werror -O2 -bundle -framework AppKit \
    TrackpadLiveView.mm -o "$bundle/Contents/MacOS/TrackpadLiveView"
cat > "$bundle/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>TrackpadLiveView</string>
<key>CFBundleIdentifier</key><string>com.kyubuns.trackpad-navigation.live-view</string>
<key>CFBundlePackageType</key><string>BNDL</string>
<key>CFBundleVersion</key><string>1</string>
<key>LSMinimumSystemVersion</key><string>12.0</string>
</dict></plist>
PLIST
codesign --force --sign - --timestamp=none "$bundle"
mkdir -p "$output/Contents/MacOS" "$output/Contents/_CodeSignature"
cp "$bundle/Contents/Info.plist" "$output/Contents/Info.plist"
cp "$bundle/Contents/_CodeSignature/CodeResources" "$output/Contents/_CodeSignature/CodeResources"
mv -f "$bundle/Contents/MacOS/TrackpadLiveView" "$output/Contents/MacOS/TrackpadLiveView"
file "$output/Contents/MacOS/TrackpadLiveView"
