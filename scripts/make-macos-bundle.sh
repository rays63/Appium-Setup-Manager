#!/bin/bash
# Assembles a macOS .app bundle from the self-contained publish output.
# Usage: scripts/make-macos-bundle.sh [publish-dir] [out-dir]
# Prereq: dotnet publish src/AppiumSetupManager/AppiumSetupManager.csproj \
#           -c Release -r osx-arm64 --self-contained -o artifacts/publish-osx-arm64
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_DIR="${1:-$ROOT/artifacts/publish-osx-arm64}"
OUT_DIR="${2:-$ROOT/artifacts}"
APP="$OUT_DIR/Appium Setup Manager.app"

[ -x "$PUBLISH_DIR/AppiumSetupManager" ] || { echo "publish output not found at $PUBLISH_DIR — run dotnet publish first" >&2; exit 1; }

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"
cp "$ROOT/src/AppiumSetupManager/Assets/Icons/app.icns" "$APP/Contents/Resources/app.icns"

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Appium Setup Manager</string>
    <key>CFBundleDisplayName</key>
    <string>Appium Setup Manager</string>
    <key>CFBundleIdentifier</key>
    <string>com.bajratechnologies.appiumsetupmanager</string>
    <key>CFBundleExecutable</key>
    <string>AppiumSetupManager</string>
    <key>CFBundleIconFile</key>
    <string>app.icns</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

echo "Bundle assembled: $APP"
