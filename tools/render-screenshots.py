"""Render cell JSON from tests/ServiceBusConsole.Tests/Screenshots.cs to PNG.

    python tools/render-screenshots.py <json dir> [<png dir>]

Use tools/update-screenshots.sh to regenerate docs/screenshots (dark) and docs/screenshots/light.
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
# Terminal color schemes (One Dark / One Light): background, text and the 16 palette colors the app uses by name.
SCHEMES = {
    "dark": ("#1b1d23", "#c8ccd4", ["#1b1d23", "#e06c75", "#98c379", "#d19a66", "#3e5f9e", "#c678dd", "#56b6c2", "#abb2bf",
                                    "#5c6370", "#ef7f88", "#a9d48b", "#e5c07b", "#61afef", "#d68ee8", "#6ccad6", "#ffffff"]),
    "light": ("#fafafa", "#383a42", ["#383a42", "#c4473a", "#50a14f", "#986801", "#3a62c9", "#a626a4", "#0184bc", "#d4d4d4",
                                     "#8a8c94", "#e45649", "#5fb85e", "#c18401", "#4078f2", "#b84ab6", "#0997b3", "#ffffff"]),
}
NAMES = ["black", "maroon", "green", "olive", "navy", "purple", "teal", "silver",
         "grey", "red", "lime", "yellow", "blue", "fuchsia", "aqua", "white"]
BACKGROUND, FOREGROUND, PALETTE = SCHEMES["dark"]
BOLD, DIM, ITALIC, UNDERLINE = 1, 2, 4, 8

regular = ImageFont.truetype(MONO, FONT_SIZE, index=0)
bold = ImageFont.truetype(MONO, FONT_SIZE, index=1)
emoji_font = ImageFont.truetype(EMOJI, 160)  # the only bitmap size Apple Color Emoji ships
cell_w = round(regular.getlength("M"))
ascent, descent = regular.getmetrics()
cell_h = ascent + descent + 3


def is_emoji(text: str) -> bool:
    return any(ord(ch) > 0x2000 and unicodedata.category(ch) == "So" and unicodedata.east_asian_width(ch) == "W" for ch in text)


def resolve(color: str) -> str:
    return PALETTE[NAMES.index(color)] if color in NAMES else "#" + color


def dim(hex_color: str) -> str:
    r, g, b = (int(hex_color[i:i + 2], 16) for i in (1, 3, 5))
    return "#%02x%02x%02x" % tuple(int(c * 0.55 + int(BACKGROUND[i:i + 2], 16) * 0.45) for c, i in ((r, 1), (g, 3), (b, 5)))


def render(path: Path, out_dir: Path) -> Path:
    global BACKGROUND, FOREGROUND, PALETTE
    data = json.loads(path.read_text())
    BACKGROUND, FOREGROUND, PALETTE = SCHEMES[data.get("Theme", "dark")]
    width, height = data["Width"], data["Height"]
    pad = 14
    image = Image.new("RGB", (width * cell_w + 2 * pad, height * cell_h + 2 * pad), BACKGROUND)
    draw = ImageDraw.Draw(image)
    # Backgrounds first: a wide glyph spans the next cell, whose background would cover it.
    for y, row in enumerate(data["rows"]):
        for x, (_, _, bg, _) in enumerate(row):
            if bg:
                px, py = pad + x * cell_w, pad + y * cell_h
                draw.rectangle([px, py, px + cell_w - 1, py + cell_h - 1], fill=resolve(bg))
    for y, row in enumerate(data["rows"]):
        for x, (text, fg, bg, deco) in enumerate(row):
            px, py = pad + x * cell_w, pad + y * cell_h
            wide = bool(text) and is_emoji(text)
            if not text or text == " ":
                continue
            color = resolve(fg) if fg else FOREGROUND
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
            # Menlo Bold has no box-drawing glyphs.
            box = "\u2500" <= text[0] <= "\u257f"
            draw.text((px, py + 1), text, font=bold if deco & BOLD and not box else regular, fill=color)
            if deco & UNDERLINE:
                draw.line([px, py + cell_h - 2, px + cell_w, py + cell_h - 2], fill=color)
    out = out_dir / path.with_suffix(".png").name
    image.save(out)
    return out


if __name__ == "__main__":
    source = Path(sys.argv[1])
    target = Path(sys.argv[2]) if len(sys.argv) > 2 else source
    target.mkdir(parents=True, exist_ok=True)
    for file in sorted(source.glob("*.json")):
        print(render(file, target))
