#!/usr/bin/env bash
#
# Builds a signed + notarized macOS .pkg that installs the (already notarized) .saver
# into /Library/Screen Savers, so it appears in System Settings > Screen Saver for all
# users via the standard Installer.app flow.
#
# Requires a Developer ID INSTALLER certificate (distinct from the Application cert used
# to sign the .saver). Only invoked by the release workflow when APPLE_INSTALLER_CERT is set.
#
# Usage: macos/build-pkg.sh <notarized-saver-zip> <version>
# Env: APPLE_INSTALLER_CERT (base64 .p12), APPLE_CERT_PASSWORD, APPLE_TEAM_ID,
#      APPLE_ID, APPLE_APP_SPECIFIC_PASSWORD
set -euo pipefail

ZIP="${1:?usage: build-pkg.sh <saver-zip> <version>}"
VERSION="${2:?version required}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
: "${APPLE_INSTALLER_CERT:?APPLE_INSTALLER_CERT not set}"
: "${APPLE_CERT_PASSWORD:?}"
: "${APPLE_TEAM_ID:?}"
: "${APPLE_ID:?}"
: "${APPLE_APP_SPECIFIC_PASSWORD:?}"

WORK="$(mktemp -d)"
PAYLOAD="$WORK/root/Library/Screen Savers"
mkdir -p "$PAYLOAD"
unzip -q "$ROOT/$ZIP" -d "$PAYLOAD"
[[ -d "$PAYLOAD/FlyingAzure.saver" ]] || { echo "ERROR: no FlyingAzure.saver in $ZIP" >&2; exit 1; }

# Import the Developer ID Installer cert into a throwaway keychain.
KEYCHAIN="$WORK/installer.keychain"
KPW="$(uuidgen)"
security create-keychain -p "$KPW" "$KEYCHAIN"
security set-keychain-settings -lut 21600 "$KEYCHAIN"
security unlock-keychain -p "$KPW" "$KEYCHAIN"
echo "$APPLE_INSTALLER_CERT" | base64 --decode > "$WORK/installer.p12"
security import "$WORK/installer.p12" -k "$KEYCHAIN" -P "$APPLE_CERT_PASSWORD" -T /usr/bin/productsign
security set-key-partition-list -S apple-tool:,apple: -s -k "$KPW" "$KEYCHAIN" >/dev/null
security list-keychains -d user -s "$KEYCHAIN" $(security list-keychains -d user | tr -d '"')

# Component package -> product archive -> sign with the installer identity.
pkgbuild --root "$WORK/root" --install-location "/" \
  --identifier "com.kevgriffin.flyingazure" --version "$VERSION" "$WORK/component.pkg"
productbuild --package "$WORK/component.pkg" "$WORK/unsigned.pkg"

OUT="$ROOT/FlyingAzure-osx-${VERSION}.pkg"
productsign --sign "Developer ID Installer" "$WORK/unsigned.pkg" "$OUT"

# Notarize + staple the installer.
xcrun notarytool submit "$OUT" \
  --apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" --password "$APPLE_APP_SPECIFIC_PASSWORD" --wait
xcrun stapler staple "$OUT"

echo "Built signed + notarized installer: $OUT"
