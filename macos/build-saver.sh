#!/usr/bin/env bash
#
# Builds the Flying Azure macOS screensaver (.saver) end to end:
#   1. publish the shared engine as a NativeAOT dylib for the target RID
#   2. generate the Xcode project (XcodeGen) and build the .saver bundle
#   3. embed the engine + SkiaSharp native dylibs into Contents/Frameworks
#   4. ad-hoc codesign and zip the bundle
#
# Usage: macos/build-saver.sh [osx-arm64|osx-x64]
# Requires: dotnet, xcodegen, xcodebuild (macOS).
set -euo pipefail

RID="${1:-osx-arm64}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENGINE_PROJ="$ROOT/src/FlyingAzure.Engine/FlyingAzure.Engine.csproj"
PUBDIR="$ROOT/src/FlyingAzure.Engine/bin/Release/net10.0/$RID/publish"

echo "== [1/4] Publishing NativeAOT engine ($RID) =="
dotnet publish "$ENGINE_PROJ" -c Release -r "$RID" -p:PublishAot=true -p:NativeLib=Shared
echo "Engine publish output:"
ls -la "$PUBDIR"

echo "== [2/4] Generating + building the .saver =="
cd "$ROOT/macos"
xcodegen generate
xcodebuild -project FlyingAzure.xcodeproj -scheme FlyingAzure -configuration Release \
  -derivedDataPath build CODE_SIGNING_ALLOWED=NO build

SAVER="$(find "$ROOT/macos/build/Build/Products" -maxdepth 2 -name 'FlyingAzure.saver' -type d | head -1)"
if [[ -z "$SAVER" ]]; then
  echo "ERROR: built FlyingAzure.saver not found under macos/build/Build/Products" >&2
  exit 1
fi
echo "Built bundle: $SAVER"

echo "== [3/4] Embedding engine + Skia dylibs =="
mkdir -p "$SAVER/Contents/Frameworks"
# Copy every dylib the AOT publish produced (engine + libSkiaSharp + libHarfBuzzSharp).
cp "$PUBDIR"/*.dylib "$SAVER/Contents/Frameworks/"
ls -la "$SAVER/Contents/Frameworks/"

echo "== [4/4] Ad-hoc codesign + zip =="
# Sign inner dylibs first, then the bundle (inside-out), so the seal is valid.
for dylib in "$SAVER/Contents/Frameworks/"*.dylib; do
  codesign --force --timestamp=none --sign - "$dylib"
done
codesign --force --deep --sign - "$SAVER"

OUT="$ROOT/FlyingAzure-${RID}.saver.zip"
rm -f "$OUT"
ditto -c -k --keepParent "$SAVER" "$OUT"
echo "Created $OUT"
