"""Download the CC0 asset kit from Poly Haven into Assets/_Project/Art/Kit/.

    python tools/fetch_assets.py

Runs in GitHub Actions (this needs internet). Safe to re-run: files that exist are skipped.
Writes Assets/_Project/Art/Kit/kit_index.json (read by RoomDresser.cs / BuildingBuilder.cs)
and docs/ASSETS.md. Everything on Poly Haven is CC0.

Textures: 2K for floors, 1K for everything else (Quest budget). Poly Haven's ARM maps
(R = ambient occlusion, G = roughness, B = metal) are converted into what URP Lit wants:
a metallic-smoothness map (R = metal, A = 1 - roughness) and an occlusion map.
"""
import argparse
import glob
import json
import os
import sys
import urllib.request

from PIL import Image

API = "https://api.polyhaven.com"
UA = {"User-Agent": "Team8-CS464-asset-fetch"}
KIT = "Assets/_Project/Art/Kit"
MAX_POLYS = 60000        # big furniture
MAX_PROP_POLYS = 10000   # small grabbable props
N_SMALL_PROPS = 8

# role -> (preferred ids, fallback keywords). First match that exists wins.
MODEL_ROLES = {
    "tall_metal": (["steel_frame_shelves_01", "steel_frame_shelves_02"], ["shelves", "shelf", "rack"]),
    "tall_wood": (["drawer_cabinet"], ["cabinet", "bookshelf", "bookcase", "wardrobe"]),
    "low_crate": (["wooden_crate_01", "wooden_crate_02"], ["crate", "box"]),
    "low_barrel": ([], ["barrel", "drum", "canister"]),
    "low_table": (["wooden_table_01"], ["desk", "table"]),
    "chair": (["school_chair_01"], ["chair", "stool"]),
    "door": ([], ["door"]),
}
SMALL_PROP_KEYWORDS = ["mug", "bottle", "book", "can_", "toolbox", "jar", "lantern", "vase",
                       "tin", "box", "cup", "kettle", "pot", "hammer", "wrench", "radio"]
TEXTURE_ROLES = {
    "floor_concrete": (["concrete_floor_worn_001"], ["concrete_floor"]),
    "floor_wood": ([], ["wood_floor", "floor_wood", "laminate", "parquet"]),
    "floor_metal": (["metal_plate"], ["metal_plate", "metal_floor"]),
    "wall_concrete": (["concrete_wall_003"], ["concrete_wall"]),
    "wall_plaster": (["painted_plaster_wall", "beige_wall_001"], ["plaster", "painted_wall"]),
    "ceiling": ([], ["ceiling", "plaster", "white_wall"]),
    "exterior": ([], ["brick_wall", "brick", "concrete_wall"]),
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
    with urllib.request.urlopen(req, timeout=180) as r, open(path + ".part", "wb") as f:
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


def pick_many(catalog, keywords, taken, n, max_polys):
    """Up to n different assets, cycling through the keywords so the set is varied."""
    chosen = []
    for _ in range(n):
        found = None
        for kw in keywords:
            found = pick(catalog, [], [kw], taken | set(chosen), max_polys)
            if found:
                break
        if not found:
            break
        chosen.append(found)
        # move the keyword that matched to the back so the next pick prefers another kind
        kw_used = next(k for k in keywords if k in found)
        keywords = [k for k in keywords if k != kw_used] + [kw_used]
    return chosen


def arm_to_urp(arm):
    """Poly Haven ARM (R=AO, G=roughness, B=metal) -> (metallic-smoothness RGBA, occlusion L)."""
    arm = arm.convert("RGB")
    ao, rough, metal = arm.split()
    smooth = rough.point(lambda v: 255 - v)
    black = Image.new("L", arm.size, 0)
    mask = Image.merge("RGBA", (metal, black, black, smooth))
    return mask, ao


def convert_arm(arm_path):
    """Writes <name>_mask.png and <name>_occlusion.png next to the ARM map. Returns their paths."""
    base = os.path.splitext(arm_path)[0]
    mask_path, occ_path = base + "_mask.png", base + "_occlusion.png"
    if not (os.path.exists(mask_path) and os.path.exists(occ_path)):
        mask, occ = arm_to_urp(Image.open(arm_path))
        mask.save(mask_path)
        occ.save(occ_path)
    return mask_path, occ_path


def maps_in(folder):
    """Find albedo / normal / ARM files in a Poly Haven download folder and convert ARM."""
    def find(*tags):
        for tag in tags:
            hits = [p for p in glob.glob(f"{folder}/**/*{tag}*", recursive=True)
                    if p.lower().endswith((".jpg", ".png")) and "_mask" not in p and "_occlusion" not in p]
            if hits:
                return sorted(hits)[0]
        return ""

    maps = {"albedo": find("_diff_", "_albedo_", "_diffuse"), "normal": find("_nor_gl_", "_normal_"),
            "mask": "", "occlusion": ""}
    arm = find("_arm_")
    if arm:
        maps["mask"], maps["occlusion"] = convert_arm(arm)
    return maps


def fetch_model(asset_id):
    files = get_json(f"{API}/files/{asset_id}")
    fbx = files["fbx"]["1k"]["fbx"]
    folder = f"{KIT}/Models/{asset_id}"
    download(fbx["url"], f"{folder}/{asset_id}.fbx")
    for rel, info in fbx.get("include", {}).items():
        download(info["url"], f"{folder}/{rel}")
    entry = {"path": f"{folder}/{asset_id}.fbx", "id": asset_id}
    entry.update(maps_in(folder))
    return entry


def fetch_texture(asset_id, res):
    files = get_json(f"{API}/files/{asset_id}")
    folder = f"{KIT}/Textures/{asset_id}"
    for key, name in (("Diffuse", "diff"), ("nor_gl", "nor_gl"), ("arm", "arm")):
        if key in files:
            sizes = files[key]
            r = res if res in sizes else "1k"
            if r in sizes and "jpg" in sizes[r]:
                download(sizes[r]["jpg"]["url"], f"{folder}/{asset_id}_{name}_{r}.jpg")
    entry = {"id": asset_id}
    entry.update(maps_in(folder))
    return entry


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
        entry = fetch_model(asset_id)
        entry["role"] = role
        index["models"].append(entry)
        credits.append((role, asset_id, models[asset_id].get("name", asset_id)))

    for n, asset_id in enumerate(pick_many(models, SMALL_PROP_KEYWORDS, taken, N_SMALL_PROPS, MAX_PROP_POLYS)):
        taken.add(asset_id)
        role = f"small_prop_{n + 1}"
        print(f"{role}: {asset_id}")
        entry = fetch_model(asset_id)
        entry["role"] = role
        index["models"].append(entry)
        credits.append((role, asset_id, models[asset_id].get("name", asset_id)))

    for role, (ids, kws) in TEXTURE_ROLES.items():
        asset_id = pick(textures, ids, kws, taken)
        if asset_id is None:
            print(f"warning: no texture for {role}")
            continue
        taken.add(asset_id)
        print(f"{role}: {asset_id}")
        entry = fetch_texture(asset_id, "2k" if role.startswith("floor") else "1k")
        entry["role"] = role
        index["textures"].append(entry)
        credits.append((role, asset_id, textures[asset_id].get("name", asset_id)))

    os.makedirs(KIT, exist_ok=True)
    with open(f"{KIT}/kit_index.json", "w") as f:
        json.dump(index, f, indent=2)
    write_credits(credits)
    print(f"done: {len(index['models'])} models, {len(index['textures'])} textures")
    return 0 if index["models"] and index["textures"] else 1


if __name__ == "__main__":
    sys.exit(main())
