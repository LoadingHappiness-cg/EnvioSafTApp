#!/usr/bin/env bash
set -euo pipefail

CHANNEL="${1:-8.0}"
INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
SCRIPT_PATH="${TMPDIR:-/tmp}/dotnet-install.sh"

if command -v dotnet >/dev/null 2>&1; then
  echo "dotnet already available at $(command -v dotnet)"
  exit 0
fi

mkdir -p "$INSTALL_DIR"

DOWNLOAD_URL="https://dot.net/v1/dotnet-install.sh"

echo "Downloading dotnet-install script from $DOWNLOAD_URL..."
if command -v curl >/dev/null 2>&1; then
  if ! curl -fsSL "$DOWNLOAD_URL" -o "$SCRIPT_PATH"; then
    echo "Failed to download dotnet-install.sh via curl." >&2
    echo "If you are behind a proxy, configure HTTPS_PROXY/HTTP_PROXY and retry." >&2
    exit 1
  fi
elif command -v wget >/dev/null 2>&1; then
  if ! wget -q "$DOWNLOAD_URL" -O "$SCRIPT_PATH"; then
    echo "Failed to download dotnet-install.sh via wget." >&2
    echo "If you are behind a proxy, configure HTTPS_PROXY/HTTP_PROXY and retry." >&2
    exit 1
  fi
else
  echo "Neither curl nor wget is available to download dotnet-install.sh." >&2
  exit 1
fi

chmod +x "$SCRIPT_PATH"

echo "Installing .NET SDK channel $CHANNEL into $INSTALL_DIR..."
"$SCRIPT_PATH" --install-dir "$INSTALL_DIR" --channel "$CHANNEL" --no-path

rm -f "$SCRIPT_PATH"

echo
cat <<MSG
Installation complete.
Add the following to your shell profile to use the newly installed dotnet:

    export DOTNET_ROOT="${INSTALL_DIR}"
    export PATH="\$DOTNET_ROOT:\$PATH"

Then run 'dotnet --info' to confirm the installation.
MSG
