"""Extract existing LPC pixels; do not download or replace source artwork."""
from pathlib import Path
import json
import shutil
from PIL import Image, ImageDraw
import hashlib

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "Assets/Scenes/Thuong/Prototype/Art/Medieval"
OUT = ROOT / "Assets/Scenes/Thuong/Maps/Art"
OUT.mkdir(parents=True, exist_ok=True)
sheet = Image.open(BASE / "Source/LPCDecorations/decorations-medieval.png").convert("RGBA")
sheet.putdata([(0, 0, 0, 0) if p == (129, 161, 0, 255) else p for p in sheet.get_flattened_data()])
rects = {
    "Grave": (0, 0, 32, 64), "GraveRound": (64, 64, 32, 32),
    "Memorial": (172, 312, 80, 88), "Lantern": (416, 64, 32, 32),
    "MarketAwning": (192, 800, 64, 160), "MarketAwningGreen": (320, 800, 64, 160), "Curtain": (0, 1216, 192, 96),
    "Tent": (0, 1600, 224, 224), "Campfire": (256, 1504, 32, 64),
    "StageBoards": (0, 1088, 128, 96), "Banner": (96, 1216, 32, 96),
    "FloorPlank": (32, 1120, 32, 32),
}
for name, (x, y, w, h) in rects.items():
    cut = sheet.crop((x, y, x + w, y + h))
    assert cut.getbbox(), name
    cut.save(OUT / (name + ".png"))
Image.open(BASE / "Source/LPCFarming/tilesets/sandwater.png").crop((0, 64, 32, 96)).save(OUT / "Water.png")
Image.open(BASE / "Source/Hypnobius/Walls.png").crop((288, 0, 336, 96)).save(OUT / "ManorWall.png")

# MAP-07: use the two official free v1.0 packs when rebuilding the delivered art.
downloads = ROOT / "Logs/Maps/Downloads"
swamp = downloads / "dark-swamp-pack/Dark_Swamp_Starter_Pack_v1.0"
cabin = downloads / "rural-cabin-exterior/Cabin_Exterior_v1.0"
assert swamp.exists() and cabin.exists(), "Download the official Dark Swamp and Rural Cabin v1.0 packs first"
swamp_ground = Image.open(swamp / "RawAssets/GroundTileset.png").convert("RGBA")
swamp_ground.crop((48, 48, 96, 96)).save(OUT / "SwampGround.png")
water = Image.open(swamp / "RawAssets/WaterAnimation.png").convert("RGBA")
for row, source_y in enumerate((48, 72, 96)):
    for column, source_x in enumerate((0, 24, 48)):
        for frame in range(3):
            x = source_x + frame * 96
            water.crop((x, source_y, x + 48, source_y + 48)).save(OUT / f"SwampWater{row}{column}_{frame}.png")
props = Image.open(swamp / "RawAssets/PropsTileset.png").convert("RGBA")
props.crop((108, 0, 172, 96)).save(OUT / "SwampBridge.png")
props.crop((284, 0, 380, 48)).save(OUT / "SwampBridgeHorizontal.png")
props.crop((384, 0, 432, 48)).save(OUT / "SwampReed.png")
for frame in range(4):
    shutil.copy2(swamp / f"RawAssets/Altar{frame}.png", OUT / f"SwampAltar{frame}.png")
house_source = Image.open(cabin / "Cabin_Exterior_v1.0.png").convert("RGBA")
house_source.crop((336, 48, 384, 96)).save(OUT / "RuralPath.png")
house = Image.new("RGBA", (240, 240))
wall = house_source.crop((0, 96, 96, 192))
for x in range(0, 240, 96):
    house.alpha_composite(wall, (x, 144))
roof = house_source.crop((0, 0, 96, 96))
for y in range(0, 144, 24):
    for x in range(0, 240, 24):
        source_x = 0 if x == 0 else 72 if x == 216 else 24 + (x % 48)
        source_y = 0 if y == 0 else 72 if y == 120 else 24 + (y % 48)
        house.alpha_composite(roof.crop((source_x, source_y, source_x + 24, source_y + 24)), (x, y))
house.alpha_composite(house_source.crop((96, 288, 144, 384)), (96, 144))
for x in (24, 168):
    house.alpha_composite(house_source.crop((48, 288, 96, 384)), (x, 144))
house.save(OUT / "RuralCabin.png")
for label, source in (("DarkSwamp", swamp), ("RuralCabin", cabin)):
    for notice in ("LICENSE.txt", "README.txt"):
        shutil.copy2(source / notice, OUT / f"{label}-{notice}")
    archive = downloads / ("dark-swamp-pack.zip" if label == "DarkSwamp" else "rural-cabin-exterior.zip")
    print(label, "v1.0 archive SHA256", hashlib.sha256(archive.read_bytes()).hexdigest())
credit = BASE / "Source/LPCDecorations/CREDITS-decorations-medieval.txt"
shutil.copy2(credit, OUT / credit.name)
manifest = {"source": "Existing LPC Decorations and LPC Farming source sheets", "rectangles_top_left": rects,
            "ppu": 48, "water_ppu": 32, "filter": "Point", "tuong": "TuongTheatreSign.png generated with the built-in imagegen tool; original source retained outside project"}
manifest["additional_packs"] = {"DarkSwamp": "https://hypnobius.itch.io/dark-swamp-pack", "RuralCabin": "https://hypnobius.itch.io/rural-cabin-exterior", "versions": "v1.0", "raw_archives": "Logs/Maps/Downloads", "water": "3 frames x 9 autotile variants, 48 PPU, centered pivot", "cabin": "240x240 composition of original wall/roof/window/door pixels"}
(OUT / "ImportManifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
preview = Image.new("RGBA", (960, ((len(rects) + 4) // 5) * 240), (42, 47, 40, 255))
draw = ImageDraw.Draw(preview)
for i, name in enumerate(rects):
    sprite = Image.open(OUT / (name + ".png")); sprite.thumbnail((150, 170), Image.Resampling.NEAREST)
    x, y = (i % 5) * 192, (i // 5) * 240
    preview.alpha_composite(sprite, (x + (192 - sprite.width) // 2, y + 30))
    draw.text((x + 10, y + 215), name, fill="white")
logs = ROOT / "Logs/Maps"; logs.mkdir(parents=True, exist_ok=True)
preview.save(logs / "art-contact-sheet.png")
print("Extracted", len(rects) + 2, "sprites with source credits")
