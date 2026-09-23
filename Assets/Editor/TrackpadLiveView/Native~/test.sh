#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
scratch=$(mktemp -d /private/tmp/trackpad-live-view-tests.XXXXXX)
trap 'rm -rf "$scratch"' EXIT
xcrun clang++ -std=c++17 -arch arm64 -fobjc-arc -Wall -Wextra -Werror \
    -framework AppKit Tests.mm -o "$scratch/tests"
"$scratch/tests"
./build.sh "$scratch/TrackpadLiveView.bundle"
binary="$scratch/TrackpadLiveView.bundle/Contents/MacOS/TrackpadLiveView"
[[ "$(lipo -archs "$binary")" == arm64 ]]
for symbol in TL_Start TL_Stop TL_Poll; do
    nm -gU "$binary" | awk '{print $3}' | grep -qx "_$symbol"
done
codesign --verify --strict "$scratch/TrackpadLiveView.bundle"
echo 'Live View: arm64, exports and signature passed.'
