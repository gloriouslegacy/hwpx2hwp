"""src/Hwpx2Hwp.App/app.ico 를 만든다(F8). Python 3 표준 라이브러리만 쓴다.

모양: 파란 문서(오른쪽 위 접힌 모서리) 위에 흰 오른쪽 화살표, 문서 아래쪽에 흰 'HWP' 글자.
16·24px 는 글자를 빼고 화살표를 키운다(작은 크기에서 뭉개지지 않게).
각 크기는 슈퍼샘플링으로 안티앨리어싱한 32bit RGBA PNG 로 만들어 ICO 컨테이너에 담는다.

실행(레포 루트): python3 tools/make_icon.py [--force]
기존 app.ico 가 있으면 --force 없이는 덮어쓰지 않는다.
"""

import argparse
import struct
import sys
import zlib
from pathlib import Path

SIZES = (16, 24, 32, 48, 64, 256)
SMALL_MAX = 24  # 이 크기 이하는 글자 없는 단순형
OUT_PATH = Path(__file__).resolve().parent.parent / "src" / "Hwpx2Hwp.App" / "app.ico"

BODY_TOP = (59, 130, 246)
BODY_BOTTOM = (29, 78, 216)
FOLD = (191, 214, 250)
WHITE = (255, 255, 255)


class Layout:
    """좌표는 캔버스 0..1 단위(왼쪽 위 원점)."""

    def __init__(self, size):
        if size <= SMALL_MAX:
            # 작은 크기는 곧은 가장자리를 픽셀 경계에 맞춰 흐려지지 않게 한다
            def snap(v):
                return round(v * size) / size

            self.doc = tuple(snap(v) for v in (0.12, 0.03, 0.88, 0.97))
            self.fold = snap(0.30)
            self.radius = 0.08
            # 화살표: 몸통(x0, x1, y 중심, 반두께) + 머리(밑변 x, 끝 x, 반높이)
            top, bottom = snap(0.58 - 0.085), snap(0.58 + 0.085)
            self.arrow = (snap(0.22), 0.56, (top + bottom) / 2, (bottom - top) / 2, snap(0.50), 0.80, 0.22)
            self.text = None
        else:
            self.doc = (0.14, 0.03, 0.86, 0.97)
            self.fold = 0.22
            self.radius = 0.06
            self.arrow = (0.27, 0.55, 0.38, 0.065, 0.50, 0.76, 0.18)
            self.text = (0.66, 0.20)  # (글자 윗선 y, 글자 높이)


def in_doc(lay, x, y):
    """문서 안이면 'body' 또는 'fold', 밖이면 None."""
    x0, y0, x1, y1 = lay.doc
    if not (x0 <= x <= x1 and y0 <= y <= y1):
        return None
    f = lay.fold
    if x >= x1 - f and y <= y0 + f:
        dx = x - (x1 - f)
        dy = y - y0
        return None if dx > dy else "fold"
    r = lay.radius
    # 접힌 모서리를 뺀 세 모서리를 둥글게: (중심 x, 중심 y, 바깥 방향 x, 바깥 방향 y)
    for cx, cy, ox, oy in ((x0 + r, y0 + r, -1, -1), (x0 + r, y1 - r, -1, 1), (x1 - r, y1 - r, 1, 1)):
        if (x - cx) * ox > 0 and (y - cy) * oy > 0 and (x - cx) ** 2 + (y - cy) ** 2 > r * r:
            return None
    return "body"


def in_arrow(lay, x, y):
    sx0, sx1, cy, half, hx0, hx1, hh = lay.arrow
    if sx0 <= x <= sx1 and abs(y - cy) <= half:
        return True
    if hx0 <= x <= hx1:
        return abs(y - cy) <= hh * (hx1 - x) / (hx1 - hx0)
    return False


def seg_dist2(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    t = ((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy)
    t = max(0.0, min(1.0, t))
    dx, dy = px - (ax + t * vx), py - (ay + t * vy)
    return dx * dx + dy * dy


def letter_h(x, y, w, h, s):
    return x <= s or x >= w - s or abs(y - h / 2) <= s / 2


def letter_w(x, y, w, h, s):
    pts = ((0.0, 0.0), (w * 0.25, h), (w * 0.5, h * 0.25), (w * 0.75, h), (w, 0.0))
    half2 = (s * 0.55) ** 2
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        if seg_dist2(x, y, ax, ay, bx, by) <= half2:
            return True
    return False


def letter_p(x, y, w, h, s):
    if x <= s:
        return True
    bowl_h = h * 0.6
    r = bowl_h / 2
    cx, cy = w - r, r
    if x <= cx:
        return y <= s or bowl_h - s <= y <= bowl_h
    d2 = (x - cx) ** 2 + (y - cy) ** 2
    return (r - s) ** 2 <= d2 <= r * r


def in_text(lay, x, y):
    if lay.text is None:
        return False
    top, h = lay.text
    if not (top <= y <= top + h):
        return False
    s = h * 0.2
    gap = h * 0.2
    letters = ((letter_h, h * 0.72), (letter_w, h * 1.15), (letter_p, h * 0.68))
    total = sum(wd for _, wd in letters) + gap * (len(letters) - 1)
    x0, _, x1, _ = lay.doc
    lx = (x0 + x1) / 2 - total / 2
    for fn, wd in letters:
        if lx <= x <= lx + wd:
            return fn(x - lx, y - top, wd, h, s)
        lx += wd + gap
    return False


def sample(lay, x, y):
    """한 점의 색(RGBA). 투명이면 None."""
    part = in_doc(lay, x, y)
    if part is None:
        return None
    if part == "fold":
        return FOLD
    if in_arrow(lay, x, y) or in_text(lay, x, y):
        return WHITE
    t = (y - lay.doc[1]) / (lay.doc[3] - lay.doc[1])
    return tuple(round(a + (b - a) * t) for a, b in zip(BODY_TOP, BODY_BOTTOM))


def render(size):
    lay = Layout(size)
    ss = 4 if size >= 128 else 8
    n = ss * ss
    rows = []
    for py in range(size):
        row = bytearray()
        for px in range(size):
            r = g = b = cov = 0
            for sy in range(ss):
                y = (py + (sy + 0.5) / ss) / size
                for sx in range(ss):
                    c = sample(lay, (px + (sx + 0.5) / ss) / size, y)
                    if c is not None:
                        r += c[0]
                        g += c[1]
                        b += c[2]
                        cov += 1
            if cov:
                row += bytes((r // cov, g // cov, b // cov, round(255 * cov / n)))
            else:
                row += b"\0\0\0\0"
        rows.append(bytes(row))
    return rows


def png_bytes(size, rows):
    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    raw = b"".join(b"\0" + row for row in rows)  # 필터 0(None)
    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)  # 8bit RGBA
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def ico_bytes(images):
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = len(header) + 16 * len(images)
    entries, blobs = b"", b""
    for size, png in images:
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(png), offset)
        blobs += png
        offset += len(png)
    return header + entries + blobs


def main():
    parser = argparse.ArgumentParser(description="app.ico 생성(F8)")
    parser.add_argument("--force", action="store_true", help="기존 app.ico 를 덮어쓴다")
    args = parser.parse_args()
    if OUT_PATH.exists() and not args.force:
        print(f"이미 있습니다: {OUT_PATH} (덮어쓰려면 --force)", file=sys.stderr)
        return 1
    images = []
    for size in SIZES:
        images.append((size, png_bytes(size, render(size))))
        print(f"rendered size={size}")
    OUT_PATH.write_bytes(ico_bytes(images))
    print(f"wrote path={OUT_PATH} sizes={','.join(map(str, SIZES))}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
