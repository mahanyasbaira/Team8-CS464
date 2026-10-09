import subprocess
import sys
from pathlib import Path

import pandas as pd
import pytest

from backtrack import cell_of, count_backtracks
from ssq import ITEMS, normalize_columns, score
from study import condition_for_trial, latin_row, order_for

HERE = Path(__file__).parent


def run(path, size=1.0, hyst=0.15):
    xs, zs = zip(*path)
    return count_backtracks(xs, zs, size, hyst)


# --- same cases as Assets/Tests/EditMode/GridBacktrackCounterTests.cs ---

def test_staying_in_one_cell_never_counts():
    r = run([(0.5, 0.5), (0.6, 0.5), (0.4, 0.7), (0.5, 0.5)])
    assert r == {"backtracks": 0, "cells_entered": 1, "unique_cells": 1}


def test_walking_straight_no_backtracks():
    r = run([(0.5, 0.5), (1.5, 0.5), (2.5, 0.5), (3.5, 0.5)])
    assert r["backtracks"] == 0 and r["cells_entered"] == 4


def test_going_back_counts_each_reentry():
    r = run([(0.5, 0.5), (1.5, 0.5), (2.5, 0.5), (1.5, 0.5), (0.5, 0.5)])
    assert r == {"backtracks": 2, "cells_entered": 5, "unique_cells": 3}


def test_many_samples_in_reentered_cell_count_once():
    r = run([(0.5, 0.5), (1.5, 0.5), (0.5, 0.5), (0.4, 0.5), (0.3, 0.6), (0.5, 0.5)])
    assert r["backtracks"] == 1


def test_swaying_on_boundary_is_ignored():
    r = run([(0.95, 0.5), (1.05, 0.5), (0.92, 0.5), (1.08, 0.5), (0.97, 0.5)])
    assert r["backtracks"] == 0 and r["cells_entered"] == 1


def test_crossing_past_hysteresis_switches():
    r = run([(0.95, 0.5), (1.20, 0.5), (0.80, 0.5)])
    assert r["cells_entered"] == 3 and r["backtracks"] == 1


def test_teleport_jump_only_landing_cell_counts():
    r = run([(0.5, 0.5), (5.5, 0.5), (0.5, 0.5)])
    assert r == {"backtracks": 1, "cells_entered": 3, "unique_cells": 2}


def test_negative_coordinates_use_floor():
    assert cell_of(-0.2, 1.0) == -1
    assert cell_of(0.0, 1.0) == 0
    assert cell_of(1.0, 0.5) == 2


# --- Latin square (matches LatinSquareTests.cs) ---

def test_latin_rows_match_proposal():
    assert [c for _, c, _ in order_for("P01")] == ["joystick", "teleport", "trail"]
    assert [c for _, c, _ in order_for("P02")] == ["teleport", "trail", "joystick"]
    assert [c for _, c, _ in order_for("P03")] == ["trail", "joystick", "teleport"]
    assert latin_row("P04") == 1 and latin_row("P12") == 3
    assert [l for _, _, l in order_for("P07")] == ["A", "B", "C"]
    assert condition_for_trial("P05", 2) == "trail"


# --- SSQ ---

def test_ssq_scoring_by_hand():
    # nausea = 1 (in N and D), headache = 2 (O only), everything else 0
    row = {item: 0 for item in ITEMS}
    row["nausea"] = 1
    row["headache"] = 2
    s = score(pd.DataFrame([row])).iloc[0]
    assert s["ssq_nausea"] == pytest.approx(9.54)
    assert s["ssq_oculomotor"] == pytest.approx(2 * 7.58)
    assert s["ssq_disorientation"] == pytest.approx(13.92)
    assert s["ssq_total"] == pytest.approx((1 + 2 + 1) * 3.74)


def test_ssq_reads_text_answers():
    row = {item: "None" for item in ITEMS}
    row["fatigue"] = "Moderate"
    row["vertigo"] = "1 - Slight"
    s = score(pd.DataFrame([row])).iloc[0]
    assert s["fatigue"] == 2 and s["vertigo"] == 1
    assert s["ssq_total"] == pytest.approx(3 * 3.74)


def test_google_form_headers_get_normalized():
    df = pd.DataFrame(columns=["Timestamp", "Participant ID", "Timepoint", "General discomfort",
                               "Dizzy (eyes open)", "Dizzy (eyes closed)", "Difficulty focusing",
                               "Difficulty concentrating", "Rank: Joystick", "Most comfortable method"])
    cols = list(normalize_columns(df).columns)
    assert cols == ["timestamp", "participant", "timepoint", "general_discomfort", "dizzy_eyes_open",
                    "dizzy_eyes_closed", "difficulty_focusing", "difficulty_concentrating",
                    "rank_joystick", "most_comfortable"]


# --- whole pipeline on fake data ---

def test_pipeline_runs_on_fake_data(tmp_path):
    fake = tmp_path / "fake"
    out = tmp_path / "results"
    subprocess.run([sys.executable, str(HERE / "fake_data.py"), "--n", "6", "--out", str(fake)], check=True)
    subprocess.run([sys.executable, str(HERE / "analyze.py"), "--data", str(fake), "--out", str(out)],
                   check=True, capture_output=True)
    summary = pd.read_csv(out / "summary_by_condition.csv")
    assert set(summary["condition"]) == {"joystick", "teleport", "trail"}
    assert "ssq_total" in set(summary["measure"])
    trials = pd.read_csv(out / "trials_clean.csv")
    assert len(trials) == 18
    # in-app counts written by fake_data use the same rules, so they must match
    assert (trials["backtracks"] == trials["backtracks_1m"]).all()
    assert (out / "plots" / "time.png").exists()
