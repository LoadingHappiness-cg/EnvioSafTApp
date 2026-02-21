#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/EnvioSafTApp.csproj"
APP_NAME="EnvioSafTApp"
DISPLAY_NAME="${DISPLAY_NAME:-EnviaSaft}"
BUNDLE_NAME="${APP_NAME}.app"
CONFIGURATION="${CONFIGURATION:-Release}"
RID="${RID:-osx-arm64}"
SELF_CONTAINED="${SELF_CONTAINED:-true}"
PUBLISH_DIR="$ROOT_DIR/bin/$CONFIGURATION/net8.0/$RID/publish"
DIST_DIR="${DIST_DIR:-$ROOT_DIR/dist/macos}"
BUNDLE_DIR="$DIST_DIR/$BUNDLE_NAME"
CONTENTS_DIR="$BUNDLE_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"
ICON_PNG="${ICON_PNG:-}"
ICONSET_DIR="$DIST_DIR/AppIcon.iconset"
ICON_ICNS="$DIST_DIR/AppIcon.icns"
ICON_WORK_PNG="$DIST_DIR/AppIcon-source.png"

mkdir -p "$DIST_DIR"
rm -rf "$BUNDLE_DIR"

if [[ -z "$ICON_PNG" ]]; then
  if [[ -f "$ROOT_DIR/Assets/EnviaSaft-v2.png" ]]; then
    ICON_PNG="$ROOT_DIR/Assets/EnviaSaft-v2.png"
  elif [[ -f "$ROOT_DIR/Assets/EnviaSaft.png" ]]; then
    ICON_PNG="$ROOT_DIR/Assets/EnviaSaft.png"
  fi
fi

printf "Publishing %s (%s, %s, self-contained=%s)...\n" "$APP_NAME" "$CONFIGURATION" "$RID" "$SELF_CONTAINED"
dotnet publish "$PROJECT_PATH" \
  -c "$CONFIGURATION" \
  -r "$RID" \
  --self-contained "$SELF_CONTAINED" \
  -o "$PUBLISH_DIR"

mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"
cp -R "$PUBLISH_DIR"/* "$MACOS_DIR/"

if [[ -n "$ICON_PNG" && -f "$ICON_PNG" ]] && command -v sips >/dev/null 2>&1 && command -v iconutil >/dev/null 2>&1; then
  rm -rf "$ICONSET_DIR"
  mkdir -p "$ICONSET_DIR"
  sips -s format png "$ICON_PNG" --out "$ICON_WORK_PNG" >/dev/null
  sips -z 16 16     "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_16x16.png" >/dev/null
  sips -z 32 32     "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_16x16@2x.png" >/dev/null
  sips -z 32 32     "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_32x32.png" >/dev/null
  sips -z 64 64     "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_32x32@2x.png" >/dev/null
  sips -z 128 128   "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_128x128.png" >/dev/null
  sips -z 256 256   "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_128x128@2x.png" >/dev/null
  sips -z 256 256   "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_256x256.png" >/dev/null
  sips -z 512 512   "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_256x256@2x.png" >/dev/null
  sips -z 512 512   "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_512x512.png" >/dev/null
  sips -z 1024 1024 "$ICON_WORK_PNG" --out "$ICONSET_DIR/icon_512x512@2x.png" >/dev/null
  iconutil -c icns "$ICONSET_DIR" -o "$ICON_ICNS"
  cp "$ICON_ICNS" "$RESOURCES_DIR/AppIcon.icns"
fi

EXECUTABLE_PATH="$MACOS_DIR/$APP_NAME"
if [[ -f "$EXECUTABLE_PATH" ]]; then
  chmod +x "$EXECUTABLE_PATH"
else
  DLL_PATH="$MACOS_DIR/$APP_NAME.dll"
  if [[ ! -f "$DLL_PATH" ]]; then
    echo "No executable or DLL found in publish directory." >&2
    exit 1
  fi

  cat > "$EXECUTABLE_PATH" <<'LAUNCHER'
#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
exec dotnet "$HERE/EnvioSafTApp.dll" "$@"
LAUNCHER
  chmod +x "$EXECUTABLE_PATH"
fi

cat > "$CONTENTS_DIR/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>$DISPLAY_NAME</string>
  <key>CFBundleDisplayName</key>
  <string>$DISPLAY_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>com.loadinghappiness.enviasaftapp</string>
  <key>CFBundleVersion</key>
  <string>2.0.0</string>
  <key>CFBundleShortVersionString</key>
  <string>2.0.0</string>
  <key>CFBundleExecutable</key>
  <string>$APP_NAME</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>LSMinimumSystemVersion</key>
  <string>11.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

printf "Bundle ready: %s\n" "$BUNDLE_DIR"
printf "Run: open '%s'\n" "$BUNDLE_DIR"
