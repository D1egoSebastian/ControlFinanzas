#!/bin/sh
# Builds ControlDeFinanzasIA-<version>-linux-x64.AppImage from the linux-x64 publish output (F-021 S9, R-23).
# CI only: needs appimagetool (set APPIMAGETOOL to its path, default: appimagetool on PATH) and a Linux host.
# Input produced earlier by scripts/publish.ps1 -Rid linux-x64: artifacts/publish/linux-x64
set -eu

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PUBLISH="$ROOT/artifacts/publish/linux-x64"
APPDIR="$ROOT/artifacts/appimage/ControlDeFinanzasIA.AppDir"
APPIMAGETOOL="${APPIMAGETOOL:-appimagetool}"

if [ ! -d "$PUBLISH" ]; then
    echo "Publish output not found: $PUBLISH (run scripts/publish.ps1 -Rid linux-x64 first)." >&2
    exit 1
fi
if ! command -v "$APPIMAGETOOL" >/dev/null 2>&1; then
    echo "appimagetool not found (set APPIMAGETOOL or add it to PATH)." >&2
    exit 1
fi

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/Directory.Build.props" | head -n 1)"
if [ -z "$VERSION" ]; then
    echo "No <Version> found in Directory.Build.props." >&2
    exit 1
fi

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
cp -R "$PUBLISH/." "$APPDIR/usr/bin/"
chmod 755 "$APPDIR/usr/bin/ControlDeFinanzasIA.UI"
cp "$ROOT/packaging/linux/AppRun" "$APPDIR/AppRun"
chmod 755 "$APPDIR/AppRun"
cp "$ROOT/packaging/linux/controldefinanzasia.desktop" "$APPDIR/controldefinanzasia.desktop"
cp "$ROOT/src/ControlDeFinanzasIA.UI/Assets/Icon/app-icon-256.png" "$APPDIR/controldefinanzasia.png"

OUT="$ROOT/artifacts/ControlDeFinanzasIA-$VERSION-linux-x64.AppImage"
ARCH=x86_64 "$APPIMAGETOOL" "$APPDIR" "$OUT"
echo "Created $OUT"
