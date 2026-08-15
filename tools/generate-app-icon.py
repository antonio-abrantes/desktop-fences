"""Gera o ícone do DesktopFences (PNG + ICO multi-tamanho)."""

from __future__ import annotations

import io
import struct
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

# Paleta: vidro da fence (#0C0C12) + acento do app (#5B8DEF).
FILL = (12, 12, 18, 255)
BORDER = (168, 198, 245, 255)
GLOW = (91, 141, 239)
TILE_A = (244, 248, 255, 255)
TILE_B = (186, 208, 240, 255)
CHROME = (210, 226, 250, 230)
SHEEN = (255, 255, 255, 36)

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "src" / "DesktopFences.App" / "Assets"


def save_ico(path: Path, images: list[Image.Image]) -> None:
    pngs: list[tuple[int, bytes]] = []
    for im in images:
        buf = io.BytesIO()
        im.save(buf, format="PNG")
        pngs.append((im.size[0], buf.getvalue()))

    count = len(pngs)
    offset = 6 + 16 * count
    header = struct.pack("<HHH", 0, 1, count)
    entries = b""
    blobs = b""
    for width, data in pngs:
        encoded = 0 if width >= 256 else width
        entries += struct.pack("<BBBBHHII", encoded, encoded, 0, 0, 1, 32, len(data), offset)
        blobs += data
        offset += len(data)
    path.write_bytes(header + entries + blobs)


def draw_icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    pad = max(1, round(size * 0.07))
    border = max(1, round(size * 0.085))
    radius = max(3, round(size * 0.22))
    box = [pad, pad, size - pad - 1, size - pad - 1]

    if size >= 32:
        glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        gd = ImageDraw.Draw(glow)
        gd.rounded_rectangle(
            box,
            radius=radius,
            outline=(*GLOW, 220),
            width=max(2, border + 1),
        )
        glow = glow.filter(ImageFilter.GaussianBlur(radius=max(1.2, size * 0.032)))
        img = Image.alpha_composite(img, glow)

    draw = ImageDraw.Draw(img)
    draw.rounded_rectangle(box, radius=radius, fill=FILL)
    draw.rounded_rectangle(box, radius=radius, outline=BORDER, width=border)

    if size >= 48:
        sheen = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        sd = ImageDraw.Draw(sheen)
        sd.ellipse(
            [
                pad + size * 0.08,
                pad - size * 0.28,
                size - pad - size * 0.08,
                pad + size * 0.42,
            ],
            fill=SHEEN,
        )
        clip = Image.new("L", (size, size), 0)
        ImageDraw.Draw(clip).rounded_rectangle(box, radius=radius, fill=255)
        sheen.putalpha(ImageChops.multiply(sheen.getchannel("A"), clip))
        img = Image.alpha_composite(img, sheen)
        draw = ImageDraw.Draw(img)

    inset = pad + border + max(1, round(size * 0.09))
    inner = [inset, inset, size - inset - 1, size - inset - 1]
    inner_w = inner[2] - inner[0] + 1
    inner_h = inner[3] - inner[1] + 1

    show_window = size >= 48
    title_h = 0
    grid_top = inner[1]

    if show_window:
        win_r = max(2, round(size * 0.06))
        draw.rounded_rectangle(inner, radius=win_r, outline=CHROME, width=max(1, round(size * 0.018)))
        title_h = max(3, round(inner_h * 0.18))
        draw.rectangle(
            [inner[0] + 1, inner[1] + 1, inner[2] - 1, inner[1] + title_h],
            fill=(32, 36, 48, 255),
        )
        grid_top = inner[1] + title_h + max(2, round(size * 0.045))

    grid_bottom = inner[3] if not show_window else inner[3] - max(1, round(size * 0.03))
    grid_left = inner[0] if not show_window else inner[0] + max(2, round(size * 0.03))
    grid_right = inner[2] if not show_window else inner[2] - max(2, round(size * 0.03))

    gap = max(1, round(size * 0.045))
    cols, rows = 2, 2
    cell_w = (grid_right - grid_left + 1 - gap) // cols
    cell_h = (grid_bottom - grid_top + 1 - gap) // rows
    tile_r = max(1, round(min(cell_w, cell_h) * 0.28))
    colors = [TILE_A, TILE_B, TILE_B, TILE_A]

    i = 0
    for row in range(rows):
        for col in range(cols):
            x = grid_left + col * (cell_w + gap)
            y = grid_top + row * (cell_h + gap)
            draw.rounded_rectangle(
                [x, y, x + cell_w - 1, y + cell_h - 1],
                radius=tile_r,
                fill=colors[i],
            )
            i += 1

    return img


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    images = [draw_icon(s) for s in sizes]
    master = draw_icon(1024)
    master.save(OUT / "app.png", format="PNG")
    save_ico(OUT / "app.ico", images)
    print(f"Wrote {OUT / 'app.ico'} and PNGs")


if __name__ == "__main__":
    main()
