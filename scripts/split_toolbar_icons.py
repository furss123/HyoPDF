"""Split 6x2 voxel toolbar icon sheet into 12 transparent 256x256 PNGs."""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image

SRC = Path(
    r"C:\Users\HyoT\.cursor\projects\c-Users-HyoT-Desktop-work-HyoPDF\assets"
    r"\c__Users_HyoT_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_"
    r"ChatGPT_Image_2026__6__28_____11_05_57-e5fca386-80c4-4a86-ab32-5d1cc711c2b4.png"
)
OUT_DIR = Path(r"C:\Users\HyoT\Desktop\work\HyoPDF\assets\icons\toolbar")
OUT_SIZE = 256
SOURCE_CROP = 155
BG_THRESHOLD = 60

NAMES = [
    "open-folder",
    "closed-folder",
    "zoom-in",
    "zoom-out",
    "rotate",
    "merge",
    "split",
    "compress",
    "print1",
    "print2",
    "settings",
    "bookmark",
]

CENTERS = [
    (121, 278),
    (270, 276),
    (421, 276),
    (583, 276),
    (765, 274),
    (891, 280),
    (121, 413),
    (266, 412),
    (421, 413),
    (572, 413),
    (747, 412),
    (881, 407),
]


def is_background(r: int, g: int, b: int) -> bool:
    return r <= BG_THRESHOLD and g <= BG_THRESHOLD and b <= BG_THRESHOLD


def remove_background(img: Image.Image) -> Image.Image:
    rgba = img.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    removable: set[tuple[int, int]] = set()
    queue: deque[tuple[int, int]] = deque()

    seeds: list[tuple[int, int]] = []
    for x in range(width):
        seeds.extend([(x, 0), (x, height - 1)])
    for y in range(height):
        seeds.extend([(0, y), (width - 1, y)])

    for x, y in seeds:
        if (x, y) in removable:
            continue
        r, g, b, _ = pixels[x, y]
        if is_background(r, g, b):
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
            if is_background(r, g, b):
                removable.add((nx, ny))
                queue.append((nx, ny))

    for x, y in removable:
        pixels[x, y] = (0, 0, 0, 0)

    return rgba


def content_bbox(img: Image.Image) -> tuple[int, int, int, int] | None:
    return img.getchannel("A").getbbox()


def fit_centered(img: Image.Image, size: int) -> Image.Image:
    bbox = content_bbox(img)
    if bbox is None:
        return Image.new("RGBA", (size, size), (0, 0, 0, 0))

    cropped = img.crop(bbox)
    cw, ch = cropped.size
    scale = min(size / cw, size / ch)
    new_w = max(1, int(round(cw * scale)))
    new_h = max(1, int(round(ch * scale)))
    resized = cropped.resize((new_w, new_h), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.paste(resized, ((size - new_w) // 2, (size - new_h) // 2), resized)
    return canvas


def crop_icon(sheet: Image.Image, center_x: int, center_y: int) -> Image.Image:
    half = SOURCE_CROP // 2
    left = max(0, center_x - half)
    top = max(0, center_y - half)
    right = min(sheet.width, left + SOURCE_CROP)
    bottom = min(sheet.height, top + SOURCE_CROP)
    left = max(0, right - SOURCE_CROP)
    top = max(0, bottom - SOURCE_CROP)
    return sheet.crop((left, top, right, bottom))


def main() -> None:
    sheet = remove_background(Image.open(SRC))
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    for name, center in zip(NAMES, CENTERS, strict=True):
        icon = crop_icon(sheet, center[0], center[1])
        output = fit_centered(icon, OUT_SIZE)
        out_path = OUT_DIR / f"{name}.png"
        output.save(out_path, format="PNG", optimize=True)
        print(f"Wrote {out_path.name}")

    print(f"Done: {len(NAMES)} icons in {OUT_DIR}")


if __name__ == "__main__":
    main()
