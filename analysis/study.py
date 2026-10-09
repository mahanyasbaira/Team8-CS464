"""Shared study constants: conditions, Latin square, file names (see docs/PLAN.md)."""

CONDITIONS = ["joystick", "teleport", "trail"]
LABELS = {"joystick": "Joystick", "teleport": "Teleport", "trail": "Teleport + Trail"}

# same rows as Assets/_Project/Scripts/Core/LatinSquare.cs (slide 9)
LATIN_ROWS = [
    ["joystick", "teleport", "trail"],
    ["teleport", "trail", "joystick"],
    ["trail", "joystick", "teleport"],
]
LAYOUTS = ["A", "B", "C"]
TIME_LIMIT = 300.0


def participant_number(pid):
    """'P05' -> 5"""
    s = str(pid).strip().upper()
    if s.startswith("P"):
        s = s[1:]
    return int(s)


def latin_row(pid):
    """1..3"""
    return (participant_number(pid) - 1) % 3 + 1


def order_for(pid):
    """list of (trial, condition, layout) for a participant"""
    row = LATIN_ROWS[latin_row(pid) - 1]
    return [(i + 1, row[i], LAYOUTS[i]) for i in range(3)]


def condition_for_trial(pid, trial):
    return LATIN_ROWS[latin_row(pid) - 1][int(trial) - 1]
