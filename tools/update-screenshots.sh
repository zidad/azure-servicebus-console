#!/usr/bin/env bash
# Regenerates docs/screenshots from the headless screenshot scenes (sample data, no Azure needed).
# Needs Python with Pillow and the macOS Menlo / Apple Color Emoji fonts (on Linux: JetBrains Mono or DejaVu Sans Mono, Noto Color Emoji).
set -euo pipefail
cd "$(dirname "$0")/.."

json=$(mktemp -d)
trap 'rm -rf "$json"' EXIT

for theme in dark light; do
  out=docs/screenshots; [ "$theme" = light ] && out=docs/screenshots/light
  rm -rf "${json:?}"/* && mkdir -p "$out" && rm -f "$out"/*.png
  SCREENSHOT_DIR="$json" SCREENSHOT_THEME="$theme" dotnet test --project tests/ServiceBusConsole.Tests --treenode-filter "/*/*/Screenshots/*"
  python3 tools/render-screenshots.py "$json" "$out"
done
