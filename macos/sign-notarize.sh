#!/usr/bin/env bash
#
# Developer ID signs + notarizes + staples the .saver, then re-zips it in place.
# Only invoked by the release workflow when the Apple secrets are present.
#
# Per the macOS research: because the .saver is loaded by Apple's legacyScreenSaver.appex
# (a process we don't sign), the reliable path is to re-sign EVERY embedded dylib and the
# bundle with our own Developer ID Team ID under the hardened runtime — not to rely on the
# disable-library-validation entitlement. No custom entitlements are applied to the bundle
# (entitlements belong on a main executable, which here is Apple's appex).
#
# Usage: macos/sign-notarize.sh <saver-zip>
# Env: APPLE_DEVELOPER_ID_CERT (base64 .p12), APPLE_CERT_PASSWORD, APPLE_TEAM_ID,
#      APPLE_ID, APPLE_APP_SPECIFIC_PASSWORD
set -euo pipefail

ZIP="${1:?usage: sign-notarize.sh <saver-zip>}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
: "${APPLE_DEVELOPER_ID_CERT:?APPLE_DEVELOPER_ID_CERT not set}"
: "${APPLE_CERT_PASSWORD:?APPLE_CERT_PASSWORD not set}"
: "${APPLE_TEAM_ID:?APPLE_TEAM_ID not set}"
: "${APPLE_ID:?APPLE_ID not set}"
: "${APPLE_APP_SPECIFIC_PASSWORD:?APPLE_APP_SPECIFIC_PASSWORD not set}"

WORK="$(mktemp -d)"
unzip -q "$ROOT/$ZIP" -d "$WORK"
SAVER="$(find "$WORK" -maxdepth 1 -name '*.saver' -type d | head -1)"
[[ -n "$SAVER" ]] || { echo "ERROR: no .saver found in $ZIP" >&2; exit 1; }

# Import the Developer ID cert into a throwaway keychain.
KEYCHAIN="$WORK/build.keychain"
KEYCHAIN_PW="$(uuidgen)"
security create-keychain -p "$KEYCHAIN_PW" "$KEYCHAIN"
security set-keychain-settings -lut 21600 "$KEYCHAIN"
security unlock-keychain -p "$KEYCHAIN_PW" "$KEYCHAIN"
echo "$APPLE_DEVELOPER_ID_CERT" | base64 --decode > "$WORK/cert.p12"
security import "$WORK/cert.p12" -k "$KEYCHAIN" -P "$APPLE_CERT_PASSWORD" -T /usr/bin/codesign
security set-key-partition-list -S apple-tool:,apple: -s -k "$KEYCHAIN_PW" "$KEYCHAIN" >/dev/null
security list-keychains -d user -s "$KEYCHAIN" $(security list-keychains -d user | tr -d '"')

IDENTITY="Developer ID Application"

# Inside-out: sign embedded dylibs first, then the bundle.
for dylib in "$SAVER/Contents/Frameworks/"*.dylib; do
  codesign --force --options runtime --timestamp --sign "$IDENTITY" "$dylib"
done
codesign --force --options runtime --timestamp --sign "$IDENTITY" "$SAVER"
codesign --verify --deep --strict --verbose=2 "$SAVER"

# Notarize the zipped bundle, wait for the verdict, then staple the ticket.
SUBMIT_ZIP="$WORK/submit.zip"
ditto -c -k --keepParent "$SAVER" "$SUBMIT_ZIP"
xcrun notarytool submit "$SUBMIT_ZIP" \
  --apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" --password "$APPLE_APP_SPECIFIC_PASSWORD" --wait
xcrun stapler staple "$SAVER"

# Replace the artifact with the signed + stapled bundle.
rm -f "$ROOT/$ZIP"
ditto -c -k --keepParent "$SAVER" "$ROOT/$ZIP"
echo "Signed + notarized + stapled: $ROOT/$ZIP"
