"""Import licensed source art and assemble/cut Unity sprites (no generated artwork).

Run after downloading the four original ZIPs into Logs/ThuongAssetDownloads.
All rectangles use the source PNG's top-left coordinates. Source pixels remain
available beside the derived sprites, with the original notices and credits.
"""
from pathlib import Path
import colorsys
import json
import shutil
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
DOWNLOADS = ROOT / "Logs/ThuongAssetDownloads"
ART = ROOT / "Assets/Scenes/Thuong/Prototype/Art/Medieval"
SOURCE = ART / "Source"
SPRITES = ART / "Sprites"
RESOURCES = ART / "Resources"


def copy_tree(source, target):
    if not source.exists() and target.exists():
        return
    target.mkdir(parents=True, exist_ok=True)
    for path in source.rglob("*"):
        if path.is_file() and not path.name.startswith("._"):
            destination = target / path.relative_to(source)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)


def write_image(image, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def copy_notice(source, target):
    if source.exists():
        shutil.copy2(source, target)
    elif not target.exists():
        raise FileNotFoundError(source)


def crop(source, name, box, folder=SPRITES / "World"):
    image = Image.open(source).convert("RGBA")
    x, y, w, h = box
    assert x >= 0 and y >= 0 and x+w <= image.width and y+h <= image.height, (name, box, image.size)
    result = image.crop((x, y, x+w, y+h))
    assert result.getbbox(), f"Empty sprite: {name}"
    write_image(result, folder / (name + ".png"))
    return result


def recolor(image, target):
    """Palette swap an existing clothing layer, retaining shading and alpha."""
    hue, target_lightness, saturation = colorsys.rgb_to_hls(*(v/255 for v in target))
    pixels = []
    for r, g, b, a in image.get_flattened_data():
        if not a:
            pixels.append((r, g, b, a))
            continue
        _, lightness, original_saturation = colorsys.rgb_to_hls(r/255, g/255, b/255)
        lightness = max(.02, min(.95, lightness * target_lightness / .65))
        rgb = colorsys.hls_to_rgb(hue, lightness, saturation)
        pixels.append(tuple(round(v*255) for v in rgb) + (a,))
    result = Image.new("RGBA", image.size)
    result.putdata(pixels)
    return result


def assemble_actor(animation, index, tunic, hair, outfit):
    source = SOURCE / "LPCCharacters" / animation
    body = Image.open(source / "BODY_male.png").convert("RGBA")
    layers = [body, Image.open(source / "FEET_shoes_brown.png").convert("RGBA")]
    robe = outfit == "robe"
    legs = "LEGS_robe_skirt.png" if robe else "LEGS_pants_greenish.png"
    torso = "TORSO_robe_shirt_brown.png" if robe else "TORSO_leather_armor_shirt_white.png"
    layers += [recolor(Image.open(source / legs).convert("RGBA"), tunic if robe else (72, 74, 50)),
               recolor(Image.open(source / torso).convert("RGBA"), tunic),
               Image.open(source / "BELT_leather.png").convert("RGBA")]
    if outfit == "guard":
        layers += [Image.open(source / "TORSO_leather_armor_torso.png").convert("RGBA"),
                   Image.open(source / "TORSO_leather_armor_shoulders.png").convert("RGBA")]
    layers.append(recolor(Image.open(source / "HEAD_hair_blonde.png").convert("RGBA"), hair))
    if robe:
        layers.append(recolor(Image.open(source / "HEAD_robe_hood.png").convert("RGBA"), tunic))
    elif index % 4 == 1:
        layers.append(Image.open(source / "HEAD_leather_armor_hat.png").convert("RGBA"))
    result = Image.new("RGBA", body.size)
    for layer in layers:
        assert layer.size == body.size
        result.alpha_composite(layer)
    write_image(result, SPRITES / "Characters" / f"Villager{index+1:02}_{animation}.png")
    return result


def main():
    copy_tree(DOWNLOADS / "Hypnobius/MedievalVillageExteriorv1.0/RawAssets", SOURCE / "Hypnobius")
    for name in ("LICENSE.txt", "README.txt"):
        copy_notice(DOWNLOADS / "Hypnobius/MedievalVillageExteriorv1.0" / name, SOURCE / "Hypnobius" / name)
    char_root = DOWNLOADS / "LPCCharacters/lpc_entry"
    for animation in ("walkcycle", "hurt"):
        copy_tree(char_root / "png" / animation, SOURCE / "LPCCharacters" / animation)
    copy_notice(char_root / "README", SOURCE / "LPCCharacters/README.txt")
    copy_tree(DOWNLOADS / "LPCDecorations/decoration_medieval", SOURCE / "LPCDecorations")
    farm_root = DOWNLOADS / "LPCFarming/submission_daneeklu"
    copy_tree(farm_root / "tilesets", SOURCE / "LPCFarming/tilesets")
    copy_tree(farm_root / "ui", SOURCE / "LPCFarming/ui")
    copy_notice(farm_root / "readme.txt", SOURCE / "LPCFarming/README.txt")

    hyp = SOURCE / "Hypnobius"
    props = hyp / "Props_Decor.png"
    deco = SOURCE / "LPCDecorations/decorations-medieval.png"
    farm = SOURCE / "LPCFarming/tilesets/farming_fishing.png"
    crop(hyp / "Ground.png", "Grass", (48, 48, 48, 48))
    crop(hyp / "Ground.png", "Cobblestone", (192, 48, 48, 48))
    crop(props, "Barrel", (0, 0, 48, 48))
    crop(props, "Crate", (48, 0, 48, 48))
    crop(props, "CrateStack", (48, 96, 96, 96))
    crop(props, "Cart", (48, 48, 144, 48))
    crop(props, "Tree", (144, 96, 48, 96))
    crop(props, "Palisade", (48, 192, 48, 96))
    crop(props, "Planter", (96, 192, 96, 96))
    crop(deco, "Well", (0, 432, 64, 48))
    crop(farm, "Woodpile", (96, 0, 64, 64))
    crop(farm, "Sacks", (96, 64, 64, 64))
    crop(deco, "Hay", (0, 640, 64, 64))
    crop(deco, "Signpost", (192, 224, 64, 32))
    crop(deco, "MarketStall", (0, 800, 96, 160))
    crop(SOURCE / "LPCFarming/tilesets/plowed_soil.png", "Soil", (32, 160, 32, 32))
    wheat = crop(SOURCE / "LPCFarming/tilesets/wheat.png", "Wheat", (32, 160, 32, 32))
    write_image(recolor(wheat, (198, 155, 47)), SPRITES / "World/Wheat.png")
    crop(SOURCE / "LPCFarming/tilesets/fence.png", "Fence", (0, 0, 96, 32))
    ui = SOURCE / "LPCFarming/ui/scrollsandblocks.png"
    crop(ui, "PaperPanel", (352, 128, 192, 64), RESOURCES / "MedievalUI")
    crop(ui, "WoodPanel", (0, 128, 96, 96), RESOURCES / "MedievalUI")
    crop(ui, "QuestMarker", (0, 0, 32, 32), SPRITES / "World")
    for i in range(6):
        crop(hyp / "Sconce_Spritesheet.png", f"Torch{i}", ((i % 3)*48, (i // 3)*48, 48, 48))

    walls = Image.open(hyp / "Walls.png").convert("RGBA")
    prop_image = Image.open(props).convert("RGBA")
    for index, (wall_x, roof_y) in enumerate(((288, 0), (192, 240), (0, 0))):
        building = Image.new("RGBA", (240, 336))
        wall_tile = walls.crop((wall_x, 0, wall_x+96, 96))
        for y in range(208, 336, 96):
            for x in range(0, 240, 96):
                building.alpha_composite(wall_tile, (x, y))
        roof = prop_image.crop((192, roof_y, 432, roof_y+240))
        building.alpha_composite(roof, (0, 0))
        for x in (24, 168):
            building.alpha_composite(prop_image.crop((48, 288, 96, 384)), (x, 240))
        building.alpha_composite(prop_image.crop((48, 384, 96, 480)), (96, 240))
        write_image(building, SPRITES / "World" / f"House{index+1}.png")

    palettes = [(111, 76, 44), (76, 99, 60), (131, 55, 47), (63, 83, 108),
                (143, 111, 59), (100, 70, 107), (66, 113, 105), (120, 91, 69),
                (68, 79, 51), (103, 62, 47)]
    hair_colors = [(81, 48, 29), (174, 139, 65), (47, 42, 39), (117, 119, 116)]
    actors = []
    for index in range(20):
        outfit = "robe" if index in (5, 9, 14, 18) else "guard" if index in (3, 13) else "tunic"
        sheet = assemble_actor("walkcycle", index, palettes[index % 10], hair_colors[index % 4], outfit)
        assemble_actor("hurt", index, palettes[index % 10], hair_colors[index % 4], outfit)
        actors.append(sheet)

    role_names = ["DogSpirit", "WhiteHound", "WhiteWolf", "RedNosedHound", "WolfCub", "WolfBoss",
                  "SerpentSpirit", "Ogre", "Villager", "Mayor", "Seer", "VillageGuardian", "Hunter",
                  "Shaman", "WeaverOfFate", "Idiot", "Cursed", "Brat", "TuongMaster", "Magistrate",
                  "Madman", "Jester", "Lover", "FoxSpirit", "Piper", "Killer", "SerialKiller", "DeathHerald"]
    role_skins = [9, 9, 18, 14, 1, 13, 5, 3, 0, 4, 5, 3, 8, 14, 18, 7, 9, 1, 6, 13, 15, 12, 2, 17, 6, 10, 19, 18]
    for name, skin in zip(role_names, role_skins):
        portrait = actors[skin].crop((0, 128, 64, 192))
        write_image(portrait, RESOURCES / "RolePortraits" / (name + ".png"))
    write_image(actors[0].crop((0, 128, 64, 192)), RESOURCES / "MedievalUI/RoleIcon.png")

    manifest = {"pixelsPerUnit": 48, "characterCell": 64, "walkColumns": 9,
                "directionRows": ["Up", "Left", "Down", "Right"], "skinCount": 20,
                "sourcePages": {"Hypnobius": "https://hypnobius.itch.io/medieval-village-exterior-tileset",
                    "LPCCharacters": "https://opengameart.org/content/lpc-medieval-fantasy-character-sprites",
                    "LPCDecorations": "https://opengameart.org/content/lpc-medieval-village-decorations",
                    "LPCFarming": "https://opengameart.org/content/lpc-farming-tilesets-magic-animations-and-ui-elements"}}
    (ART / "ImportManifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    credits = """MEDIEVAL ART CREDITS — THƯƠNG PROTOTYPE

Medieval Village Exterior v1.0 — Hypnobius
https://hypnobius.itch.io/medieval-village-exterior-tileset
Used under the author's asset license. Original LICENSE.txt and README.txt
are preserved in Art/Medieval/Source/Hypnobius. No standalone asset resale.
Derived sprites: cropped props, tiled house walls and composed houses.

LPC Medieval Fantasy Character Sprites — Johannes Sjölund (wulax)
https://opengameart.org/content/lpc-medieval-fantasy-character-sprites
LPC base characters: Stephen Challener (Redshrike), Lanea Zimmerman (Sharm).
License used: Creative Commons Attribution-ShareAlike 3.0.
https://creativecommons.org/licenses/by-sa/3.0/
Derived sprites: clothing layers composited and palette swapped into 20 skins;
walk/death frames and role portraits cropped from these sheets.
See Source/LPCCharacters/README.txt for animation/source information.

LPC Farming tilesets, magic animations and UI elements — Daniel Eddeland (daneeklu)
https://opengameart.org/content/lpc-farming-tilesets-magic-animations-and-ui-elements
License used: Creative Commons Attribution-ShareAlike 3.0.
https://creativecommons.org/licenses/by-sa/3.0/
Derived sprites: crops, soil, fence, sacks, woodpile, parchment and wood UI.
Original README preserved in Source/LPCFarming.

LPC Medieval Village Decorations — see the full author notice below.
https://opengameart.org/content/lpc-medieval-village-decorations
License used: Creative Commons Attribution-ShareAlike 3.0+.
Derived sprites: well, hay, signpost and market stall crops.
LPC-derived sprites remain under CC BY-SA 3.0; this notice does not change
the project's code license or Hypnobius's original art license.

FULL ORIGINAL DECORATION CREDITS
"""
    credits += (SOURCE / "LPCDecorations/CREDITS-decorations-medieval.txt").read_text(encoding="utf-8")
    (ART / "ATTRIBUTION.txt").write_text(credits, encoding="utf-8")
    (RESOURCES / "MedievalCredits.txt").write_text(credits, encoding="utf-8")

    # Technical QA contact sheet: lets us inspect the imported crops before wiring.
    files = sorted((SPRITES / "World").glob("*.png"))
    board = Image.new("RGB", (900, ((len(files)+5)//6)*150), (45, 49, 42))
    draw = ImageDraw.Draw(board)
    for index, file in enumerate(files):
        tile = Image.open(file).convert("RGBA")
        tile.thumbnail((140, 118), Image.Resampling.NEAREST)
        x, y = (index % 6)*150, (index // 6)*150
        board.paste(tile, (x+(150-tile.width)//2, y+5), tile)
        draw.text((x+5, y+128), file.stem, fill="white")
    write_image(board, ROOT / "Logs/thuong-medieval-import-contact.png")
    print(f"Prepared {len(files)} world sprites, 20 walking/death sheets and {len(role_names)} role portraits.")


if __name__ == "__main__":
    main()
