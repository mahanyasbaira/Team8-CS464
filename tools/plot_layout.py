"""Top-down map of the three rooms from Builds/layout.json (written by BatchBuild.BuildWorld).

    python tools/plot_layout.py Builds/layout.json Builds/layout.png
"""
import json
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib.patches import Rectangle  # noqa: E402

INK = "#2b2b2b"
SOLID = "#8a8a85"
SPOT = {"LeverSpot_A": "#2a78d6", "LeverSpot_B": "#eb6834", "LeverSpot_C": "#1baf7a"}


def main(src, dst):
    items = json.load(open(src))["boxes"]
    fig, ax = plt.subplots(figsize=(12, 4.6), dpi=140)
    for it in items:
        if it["name"] in SPOT:
            continue
        ax.add_patch(Rectangle((it["x"] - it["w"] / 2, it["z"] - it["d"] / 2), it["w"], it["d"],
                               facecolor=SOLID, edgecolor="white", linewidth=0.5))
    for letter, color in SPOT.items():
        pts = [it for it in items if it["name"] == letter]
        ax.scatter([p["x"] for p in pts], [p["z"] for p in pts], s=60, color=color,
                   edgecolor="white", linewidth=1.5, zorder=3, label="lever spot " + letter[-1])
    for i, x in enumerate((4, 12, 20)):
        ax.text(x, 8.4, f"Room {i + 1}", ha="center", color=INK, fontsize=10)
    ax.scatter([0.8], [4], marker=">", s=80, color=INK, zorder=3, label="start")
    ax.set_xlim(-0.5, 26.5)
    ax.set_ylim(-0.5, 9)
    ax.set_aspect("equal")
    ax.set_xlabel("x (m)")
    ax.set_ylabel("z (m)")
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.15), ncol=4, frameon=False)
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    ax.set_title("Team 8 rooms, top-down (grey = walls and occluders)", loc="left", color=INK, fontsize=11)
    fig.tight_layout()
    fig.savefig(dst)
    print("wrote", dst)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
