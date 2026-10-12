#!/usr/bin/env python3
"""Render the TheTimer icon (blue clock, white rim, hands, top button) as a PNG.

Stdlib only and deterministic. Usage: make_icon.py [SIZE] [OUTPUT]
Geometry is defined on a 64-unit grid and scaled to any size; edges are
anti-aliased by 4x4 supersampling.
"""
import math
import struct
import sys
import zlib

BLUE = (30, 111, 217)
WHITE = (255, 255, 255)
SS = 4  # supersampling factor per axis

# Design on a 64x64 grid (derived from src/TheTimer.App/Assets/thetimer.png).
CX, CY, R, RIM = 32.0, 35.0, 27.0, 3.0
BUTTON = (26.0, 2.0, 38.0, 9.0)  # x0, y0, x1, y1
HAND_W = 4.0
HAND_V = ((32.0, 14.0), (32.0, 35.0))   # minute hand, straight up
HAND_D = ((32.0, 35.0), (43.0, 42.5))   # hour hand, lower right


def seg_dist(px, py, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def sample(x, y):
    """Return (r, g, b, a) at a point on the 64-unit grid."""
    x0, y0, x1, y1 = BUTTON
    if x0 <= x <= x1 and y0 <= y <= y1:
        return WHITE + (1,)
    d = math.hypot(x - CX, y - CY)
    if d > R:
        return (0, 0, 0, 0)
    if d > R - RIM:
        return WHITE + (1,)
    for seg in (HAND_V, HAND_D):
        if seg_dist(x, y, *seg) <= HAND_W / 2:
            return WHITE + (1,)
    return BLUE + (1,)


def render(size):
    scale = 64.0 / size
    rows = []
    for j in range(size):
        row = bytearray([0])  # PNG filter type 0
        for i in range(size):
            acc = [0, 0, 0, 0]
            for sj in range(SS):
                for si in range(SS):
                    r, g, b, a = sample((i + (si + 0.5) / SS) * scale,
                                        (j + (sj + 0.5) / SS) * scale)
                    acc[0] += r * a
                    acc[1] += g * a
                    acc[2] += b * a
                    acc[3] += a
            n = SS * SS
            if acc[3] == 0:
                row += bytes((0, 0, 0, 0))
            else:
                row += bytes((round(acc[0] / acc[3]), round(acc[1] / acc[3]),
                              round(acc[2] / acc[3]), round(255 * acc[3] / n)))
        rows.append(bytes(row))
    return b"".join(rows)


def chunk(tag, data):
    body = tag + data
    return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))


def write_png(path, size):
    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    png = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
           + chunk(b"IDAT", zlib.compress(render(size), 9)) + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)


if __name__ == "__main__":
    out_size = int(sys.argv[1]) if len(sys.argv) > 1 else 256
    out_path = sys.argv[2] if len(sys.argv) > 2 else "thetimer.png"
    write_png(out_path, out_size)
