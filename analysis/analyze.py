"""Team 8 analysis: per-condition summaries, Friedman + Wilcoxon tests, plots.

    python analysis/analyze.py --data data --out results

Expects <data>/raw/<session folder>/{positions,trials}.csv and <data>/forms/ssq.csv
(see docs/PLAN.md sections 2 and 4).
"""
import argparse
import glob
import itertools
import os
import sys

import numpy as np
import pandas as pd
from scipy import stats

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

from backtrack import count_backtracks  # noqa: E402
from ssq import normalize_columns, score  # noqa: E402
from study import CONDITIONS, LABELS, condition_for_trial, latin_row  # noqa: E402

CELL_SIZES = [0.5, 1.0, 2.0]
HYSTERESIS = 0.15

# categorical colors, fixed per condition (checked for colorblind separation)
COLORS = {"joystick": "#2a78d6", "teleport": "#eb6834", "trail": "#1baf7a"}
INK = "#2b2b2b"
MUTED = "#8a8a85"
GRID = "#e6e6e3"


# ---------- loading ----------

def load_raw(data_dir):
    folders = sorted(glob.glob(os.path.join(data_dir, "raw", "*")))
    trials, positions = [], []
    for folder in folders:
        tf = os.path.join(folder, "trials.csv")
        pf = os.path.join(folder, "positions.csv")
        if not os.path.exists(tf):
            continue
        t = pd.read_csv(tf, dtype={"participant": str, "layout": str})
        t["session_folder"] = os.path.basename(folder)
        trials.append(t)
        if os.path.exists(pf):
            p = pd.read_csv(pf, dtype={"participant": str, "layout": str, "cell_id": str})
            p["session_folder"] = os.path.basename(folder)
            positions.append(p)
    if not trials:
        sys.exit(f"no trials.csv found under {data_dir}/raw/*/")
    trials = pd.concat(trials, ignore_index=True)
    positions = pd.concat(positions, ignore_index=True) if positions else pd.DataFrame()
    return trials, positions


def dedupe_trials(trials):
    """A resumed session makes a second folder. Keep the latest row per participant+trial."""
    trials = trials.sort_values("session_folder")
    dupes = trials.duplicated(["participant", "trial"], keep="last")
    if dupes.any():
        print(f"note: {dupes.sum()} repeated trial rows (resumed sessions), keeping the latest")
    return trials[~dupes].reset_index(drop=True)


def add_backtracks(trials, positions):
    """Recompute backtracking from the position log for each cell size."""
    if positions.empty:
        print("warning: no positions.csv, using the in-app backtrack numbers")
        trials["backtracks_1m"] = trials["backtracks"]
        return trials

    keep = trials[["participant", "trial", "session_folder"]]
    pos = positions.merge(keep, on=["participant", "trial", "session_folder"])
    rows = []
    for (pid, trial), g in pos.groupby(["participant", "trial"]):
        g = g.sort_values("t")
        row = {"participant": pid, "trial": trial}
        for size in CELL_SIZES:
            r = count_backtracks(g["x"].to_numpy(), g["z"].to_numpy(), size, HYSTERESIS)
            tag = f"{size:g}m"
            row[f"backtracks_{tag}"] = r["backtracks"]
            row[f"cells_entered_{tag}"] = r["cells_entered"]
        rows.append(row)
    bt = pd.DataFrame(rows)
    out = trials.merge(bt, on=["participant", "trial"], how="left")

    mismatch = out[out["backtracks_1m"] != out["backtracks"]]
    if len(mismatch):
        print(f"warning: {len(mismatch)} trials where in-app and recomputed backtracks differ")
    out["backtrack_rate"] = out["backtracks_1m"] / out["cells_entered_1m"]
    return out


def load_ssq(data_dir):
    path = os.path.join(data_dir, "forms", "ssq.csv")
    if not os.path.exists(path):
        print("note: no forms/ssq.csv yet, skipping SSQ and preference")
        return None
    df = normalize_columns(pd.read_csv(path, dtype=str))
    df["participant"] = df["participant"].str.strip().str.upper()
    df["timepoint"] = df["timepoint"].str.strip().str.lower()
    df = score(df)

    def cond(row):
        if row["timepoint"].startswith("after_"):
            return condition_for_trial(row["participant"], int(row["timepoint"][-1]))
        return "baseline"

    df["condition"] = df.apply(cond, axis=1)
    base = df[df["timepoint"] == "baseline"].set_index("participant")["ssq_total"]
    df["ssq_total_delta"] = df["ssq_total"] - df["participant"].map(base)
    return df


# ---------- stats ----------

def wide(df, value):
    """participant x condition table, complete rows only"""
    w = df.pivot_table(index="participant", columns="condition", values=value, aggfunc="first")
    w = w.reindex(columns=CONDITIONS)
    return w.dropna()


def friedman(w):
    if len(w) < 3:
        return None
    chi2, p = stats.friedmanchisquare(*[w[c] for c in CONDITIONS])
    n, k = w.shape
    kendall_w = chi2 / (n * (k - 1))
    return {"n": n, "chi2": chi2, "p": p, "W": kendall_w}


def wilcoxon(w, a, b, alternative="two-sided"):
    """Wilcoxon signed-rank a vs b, with effect size r = Z / sqrt(N)."""
    d = (w[a] - w[b]).to_numpy()
    if np.all(d == 0) or len(d) < 3:
        return {"pair": f"{a} vs {b}", "n": len(d), "stat": np.nan, "p": 1.0, "r": 0.0,
                "median_diff": float(np.median(d)) if len(d) else np.nan, "alt": alternative}
    res = stats.wilcoxon(w[a], w[b], alternative=alternative, zero_method="wilcox")
    # z from the normal approximation of W+ (zero differences dropped), r = z / sqrt(N)
    nz = d[d != 0]
    n = len(nz)
    ranks = stats.rankdata(np.abs(nz))
    w_plus = ranks[nz > 0].sum()
    z = (w_plus - n * (n + 1) / 4) / np.sqrt(n * (n + 1) * (2 * n + 1) / 24)
    return {"pair": f"{a} vs {b}", "n": len(d), "stat": res.statistic, "p": res.pvalue,
            "r": z / np.sqrt(n), "median_diff": float(np.median(d)), "alt": alternative}


def holm(results):
    """Holm-Bonferroni correction across a family of tests (adds p_holm)."""
    order = sorted(range(len(results)), key=lambda i: results[i]["p"])
    m = len(results)
    running = 0.0
    for rank, i in enumerate(order):
        adj = min(1.0, (m - rank) * results[i]["p"])
        running = max(running, adj)
        results[i]["p_holm"] = running
    return results


def bootstrap_median_ci(d, reps=5000, seed=0):
    rng = np.random.default_rng(seed)
    d = np.asarray(d, dtype=float)
    meds = np.median(rng.choice(d, size=(reps, len(d)), replace=True), axis=1)
    return float(np.percentile(meds, 2.5)), float(np.percentile(meds, 97.5))


def describe(df, value):
    g = df.groupby("condition")[value]
    out = pd.DataFrame({
        "n": g.count(), "mean": g.mean(), "sd": g.std(), "median": g.median(),
        "q1": g.quantile(0.25), "q3": g.quantile(0.75),
    }).reindex(CONDITIONS)
    out.insert(0, "measure", value)
    return out.reset_index()


# ---------- plots ----------

def style_axes(ax):
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    for side in ("left", "bottom"):
        ax.spines[side].set_color(MUTED)
    ax.tick_params(colors=INK, labelsize=9)
    ax.yaxis.grid(True, color=GRID, linewidth=0.8)
    ax.set_axisbelow(True)


def paired_plot(w, ylabel, title, path):
    """Box per condition + one thin grey line per participant."""
    fig, ax = plt.subplots(figsize=(5.2, 4.0), dpi=150)
    xs = np.arange(len(CONDITIONS))
    for _, row in w.iterrows():
        ax.plot(xs, row[CONDITIONS].to_numpy(), color=MUTED, alpha=0.45, linewidth=1, zorder=1)
    bp = ax.boxplot([w[c] for c in CONDITIONS], positions=xs, widths=0.45, patch_artist=True,
                    showfliers=False, medianprops={"color": INK, "linewidth": 2})
    for patch, c in zip(bp["boxes"], CONDITIONS):
        patch.set_facecolor(COLORS[c])
        patch.set_alpha(0.75)
        patch.set_edgecolor(COLORS[c])
    for c, x in zip(CONDITIONS, xs):
        ax.scatter(np.full(len(w), x), w[c], s=16, color=COLORS[c], edgecolor="white",
                   linewidth=1, zorder=3)
    ax.set_xticks(xs, [LABELS[c] for c in CONDITIONS])
    ax.set_ylabel(ylabel, color=INK)
    ax.set_title(f"{title} (n = {len(w)})", color=INK, fontsize=11, loc="left")
    style_axes(ax)
    fig.tight_layout()
    fig.savefig(path)
    plt.close(fig)


def preference_plot(ssq, path):
    final = ssq.dropna(subset=["most_comfortable"])
    final = final[final["most_comfortable"].str.strip() != ""]
    if final.empty:
        return
    counts = final["most_comfortable"].str.strip().str.lower().value_counts().reindex(CONDITIONS, fill_value=0)
    fig, ax = plt.subplots(figsize=(5.2, 3.2), dpi=150)
    ys = np.arange(len(CONDITIONS))
    ax.barh(ys, counts.to_numpy(), color=[COLORS[c] for c in CONDITIONS], height=0.55)
    for y, v in zip(ys, counts.to_numpy()):
        ax.text(v + 0.1, y, str(v), va="center", color=INK, fontsize=9)
    ax.set_yticks(ys, [LABELS[c] for c in CONDITIONS])
    ax.invert_yaxis()
    ax.set_xlabel("participants", color=INK)
    ax.set_title("Most comfortable method", color=INK, fontsize=11, loc="left")
    style_axes(ax)
    ax.yaxis.grid(False)
    ax.xaxis.grid(True, color=GRID, linewidth=0.8)
    fig.tight_layout()
    fig.savefig(path)
    plt.close(fig)


def path_plots(trials, positions, folder):
    """Top-down path of each participant's three trials (one figure per participant)."""
    os.makedirs(folder, exist_ok=True)
    keep = trials[["participant", "trial", "session_folder"]]
    pos = positions.merge(keep, on=["participant", "trial", "session_folder"])
    for pid, g in pos.groupby("participant"):
        fig, axes = plt.subplots(3, 1, figsize=(7, 7.5), dpi=130, sharex=True)
        for ax, (trial, tg) in zip(axes, g.groupby("trial")):
            cond = tg["condition"].iloc[0]
            ax.plot(tg["x"], tg["z"], color=COLORS[cond], linewidth=1.2)
            ax.scatter(tg["x"].iloc[0], tg["z"].iloc[0], s=30, color=INK, zorder=3)
            for gate_x in (8, 16, 24):
                ax.axvline(gate_x, color=MUTED, linewidth=0.8, linestyle="--")
            ax.set_aspect("equal")
            ax.set_title(f"trial {trial}: {LABELS[cond]}, layout {tg['layout'].iloc[0]}",
                         fontsize=9, color=INK, loc="left")
            style_axes(ax)
        axes[-1].set_xlabel("x (m)")
        fig.suptitle(f"{pid} paths (dot = start, dashed = gates/exit)", fontsize=10, color=INK)
        fig.tight_layout()
        fig.savefig(os.path.join(folder, f"{pid}.png"))
        plt.close(fig)


# ---------- main ----------

def main():
    ap = argparse.ArgumentParser(description="Team 8 analysis")
    ap.add_argument("--data", default="data")
    ap.add_argument("--out", default="results")
    args = ap.parse_args()
    os.makedirs(os.path.join(args.out, "plots"), exist_ok=True)

    trials, positions = load_raw(args.data)
    trials = dedupe_trials(trials)
    trials["latin_row_check"] = trials["participant"].map(latin_row)
    bad = trials[trials["latin_row"] != trials["latin_row_check"]]
    if len(bad):
        print(f"warning: {len(bad)} rows with a latin_row that doesn't match the participant ID")
    trials = add_backtracks(trials, positions)
    trials.to_csv(os.path.join(args.out, "trials_clean.csv"), index=False)

    # aborted trials don't count for time/backtracks; timeouts stay in at 300 s
    valid = trials[trials["aborted"] == 0]

    report = []
    say = report.append
    say("Team 8 locomotion study: results")
    say("=" * 40)
    say(f"participants: {trials['participant'].nunique()}   trials: {len(trials)}   "
        f"aborted: {int(trials['aborted'].sum())}   timeouts: {int(trials['timed_out'].sum())}")
    say("timeouts by condition: " + ", ".join(
        f"{c}={int(trials.loc[trials['condition'] == c, 'timed_out'].sum())}" for c in CONDITIONS))
    say("")

    summaries = [describe(valid, v) for v in
                 ["completion_s", "backtracks_1m", "backtracks_0.5m", "backtracks_2m",
                  "backtrack_rate", "teleports", "path_m"] if v in valid]

    def omnibus(df, value, label):
        w = wide(df, value)
        f = friedman(w)
        if f is None:
            say(f"{label}: not enough complete participants")
            return w
        say(f"{label}: Friedman chi2({len(CONDITIONS) - 1}) = {f['chi2']:.2f}, p = {f['p']:.4f}, "
            f"Kendall's W = {f['W']:.2f}, n = {f['n']}")
        return w

    def planned(w, pairs, family):
        res = holm([wilcoxon(w, a, b, alt) for a, b, alt in pairs])
        for r in res:
            say(f"   {family}: {r['pair']} ({r['alt']}): median diff = {r['median_diff']:.2f}, "
                f"W = {r['stat']}, p = {r['p']:.4f}, Holm p = {r['p_holm']:.4f}, r = {r['r']:.2f}")

    say("RQ1: efficiency")
    w_time = omnibus(valid, "completion_s", "completion time")
    planned(w_time, [("trail", "teleport", "less"), ("trail", "joystick", "less")], "H1a time")
    w_bt = omnibus(valid, "backtracks_1m", "backtracks (1 m cells)")
    planned(w_bt, [("trail", "teleport", "less")], "H1b backtracks")
    for size in ("0.5m", "2m"):
        if f"backtracks_{size}" in valid:
            omnibus(valid, f"backtracks_{size}", f"   sensitivity: backtracks ({size} cells)")
    omnibus(valid, "backtrack_rate", "   backtracks / cells entered (fairer for joystick)")
    say("")

    paired_plot(w_time, "seconds (capped at 300)", "Completion time", os.path.join(args.out, "plots", "time.png"))
    paired_plot(w_bt, "re-entries into visited 1 m cells", "Backtracking", os.path.join(args.out, "plots", "backtracks.png"))

    ssq = load_ssq(args.data)
    if ssq is not None:
        ssq.to_csv(os.path.join(args.out, "ssq_scored.csv"), index=False)
        post = ssq[ssq["condition"].isin(CONDITIONS)]
        summaries += [describe(post, "ssq_total"), describe(post, "ssq_total_delta")]
        base = ssq[ssq["timepoint"] == "baseline"]["ssq_total"]
        say("RQ2: motion sickness")
        say(f"baseline SSQ total: mean {base.mean():.1f}, median {base.median():.1f}")
        w_ssq = omnibus(post, "ssq_total", "SSQ total (post)")
        planned(w_ssq, [("joystick", "teleport", "greater"), ("joystick", "trail", "greater")], "H2a SSQ")
        w_delta = omnibus(post, "ssq_total_delta", "SSQ total change from baseline")
        if len(w_ssq) >= 3:
            d = w_ssq["trail"] - w_ssq["teleport"]
            lo, hi = bootstrap_median_ci(d)
            r = wilcoxon(w_ssq, "trail", "teleport")
            say(f"   H2b trail - teleport SSQ: median diff = {d.median():.2f}, "
                f"95% bootstrap CI [{lo:.2f}, {hi:.2f}], two-sided p = {r['p']:.4f}")
            say("   (a non-significant p is not proof of 'no sicker'; read the CI)")
        say("")
        paired_plot(w_ssq, "SSQ total score", "Sickness after each method", os.path.join(args.out, "plots", "ssq_total.png"))
        paired_plot(w_delta, "SSQ total minus baseline", "Sickness change from baseline", os.path.join(args.out, "plots", "ssq_delta.png"))

        rank_cols = [f"rank_{c}" for c in CONDITIONS]
        if all(c in ssq for c in rank_cols):
            ranks = ssq.dropna(subset=rank_cols).copy()
            ranks = ranks[ranks[rank_cols].apply(lambda r: all(str(v).strip() != "" for v in r), axis=1)]
            if len(ranks) >= 3:
                rw = ranks[rank_cols].astype(float)
                rw.columns = CONDITIONS
                f = friedman(rw)
                say("Preference ranks (1 = best): mean " + ", ".join(
                    f"{c} {rw[c].mean():.2f}" for c in CONDITIONS))
                say(f"   Friedman chi2(2) = {f['chi2']:.2f}, p = {f['p']:.4f}, n = {f['n']}")
            mc = ssq["most_comfortable"].dropna().str.strip().str.lower()
            mc = mc[mc != ""]
            say("Most comfortable: " + ", ".join(f"{c} {int((mc == c).sum())}" for c in CONDITIONS))
            preference_plot(ssq, os.path.join(args.out, "plots", "most_comfortable.png"))
        say("")

    say("Sanity checks")
    w_pos = valid.pivot_table(index="participant", columns="trial", values="completion_s").dropna()
    if len(w_pos) >= 3 and w_pos.shape[1] == 3:
        chi2, p = stats.friedmanchisquare(*[w_pos[c] for c in w_pos.columns])
        say(f"order effect (time by trial position 1/2/3): medians "
            f"{', '.join(f'{w_pos[c].median():.0f}' for c in w_pos.columns)} s, Friedman p = {p:.4f}")
    by_layout = valid.groupby("layout")["completion_s"].median()
    say("median time by layout: " + ", ".join(f"{k} {v:.0f} s" for k, v in by_layout.items()))

    pd.concat(summaries, ignore_index=True).to_csv(os.path.join(args.out, "summary_by_condition.csv"), index=False)
    if not positions.empty:
        path_plots(trials, positions, os.path.join(args.out, "plots", "paths"))

    text = "\n".join(report)
    with open(os.path.join(args.out, "stats.txt"), "w") as f:
        f.write(text + "\n")
    print(text)
    print(f"\nwrote {args.out}/summary_by_condition.csv, stats.txt, trials_clean.csv, plots/")


if __name__ == "__main__":
    main()
