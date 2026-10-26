#!/usr/bin/env bash
# Regenerates docs/screenshots from the headless screenshot scenes (sample data, no Azure needed).
# Needs Python with Pillow and the macOS Menlo / Apple Color Emoji fonts.
set -euo pipefail
cd "$(dirname "$0")/.."

json=$(mktemp -d)
trap 'rm -rf "$json"' EXIT

SCREENSHOT_DIR="$json" dotnet test tests/ServiceBusConsole.Tests -- --filter-class "*Screenshots"
rm -f docs/screenshots/*.png
python3 tools/render-screenshots.py "$json" docs/screenshots
