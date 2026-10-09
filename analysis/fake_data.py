"""Make fake study data in the exact CSV formats the app writes (docs/PLAN.md section 2).

    python analysis/fake_data.py --n 12 --out /tmp/fake

Writes <out>/raw/<P01_...>/{positions,events,trials}.csv and <out>/forms/ssq.csv.
The effects are baked in on purpose (trail fastest, teleport backtracks most,
joystick sickest) so we can see the analysis pick them up. NOT real data.
"""
import argparse
import csv
import math
import os
import random

from backtrack import cell_of, count_backtracks
from ssq import ITEMS
from study import TIME_LIMIT, order_for, latin_row

ROOM = 8.0          # each room is 8 x 8 m, rooms in a row along +x
HZ = 10.0
CELL = 1.0
WALK_SPEED = 1.5    # joystick speed, m/s

# per condition: (search waypoints per room, chance a waypoint is a spot we already checked)
SEARCH = {"joystick": (4, 0.20), "teleport": (5, 0.40), "trail": (3, 0.08)}
# lever spots inside a room (local coords), by layout
LEVER_SPOTS = {"A": [(6.5, 6.5), (1.5, 6.5), (6.5, 1.5)],
               "B": [(1.5, 1.5), (6.5, 6.0), (1.5, 6.5)],
               "C": [(6.0, 1.5), (1.5, 1.5), (6.5, 6.5)]}
SICKNESS = {"joystick": 1.0, "teleport": 0.35, "trail": 0.4}  # mean raw score per item


def room_of(x):
    return min(3, max(1, int(x // ROOM) + 1))


def waypoints_for_room(rng, room, condition, layout):
    """Points the fake participant goes to in one room: search spots, lever, gate."""
    x0 = (room - 1) * ROOM
    n, p_revisit = SEARCH[condition]
    n = max(1, n + rng.randint(-1, 2))
    pts = []
    for _ in range(n):
        if pts and rng.random() < p_revisit:
            pts.append(rng.choice(pts))           # forgot we already looked there
        else:
            pts.append((x0 + rng.uniform(1, 7), rng.uniform(1, 7)))
    lx, lz = LEVER_SPOTS[layout][room - 1]
    pts.append(("lever", x0 + lx, lz))
    pts.append((x0 + ROOM + 0.6, 4.0))            # through the gate / out the exit
    return pts


def simulate_trial(rng, condition, layout):
    """Returns samples [(t, x, z, yaw)], events [(t, name, detail)], lever times."""
    samples, events = [], []
    lever_times = [None, None, None]
    t = 0.0
    x, z, yaw = 0.8, 4.0, 90.0
    events.append((0.0, "trial_start", ""))
    samples.append((t, x, z, yaw))
    dt = 1.0 / HZ

    def sway():
        return rng.gauss(0, 0.03)

    for room in (1, 2, 3):
        for wp in waypoints_for_room(rng, room, condition, layout):
            is_lever = wp[0] == "lever"
            tx, tz = (wp[1], wp[2]) if is_lever else wp
            dist = math.hypot(tx - x, tz - z)
            if dist > 0.01:
                yaw = math.degrees(math.atan2(tx - x, tz - z)) % 360

            if condition == "joystick":
                steps = max(1, int(dist / (WALK_SPEED * dt)))
                for i in range(1, steps + 1):
                    t += dt
                    f = i / steps
                    samples.append((t, x + (tx - x) * f, z + (tz - z) * f, yaw))
            else:
                hops = max(1, math.ceil(dist / rng.uniform(2.2, 3.2)))
                for i in range(1, hops + 1):
                    aim = rng.uniform(1.0, 2.2)      # time spent aiming before the jump
                    hx = x + (tx - x) * (i - 1) / hops
                    hz = z + (tz - z) * (i - 1) / hops
                    end = t + aim
                    while t + dt < end:
                        t += dt
                        samples.append((t, hx + sway(), hz + sway(), yaw))
                    nx = x + (tx - x) * i / hops
                    nz = z + (tz - z) * i / hops
                    t += dt
                    events.append((t, "teleport", f"{hx:.3f};{hz:.3f};{nx:.3f};{nz:.3f}"))
                    samples.append((t, nx, nz, yaw))
            x, z = tx, tz

            # look around a bit at each stop
            for _ in range(rng.randint(5, 25)):
                t += dt
                samples.append((t, x + sway(), z + sway(), (yaw + rng.uniform(-60, 60)) % 360))

            if is_lever:
                lever_times[room - 1] = t
                events.append((t, "lever_flip", str(room)))
                events.append((t, "gate_open" if room < 3 else "exit_unlock", str(room) if room < 3 else ""))

            if t >= TIME_LIMIT:
                return cut(samples, events, lever_times)

    events.append((t, "escaped", ""))
    return samples, events, lever_times, t, False


def cut(samples, events, lever_times):
    samples = [s for s in samples if s[0] <= TIME_LIMIT]
    events = [e for e in events if e[0] <= TIME_LIMIT] + [(TIME_LIMIT, "timeout", "")]
    lever_times = [lt if lt is not None and lt <= TIME_LIMIT else None for lt in lever_times]
    return samples, events, lever_times, TIME_LIMIT, True


def fmt(v, d=3):
    return "" if v is None else f"{v:.{d}f}"


def write_session(rng, out, pid, start_ms):
    folder = os.path.join(out, "raw", f"{pid}_20261116-{100000 + int(pid[1:]) * 100:06d}")
    os.makedirs(folder, exist_ok=True)
    pos_f = open(os.path.join(folder, "positions.csv"), "w", newline="")
    ev_f = open(os.path.join(folder, "events.csv"), "w", newline="")
    tr_f = open(os.path.join(folder, "trials.csv"), "w", newline="")
    pos, ev, tr = csv.writer(pos_f), csv.writer(ev_f), csv.writer(tr_f)
    pos.writerow(["participant", "trial", "condition", "layout", "t", "unix_ms", "x", "z", "yaw",
                  "room", "cell_x", "cell_z", "cell_id"])
    ev.writerow(["participant", "trial", "condition", "layout", "t", "unix_ms", "event", "detail"])
    tr.writerow(["participant", "latin_row", "trial", "condition", "layout", "completion_s", "timed_out",
                 "aborted", "lever1_s", "lever2_s", "lever3_s", "backtracks", "cells_entered",
                 "unique_cells", "teleports", "path_m", "app_version"])

    for trial, condition, layout in order_for(pid):
        samples, events, levers, done_t, timed_out = simulate_trial(rng, condition, layout)
        base = start_ms + (trial - 1) * 15 * 60 * 1000
        for t, x, z, yaw in samples:
            cx, cz = cell_of(x, CELL), cell_of(z, CELL)
            pos.writerow([pid, trial, condition, layout, fmt(t), base + int(t * 1000), fmt(x), fmt(z),
                          fmt(yaw, 1), room_of(x), cx, cz, f"{cx}_{cz}"])
        for t, name, detail in events:
            ev.writerow([pid, trial, condition, layout, fmt(t), base + int(t * 1000), name, detail])

        bt = count_backtracks([s[1] for s in samples], [s[2] for s in samples], CELL, 0.15)
        path = sum(math.hypot(b[1] - a[1], b[2] - a[2]) for a, b in zip(samples, samples[1:]))
        teleports = sum(1 for e in events if e[1] == "teleport")
        tr.writerow([pid, latin_row(pid), trial, condition, layout, fmt(done_t), int(timed_out), 0,
                     fmt(levers[0]), fmt(levers[1]), fmt(levers[2]), bt["backtracks"],
                     bt["cells_entered"], bt["unique_cells"], teleports, fmt(path), "fake"])
    for f in (pos_f, ev_f, tr_f):
        f.close()


def ssq_answers(rng, level):
    return [min(3, max(0, int(round(rng.gauss(level, 0.6))))) for _ in ITEMS]


def write_forms(rng, out, pids):
    os.makedirs(os.path.join(out, "forms"), exist_ok=True)
    with open(os.path.join(out, "forms", "ssq.csv"), "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["timestamp", "participant", "timepoint"] + ITEMS +
                   ["rank_joystick", "rank_teleport", "rank_trail", "most_comfortable", "comment"])
        for pid in pids:
            sensitivity = rng.uniform(0.6, 1.4)
            w.writerow(["2026-11-16 10:00:00", pid, "baseline"] + ssq_answers(rng, 0.2) + ["", "", "", "", ""])
            for trial, condition, _ in order_for(pid):
                row = ["2026-11-16 10:%02d:00" % (trial * 15), pid, f"after_{trial}"]
                row += ssq_answers(rng, SICKNESS[condition] * sensitivity)
                if trial == 3:
                    ranks = ["trail", "teleport", "joystick"]
                    if rng.random() < 0.3:
                        ranks[0], ranks[1] = ranks[1], ranks[0]
                    row += [ranks.index("joystick") + 1, ranks.index("teleport") + 1,
                            ranks.index("trail") + 1, ranks[0], ""]
                else:
                    row += ["", "", "", "", ""]
                w.writerow(row)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--n", type=int, default=12, help="number of participants")
    ap.add_argument("--out", default="fake_data")
    ap.add_argument("--seed", type=int, default=464)
    args = ap.parse_args()

    rng = random.Random(args.seed)
    pids = [f"P{i:02d}" for i in range(1, args.n + 1)]
    for i, pid in enumerate(pids):
        write_session(rng, args.out, pid, 1794830400000 + i * 86400000)
    write_forms(rng, args.out, pids)
    print(f"wrote fake data for {len(pids)} participants to {args.out}")


if __name__ == "__main__":
    main()
