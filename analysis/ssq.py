"""Simulator Sickness Questionnaire scoring (Kennedy, Lane, Berbaum & Lilienthal, 1993)."""
import re

import pandas as pd

ITEMS = [
    "general_discomfort", "fatigue", "headache", "eyestrain", "difficulty_focusing",
    "increased_salivation", "sweating", "nausea", "difficulty_concentrating",
    "fullness_of_head", "blurred_vision", "dizzy_eyes_open", "dizzy_eyes_closed",
    "vertigo", "stomach_awareness", "burping",
]

NAUSEA = ["general_discomfort", "increased_salivation", "sweating", "nausea",
          "difficulty_concentrating", "stomach_awareness", "burping"]
OCULOMOTOR = ["general_discomfort", "fatigue", "headache", "eyestrain",
              "difficulty_focusing", "difficulty_concentrating", "blurred_vision"]
DISORIENTATION = ["difficulty_focusing", "nausea", "fullness_of_head", "blurred_vision",
                  "dizzy_eyes_open", "dizzy_eyes_closed", "vertigo"]

ANSWER_VALUES = {"none": 0, "slight": 1, "moderate": 2, "severe": 3}

OTHER_FIELDS = ["timestamp", "participant", "timepoint", "rank_joystick", "rank_teleport",
                "rank_trail", "most_comfortable", "comment"]


def _key(text):
    return re.sub(r"[^a-z0-9]+", "_", str(text).lower()).strip("_")


def normalize_columns(df):
    """Map Google Form question titles to our short column names.

    A column matches if its cleaned-up title contains the key, so
    "Dizzy (eyes open)" -> dizzy_eyes_open and "Participant ID" -> participant.
    """
    rename = {}
    keys = ITEMS + OTHER_FIELDS
    # longest keys first so "dizzy_eyes_closed" doesn't get grabbed by something shorter
    for col in df.columns:
        k = _key(col)
        for target in sorted(keys, key=len, reverse=True):
            if target in k and target not in rename.values():
                rename[col] = target
                break
    return df.rename(columns=rename)


def _to_score(v):
    if pd.isna(v):
        return float("nan")
    s = str(v).strip().lower()
    if s in ANSWER_VALUES:
        return float(ANSWER_VALUES[s])
    # "1 - slight" or "2"
    m = re.match(r"^\s*([0-3])", s)
    if m:
        return float(m.group(1))
    for word, val in ANSWER_VALUES.items():
        if s.startswith(word):
            return float(val)
    raise ValueError(f"can't read SSQ answer {v!r}")


def score(df):
    """Adds raw item scores and the N / O / D / total columns."""
    out = df.copy()
    for item in ITEMS:
        out[item] = out[item].map(_to_score)
    n_raw = out[NAUSEA].sum(axis=1)
    o_raw = out[OCULOMOTOR].sum(axis=1)
    d_raw = out[DISORIENTATION].sum(axis=1)
    out["ssq_nausea"] = n_raw * 9.54
    out["ssq_oculomotor"] = o_raw * 7.58
    out["ssq_disorientation"] = d_raw * 13.92
    out["ssq_total"] = (n_raw + o_raw + d_raw) * 3.74
    return out
