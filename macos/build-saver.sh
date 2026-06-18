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

echo "== make universal dylibs =="
rm -rf "$UNI"; mkdir -p "$UNI"
for f in "$ARM"/*.dylib; do
  name="$(basename "$f")"
  archs="$(lipo -archs "$ARM/$name" 2>/dev/null || echo unknown)"
  if [[ "$archs" == *arm64* && "$archs" == *x86_64* ]]; then
    # Already a fat/universal binary (e.g. SkiaSharp's native lib) — use as-is.
    cp "$ARM/$name" "$UNI/$name"
  elif [[ -f "$X64/$name" && "$(lipo -archs "$X64/$name" 2>/dev/null)" != "$archs" ]]; then
    # Thin arm64 + thin x86_64 slices (the NativeAOT engine) — combine them.
    lipo -create "$ARM/$name" "$X64/$name" -output "$UNI/$name"
  else
    cp "$ARM/$name" "$UNI/$name"
  fi
  echo "  $name: arm-publish=[$archs] -> universal=[$(lipo -archs "$UNI/$name")]"
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

echo "== generating app icon (.icns) =="
ICONSET="$ROOT/macos/FlyingAzure.iconset"
I="$ROOT/assets/icons"
rm -rf "$ICONSET"; mkdir -p "$ICONSET"
cp "$I/icon_16.png"   "$ICONSET/icon_16x16.png"
cp "$I/icon_32.png"   "$ICONSET/icon_16x16@2x.png"
cp "$I/icon_32.png"   "$ICONSET/icon_32x32.png"
cp "$I/icon_64.png"   "$ICONSET/icon_32x32@2x.png"
cp "$I/icon_128.png"  "$ICONSET/icon_128x128.png"
cp "$I/icon_256.png"  "$ICONSET/icon_128x128@2x.png"
cp "$I/icon_256.png"  "$ICONSET/icon_256x256.png"
cp "$I/icon_512.png"  "$ICONSET/icon_256x256@2x.png"
cp "$I/icon_512.png"  "$ICONSET/icon_512x512.png"
cp "$I/icon_1024.png" "$ICONSET/icon_512x512@2x.png"
mkdir -p "$SAVER/Contents/Resources"
iconutil -c icns "$ICONSET" -o "$SAVER/Contents/Resources/FlyingAzure.icns"

# Screen Saver list thumbnail — System Settings reads thumbnail.png / thumbnail@2x.png
# straight from the bundle's Resources (no Info.plist entry needed).
cp "$ROOT/assets/thumbnail.png" "$ROOT/assets/thumbnail@2x.png" "$SAVER/Contents/Resources/"

echo "== [4/4] Ad-hoc codesign + zip =="
for dylib in "$SAVER/Contents/Frameworks/"*.dylib; do
  codesign --force --timestamp=none --sign - "$dylib"
done
codesign --force --deep --sign - "$SAVER"

OUT="$ROOT/FlyingAzure-osx-universal.saver.zip"
rm -f "$OUT"
ditto -c -k --keepParent "$SAVER" "$OUT"
echo "Created $OUT"
