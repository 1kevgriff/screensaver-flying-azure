#!/usr/bin/env bash
#
# Builds the Flying Azure macOS screensaver (.saver) as a UNIVERSAL binary (arm64 + x86_64),
# so it runs on both Apple Silicon and Intel Macs:
#   1. publish the NativeAOT engine for osx-arm64 AND osx-x64
#   2. lipo the engine + SkiaSharp dylibs into universal binaries
#   3. xcodebuild the universal Swift .saver, embed the dylibs, ad-hoc sign, zip
#
# Output: FlyingAzure-osx-universal.saver.zip
# Requires: dotnet, xcodegen, xcodebuild, lipo (macOS).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENGINE_PROJ="$ROOT/src/FlyingAzure.Engine/FlyingAzure.Engine.csproj"
BIN="$ROOT/src/FlyingAzure.Engine/bin/Release/net10.0"
ARM="$BIN/osx-arm64/publish"
X64="$BIN/osx-x64/publish"
UNI="$ROOT/macos/universal-libs"

echo "== [1/4] Publishing NativeAOT engine (osx-arm64 + osx-x64) =="
dotnet publish "$ENGINE_PROJ" -c Release -r osx-arm64 -p:PublishAot=true -p:NativeLib=Shared
dotnet publish "$ENGINE_PROJ" -c Release -r osx-x64   -p:PublishAot=true -p:NativeLib=Shared

echo "== lipo dylibs into universal =="
rm -rf "$UNI"; mkdir -p "$UNI"
for f in "$ARM"/*.dylib; do
  name="$(basename "$f")"
  if [[ -f "$X64/$name" ]]; then
    lipo -create "$ARM/$name" "$X64/$name" -output "$UNI/$name"
  else
    cp "$f" "$UNI/$name" # arch-neutral or arm-only file: keep as-is
  fi
  echo "  $name -> $(lipo -archs "$UNI/$name")"
done

echo "== [2/4] Generating + building the universal .saver =="
cd "$ROOT/macos"
xcodegen generate
xcodebuild -project FlyingAzure.xcodeproj -scheme FlyingAzure -configuration Release \
  -derivedDataPath build ARCHS="arm64 x86_64" ONLY_ACTIVE_ARCH=NO CODE_SIGNING_ALLOWED=NO build

SAVER="$(find "$ROOT/macos/build/Build/Products" -maxdepth 2 -name 'FlyingAzure.saver' -type d | head -1)"
if [[ -z "$SAVER" ]]; then
  echo "ERROR: built FlyingAzure.saver not found under macos/build/Build/Products" >&2
  exit 1
fi
echo "Built bundle: $SAVER ($(lipo -archs "$SAVER/Contents/MacOS/FlyingAzure"))"

echo "== [3/4] Embedding universal engine + Skia dylibs =="
mkdir -p "$SAVER/Contents/Frameworks"
cp "$UNI"/*.dylib "$SAVER/Contents/Frameworks/"
ls -la "$SAVER/Contents/Frameworks/"

echo "== [4/4] Ad-hoc codesign + zip =="
for dylib in "$SAVER/Contents/Frameworks/"*.dylib; do
  codesign --force --timestamp=none --sign - "$dylib"
done
codesign --force --deep --sign - "$SAVER"

OUT="$ROOT/FlyingAzure-osx-universal.saver.zip"
rm -f "$OUT"
ditto -c -k --keepParent "$SAVER" "$OUT"
echo "Created $OUT"
