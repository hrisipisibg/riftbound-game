"""Regenerate Emberford's tileable color textures with Blender in background mode."""

from math import floor, pi, sin
from pathlib import Path
import sys

import bpy


def smooth(t):
    return t * t * (3 - 2 * t)


def lattice(x, y, seed):
    value = (x * 374761393 + y * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    value = ((value ^ (value >> 13)) * 1274126177) & 0xFFFFFFFF
    return (value ^ (value >> 16)) / 4294967295


def noise(u, v, columns, rows, seed):
    x, y = u * columns, v * rows
    ix, iy = floor(x), floor(y)
    dx, dy = smooth(x - ix), smooth(y - iy)
    a = lattice(ix % columns, iy % rows, seed)
    b = lattice((ix + 1) % columns, iy % rows, seed)
    c = lattice(ix % columns, (iy + 1) % rows, seed)
    d = lattice((ix + 1) % columns, (iy + 1) % rows, seed)
    return (a + (b - a) * dx) * (1 - dy) + (c + (d - c) * dx) * dy


def linear(srgb):
    return srgb / 12.92 if srgb <= 0.04045 else ((srgb + 0.055) / 1.055) ** 2.4


def color(kind, u, v):
    fine = noise(u, v, 73, 68, 6) - 0.5
    broad = noise(u, v, 4, 5, 5) - 0.5
    if kind == "wood":
        drift = (noise(u, v, 5, 4, 8) - 0.5) * 0.063
        spindles = noise(u + drift, v, 25, 3, 21) - 0.5
        shade = 0.92 + broad * 0.12 + spindles * 0.22 + fine * 0.05
        return shade, shade * 0.987, shade * 0.966
    if kind == "stone":
        strata = noise(u, v, 12, 9, 4) - 0.5
        shade = 0.92 + 0.15 * broad + 0.11 * strata + 0.04 * fine
        return shade, shade * 1.004, shade * 1.012
    if kind == "trail":
        dirt = noise(u, v, 21, 19, 8) - 0.5
        dust = noise(u, v, 97, 91, 3)
        shade = 0.86 + broad * 0.13 + dirt * 0.13 + fine * 0.08 + max(0, dust - 0.77) * 0.28
        return shade, shade * 0.974, shade * 0.926
    if kind == "river":
        warp = noise(u, v, 7, 7, 16) - 0.5
        wave = sin(2 * pi * (12 * v + warp * 1.2 + 0.22 * sin(u * 2 * pi)))
        spark = max(0, wave - 0.81) * 0.34
        shade = 0.86 + broad * 0.15 + fine * 0.04 + spark
        return shade * 0.89, shade, min(1, shade * 1.06)
    if kind == "fabric":
        weave = (sin(2 * pi * 39 * u) + sin(2 * pi * 43 * v)) * 0.011
        shade = 0.94 + weave + broad * 0.052 + fine * 0.026
        return shade, shade, shade
    shade = 0.93 + 0.10 * broad + (noise(u, v, 3, 37, 9) - 0.5) * 0.07 + 0.04 * fine
    return shade, shade, min(1, shade * 1.017)


def generate(destination):
    destination.mkdir(parents=True, exist_ok=True)
    size = 256
    for kind in ("wood", "stone", "trail", "river", "fabric", "metal"):
        image = bpy.data.images.new(kind, width=size, height=size, alpha=False)
        pixels = []
        for row in range(size):
            for column in range(size):
                srgb = color(kind, column / size, row / size)
                pixels.extend((*[linear(max(0, min(1, channel))) for channel in srgb], 1))
        image.pixels.foreach_set(pixels)
        image.update()
        image.filepath_raw = str(destination / (kind + "_albedo.png"))
        image.file_format = "PNG"
        image.save()
        bpy.data.images.remove(image)
        print(f"Wrote {kind}_albedo.png")


if __name__ == "__main__":
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    folder = Path(arguments[0]) if arguments else Path("Assets/RPG/ProceduralKit/Textures")
    generate(folder.resolve())
