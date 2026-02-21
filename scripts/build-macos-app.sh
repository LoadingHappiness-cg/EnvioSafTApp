#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/EnvioSafTApp.csproj"
APP_NAME="EnvioSafTApp"
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

mkdir -p "$DIST_DIR"
rm -rf "$BUNDLE_DIR"

printf "Publishing %s (%s, %s, self-contained=%s)...\n" "$APP_NAME" "$CONFIGURATION" "$RID" "$SELF_CONTAINED"
dotnet publish "$PROJECT_PATH" \
  -c "$CONFIGURATION" \
  -r "$RID" \
  --self-contained "$SELF_CONTAINED" \
  -o "$PUBLISH_DIR"

mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"
cp -R "$PUBLISH_DIR"/* "$MACOS_DIR/"

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
  <string>$APP_NAME</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>com.loadinghappiness.enviasaftapp</string>
  <key>CFBundleVersion</key>
  <string>2.0.0</string>
  <key>CFBundleShortVersionString</key>
  <string>2.0.0</string>
  <key>CFBundleExecutable</key>
  <string>$APP_NAME</string>
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
