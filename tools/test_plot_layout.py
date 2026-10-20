import json

from plot_layout import load, main


def sample():
    items = [
        {"kind": "solid", "group": "Room_1", "name": "Wall_S", "x": 4, "z": 0, "w": 8.2, "d": 0.2},
        {"kind": "solid", "group": "Building", "name": "Wall_South", "x": 12, "z": -3, "w": 30.2, "d": 0.2},
        {"kind": "lever_spot", "group": "Room_1", "name": "LeverSpot_A", "x": 6.5, "z": 6.5, "w": 0, "d": 0},
        {"kind": "prop", "group": "Room_1", "name": "mug", "x": 4, "z": 4, "w": 0, "d": 0},
    ]
    return {"max_triangles": 750000, "triangles": {"Building": 20000, "Room_1": 180000}, "items": items}


def test_plot_writes_png(tmp_path):
    src, dst = tmp_path / "layout.json", tmp_path / "layout.png"
    src.write_text(json.dumps(sample()))
    main(str(src), str(dst))
    assert dst.stat().st_size > 1000


def test_old_format_still_loads(tmp_path):
    src = tmp_path / "old.json"
    src.write_text(json.dumps({"boxes": [{"room": "Room_1", "name": "LeverSpot_B", "x": 1, "z": 1, "w": 0, "d": 0},
                                         {"room": "Room_1", "name": "Prop_1", "x": 3, "z": 2, "w": 1, "d": 1}]}))
    kinds = [it["kind"] for it in load(str(src))["items"]]
    assert kinds == ["lever_spot", "solid"]
