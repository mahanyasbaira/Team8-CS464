from fetch_assets import pick

CATALOG = {
    "wooden_crate_01": {"polycount": 2000},
    "wooden_crate_02": {"polycount": 3000},
    "steel_frame_shelves_01": {"polycount": 90000},
    "metal_shelf_02": {"polycount": 8000},
    "old_barrel": {},
}


def test_preferred_id_wins():
    assert pick(CATALOG, ["wooden_crate_02"], ["crate"], set()) == "wooden_crate_02"


def test_falls_back_to_keyword():
    assert pick(CATALOG, ["missing_id"], ["barrel"], set()) == "old_barrel"


def test_skips_taken_and_heavy_models():
    assert pick(CATALOG, ["wooden_crate_01"], ["crate"], {"wooden_crate_01"}) == "wooden_crate_02"
    assert pick(CATALOG, ["steel_frame_shelves_01"], ["shelf"], set(), max_polys=60000) == "metal_shelf_02"


def test_nothing_found():
    assert pick(CATALOG, [], ["sofa"], set()) is None


# --- added with the PBR pipeline ---
from PIL import Image  # noqa: E402

from fetch_assets import arm_to_urp, maps_in, pick_many  # noqa: E402


def test_arm_to_urp_channels():
    # one pixel: AO 200, roughness 60, metal 255
    arm = Image.new("RGB", (1, 1), (200, 60, 255))
    mask, occ = arm_to_urp(arm)
    assert mask.mode == "RGBA" and occ.mode == "L"
    assert mask.getpixel((0, 0)) == (255, 0, 0, 195)  # metal in R, smoothness = 255 - 60 in A
    assert occ.getpixel((0, 0)) == 200


def test_pick_many_varies_kinds_and_respects_budget():
    catalog = {"red_mug": {"polycount": 500}, "blue_mug": {"polycount": 600},
               "old_book": {"polycount": 300}, "huge_bottle": {"polycount": 50000}}
    got = pick_many(catalog, ["mug", "book", "bottle"], set(), 3, 10000)
    assert got[:2] == ["blue_mug", "old_book"]  # second pick prefers a different kind
    assert "huge_bottle" not in got
    assert len(got) == 3


def test_maps_in_finds_and_converts(tmp_path):
    folder = tmp_path / "crate"
    (folder / "textures").mkdir(parents=True)
    Image.new("RGB", (2, 2), (10, 20, 30)).save(folder / "textures" / "crate_diff_1k.jpg")
    Image.new("RGB", (2, 2), (128, 128, 255)).save(folder / "textures" / "crate_nor_gl_1k.jpg")
    Image.new("RGB", (2, 2), (255, 100, 0)).save(folder / "textures" / "crate_arm_1k.jpg")
    maps = maps_in(str(folder))
    assert maps["albedo"].endswith("crate_diff_1k.jpg")
    assert maps["normal"].endswith("crate_nor_gl_1k.jpg")
    assert maps["mask"].endswith("_mask.png") and maps["occlusion"].endswith("_occlusion.png")
    # running again doesn't pick the generated files as inputs
    assert maps_in(str(folder)) == maps
