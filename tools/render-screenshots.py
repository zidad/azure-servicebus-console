"""Render cell JSON from tests/ServiceBusConsole.Tests/Screenshots.cs to PNG.

    SCREENSHOT_DIR=/tmp/shots dotnet test tests/ServiceBusConsole.Tests
    python tools/render-screenshots.py /tmp/shots

Needs Pillow; uses macOS Menlo and Apple Color Emoji.
"""
import json
import sys
import unicodedata
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

FONT_SIZE = 15
MONO = "/System/Library/Fonts/Menlo.ttc"
EMOJI = "/System/Library/Fonts/Apple Color Emoji.ttc"
BACKGROUND = "#1b1d23"
FOREGROUND = "#c8ccd4"
BOLD, DIM, ITALIC, UNDERLINE = 1, 2, 4, 8

regular = ImageFont.truetype(MONO, FONT_SIZE, index=0)
bold = ImageFont.truetype(MONO, FONT_SIZE, index=1)
emoji_font = ImageFont.truetype(EMOJI, 160)  # the only bitmap size Apple Color Emoji ships
cell_w = round(regular.getlength("M"))
ascent, descent = regular.getmetrics()
cell_h = ascent + descent + 3


def is_emoji(text: str) -> bool:
    return any(ord(ch) > 0x2000 and unicodedata.category(ch) == "So" and unicodedata.east_asian_width(ch) == "W" for ch in text)


def dim(hex_color: str) -> str:
    r, g, b = (int(hex_color[i:i + 2], 16) for i in (1, 3, 5))
    return "#%02x%02x%02x" % tuple(int(c * 0.55 + int(BACKGROUND[i:i + 2], 16) * 0.45) for c, i in ((r, 1), (g, 3), (b, 5)))


def render(path: Path) -> Path:
    data = json.loads(path.read_text())
    width, height = data["Width"], data["Height"]
    pad = 14
    image = Image.new("RGB", (width * cell_w + 2 * pad, height * cell_h + 2 * pad), BACKGROUND)
    draw = ImageDraw.Draw(image)
    # Backgrounds first: a wide glyph spans the next cell, whose background would cover it.
    for y, row in enumerate(data["rows"]):
        for x, (_, _, bg, _) in enumerate(row):
            if bg:
                px, py = pad + x * cell_w, pad + y * cell_h
                draw.rectangle([px, py, px + cell_w - 1, py + cell_h - 1], fill="#" + bg)
    for y, row in enumerate(data["rows"]):
        for x, (text, fg, bg, deco) in enumerate(row):
            px, py = pad + x * cell_w, pad + y * cell_h
            wide = bool(text) and is_emoji(text)
            if not text or text == " ":
                continue
            color = "#" + fg if fg else FOREGROUND
            if deco & DIM:
                color = dim(color)
            if wide:
                glyph = Image.new("RGBA", (180, 180), (0, 0, 0, 0))
                ImageDraw.Draw(glyph).text((0, 0), text, font=emoji_font, embedded_color=True)
                glyph = glyph.crop(glyph.getbbox() or (0, 0, 1, 1))
                size = cell_h - 3
                glyph.thumbnail((size, size), Image.LANCZOS)
                image.paste(glyph, (px + (2 * cell_w - glyph.width) // 2, py + (cell_h - glyph.height) // 2), glyph)
                continue
            draw.text((px, py + 1), text, font=bold if deco & BOLD else regular, fill=color)
            if deco & UNDERLINE:
                draw.line([px, py + cell_h - 2, px + cell_w, py + cell_h - 2], fill=color)
    out = path.with_suffix(".png")
    image.save(out)
    return out


if __name__ == "__main__":
    for file in sorted(Path(sys.argv[1]).glob("*.json")):
        print(render(file))
