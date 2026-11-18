"""Builds app_icon.ico with several sizes (PNG-compressed entries, supported since Windows Vista)."""
import os
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
SIZES = [16, 32, 48, 128]

images = []
for size in SIZES:
    with open(os.path.join(HERE, "icons", "icon%d.png" % size), "rb") as f:
        data = f.read()
    w, h = struct.unpack(">II", data[16:24])
    if (w, h) != (size, size):
        raise SystemExit("icons/icon%d.png is %dx%d" % (size, w, h))
    images.append((size, data))

header = struct.pack("<HHH", 0, 1, len(images))
offset = 6 + 16 * len(images)
entries = b""
for size, data in images:
    dim = 0 if size >= 256 else size
    entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
    offset += len(data)

with open(os.path.join(HERE, "app_icon.ico"), "wb") as f:
    f.write(header + entries + b"".join(d for _, d in images))
print("app_icon.ico written with sizes", SIZES)
