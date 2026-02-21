#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/EnvioSafTApp.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"
RID="${RID:-win-x64}"
SELF_CONTAINED="${SELF_CONTAINED:-true}"
PUBLISH_DIR="${PUBLISH_DIR:-$ROOT_DIR/dist/windows/publish}"

mkdir -p "$PUBLISH_DIR"

printf "Publishing Windows build (%s, %s, self-contained=%s)...\n" "$CONFIGURATION" "$RID" "$SELF_CONTAINED"

dotnet publish "$PROJECT_PATH" \
  -c "$CONFIGURATION" \
  -r "$RID" \
  --self-contained "$SELF_CONTAINED" \
  -o "$PUBLISH_DIR"

printf "Windows publish ready: %s\n" "$PUBLISH_DIR"
