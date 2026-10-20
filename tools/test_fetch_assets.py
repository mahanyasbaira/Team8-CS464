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
