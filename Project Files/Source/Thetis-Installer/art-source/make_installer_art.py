from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageEnhance

SRC = r'C:\Users\Justin\projects\Kainos-dev\Kainos\Project Files\Source\Console\Resources\kainos-splash.png'
OUT = r'C:\Users\Justin\projects\Kainos-dev\Kainos\Project Files\Source\Thetis-Installer\binary'
PREV = r'C:\Users\Justin\AppData\Local\Temp\radeui'

splash = Image.open(SRC).convert('RGB')          # 720 x 307
NAVY = (5, 11, 19)

# pieces of the splash: the flame, the word, and the waves
flame = splash.crop((322, 52, 402, 118))           # the flame / sparkle above the word
word = splash.crop((212, 118, 508, 190))           # KAINOΣ
tag = splash.crop((236, 192, 486, 222))            # "Powered by Thetis SDR heritage"

def waves(w, h, box):
    """the splash's waves, scaled to cover w x h, dimmed so the logo stands out"""
    region = splash.crop(box)
    rw, rh = region.size
    s = max(w / rw, h / rh)
    region = region.resize((int(rw * s + 1), int(rh * s + 1)), Image.LANCZOS)
    x = (region.width - w) // 2
    y = (region.height - h) // 2
    region = region.crop((x, y, x + w, y + h))
    return ImageEnhance.Brightness(region).enhance(0.55)

def paste_light(base, piece, x, y, lo=70, hi=170):
    """put a light-on-dark piece on a dark background without its box: only its bright parts, faded in by
    brightness (below lo nothing, above hi all of it)"""
    lum = piece.convert('L').point(lambda v: 0 if v <= lo else 255 if v >= hi else int((v - lo) * 255 / (hi - lo)))
    base.paste(piece, (x, y), lum)

def scaled(img, width=None, height=None):
    w, h = img.size
    if width: return img.resize((width, max(1, round(h * width / w))), Image.LANCZOS)
    return img.resize((max(1, round(w * height / h)), height), Image.LANCZOS)

# ---- the welcome / finish picture (493 x 312): Kainos panel on the left, white for the installer's text ----
bg = Image.new('RGB', (493, 312), (255, 255, 255))
panel_w = 164
panel = waves(panel_w, 312, (0, 0, 200, 307))           # waves left of the lettering
# a darker band behind the logo
band = Image.new('RGB', (panel_w, 312), NAVY)
mask = Image.new('L', (panel_w + 80, 312), 0)            # wider than the panel so the blur doesn't fade at its edges
ImageDraw.Draw(mask).rectangle((0, 70, panel_w + 80, 215), fill=170)
mask = mask.filter(ImageFilter.GaussianBlur(18)).crop((40, 0, 40 + panel_w, 312))
panel = Image.composite(band, panel, mask)
f = scaled(flame, height=58)
paste_light(panel, f, (panel_w - f.width) // 2, 78, 50, 140)
wd = scaled(word, width=128)
paste_light(panel, wd, (panel_w - wd.width) // 2, 142)
tg = scaled(tag, width=140)
paste_light(panel, tg, (panel_w - tg.width) // 2, 142 + wd.height + 8)
bg.paste(panel, (0, 0))
# a thin gold edge between the panel and the page (Kainos gold)
ImageDraw.Draw(bg).line((panel_w, 0, panel_w, 312), fill=(212, 173, 106), width=2)
bg.save(OUT + r'\kainos_background.bmp')
bg.save(PREV + r'\kainos_background.png')

# ---- the banner (493 x 58): white for the page title, a Kainos tile at the right ----
bn = Image.new('RGB', (493, 58), (255, 255, 255))
tile_w = 150
tile = waves(tile_w, 58, (0, 215, 300, 307))             # waves only (no lettering)
tile = Image.composite(Image.new('RGB', tile.size, NAVY), tile, Image.new('L', tile.size, 120))
f = scaled(flame, height=40)
paste_light(tile, f, 20, 9, 50, 140)
wd = scaled(word, width=tile_w - 20 - f.width - 4 - 8)
paste_light(tile, wd, 20 + f.width + 4, (58 - wd.height) // 2)
# soften the tile's left edge into the white
fade = Image.new('L', (tile_w, 58), 255)
d = ImageDraw.Draw(fade)
for x in range(18):
    d.line((x, 0, x, 58), fill=int(255 * x / 18))
bn.paste(tile, (493 - tile_w, 0), fade)
bn.save(OUT + r'\kainos_banner.bmp')
bn.save(PREV + r'\kainos_banner.png')
print('ok')
