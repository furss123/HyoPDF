"""Remove dark background from isometric desk illustration, output 1200x1200 transparent PNG."""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image

SRC = Path(
    r"C:\Users\HyoT\.cursor\projects\c-Users-HyoT-Desktop-work-HyoPDF\assets"
    r"\c__Users_HyoT_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_"
    r"ChatGPT_Image_2026__6__28_____11_06_58-3aae3ff0-7e6a-46c9-83af-b9f21c083139.png"
)
OUT = Path(r"C:\Users\HyoT\Desktop\work\HyoPDF\assets\icons\desk-illustration-1200.png")
OUT_SIZE = 1200
BG_THRESHOLD = 40
CONTENT_MARGIN = 24


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


def soften_edge_alpha(img: Image.Image, removed: set[tuple[int, int]]) -> None:
    pixels = img.load()
    width, height = img.size

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
            if max(r, g, b) <= BG_THRESHOLD + 55:
                pixels[x, y] = (r, g, b, min(a, int(a * (0.5 + 0.12 * neighbor_removed))))


def fit_on_canvas(img: Image.Image, size: int, margin: int) -> Image.Image:
    bbox = img.getchannel("A").getbbox()
    if bbox is None:
        return Image.new("RGBA", (size, size), (0, 0, 0, 0))

    cropped = img.crop(bbox)
    max_side = size - margin * 2
    cw, ch = cropped.size
    scale = min(max_side / cw, max_side / ch)
    new_w = max(1, int(round(cw * scale)))
    new_h = max(1, int(round(ch * scale)))
    resized = cropped.resize((new_w, new_h), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.paste(resized, ((size - new_w) // 2, (size - new_h) // 2), resized)
    return canvas


def main() -> None:
    source = Image.open(SRC)
    transparent = remove_background(source)
    output = fit_on_canvas(transparent, OUT_SIZE, CONTENT_MARGIN)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    output.save(OUT, format="PNG", optimize=True)

    px = output.load()
    corner_alpha = px[0, 0][3]
    trans = sum(1 for y in range(OUT_SIZE) for x in range(OUT_SIZE) if px[x, y][3] == 0)
    print(f"Saved: {OUT}")
    print(f"Size: {output.size}, transparent: {100 * trans / (OUT_SIZE * OUT_SIZE):.1f}%, corner alpha: {corner_alpha}")


if __name__ == "__main__":
    main()
