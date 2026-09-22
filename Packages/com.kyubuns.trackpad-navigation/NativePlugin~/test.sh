#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p build
xcrun clang++ -std=c++17 -arch arm64 -Wall -Wextra -Werror -fsanitize=address,undefined CoreTests.cpp -o build/core-tests
./build/core-tests
xcrun clang++ -std=c++17 -arch arm64 -fobjc-arc -Wall -Wextra -Werror -framework AppKit AppKitTests.mm -o build/appkit-tests
./build/appkit-tests
./build.sh build/TrackpadBridge.bundle
binary=build/TrackpadBridge.bundle/Contents/MacOS/TrackpadBridge
[[ "$(lipo -archs "$binary")" == arm64 ]]
for symbol in TN_ApiVersion TN_EventSize TN_Start TN_Stop TN_Poll TN_SetCapture TN_GetPointer TN_GetStats; do
    nm -gU "$binary" | awk '{print $3}' | grep -qx "_$symbol"
done
codesign --verify --strict build/TrackpadBridge.bundle
echo 'arm64, exported ABI, independent rebuild and code signature: passed.'
