#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
APP_NAME="EnvioSafTApp"
APP_BUNDLE="$ROOT_DIR/dist/macos/${APP_NAME}.app"
DMG_DIR="$ROOT_DIR/dist/macos"
DMG_NAME="${APP_NAME}.dmg"
DMG_PATH="$DMG_DIR/$DMG_NAME"
TMP_DMG_PATH="$DMG_DIR/${APP_NAME}-tmp.dmg"
STAGING_DIR="$DMG_DIR/dmg-staging"
VOLUME_NAME="${VOLUME_NAME:-EnviaSaft}"
ICON_ICNS_PATH="$DMG_DIR/AppIcon.icns"

if ! command -v hdiutil >/dev/null 2>&1; then
  echo "hdiutil não encontrado. Este script deve ser executado no macOS." >&2
  exit 1
fi

"$ROOT_DIR/scripts/build-macos-app.sh"

rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR"
cp -R "$APP_BUNDLE" "$STAGING_DIR/"
ln -s /Applications "$STAGING_DIR/Applications"

rm -f "$DMG_PATH"
rm -f "$TMP_DMG_PATH"

hdiutil create \
  -volname "$VOLUME_NAME" \
  -srcfolder "$STAGING_DIR" \
  -ov \
  -format UDRW \
  "$TMP_DMG_PATH"

MOUNT_OUTPUT="$(hdiutil attach "$TMP_DMG_PATH" -nobrowse -readwrite)"
MOUNT_POINT="$(echo "$MOUNT_OUTPUT" | awk '/\/Volumes\// {print $NF; exit}')"

if [[ -n "$MOUNT_POINT" && -d "$MOUNT_POINT" && -f "$ICON_ICNS_PATH" ]]; then
  cp "$ICON_ICNS_PATH" "$MOUNT_POINT/.VolumeIcon.icns"
  if command -v SetFile >/dev/null 2>&1; then
    SetFile -a C "$MOUNT_POINT" || true
  fi
fi

hdiutil detach "$MOUNT_POINT"
hdiutil convert "$TMP_DMG_PATH" -format UDZO -o "$DMG_PATH" -ov >/dev/null
rm -f "$TMP_DMG_PATH"

rm -rf "$STAGING_DIR"

echo "DMG pronto: $DMG_PATH"
