"""Top-down map of the whole building from Builds/layout.json (written by BatchBuild.BuildWorld).

    python tools/plot_layout.py Builds/layout.json Builds/layout.png
"""
import json
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib.patches import Rectangle  # noqa: E402

INK = "#2b2b2b"
MUTED = "#8a8a85"
SOLID = "#8a8a85"
PROP = "#e87ba4"
SPOT = {"LeverSpot_A": "#2a78d6", "LeverSpot_B": "#eb6834", "LeverSpot_C": "#1baf7a"}
LABELS = [(4, "Room 1"), (12, "Room 2"), (20, "Room 3")]


def load(src):
    data = json.load(open(src))
    # older layout.json files had a flat "boxes" list
    if "items" not in data:
        data["items"] = [dict(it, kind="lever_spot" if it["name"] in SPOT else "solid") for it in data.get("boxes", [])]
    return data


def main(src, dst):
    data = load(src)
    items = data["items"]
    fig, ax = plt.subplots(figsize=(13, 5.4), dpi=140)

    for it in items:
        if it["kind"] == "solid":
            ax.add_patch(Rectangle((it["x"] - it["w"] / 2, it["z"] - it["d"] / 2), it["w"], it["d"],
                                   facecolor=SOLID, edgecolor="white", linewidth=0.4))
    props = [it for it in items if it["kind"] == "prop"]
    if props:
        ax.scatter([p["x"] for p in props], [p["z"] for p in props], s=28, marker="s", color=PROP,
                   edgecolor="white", linewidth=1, zorder=3, label="grabbable decoy")
    for letter, color in SPOT.items():
        pts = [it for it in items if it["kind"] == "lever_spot" and it["name"] == letter]
        ax.scatter([p["x"] for p in pts], [p["z"] for p in pts], s=60, color=color,
                   edgecolor="white", linewidth=1.5, zorder=4, label="lever spot " + letter[-1])
    ax.scatter([0.8], [4], marker=">", s=80, color=INK, zorder=4, label="start")

    tris = data.get("triangles", {})
    for x, name in LABELS:
        room_tris = tris.get("Room_" + name[-1])
        text = name if room_tris is None else f"{name}\n{(room_tris + tris.get('Building', 0)) / 1000:.0f}k tris"
        ax.text(x, 8.5, text, ha="center", va="bottom", color=INK, fontsize=9)

    xs = [it["x"] for it in items] or [0, 24]
    zs = [it["z"] for it in items] or [0, 8]
    ax.set_xlim(min(xs) - 1, max(xs) + 1)
    ax.set_ylim(min(zs) - 1, max(max(zs) + 1, 10.5))
    ax.set_aspect("equal")
    ax.set_xlabel("x (m)")
    ax.set_ylabel("z (m)")
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.14), ncol=6, frameon=False, fontsize=8)
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    budget = data.get("max_triangles")
    title = "Team 8 building, top-down (grey = walls and obstacles)"
    if budget:
        title += f"; budget {budget / 1000:.0f}k tris per room incl. shell"
    ax.set_title(title, loc="left", color=INK, fontsize=10)
    fig.tight_layout()
    fig.savefig(dst)
    print("wrote", dst)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
