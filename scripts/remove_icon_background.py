"""Remove outer black background from HyoPDF app icon, output 512x512 transparent PNG."""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image

SRC = Path(
    r"C:\Users\HyoT\.cursor\projects\c-Users-HyoT-Desktop-work-HyoPDF\assets"
    r"\c__Users_HyoT_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_"
    r"ChatGPT_Image_2026__6__28_____11_03_13-48db14e3-d10f-4fec-8131-46c077e01c39.png"
)
OUT = Path(r"C:\Users\HyoT\Desktop\work\HyoPDF\assets\icons\app-icon-512.png")

BG_THRESHOLD = 32  # pixels at or below this on all RGB channels are background candidates


def is_background(r: int, g: int, b: int) -> bool:
  return r <= BG_THRESHOLD and g <= BG_THRESHOLD and b <= BG_THRESHOLD


def flood_fill_background(pixels, width: int, height: int) -> set[tuple[int, int]]:
  removable: set[tuple[int, int]] = set()
  queue: deque[tuple[int, int]] = deque()

  for seed in ((0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)):
    x, y = seed
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

  return removable


def soften_outer_edge(pixels, width: int, height: int, removed: set[tuple[int, int]]) -> None:
  """Feather alpha on pixels adjacent to removed background for smoother glow falloff."""
  for x in range(width):
    for y in range(height):
      if (x, y) in removed:
        continue
      r, g, b, a = pixels[x, y]
      neighbors_removed = sum(
        1
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))
        if (nx, ny) in removed
      )
      if neighbors_removed == 0:
        continue
      # Dark fringe pixels that border transparency get partial alpha
      darkness = max(r, g, b)
      if darkness <= BG_THRESHOLD + 40:
        feather = min(255, int(a * (0.55 + 0.11 * neighbors_removed)))
        pixels[x, y] = (r, g, b, feather)


def main() -> None:
  img = Image.open(SRC).convert("RGBA")
  pixels = img.load()
  width, height = img.size

  removed = flood_fill_background(pixels, width, height)
  for x, y in removed:
    pixels[x, y] = (0, 0, 0, 0)

  soften_outer_edge(pixels, width, height, removed)

  out = img.resize((512, 512), Image.Resampling.LANCZOS)
  OUT.parent.mkdir(parents=True, exist_ok=True)
  out.save(OUT, format="PNG", optimize=True)
  print(f"Saved: {OUT} ({OUT.stat().st_size} bytes)")


if __name__ == "__main__":
  main()
