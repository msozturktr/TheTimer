#!/usr/bin/env bash
# Build release artifacts into artifacts/release.
# Usage: packaging/build-release.sh <version> [target...]
# Targets: tar deb rpm appimage flatpak sums   (default: tar deb rpm appimage sums)
# Tools: nfpm (deb/rpm; set NFPM=/path), appimagetool (appimage; set APPIMAGETOOL=/path),
#        flatpak-builder + flatpak (flatpak).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:?usage: build-release.sh <version> [target...]}"
VERSION="${VERSION#v}"
shift || true
TARGETS=("$@")
[ ${#TARGETS[@]} -gt 0 ] || TARGETS=(tar deb rpm appimage sums)

ARTIFACTS="$ROOT/artifacts"
OUT="$ARTIFACTS/release"
STAGE="$ARTIFACTS/stage"
APP_ID="io.github.msozturktr.TheTimer"
cd "$ROOT"
mkdir -p "$OUT"

has() { local t; for t in "${TARGETS[@]}"; do [ "$t" = "$1" ] && return 0; done; return 1; }

# Self-contained publish + desktop file adjusted for the installed layout (/usr/bin/thetimer).
prepare_stage() {
  [ -d "$STAGE/app" ] && return 0
  echo "==> Publishing TheTimer $VERSION"
  rm -rf "$STAGE"
  mkdir -p "$STAGE"
  dotnet publish src/TheTimer.App -c Release -r linux-x64 --self-contained \
    -p:Version="$VERSION" -p:PublishSingleFile=false -o "$STAGE/app"
  sed -e 's/^Exec=.*/Exec=thetimer/' packaging/linux/thetimer.desktop > "$STAGE/$APP_ID.desktop"
}

if has tar; then
  prepare_stage
  echo "==> tar.gz"
  NAME="thetimer-$VERSION-linux-x64"
  T="$STAGE/tar/$NAME"
  rm -rf "$STAGE/tar"
  mkdir -p "$T"
  cp -a "$STAGE/app" "$T/app"
  install -m 644 "$STAGE/$APP_ID.desktop" "$T/thetimer.desktop"
  install -m 644 packaging/linux/thetimer.png "$T/thetimer.png"
  install -m 644 LICENSE "$T/LICENSE"
  cat > "$T/INSTALL.txt" <<NOTE
TheTimer $VERSION (linux-x64, self-contained)

Run in place:
  ./app/TheTimer

Install for the current user:
  mkdir -p ~/.local/lib ~/.local/bin ~/.local/share/applications ~/.local/share/icons/hicolor/256x256/apps
  cp -a app ~/.local/lib/thetimer
  ln -sf ~/.local/lib/thetimer/TheTimer ~/.local/bin/thetimer
  cp thetimer.png ~/.local/share/icons/hicolor/256x256/apps/thetimer.png
  cp thetimer.desktop ~/.local/share/applications/thetimer.desktop

Runtime needs: fontconfig, libX11, libICE, libSM and ICU (libicu).
NOTE
  tar -C "$STAGE/tar" --owner=0 --group=0 -czf "$OUT/$NAME.tar.gz" "$NAME"
fi

if has deb || has rpm; then
  prepare_stage
  NFPM="${NFPM:-$(command -v nfpm || true)}"
  [ -n "$NFPM" ] || { echo "error: nfpm not found (set NFPM=/path/to/nfpm)" >&2; exit 1; }
  export NFPM_VERSION="$VERSION" NFPM_STAGE="$STAGE"
  if has deb; then
    echo "==> deb"
    "$NFPM" package -f packaging/nfpm/nfpm.yaml -p deb -t "$OUT/thetimer_${VERSION}_amd64.deb"
  fi
  if has rpm; then
    echo "==> rpm"
    "$NFPM" package -f packaging/nfpm/nfpm.yaml -p rpm -t "$OUT/thetimer-${VERSION}-1.x86_64.rpm"
  fi
fi

if has appimage; then
  echo "==> AppImage"
  bash packaging/appimage/build-appimage.sh
  cp "$ARTIFACTS/TheTimer-$VERSION-x86_64.AppImage" "$OUT/"
  [ -f "$OUT/TheTimer-$VERSION-x86_64.AppImage" ] || { echo "error: AppImage missing" >&2; exit 1; }
fi

if has flatpak; then
  echo "==> flatpak bundle"
  rm -rf "$ARTIFACTS/flatpak-publish" "$ARTIFACTS/flatpak-build" "$ARTIFACTS/flatpak-repo"
  dotnet publish src/TheTimer.App -c Release -r linux-x64 --self-contained \
    -p:Version="$VERSION" -p:PublishSingleFile=false -o "$ARTIFACTS/flatpak-publish"
  flatpak-builder --force-clean --repo="$ARTIFACTS/flatpak-repo" "$ARTIFACTS/flatpak-build" \
    packaging/flatpak/$APP_ID.yml
  flatpak build-bundle "$ARTIFACTS/flatpak-repo" "$OUT/TheTimer-$VERSION.flatpak" $APP_ID
fi

if has sums; then
  echo "==> SHA256SUMS"
  (cd "$OUT" && find . -maxdepth 1 -type f ! -name SHA256SUMS -printf '%P\n' | sort | xargs sha256sum > SHA256SUMS)
fi

echo "Artifacts in $OUT:"
ls -l "$OUT"
