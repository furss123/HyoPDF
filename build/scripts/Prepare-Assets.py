"""Convert HyoPDF branding PNGs to transparent ICO/BMP installer assets."""
from __future__ import annotations

import argparse
import struct
from collections import deque
from pathlib import Path

from PIL import Image

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_APP_ICON = REPO_ROOT / "assets" / "icons" / "app-icon-512.png"
DEFAULT_BANNER = REPO_ROOT / "assets" / "installer" / "banner-source.png"
DEFAULT_SIDE = REPO_ROOT / "assets" / "installer" / "side-image-source.png"
DESK_ILLUSTRATION = REPO_ROOT / "assets" / "icons" / "desk-illustration-1200.png"

ICON_SIZES = (256, 128, 64, 48, 32, 16)
BANNER_SIZE = (497, 58)
SIDE_SIZE = (164, 314)
ICON_BG_THRESHOLD = 32
ILLUSTRATION_BG_THRESHOLD = 40


def is_dark_background(r: int, g: int, b: int, threshold: int) -> bool:
    return r <= threshold and g <= threshold and b <= threshold


def flood_fill_background(
    image: Image.Image,
    threshold: int,
    *,
    all_edges: bool = False,
) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    removable: set[tuple[int, int]] = set()
    queue: deque[tuple[int, int]] = deque()

    seeds: list[tuple[int, int]] = []
    if all_edges:
        for x in range(width):
            seeds.extend([(x, 0), (x, height - 1)])
        for y in range(height):
            seeds.extend([(0, y), (width - 1, y)])
    else:
        seeds.extend([(0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)])

    for x, y in seeds:
        if (x, y) in removable:
            continue
        r, g, b, _ = pixels[x, y]
        if is_dark_background(r, g, b, threshold):
            removable.add((x, y))
            queue.append((x, y))

    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if nx < 0 or ny < 0 or nx >= width or ny >= height:
                continue
            if (nx, ny) in removable:
                continue
            r, g, b, _ = pixels[nx, ny]
            if is_dark_background(r, g, b, threshold):
                removable.add((nx, ny))
                queue.append((nx, ny))

    for x, y in removable:
        pixels[x, y] = (0, 0, 0, 0)

    soften_edge_alpha(pixels, width, height, removable, threshold)
    return rgba


def soften_edge_alpha(
    pixels,
    width: int,
    height: int,
    removed: set[tuple[int, int]],
    threshold: int,
) -> None:
    fringe = 55 if threshold >= 40 else 40
    for x in range(width):
        for y in range(height):
            if (x, y) in removed:
                continue
            r, g, b, a = pixels[x, y]
            neighbor_removed = sum(
                1
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))
                if (nx, ny) in removed
            )
            if neighbor_removed == 0:
                continue
            if max(r, g, b) <= threshold + fringe:
                pixels[x, y] = (r, g, b, min(a, int(a * (0.5 + 0.12 * neighbor_removed))))


def remove_icon_background(source: Path) -> Image.Image:
    return flood_fill_background(Image.open(source), ICON_BG_THRESHOLD, all_edges=False)


def remove_illustration_background(source: Path) -> Image.Image:
    return flood_fill_background(
        Image.open(source),
        ILLUSTRATION_BG_THRESHOLD,
        all_edges=True,
    )


def ensure_source_files(banner: Path, side: Path) -> None:
    banner.parent.mkdir(parents=True, exist_ok=True)
    if not side.exists() and DESK_ILLUSTRATION.exists():
        side.write_bytes(DESK_ILLUSTRATION.read_bytes())
    if not banner.exists() and DESK_ILLUSTRATION.exists():
        image = Image.open(DESK_ILLUSTRATION).convert("RGBA")
        width, height = image.size
        crop_height = max(1, height // 4)
        banner_crop = image.crop((0, 0, width, crop_height))
        banner_crop.resize((1200, 140), Image.Resampling.LANCZOS).save(banner, format="PNG")


def fit_cover_transparent(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    target_w, target_h = size
    scale = max(target_w / image.width, target_h / image.height)
    resized = image.resize(
        (max(1, int(image.width * scale)), max(1, int(image.height * scale))),
        Image.Resampling.LANCZOS,
    )
    left = (resized.width - target_w) // 2
    top = (resized.height - target_h) // 2
    cropped = resized.crop((left, top, left + target_w, top + target_h))

    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.paste(cropped, (0, 0), cropped)
    return canvas


def save_ico(path: Path, image: Image.Image) -> None:
    images = []
    for size in ICON_SIZES:
        resized = image.resize((size, size), Image.Resampling.LANCZOS)
        images.append(resized)
    images[0].save(
        path,
        format="ICO",
        sizes=[(img.width, img.height) for img in images],
        append_images=images[1:],
    )


def save_bmp32_alpha(path: Path, image: Image.Image) -> None:
    rgba = image.convert("RGBA")
    width, height = rgba.size
    pixels = rgba.load()
    pixel_data = bytearray()

    for y in range(height - 1, -1, -1):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            pixel_data.extend((b, g, r, a))

    pixel_array_size = len(pixel_data)
    file_size = 14 + 40 + pixel_array_size
    file_header = struct.pack("<2sIHHI", b"BM", file_size, 0, 0, 54)
    info_header = struct.pack(
        "<IIIHHIIIIII",
        40,
        width,
        height,
        1,
        32,
        0,
        pixel_array_size,
        0,
        0,
        0,
        0,
    )

    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("wb") as handle:
        handle.write(file_header)
        handle.write(info_header)
        handle.write(pixel_data)


def save_bmp(path: Path, image: Image.Image, size: tuple[int, int]) -> None:
    fitted = fit_cover_transparent(image, size)
    save_bmp32_alpha(path, fitted)


def save_png(path: Path, image: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="PNG", optimize=True)


def transparent_ratio(image: Image.Image) -> float:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    transparent = sum(1 for y in range(height) for x in range(width) if pixels[x, y][3] == 0)
    return 100.0 * transparent / (width * height)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--app-icon", type=Path, default=DEFAULT_APP_ICON)
    parser.add_argument("--banner", type=Path, default=DEFAULT_BANNER)
    parser.add_argument("--side", type=Path, default=DEFAULT_SIDE)
    args = parser.parse_args()

    icons_dir = REPO_ROOT / "assets" / "icons"
    installer_dir = REPO_ROOT / "assets" / "installer"
    icons_dir.mkdir(parents=True, exist_ok=True)
    installer_dir.mkdir(parents=True, exist_ok=True)

    ensure_source_files(args.banner, args.side)

    if not args.app_icon.exists():
        raise FileNotFoundError(f"App icon not found: {args.app_icon}")
    if not args.banner.exists():
        raise FileNotFoundError(f"Banner source not found: {args.banner}")
    if not args.side.exists():
        raise FileNotFoundError(f"Side image source not found: {args.side}")

    app_icon = remove_icon_background(args.app_icon)
    banner = remove_illustration_background(args.banner)
    side = remove_illustration_background(args.side)

    save_png(args.banner, banner)
    save_png(args.side, side)

    app_ico = icons_dir / "app.ico"
    setup_ico = installer_dir / "setup-icon.ico"
    banner_bmp = installer_dir / "wizard-banner.bmp"
    side_bmp = installer_dir / "wizard-image.bmp"

    save_ico(app_ico, app_icon)
    setup_ico.write_bytes(app_ico.read_bytes())
    save_bmp(banner_bmp, banner, BANNER_SIZE)
    save_bmp(side_bmp, side, SIDE_SIZE)

    print(f"Wrote {app_ico} (transparent {transparent_ratio(app_icon):.1f}%)")
    print(f"Wrote {setup_ico}")
    print(f"Wrote {banner_bmp} (transparent {transparent_ratio(banner):.1f}%)")
    print(f"Wrote {side_bmp} (transparent {transparent_ratio(side):.1f}%)")
    print(f"Updated {args.banner}")
    print(f"Updated {args.side}")
    print("Assets prepared successfully.")


if __name__ == "__main__":
    main()
