"""Paint tileable terrain, masonry and shoreline textures for the Riftbound campaign."""

import math
from pathlib import Path
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from emberford_textures import lattice, linear, noise


PALETTES = {
    "meadow": ((.34, .40, .25), (.56, .56, .35), (.72, .68, .46)),
    "trail": ((.39, .32, .24), (.59, .49, .36), (.72, .63, .48)),
    "bedrock": ((.41, .42, .40), (.55, .56, .52), (.66, .68, .61)),
    "forest": ((.28, .27, .22), (.43, .40, .29), (.57, .52, .34)),
    "highland": ((.34, .37, .35), (.53, .55, .47), (.67, .67, .58)),
    "snow": ((.55, .63, .66), (.78, .81, .78), (.93, .92, .84)),
    "canyon": ((.43, .27, .20), (.65, .44, .31), (.78, .56, .38)),
    "cobbles": ((.35, .32, .26), (.56, .53, .46), (.72, .67, .56)),
    "silt": ((.25, .29, .23), (.44, .43, .32), (.61, .58, .43)),
    "vault": ((.24, .25, .26), (.37, .38, .39), (.54, .52, .48)),
}


def smooth(a, b, value):
    t = max(0, min(1, (value - a) / (b - a)))
    return t * t * (3 - 2 * t)


def stone_cells(x, y, columns, rows, seed):
    x *= columns
    y *= rows
    ix, iy = math.floor(x), math.floor(y)
    nearest = second = 1e10
    tone = 0.5
    for a in range(ix - 1, ix + 2):
        for b in range(iy - 1, iy + 2):
            tile_x, tile_y = a % columns, b % rows
            px = a + 0.18 + 0.64 * lattice(tile_x, tile_y, seed)
            py = b + 0.18 + 0.64 * lattice(tile_x, tile_y, seed + 8)
            d = (px - x) ** 2 + (py - y) ** 2
            if d < nearest:
                second, nearest = nearest, d
                tone = lattice(tile_x, tile_y, seed + 3)
            elif d < second:
                second = d
    return tone, second - nearest


def masonry(x, y, columns, rows, seed):
    warp = noise(x, y, 9, 9, seed) - .5
    y = y * rows + warp * .10
    row = math.floor(y)
    stagger = (.51 * (row % 2)) + lattice(row % rows, 0, seed) * .16
    x = x * columns + stagger + warp * .13
    column = math.floor(x)
    dx = abs((x % 1) - .5) * 2
    dy = abs((y % 1) - .5) * 2
    distance = dx ** 5 + dy ** 5
    interior = 1 - smooth(.67, .98, distance)
    rim = smooth(.31, .67, distance) * interior
    shade = lattice(column % columns, row % rows, seed + 1)
    return interior, rim, shade


def surface(kind, u, v):
    broad = noise(u, v, 3, 3, 33)
    medium = noise(u, v, 11, 10, 53)
    dust = noise(u, v, 49, 53, 77)
    small = noise(u, v, 113, 101, 11)
    if kind in ("cobbles", "vault"):
        tile, rim, tone = masonry(u, v, 16 if kind == "cobbles" else 6, 20 if kind == "cobbles" else 8, 220)
        value = .22 + tile * (.39 + .23 * tone + (dust - .5) * .08) - rim * .105 + .055 * (medium - .5)
        elevation = tile * (.56 + .11 * tone - rim * .27) + .07 * medium + .03 * dust
    elif kind in ("bedrock", "highland"):
        tone, gap = stone_cells(u + .038 * medium, v + .04 * broad, 8, 10, 90)
        face = smooth(.012, .099, gap)
        layer = .5 + .5 * math.sin(2 * math.pi * (v * 13 + .36 * broad + .19 * medium))
        fissure = (1 - face) * smooth(.43, .78, medium)
        value = .32 + .13 * face + .16 * tone + .19 * medium + .13 * (layer - .5) - .11 * fissure
        value += .07 * (dust - .5) + .045 * (small - .5)
        elevation = .27 * face + .19 * layer + .31 * medium + .073 * dust
    elif kind == "snow":
        melt = smooth(.13, .43, .67 * broad + .33 * medium)
        value = .28 + .53 * melt + .10 * (dust - .5) + .025 * small
        elevation = .30 * melt + .14 * medium + .025 * dust
    elif kind == "canyon":
        strata = .5 + .5 * math.sin(2 * math.pi * (v * 7 + .42 * broad + .14 * medium))
        grains = smooth(.75, .96, dust)
        value = .42 + .17 * medium + .10 * strata + .14 * broad + .075 * grains
        elevation = .46 * medium + .19 * strata + .055 * dust
    else:
        grain = smooth(.74, .97, dust)
        value = .25 + .30 * broad + .19 * medium + .12 * dust
        elevation = .33 * broad + .24 * medium + .085 * dust
        if kind == "meadow":
            blades = noise(u, v, 103, 37, 31) * noise(u, v, 23, 51, 12)
            value = value * .93 + .16 * blades
            elevation += .075 * blades
        elif kind == "forest":
            leaf = smooth(.69, .90, .66 * dust + .34 * small)
            value += .19 * leaf
            elevation += .12 * leaf
        elif kind == "silt":
            value += .10 * grain
            elevation += .07 * grain
        else:
            value += .14 * grain
            elevation += .085 * grain
    return max(0, min(1, value)), elevation


def save_png(path, size, values, normal=False):
    image = bpy.data.images.new(path.stem, width=size, height=size, alpha=False)
    if normal:
        image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(values)
    image.update()
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def generate(folder, selected):
    folder.mkdir(parents=True, exist_ok=True)
    size = 384
    for kind, (shadow, base, lit) in PALETTES.items():
        if selected and kind not in selected:
            continue
        albedo = []
        heights = []
        for row in range(size):
            for column in range(size):
                f, height = surface(kind, column / size, row / size)
                low, high, mix = (shadow, base, f * 2) if f <= .5 else (base, lit, (f - .5) * 2)
                albedo.extend((*[linear(a + (b - a) * mix) for a, b in zip(low, high)], 1))
                heights.append(height)
        normal = []
        intensity = 2.4 if kind in ("cobbles", "vault", "bedrock") else 1.55
        for row in range(size):
            for column in range(size):
                dx = (heights[row * size + ((column + 1) % size)] - heights[row * size + ((column - 1) % size)]) * intensity
                dy = (heights[((row + 1) % size) * size + column] - heights[((row - 1) % size) * size + column]) * intensity
                length = math.sqrt(dx * dx + dy * dy + 1)
                normal.extend((.5 - .5 * dx / length, .5 - .5 * dy / length, .5 + .5 / length, 1))
        save_png(folder / (kind + "_albedo.png"), size, albedo)
        save_png(folder / (kind + "_normal.png"), size, normal, normal=True)
        print("Generated seamless " + kind)


if __name__ == "__main__":
    output, *selected = sys.argv[sys.argv.index("--") + 1:]
    generate(Path(output).resolve(), selected)
