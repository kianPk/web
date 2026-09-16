from PIL import Image
import os

src = r"C:\Users\kian\.cursor\projects\c-Users-kian-Desktop-web\assets\c__Users_kian_AppData_Roaming_Cursor_User_workspaceStorage_423b5a2be1093360f0fb142fce96da5e_images_photo_2026_09_06_19_41_38_Nero_AI_Background_Remover_transparent-6ddc58b2-843e-481a-b882-c1887884cfd1.png"
out_dir = r"C:\Users\kian\Desktop\web\public\favicon"
os.makedirs(out_dir, exist_ok=True)

img = Image.open(src).convert("RGBA")
pixels = img.load()
w, h = img.size

# Make near-black (background + Y cutout) fully transparent; keep red shield.
for y in range(h):
    for x in range(w):
        r, g, b, a = pixels[x, y]
        if r < 40 and g < 40 and b < 40:
            pixels[x, y] = (0, 0, 0, 0)
        elif r < 55 and g < 45 and b < 45 and a < 250:
            pixels[x, y] = (0, 0, 0, 0)

bbox = img.getbbox()
if not bbox:
    raise SystemExit("no opaque pixels found")
cropped = img.crop(bbox)
print("cropped size", cropped.size, "bbox", bbox)


def fit_square(src_img, size, pad_ratio=0.12, bg=None):
    canvas = Image.new(
        "RGBA", (size, size), bg if bg is not None else (0, 0, 0, 0)
    )
    max_logo = int(size * (1 - 2 * pad_ratio))
    sw, sh = src_img.size
    scale = min(max_logo / sw, max_logo / sh)
    nw, nh = max(1, int(sw * scale)), max(1, int(sh * scale))
    resized = src_img.resize((nw, nh), Image.Resampling.LANCZOS)
    ox = (size - nw) // 2
    oy = (size - nh) // 2
    canvas.paste(resized, (ox, oy), resized)
    return canvas


for size in (64, 192, 512):
    out = fit_square(cropped, size, pad_ratio=0.10)
    path = os.path.join(out_dir, f"{size}.png")
    out.save(path, "PNG", optimize=True)
    print("wrote", path)

# Maskable: same crest on opaque black (Android adaptive safe-zone padding)
maskable = fit_square(cropped, 512, pad_ratio=0.20, bg=(0, 0, 0, 255))
maskable_path = os.path.join(out_dir, "512-maskable.png")
maskable.save(maskable_path, "PNG", optimize=True)
print("wrote", maskable_path)

sample = Image.open(os.path.join(out_dir, "512.png"))
opaque = sum(1 for p in sample.getdata() if p[3] > 10)
print("512 opaque pixels", opaque, "of", 512 * 512)
