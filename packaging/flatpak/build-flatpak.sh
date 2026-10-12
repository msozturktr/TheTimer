#!/usr/bin/env bash
# Build and install TheTimer as a user Flatpak: dotnet publish -> flatpak-builder --user --install.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
command -v flatpak-builder >/dev/null || { echo "flatpak-builder missing: sudo pacman -S flatpak-builder" >&2; exit 1; }
flatpak install --user -y --noninteractive flathub org.freedesktop.Platform//25.08 org.freedesktop.Sdk//25.08
rm -rf artifacts/flatpak-publish
dotnet publish src/TheTimer.App -c Release -r linux-x64 --self-contained -p:PublishSingleFile=false -o artifacts/flatpak-publish
flatpak-builder --user --install --force-clean artifacts/flatpak-build packaging/flatpak/io.github.msozturktr.TheTimer.yml
echo "Run: flatpak run io.github.msozturktr.TheTimer"
