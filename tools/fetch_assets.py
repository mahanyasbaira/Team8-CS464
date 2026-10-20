"""Download the CC0 asset kit from Poly Haven into Assets/_Project/Art/Kit/.

    python tools/fetch_assets.py

Runs in GitHub Actions (this needs internet). Safe to re-run: files that exist are skipped.
Writes Assets/_Project/Art/Kit/kit_index.json (read by RoomDresser.cs) and docs/ASSETS.md.
Everything on Poly Haven is CC0.
"""
import argparse
import json
import os
import sys
import urllib.request

API = "https://api.polyhaven.com"
UA = {"User-Agent": "Team8-CS464-asset-fetch"}
KIT = "Assets/_Project/Art/Kit"
MAX_POLYS = 60000  # Quest budget: skip heavy models

# role -> (preferred ids, fallback keywords). First match that exists wins.
MODEL_ROLES = {
    "tall_metal": (["steel_frame_shelves_01", "steel_frame_shelves_02"], ["shelves", "shelf", "rack"]),
    "tall_wood": (["drawer_cabinet"], ["cabinet", "bookshelf", "bookcase", "wardrobe"]),
    "low_crate": (["wooden_crate_01", "wooden_crate_02"], ["crate", "box"]),
    "low_barrel": ([], ["barrel", "drum", "canister"]),
    "low_table": (["wooden_table_01"], ["desk", "table"]),
    "chair": (["school_chair_01"], ["chair", "stool"]),
}
TEXTURE_ROLES = {
    "floor_concrete": (["concrete_floor_worn_001"], ["concrete_floor"]),
    "floor_wood": ([], ["wood_floor", "floor_wood", "laminate", "parquet"]),
    "floor_metal": (["metal_plate"], ["metal_plate", "metal_floor"]),
    "wall_concrete": (["concrete_wall_003"], ["concrete_wall"]),
    "wall_plaster": (["painted_plaster_wall", "beige_wall_001"], ["plaster", "painted_wall"]),
}


def get_json(url):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def download(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=120) as r, open(path + ".part", "wb") as f:
        f.write(r.read())
    os.replace(path + ".part", path)
    print("  got", path)


def pick(catalog, ids, keywords, taken, max_polys=None):
    """Pick an asset id: preferred ids first, then the first id containing a keyword.
    Skips ids already used for another role and models over the polygon budget."""
    def ok(i):
        if i not in catalog or i in taken:
            return False
        polys = catalog[i].get("polycount")
        return max_polys is None or polys is None or polys <= max_polys

    for i in ids:
        if ok(i):
            return i
    for kw in keywords:
        for i in sorted(catalog):
            if kw in i and ok(i):
                return i
    return None


def fetch_model(asset_id):
    files = get_json(f"{API}/files/{asset_id}")
    fbx = files["fbx"]["1k"]["fbx"]
    folder = f"{KIT}/Models/{asset_id}"
    download(fbx["url"], f"{folder}/{asset_id}.fbx")
    for rel, info in fbx.get("include", {}).items():
        download(info["url"], f"{folder}/{rel}")
    return f"{folder}/{asset_id}.fbx"


def fetch_texture(asset_id):
    files = get_json(f"{API}/files/{asset_id}")
    folder = f"{KIT}/Textures/{asset_id}"
    out = {}
    for key, name in (("Diffuse", "albedo"), ("nor_gl", "normal")):
        if key in files and "1k" in files[key]:
            path = f"{folder}/{asset_id}_{name}_1k.jpg"
            download(files[key]["1k"]["jpg"]["url"], path)
            out[name] = path
    return out


def write_credits(rows, path="docs/ASSETS.md"):
    lines = [
        "# Asset credits",
        "",
        "All kit assets are CC0 from [Poly Haven](https://polyhaven.com) (no attribution required, listed anyway).",
        "Downloaded by `tools/fetch_assets.py`. Add any hand-picked asset here too.",
        "",
        "| Role | Asset | Link | License |",
        "|---|---|---|---|",
    ]
    for role, asset_id, name in rows:
        lines.append(f"| {role} | {name} | https://polyhaven.com/a/{asset_id} | CC0 |")
    with open(path, "w") as f:
        f.write("\n".join(lines) + "\n")


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.parse_args()

    models = get_json(f"{API}/assets?t=models")
    textures = get_json(f"{API}/assets?t=textures")
    index = {"models": [], "textures": []}
    credits = []
    taken = set()

    for role, (ids, kws) in MODEL_ROLES.items():
        asset_id = pick(models, ids, kws, taken, MAX_POLYS)
        if asset_id is None:
            print(f"warning: no model for {role}")
            continue
        taken.add(asset_id)
        print(f"{role}: {asset_id}")
        index["models"].append({"role": role, "path": fetch_model(asset_id)})
        credits.append((role, asset_id, models[asset_id].get("name", asset_id)))

    for role, (ids, kws) in TEXTURE_ROLES.items():
        asset_id = pick(textures, ids, kws, taken)
        if asset_id is None:
            print(f"warning: no texture for {role}")
            continue
        taken.add(asset_id)
        print(f"{role}: {asset_id}")
        maps = fetch_texture(asset_id)
        index["textures"].append({"role": role, "albedo": maps.get("albedo", ""), "normal": maps.get("normal", "")})
        credits.append((role, asset_id, textures[asset_id].get("name", asset_id)))

    os.makedirs(KIT, exist_ok=True)
    with open(f"{KIT}/kit_index.json", "w") as f:
        json.dump(index, f, indent=2)
    write_credits(credits)
    print(f"done: {len(index['models'])} models, {len(index['textures'])} textures")
    return 0 if index["models"] and index["textures"] else 1


if __name__ == "__main__":
    sys.exit(main())
