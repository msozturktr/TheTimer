#!/usr/bin/env bash
# Build the TheTimer AppImage: dotnet publish -> AppDir -> appimagetool.
# Output: artifacts/TheTimer-<version>-x86_64.AppImage (AppDir is left in artifacts/AppDir).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ARTIFACTS="$ROOT/artifacts"
APPDIR="$ARTIFACTS/AppDir"
PUBLISH_DIR="$(mktemp -d "${TMPDIR:-/tmp}/thetimer-publish.XXXXXX")"
trap 'rm -rf "$PUBLISH_DIR"' EXIT

cd "$ROOT"

VERSION="$(dotnet msbuild src/TheTimer.App -getProperty:Version | tr -d '[:space:]')"
[ -n "$VERSION" ] || { echo "error: could not read Version from MSBuild" >&2; exit 1; }

echo "==> Publishing TheTimer $VERSION"
dotnet publish src/TheTimer.App -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -o "$PUBLISH_DIR"

echo "==> Building AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin" \
  "$APPDIR/usr/share/applications" \
  "$APPDIR/usr/share/icons/hicolor/256x256/apps"
install -m 755 "$PUBLISH_DIR/TheTimer" "$APPDIR/usr/bin/TheTimer"
install -Dm644 "$PUBLISH_DIR/Assets/Sounds/complete.wav" "$APPDIR/usr/bin/Assets/Sounds/complete.wav"
cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/TheTimer" "$@"
EOF
chmod 755 "$APPDIR/AppRun"
LINUX_DIR="$ROOT/packaging/linux"
install -m 644 "$LINUX_DIR/thetimer.desktop" "$APPDIR/thetimer.desktop"
install -m 644 "$LINUX_DIR/thetimer.png" "$APPDIR/thetimer.png"
install -m 644 "$LINUX_DIR/thetimer.desktop" "$APPDIR/usr/share/applications/thetimer.desktop"
install -m 644 "$LINUX_DIR/thetimer.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/thetimer.png"

TOOL="${APPIMAGETOOL:-}"
if [ -z "$TOOL" ]; then
  TOOL="$(command -v appimagetool || true)"
fi
if [ -z "$TOOL" ] || [ ! -x "$TOOL" ]; then
  echo "AppDir ready at: $APPDIR"
  echo "appimagetool not found. Install it with: paru -S appimagetool-bin" >&2
  echo "(or set APPIMAGETOOL=/path/to/appimagetool), then re-run this script." >&2
  exit 0
fi

OUT="$ARTIFACTS/TheTimer-$VERSION-x86_64.AppImage"
echo "==> Running appimagetool"
ARCH=x86_64 "$TOOL" "$APPDIR" "$OUT"
echo "AppImage: $OUT"
