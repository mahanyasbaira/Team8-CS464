"""Grid re-entry counting. Must match GridBacktrackCounter.cs (docs/PLAN.md section 2.4)."""
import math


def cell_of(v, size):
    return int(math.floor(v / size))


def count_backtracks(xs, zs, cell_size=1.0, hysteresis=0.15):
    """Returns dict with backtracks, cells_entered, unique_cells for one trial's samples.

    - cell = floor(x / size), floor(z / size)
    - the current cell only changes once the point is more than `hysteresis`
      metres past the edge of the current cell
    - entering a cell that was visited before counts 1; staying put never counts
    """
    visited = set()
    current = None
    backtracks = 0
    entered = 0

    for x, z in zip(xs, zs):
        cell = (cell_of(x, cell_size), cell_of(z, cell_size))
        if current is None:
            current = cell
            visited.add(cell)
            entered = 1
            continue
        if cell == current:
            continue

        # distance outside the current cell's square (Chebyshev)
        min_x, min_z = current[0] * cell_size, current[1] * cell_size
        dx = max(min_x - x, x - (min_x + cell_size), 0.0)
        dz = max(min_z - z, z - (min_z + cell_size), 0.0)
        if max(dx, dz) <= hysteresis:
            continue

        current = cell
        entered += 1
        if cell in visited:
            backtracks += 1
        else:
            visited.add(cell)

    return {"backtracks": backtracks, "cells_entered": entered, "unique_cells": len(visited)}
