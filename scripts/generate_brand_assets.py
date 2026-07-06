"""Generate on-brand HyoPDF icon + banner assets from the HyoT brand kit.

Deterministic Pillow generator. Brand constants (HyoT-brand-kit_2.md):
  - Tile: rounded square, diagonal gradient #4A9FE0 (top-left) -> #2B7CC7
    (bottom-right) ~135deg, corner radius ~22% of tile.
  - Glyph: single white "document page with a folded corner", generous padding,
    reads at 24px. Flat, minimal, soft depth. No text inside the icon.

Outputs:
  assets/icons/app-icon-512.png     master 512 icon (transparent outside tile)
  assets/icons/icon-1024.png        master 1024
  data/icon.webp (root hyopdf_icon.webp)  512 site icon
  root hyopdf_banner.webp           1200x480 banner (tile + wordmark)

Run:  python scripts/generate_brand_assets.py
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageFilter

REPO = Path(__file__).resolve().parents[1]

# --- Brand constants -------------------------------------------------------
BLUE_TL = (74, 159, 224)    # #4A9FE0
BLUE_BR = (43, 124, 199)    # #2B7CC7
BLUE_DARK = (43, 124, 199)  # #2B7CC7  (wordmark)
BLUE_ACCENT = (74, 159, 224)  # #4A9FE0 (tagline)
FOLD_TINT = (206, 228, 248)  # light blue underside of the dog-ear
LINE_TINT = (191, 220, 245)  # faint document text lines

SEGOE_BOLD = Path("C:/Windows/Fonts/segoeuib.ttf")
SEGOE_SEMI = Path("C:/Windows/Fonts/seguisb.ttf")


def diagonal_gradient(size: int) -> Image.Image:
    """Smooth top-left -> bottom-right gradient tile fill (no numpy)."""
    g = 64
    mask = Image.new("L", (g, g))
    mpx = mask.load()
    denom = (g - 1) * 2
    for y in range(g):
        for x in range(g):
            mpx[x, y] = int(255 * (x + y) / denom)
    mask = mask.resize((size, size), Image.Resampling.LANCZOS)
    base = Image.new("RGB", (size, size), BLUE_TL)
    end = Image.new("RGB", (size, size), BLUE_BR)
    return Image.composite(end, base, mask)


def rounded_mask(size: int, radius: int, margin: int) -> Image.Image:
    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle(
        (margin, margin, size - margin - 1, size - margin - 1),
        radius=radius,
        fill=255,
    )
    return mask


def make_glyph(size: int) -> Image.Image:
    """White document with a folded top-right corner, centered on `size`."""
    S = size
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    # Page box (portrait), centered with generous padding.
    pw = int(S * 0.40)
    ph = int(S * 0.52)
    x0 = (S - pw) // 2
    y0 = (S - ph) // 2
    x1 = x0 + pw
    y1 = y0 + ph
    fold = int(pw * 0.30)

    # Soft drop shadow for subtle depth.
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    off = int(S * 0.012)
    sd.polygon(
        [
            (x0 + off, y0 + off),
            (x1 - fold + off, y0 + off),
            (x1 + off, y0 + fold + off),
            (x1 + off, y1 + off),
            (x0 + off, y1 + off),
        ],
        fill=(10, 30, 60, 90),
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(S * 0.02))
    layer = Image.alpha_composite(layer, shadow)
    d = ImageDraw.Draw(layer)

    # Page body with chamfered (folded) top-right corner.
    page = [
        (x0, y0),
        (x1 - fold, y0),
        (x1, y0 + fold),
        (x1, y1),
        (x0, y1),
    ]
    d.polygon(page, fill=(255, 255, 255, 255))

    # Dog-ear (the folded-over corner underside).
    d.polygon(
        [(x1 - fold, y0), (x1, y0 + fold), (x1 - fold, y0 + fold)],
        fill=FOLD_TINT + (255,),
    )
    d.line([(x1 - fold, y0), (x1 - fold, y0 + fold)], fill=(120, 170, 215, 180),
           width=max(1, int(S * 0.006)))
    d.line([(x1 - fold, y0 + fold), (x1, y0 + fold)], fill=(120, 170, 215, 180),
           width=max(1, int(S * 0.006)))

    # A few faint "text" lines to read clearly as a document.
    lh = max(2, int(S * 0.018))
    lx0 = x0 + int(pw * 0.16)
    lx1 = x1 - int(pw * 0.16)
    ly = y0 + fold + int(ph * 0.14)
    gap = int(ph * 0.13)
    widths = [1.0, 1.0, 0.6]
    for i, w in enumerate(widths):
        yy = ly + i * gap
        d.rounded_rectangle(
            (lx0, yy, lx0 + int((lx1 - lx0) * w), yy + lh),
            radius=lh // 2,
            fill=LINE_TINT + (255,),
        )
    return layer


def make_icon(size: int) -> Image.Image:
    """Full app icon: gradient rounded tile + white document glyph."""
    margin = int(size * 0.055)
    radius = int((size - 2 * margin) * 0.22)
    tile = diagonal_gradient(size).convert("RGBA")
    mask = rounded_mask(size, radius, margin)

    icon = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    icon.paste(tile, (0, 0), mask)

    # Soft top-down sheen (no hard seam): white overlay whose alpha ramps
    # from a gentle highlight at the top to zero by mid-tile.
    ramp = Image.new("L", (1, size))
    rpx = ramp.load()
    peak = 30
    fade_to = int(size * 0.62)
    for y in range(size):
        rpx[0, y] = int(peak * max(0.0, 1.0 - y / fade_to)) if y < fade_to else 0
    ramp = ramp.resize((size, size))
    ramp = Image.composite(ramp, Image.new("L", (size, size), 0), mask)
    sheen = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    sheen.putalpha(ramp)
    icon = Image.alpha_composite(icon, sheen)

    glyph = make_glyph(size)
    icon = Image.alpha_composite(icon, glyph)
    return icon


def load_font(path: Path, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(path), size)


def make_banner(w: int = 1200, h: int = 480) -> Image.Image:
    banner = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    tile_size = int(h * 0.72)
    tile = make_icon(tile_size)
    ty = (h - tile_size) // 2
    tx = int(h * 0.16)
    banner.alpha_composite(tile, (tx, ty))

    d = ImageDraw.Draw(banner)
    text_x = tx + tile_size + int(h * 0.16)
    title_font = load_font(SEGOE_BOLD, int(h * 0.24))
    tag_path = SEGOE_SEMI if SEGOE_SEMI.exists() else SEGOE_BOLD
    tag_font = load_font(tag_path, int(h * 0.095))

    title = "HyoPDF"
    tag = "PDF Viewer & Editor"
    tbox = d.textbbox((0, 0), title, font=title_font)
    th = tbox[3] - tbox[1]
    gbox = d.textbbox((0, 0), tag, font=tag_font)
    gh = gbox[3] - gbox[1]
    gap = int(h * 0.06)
    block_h = th + gap + gh
    ty0 = (h - block_h) // 2 - tbox[1]
    d.text((text_x, ty0), title, font=title_font, fill=BLUE_DARK + (255,))
    d.text((text_x, ty0 + th + gap - gbox[1]), tag, font=tag_font,
           fill=BLUE_ACCENT + (255,))
    return banner


def make_empty_illustration(size: int = 1200) -> Image.Image:
    """On-brand empty-state art: a soft blue glow behind two document pages."""
    S = size
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    # Soft radial blue glow.
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    cx, cy, r = S // 2, int(S * 0.52), int(S * 0.36)
    gd.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(74, 159, 224, 46))
    glow = glow.filter(ImageFilter.GaussianBlur(S * 0.06))
    img = Image.alpha_composite(img, glow)

    def page(layer_draw, box, fold_ratio, fill, edge, lines=False):
        x0, y0, x1, y1 = box
        pw = x1 - x0
        ph = y1 - y0
        f = int(pw * fold_ratio)
        poly = [(x0, y0), (x1 - f, y0), (x1, y0 + f), (x1, y1), (x0, y1)]
        layer_draw.polygon(poly, fill=fill)
        layer_draw.polygon([(x1 - f, y0), (x1, y0 + f), (x1 - f, y0 + f)],
                           fill=edge)
        if lines:
            lh = max(3, int(ph * 0.028))
            lx0 = x0 + int(pw * 0.18)
            lx1 = x1 - int(pw * 0.18)
            ly = y0 + f + int(ph * 0.16)
            gap = int(ph * 0.11)
            for i, w in enumerate((1.0, 1.0, 0.9, 0.55)):
                yy = ly + i * gap
                layer_draw.rounded_rectangle(
                    (lx0, yy, lx0 + int((lx1 - lx0) * w), yy + lh),
                    radius=lh // 2, fill=(191, 220, 245, 255))

    # Back page (tilted, light blue).
    back = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    bd = ImageDraw.Draw(back)
    bw, bh = int(S * 0.34), int(S * 0.44)
    bx = cx - bw // 2 + int(S * 0.02)
    by = cy - bh // 2 - int(S * 0.02)
    page(bd, (bx, by, bx + bw, by + bh), 0.26,
         (206, 228, 248, 255), (168, 203, 236, 255))
    back = back.rotate(-9, resample=Image.BICUBIC, center=(cx, cy))
    img = Image.alpha_composite(img, back)

    # Front page (white with blue text lines + soft shadow).
    fw, fh = int(S * 0.36), int(S * 0.47)
    fx = cx - fw // 2 - int(S * 0.02)
    fy = cy - fh // 2 + int(S * 0.01)
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.rectangle((fx, fy, fx + fw, fy + fh), fill=(20, 50, 90, 70))
    shadow = shadow.filter(ImageFilter.GaussianBlur(S * 0.02))
    img = Image.alpha_composite(img, shadow)
    front = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    fd = ImageDraw.Draw(front)
    page(fd, (fx, fy, fx + fw, fy + fh), 0.24,
         (255, 255, 255, 255), (206, 228, 248, 255), lines=True)
    front = front.rotate(4, resample=Image.BICUBIC, center=(cx, cy))
    img = Image.alpha_composite(img, front)
    return img


def save_webp(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="WEBP", quality=95, method=6)


def main() -> None:
    icons_dir = REPO / "assets" / "icons"
    icons_dir.mkdir(parents=True, exist_ok=True)

    icon1024 = make_icon(1024)
    icon512 = icon1024.resize((512, 512), Image.Resampling.LANCZOS)

    icon1024.save(icons_dir / "icon-1024.png", format="PNG", optimize=True)
    icon512.save(icons_dir / "app-icon-512.png", format="PNG", optimize=True)
    save_webp(icon512, REPO / "hyopdf_icon.webp")

    banner = make_banner()
    save_webp(banner, REPO / "hyopdf_banner.webp")

    illo = make_empty_illustration(1200)
    illo.save(icons_dir / "desk-illustration-1200.png", format="PNG",
              optimize=True)

    # Quick legibility proof at small sizes.
    proof = Image.new("RGBA", (24 + 48 + 96 + 40, 110), (240, 242, 246, 255))
    x = 8
    for s in (24, 48, 96):
        proof.alpha_composite(icon1024.resize((s, s), Image.Resampling.LANCZOS),
                              (x, (110 - s) // 2))
        x += s + 8
    proof.convert("RGB").save(icons_dir / "_icon-proof.png")

    print("Wrote:")
    for p in ("assets/icons/icon-1024.png", "assets/icons/app-icon-512.png",
              "hyopdf_icon.webp", "hyopdf_banner.webp",
              "assets/icons/_icon-proof.png"):
        print("  ", p)


if __name__ == "__main__":
    main()
