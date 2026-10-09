# Team 8 study plan

**Does Teleporting Get You Lost? Comparing Joystick, Teleport, and Trail-Assisted Teleport Movement in a VR Escape Task**

CS 464, Fall 2026, Colorado State University. Mahanyas Baira, Yulisa Medrano, Ashley Poppa, Sai Donepudi.

Source docs: [proposal report](CS464_Report_Final_Draft-2.pdf), [proposal slides](CS464_Proposal_Presentation.pptx).

> Open questions are in section 9. Until they're answered, the recommended default in each question is what the code and issues assume.

## 0. Study summary

1. Study: "Does Teleporting Get You Lost?" compares joystick, plain teleport, and trail-assisted teleport (landing markers joined by a line) in a VR escape task.
2. Task: 3 rooms in a row; each room hides a lever; lever 1 opens gate 1, lever 2 opens gate 2, lever 3 unlocks the exit. 5-minute cap. Practice room before each method.
3. IV: locomotion method (3 levels). Turning, rooms, and practice time are held constant. Joystick moves at a fixed walking speed.
4. DVs: completion time (s), backtracking (re-entries into already-visited floor grid cells, from the position log), SSQ (Kennedy et al., 1993) at baseline and after each method, and final ranking + "most comfortable".
5. Design: within-subjects, 9 to 12 participants (multiple of 3), 3x3 Latin square, 5-minute breaks.
6. Latin square from slide 9: G1 = Joystick, Teleport, Trail; G2 = Teleport, Trail, Joystick; G3 = Trail, Joystick, Teleport. Layout A is always 1st, B 2nd, C 3rd, so each method meets each layout once.
7. RQ1 efficiency: H1a trail is fastest; H1b trail backtracks less than plain teleport.
8. RQ2 sickness: H2a joystick is sickest; H2b trail is no sicker than plain teleport.
9. Hardware: Meta Quest 3 (Ashley's), Unity + XR Interaction Toolkit, standalone APK, app logs position and time.
10. Team: Mahanyas Baira, Yulisa Medrano, Ashley Poppa, Sai Donepudi; Dr. Mohammed Safayet Arefin, CS464 Fall 2026, CSU.

---

## Research and tool choices

### R1. Versions and XR stack
- **Unity 6.3 LTS (6000.3.x)**. 6.3 LTS shipped Dec 4, 2025 and is supported to Dec 2027. 6.0 LTS support ends this month (Oct 2026), so do not start on 6.0. Pin the exact patch: whoever creates the project takes the newest 6000.3.x patch in Unity Hub on day 1, and that string in `ProjectSettings/ProjectVersion.txt` becomes law. ([Unity 6.3 LTS blog](https://unity.com/blog/unity-6-3-lts-is-now-available), [Unity 6 support page](https://unity.com/releases/unity-6/support))
- **XR Interaction Toolkit 3.4.x** (3.4.0 is the version listed as released for 6000.3; 3.4.1 is a later patch). Use the newest 3.4.x the Package Manager offers, not 3.5/3.6 (those target newer editors). ([Unity 6.3 manual: XRI](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.xr.interaction.toolkit.html), [XRI 3.4 changelog](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.4/changelog/CHANGELOG.html))
- **OpenXR plug-in + Unity OpenXR: Meta (Meta Quest feature group), not the Meta XR SDK.** Reasons: (a) XRI already gives us continuous move, teleport, snap turn, and interactables, and Meta's Interaction SDK would duplicate them with its own locomotion; (b) fewer packages and no Meta-specific rig to learn; (c) the legacy Oculus XR plug-in is being phased out and mixing it with OpenXR breaks Quest validation; (d) we need no passthrough, hand tracking, or platform features. Enable only OpenXR in XR Plug-in Management for Android. ([Unity OpenXR: Meta docs](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@1.0/manual/index.html), [Unity forum: Unity OpenXR vs Meta OpenXR](https://discussions.unity.com/t/whats-the-difference-between-unity-open-xr-and-meta-open-xr-can-i-use-both/946470), [Quest validation fix](https://gamineai.com/help/unity-openxr-validation-failed-quest-3-xr-plugin-feature-group-fix))

### R2. Continuous move vs teleport in XRI, and switching per trial
- XRI 3.x locomotion: a **Locomotion Mediator** hands **XR Body Transformer** access to providers: Continuous Move Provider, Teleportation Provider, Snap Turn Provider, etc. ([XRI 3.2 locomotion manual](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.2/manual/locomotion))
- The Starter Assets rig already has a per-hand `ControllerInputActionManager` with `smoothMotionEnabled`: true enables Move and disables Teleport Mode; false does the reverse (and it also turns that hand's turn actions off while smooth motion is on). ([source mirror](https://github.com/needle-mirror/com.unity.xr.interaction.toolkit))
- **Recommendation:** `LocomotionSwitcher.Apply(condition)` called by `TrialManager` only between trials (never mid-trial, because a provider can hold the rig locked during a teleport). Left stick = locomotion (move in Joystick, teleport in both teleport conditions). Right stick = snap turn 45 degrees in all conditions, with the right-hand teleport ray turned off, so turning is identical. It sets `smoothMotionEnabled` on the left manager and also explicitly enables/disables the `ContinuousMoveProvider` and the left teleport interactor, plus toggles the `TeleportTrail`. Comfort vignette (Tunneling Vignette) is off in all conditions so it does not mask joystick sickness. Exact component wiring is verified in the Editor in issue #9.

### R3. Breadcrumb trail on Quest 3
- Options: one **LineRenderer** (1 draw call, cheap), **URP decals** (need a renderer feature and depth work; avoid on mobile), **spawned marker meshes** (fine if they share one material/mesh). LineRenderers do not GPU-instance, so never one LineRenderer per segment. ([Unity forum: LineRenderer instancing](https://discussions.unity.com/t/line-renderer-gpu-instancing-breaks-when-setting-the-propertyblock/649226), [Meta: instancing and draw calls](https://developers.meta.com/horizon/documentation/unity/po-renderdoc-optimizations-1/), [Quest SRP batcher/instancing report](https://discussions.unity.com/t/gpu-instancing-dynamic-batching-not-working-on-oculus-quest-2-android-works-fine-in-editor/819564))
- **Recommendation (matches slide 7):** on each teleport landing (`TeleportationProvider.locomotionEnded`), place a flat unlit disc from a pre-allocated pool (cap 300, one shared URP Unlit material, no shadows) and append the point to a single LineRenderer drawn 2 cm above the floor. A 5-minute trial produces well under 150 landings, so cost is a handful of batched draws. Trail only exists in the Trail condition and is cleared at trial start. Check frame rate with the Meta OVR Metrics overlay during the first device test.

### R4. Unity + Git team setup
- `.gitignore`: GitHub's Unity template (Library/, Temp/, Obj/, Build/, Builds/, Logs/, UserSettings/, *.csproj, *.sln, *.apk, etc.).
- `.gitattributes`: text YAML (`*.unity *.prefab *.asset *.mat *.anim *.controller *.meta`) gets `merge=unityyamlmerge eol=lf`; binaries (`*.fbx *.png *.jpg *.psd *.tga *.wav *.mp3 *.ogg *.exr *.hdr *.tif *.blend *.obj *.ttf *.otf`) get `filter=lfs diff=lfs merge=lfs -text`. Never LFS the YAML. ([community .gitattributes](https://gist.github.com/DryreL/a0d9b8f0193ba06c52c090a975a78924), [JMU Unity+Git notes](https://w3.cs.jmu.edu/wangid/cs480/fa23/misc/unity-and-git/))
- Project Settings > Editor: Asset Serialization **Force Text**, Version Control **Visible Meta Files** (Unity 6 defaults, but verify). ([uhiyama-lab guide](https://uhiyama-lab.com/en/notes/unity/unity-version-control-git-lfs-team-workflow/))
- Each teammate runs once: `git config merge.unityyamlmerge.name "UnityYAMLMerge"` and `git config merge.unityyamlmerge.driver '"<Unity path>/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'` (Mac path: `/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/Tools/UnityYAMLMerge`). CONTRIBUTING.md will have copy-paste lines per OS. ([Unity SmartMerge manual](https://docs.unity3d.com/2022.3/Documentation/Manual/SmartMerge.html), [gamedeveloper.com guide](https://gamedeveloper.com/programming/the-complete-guide-to-unity-git))
- Conflicts: SmartMerge is a safety net, not the plan. The plan is **one owner per scene and per room prefab**, work done in prefab mode or personal sandbox scenes, and `Main.unity` touched only by its owner. ([Unity blog: scenes and prefabs for version control](https://unity.com/blog/author-scenes-and-prefabs-with-verson-control))

### R5. Agent tooling for local developers
- **Unity's official Claude Code plugin** (released Sep 9, 2026, needs Unity 6+): Unity-authored skills + the Unity CLI + live Editor control. Install inside Claude Code: `/plugin marketplace add Unity-Technologies/unity-agent-plugin` then `/plugin install unity@unity-agent-plugin`. It drives an open Editor through the Unity CLI; without the Editor it falls back to hand-editing scene files, which we ban. The older in-Editor "Unity MCP server" in `com.unity.ai.assistant` is deprecated in favor of the CLI, so skip it. ([plugin repo](https://github.com/Unity-Technologies/unity-agent-plugin), [GameDev.net news](https://gamedev.net/news/5637-official-unity-plugin-for-claude-code/), [Unity MCP deprecation note](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.20/manual/integration/unity-mcp-get-started.html))
- **Meta VR CLI (`metavr`)** + MCP: device list, `app` install/launch, `files ls/pull/push`, `log` (logcat), `capture` (screenshots/recording), perf traces, Meta docs search. Install with `npx -y metavr@latest ...` (Node 18+) or `/plugin marketplace add meta-quest/agentic-tools` then `/plugin install meta-vr@meta-vr`. Pick one install method only, or tools register twice. ([agentic-tools repo](https://github.com/meta-quest/agentic-tools), [Install Meta VR CLI](https://developers.meta.com/vr/essentials/metavr-install/), [metavr overview](https://developers.meta.com/horizon/essentials/metavr-overview))
- **Who installs what:**

| Person | Install locally | Optional |
|---|---|---|
| Mahanyas (rig, Main scene, builds) | Unity Hub + 6000.3.x (with Android Build Support, OpenJDK, Android SDK/NDK), Git + Git LFS, UnityYAMLMerge config | Claude Code + Unity plugin |
| Yulisa, Sai (rooms) | Unity Hub + same 6000.3.x (Android module only if they build), Git + Git LFS, UnityYAMLMerge config | Unity plugin (helpful for prop placement chores) |
| Ashley (headset) | Meta Horizon app (developer mode), Meta Quest Developer Hub (drag-drop APK install, file browser), Python 3 only if she runs analysis | `metavr` via npx for one-line pull/install; Unity not required |

- **Useless in the cloud container:** both plugins' live features (no Editor, no USB headset). Cloud work is limited to C#, Python, docs, CI, and GitHub issues.

### R6. Logging on Quest
- Write with `Path.Combine(Application.persistentDataPath, ...)`, which on Quest is `/sdcard/Android/data/<package>/files/`. The folder only exists after the first write. Flush after every trial so a crash loses at most one trial. ([Meta forum: adb pull path](https://communityforums.atmeta.com/discussions/dev-quest/is-it-possible-to-write-a-text-file-from-the-oculus-quest-and-save-it-in-the-int/781445), [Unity forum: writing files on Quest](https://forum.unity.com/threads/writing-files-to-oculus-quest.1076615/))
- Pull: `adb pull /sdcard/Android/data/edu.colostate.cs464.team8/files/StudyData ./StudyData`, or `npx metavr@latest files pull ...` (check `--help` for exact syntax), or MQDH file manager with "Show app data" on.

---

## 1. Architecture

### 1.1 Scenes
| Scene | Purpose | Owner |
|---|---|---|
| `Assets/_Project/Scenes/Main.unity` | The only scene in the build. Contains PlayerRig, StudyManagers, OperatorPanel, Lobby, PracticeRoom, Room_1..3 prefab instances. | Mahanyas |
| `Assets/_Project/Scenes/Sandbox/Sandbox_<Name>.unity` | Personal scratch scenes for building/testing a room. Not in build. | each person |

Rooms are **prefabs, not separate scenes**: one build scene, no additive loading, and each room file has one owner.

### 1.2 Prefabs
| Prefab | Contents | Owner |
|---|---|---|
| `PlayerRig` | XRI Starter Assets XR Origin (Character Controller), LocomotionSwitcher, PositionLogger, TeleportTrail | Mahanyas |
| `Room_1`, `Room_2`, `Room_3` | ~8 x 8 m greybox then props; floor with TeleportationArea; occluding props; `LeverSpot_A/B/C` empties; one Lever; one Gate (Room_3 gets ExitDoor instead); `RoomZone` trigger | Room_1+2: Yulisa, Room_3: Sai |
| `PracticeRoom` | Small room with one practice lever, separate from the 3 rooms | Yulisa |
| `Kit_*` | Shared wall/floor/crate/shelf pieces + materials | Sai |
| `Lever` | Lever model + XRSimpleInteractable (direct/near only) + LeverController + click sound | Sai (visual), Mahanyas (script) |
| `Gate` | Full-height door/bars with collider that blocks walking and teleport ray + GateController | Sai |
| `ExitDoor` | Locked door + exit trigger volume | Sai |
| `StudyManagers` | TrialManager, SessionConfig, LayoutLoader (StudyDataWriter is a plain class) | Mahanyas |
| `OperatorPanel` / `Lobby` | World-space UI in a neutral lobby: participant ID +/-, start trial #, Start, break screen | Mahanyas |

### 1.3 Scripts
Folder `Assets/_Project/Scripts/`. `Core/` is plain C# (asmdef `Team8.Core` with no engine references) so it can be unit tested anywhere. `Runtime/` has no asmdef, so it can see the XRI Starter Assets sample without extra setup.

| Script | Folder | What it does |
|---|---|---|
| `LatinSquare` | Core | Rows from slide 9. `GetOrder(participantNumber)` -> 3 x (condition, layout). Row = (n - 1) % 3. |
| `GridBacktrackCounter` | Core | Feed (x, z) samples; converts to cells; counts re-entries with hysteresis (see 2.4). Same rules as the Python version. |
| `Condition`, `TrialResult` | Core | Enum Joystick/Teleport/Trail; plain data for one trial summary row. |
| `CsvUtil` | Core | Header + row formatting with invariant culture (so a European locale does not write `1,5`). |
| `SessionConfig` | Runtime | Holds participant ID (P01..P12), Latin row, condition order, start-trial index; set from OperatorPanel. |
| `TrialManager` | Runtime | State machine: Lobby -> Practice -> Trial -> Break -> ... -> Done. Starts the 300 s timer, listens for lever events, ends on exit or timeout, writes summary, calls LocomotionSwitcher and LayoutLoader. |
| `LocomotionSwitcher` | Runtime | Applies a condition to the rig (section R2). Called only between trials. |
| `TeleportTrail` | Runtime | Pooled disc markers + one LineRenderer, fed by teleport landings, Trail condition only. |
| `LeverController` | Runtime | On select: animate flip once, fire `OnFlipped(roomIndex)`. Ignores repeat flips. |
| `GateController` | Runtime | `Open()`: disable collider, animate door. `ResetGate()` for next trial. |
| `ExitDoor` | Runtime | Unlocks after lever 3; trigger volume tells TrialManager the player escaped. |
| `LayoutLoader` | Runtime | For layout A/B/C, moves each room's Lever to that room's `LeverSpot_<layout>`. |
| `PositionLogger` | Runtime | 10 Hz sample of head (camera) x, z, yaw during a trial; writes position CSV; feeds GridBacktrackCounter. |
| `RoomZone` | Runtime | Box trigger that reports room index for the logger. |
| `StudyDataWriter` | Runtime | Opens files under `persistentDataPath/StudyData/<P05_timestamp>/`, flushes per trial and when the headset is taken off, never overwrites. |
| `OperatorPanel` | Runtime | UI buttons -> SessionConfig / TrialManager. |

### 1.4 Diagram
```
 OperatorPanel ──sets──> SessionConfig ──(LatinSquare)──> condition+layout order
                                   │
                                   v
                             TrialManager ──────────────┬───────────────┐
            Apply(condition)  │          │ Apply(layout) │ Start/Stop    │ write summary
                              v          v               v               v
                   LocomotionSwitcher  LayoutLoader  PositionLogger   StudyDataWriter ──> persistentDataPath/StudyData/*.csv
                     │        │            │             │  feeds                         │
             MoveProvider  Teleport+Trail  Lever spots  GridBacktrackCounter              │ adb pull / MQDH
                                                                                          v
 Lever1 ─OnFlipped─> Gate1.Open   Lever2 ─> Gate2.Open   Lever3 ─> ExitDoor.Unlock ─> TrialManager.End
                                                                                          v
                                                       analysis/analyze.py ──> results/ (tables, plots)
```
Room flow: `[Lobby] -> [Practice] -> [Room 1 | Gate 1 | Room 2 | Gate 2 | Room 3 | Exit]`

### 1.5 Repo layout
```
Assets/_Project/{Scripts/Core, Scripts/Runtime, Prefabs, Scenes, Materials, Art, Audio}
Assets/Tests/EditMode/          NUnit tests for Core (also compiled by tests/dotnet)
Packages/  ProjectSettings/
analysis/  analyze.py, fake_data.py, ssq.py, requirements.txt, test_analysis.py
data/raw/<P01>/...  data/forms/ssq.csv   results/
docs/  PLAN.md, OPERATOR_CHECKLIST.md, proposal PDF + PPTX
tests/dotnet/  StudyCore.Tests.csproj (links Core + EditMode tests, runs in CI)
.github/workflows/ci.yml   .github/CODEOWNERS
```

## 2. Data spec
All CSVs: UTF-8, comma, header row, `.` decimal, times in seconds with 3 decimals. One folder per session: `StudyData/P05_20261118-143012/`.

### 2.1 `positions.csv` (10 Hz, during trials only)
| column | example | notes |
|---|---|---|
| participant | P05 | |
| trial | 2 | position in order, 1..3 |
| condition | trail | joystick / teleport / trail |
| layout | B | |
| t | 12.300 | s since trial start |
| unix_ms | 1795000000000 | wall clock, for syncing with notes |
| x, z | 3.412, 7.905 | head position, metres, world space |
| yaw | 182.4 | head yaw, degrees 0..360 |
| room | 2 | 0 = outside a zone |
| cell_x, cell_z | 3, 7 | floor(x / cell), floor(z / cell) |
| cell_id | 3_7 | |

### 2.2 `events.csv`
`participant, trial, condition, layout, t, unix_ms, event, detail`. Events: `trial_start, lever_flip (detail=1..3), gate_open, exit_unlock, teleport (detail="x0;z0;x1;z1"), escaped, timeout, trial_abort`.

### 2.3 `trials.csv` (one row per trial)
`participant, latin_row, trial, condition, layout, completion_s, timed_out, aborted, lever1_s, lever2_s, lever3_s, backtracks, cells_entered, unique_cells, teleports, path_m, app_version`.
- `aborted=1` when the operator stopped the trial (e.g. sickness). Aborted trials are excluded from time/backtrack tests and reported separately.
- `path_m` is the summed horizontal head movement between 10 Hz samples (teleport jumps included).
- `completion_s` = time from Start to entering exit trigger; 300.000 and `timed_out=1` on timeout. Lever times blank if not reached.
- In-app backtracks are a live check; the analysis script recomputes from `positions.csv` and that is the reported number.

### 2.4 Grid and re-entry rules
- World origin at Room 1's outer corner, rooms laid along +x, so every cell id is unique across rooms.
- **Cell size 1.0 m** (about one teleport hop shorter than typical, coarse enough to ignore head sway). The analysis also reports 0.5 m and 2 m as a sensitivity check.
- **Hysteresis 0.15 m**: the current cell only changes when the head is more than 0.15 m past the edge of the current cell. This stops a person standing on a line from scoring A-B-A-B.
- Collapse the sample stream into a sequence of cell entries (consecutive duplicates removed).
- **Re-entry** = entering a cell that is already in the visited set (and is not the current cell). Each such entry counts 1. Consecutive frames in the same cell never count.
- Teleport landings count only the landing cell (no cells in between). So backtracking is directly comparable between teleport and trail (H1b). For joystick vs teleport we report it with a caveat and add a normalized rate `backtracks / cells_entered`.
- Lobby and practice are never logged.

### 2.5 SSQ and preference (Google Form, exported to `data/forms/ssq.csv`)
- Fields per submission: `participant, timepoint (baseline/after_1/after_2/after_3), 16 SSQ items (0 none .. 3 severe)`; condition is filled in by the analysis script from the Latin row, so Ashley never types it.
- Final section after `after_3`: rank the three methods 1..3, "most comfortable" (single choice), optional comment.
- Scoring (Kennedy et al., 1993): Nausea = raw x 9.54, Oculomotor = raw x 7.58, Disorientation = raw x 13.92, Total = (N + O + D raw sums) x 3.74.
- **Recommendation: Google Form, not in-VR.** The SSQ is meant to be answered right after removing the headset; reading 16 items in VR extends exposure, needs extra UI code, and needs a hand-off of the controllers. A form also gives timestamps for free.

## 3. Session flow for Ashley
Detailed version goes in `docs/OPERATOR_CHECKLIST.md`. Est. 50 to 55 min per participant.

**Before the participant arrives**
1. Headset charged above 70%, lenses clean, guardian set for seated use, app installed (version on the lobby panel matches the release).
2. Laptop/phone open on the Google Form. Chair with no swivel in the clear area (if we go seated, see open questions).
3. Consent (if required), assign the next ID from the sign-up sheet (P01, P02, ...).

**Session**
1. Baseline SSQ + demographics form (headset off). 2 min.
2. Ashley puts on the headset, opens the app, on the lobby panel sets Participant = P05, checks the displayed order (e.g. "Teleport/A, Trail/B, Joystick/C"), sets Start trial = 1.
3. Participant puts on headset. Ashley reads the standard script: goal, controls for this method, "find the lever in each room, flip it, go through the gate, leave by the exit, you have 5 minutes".
4. **Practice** for this condition (about 2 min in the practice room, flip the practice lever). Participant presses Start when ready.
5. **Trial** (max 5 min). App returns to the lobby automatically at escape or timeout. Ashley does not give hints.
6. Headset off. **SSQ for this timepoint** (after_1). Then rest until 5 minutes total have passed since the trial ended.
7. Repeat 3 to 6 for trials 2 and 3 (app advances automatically).
8. After the last SSQ: ranking + most comfortable + comment.
9. If the participant asks to stop or SSQ looks bad: end, mark in notes, do not push them. If the app crashes: reopen, set the same ID and "Start trial = n" to resume.

**Afterwards (same day)**
1. Connect headset to laptop, pull `StudyData/` (MQDH file manager drag-out, or one `adb pull` line).
2. Upload the session folder to the shared Drive folder `Team8/data/raw/`, plus a one-line note in the session log sheet (ID, date, any issues).
3. Mahanyas (or whoever is on data duty) commits it to `data/raw/` in a PR. No names or emails in any file; only P-codes.

## 4. Analysis plan
- `analysis/analyze.py --data data/ --out results/`: loads all `positions.csv` / `trials.csv` / `ssq.csv`, recomputes backtracking (1 m, plus 0.5 m and 2 m), scores SSQ, joins condition from the Latin row, writes `results/summary_by_condition.csv` (mean, SD, median, IQR), `results/stats.txt`, and plots: paired box + line plots for time, backtracks, SSQ total and delta-from-baseline; preference bar chart; top-down path plots per trial (nice for slides).
- `analysis/fake_data.py --n 12`: makes realistic fake sessions in the exact formats above so the pipeline and CI run before real data exists.
- Libraries: pandas, numpy, scipy, matplotlib. pytest for tests.
- **Tests: Friedman + Wilcoxon signed-rank post-hoc (Holm-corrected), not RM-ANOVA.** With n = 9 to 12 we cannot meaningfully check normality or sphericity; completion time is capped at 300 s (censored), backtracks are counts, and SSQ is skewed with many zeros. All of those suit rank-based tests.
  - Omnibus: Friedman per DV (completion time, backtracks, SSQ total post score, SSQ delta), effect size Kendall's W.
  - Planned comparisons matching the hypotheses: H1a trail vs teleport and trail vs joystick on time; H1b trail vs teleport on backtracks; H2a joystick vs teleport and joystick vs trail on SSQ. Wilcoxon, Holm across each family, effect size r = Z / sqrt(N).
  - H2b ("no sicker") is a no-difference claim; a non-significant Wilcoxon is not proof. We report the median difference with a bootstrap 95% CI and say it plainly.
  - Preference: Friedman on ranks + counts for "most comfortable".
  - Sanity checks: order/learning effect (Friedman on trial position), layout difficulty (time by layout), number of timeouts per condition.

## 5. Work breakdown and ownership
Labels: `cloud-ok`, `needs-unity-local`, `needs-headset`, plus `world`, `scripts`, `data`, `study`, `docs`. Estimates in hours. Names are editable in the issues.

| # | Title | Owner | Est | Depends | Labels |
|---|---|---|---|---|---|
| 1 | Repo hygiene: .gitignore, .gitattributes, CLAUDE.md, CONTRIBUTING.md, CI | Mahanyas | 2 | none | cloud-ok, docs |
| 2 | Everyone: install Unity 6000.3.x + Git LFS + YAMLMerge, clone, open project | all 4 | 1 each | 3 | needs-unity-local |
| 3 | Create Unity project (URP, Android, OpenXR+Meta, XRI 3.4 + Starter Assets), Force Text, commit | Mahanyas | 3 | 1 | needs-unity-local |
| 4 | Ashley: enable developer mode, install MQDH, sideload a hello-world APK, test adb pull | Ashley | 2 | 3 | needs-headset |
| 5 | Core scripts: LatinSquare, GridBacktrackCounter, CsvUtil + EditMode tests | Mahanyas (cloud) | 4 | 1 | cloud-ok, scripts |
| 6 | Runtime scripts: TrialManager, SessionConfig, LayoutLoader, PositionLogger, StudyDataWriter, Lever/Gate/Exit | Mahanyas (cloud) | 6 | 5 | cloud-ok, scripts |
| 7 | Shared kit: walls, floor, crates, shelves, materials (low poly, mobile-friendly) | Sai | 5 | 3 | needs-unity-local, world |
| 8 | Lever, Gate, ExitDoor prefabs (visuals + colliders), wired to scripts | Sai | 4 | 6, 7 | needs-unity-local, world |
| 9 | PlayerRig prefab: Starter Assets rig, LocomotionSwitcher wiring, snap turn, vignette off, move speed | Mahanyas | 5 | 3, 6 | needs-unity-local, scripts |
| 10 | TeleportTrail (pooled markers + LineRenderer) | Mahanyas | 3 | 9 | needs-unity-local, scripts |
| 11 | Room_1 prefab: greybox, props, LeverSpot A/B/C, RoomZone | Yulisa | 6 | 7 | needs-unity-local, world |
| 12 | Room_2 prefab | Yulisa | 6 | 7 | needs-unity-local, world |
| 13 | Room_3 prefab + exit area | Sai | 6 | 7, 8 | needs-unity-local, world |
| 14 | PracticeRoom + Lobby geometry | Yulisa | 3 | 7 | needs-unity-local, world |
| 15 | Main.unity assembly: place rooms, managers, operator panel; end-to-end in XR Device Simulator | Mahanyas | 4 | 8-14 | needs-unity-local |
| 16 | OperatorPanel UI (world space: ID, start trial, order display, version) | Mahanyas | 3 | 6 | needs-unity-local, scripts |
| 17 | Android build settings + build checklist + GitHub Release with APK | Mahanyas | 2 | 15 | needs-unity-local |
| 18 | Device test checklist, run on every release (fps, locomotion, levers, CSV pull) | Ashley | 1 per build | 17 | needs-headset |
| 19 | Google Form: baseline + demographics, SSQ x4, final ranking | Ashley | 2 | none | cloud-ok, study |
| 20 | Operator checklist + participant script (docs/OPERATOR_CHECKLIST.md) | Ashley (Mahanyas drafts) | 2 | 19 | cloud-ok, docs, study |
| 21 | analysis/analyze.py + fake_data.py + tests | Sai (Mahanyas drafts in cloud) | 6 | 5 | cloud-ok, data |
| 22 | Layout balancing: lever spot difficulty check across A/B/C | Yulisa | 2 | 11-13 | needs-unity-local, world |
| 23 | Pilot with 1-2 people, fix list | Ashley | 3 | 17, 19, 20 | needs-headset, study |
| 24 | Recruitment + scheduling 9-12 participants | Ashley (+Yulisa helps find people) | 3 | 23 | study |
| 25 | Data collection P01-P12 | Ashley | 12 | 23 | needs-headset, study |
| 26 | Data upload + commit per session | Ashley + Mahanyas | 2 | 25 | data |
| 27 | Run analysis, results tables and plots | Sai | 4 | 21, 25 | cloud-ok, data |
| 28 | Final report: methods/results/discussion | all (sections split) | 4 each | 27 | docs |
| 29 | Final slides + rehearsal | all | 2 each | 27 | docs |

Rough totals: Mahanyas ~36 h (setup, rig, scripts, Main scene, builds), Yulisa ~25 h (rooms 1+2, practice, layout balance, recruiting help), Sai ~31 h (kit, interactables, room 3, analysis), Ashley ~32 h (device QA, forms, pilot, all sessions, data). Report/slides split evenly.

## 6. Timeline (assumed 8 weeks; final presentation date NOT found, assumed week of Nov 30. Please confirm)
| Week | Dates | Milestone (GitHub milestone name) |
|---|---|---|
| W1 | Oct 12-18 | **M1 Setup**: repo hygiene, Unity project, everyone opens it, Ashley sideloads a test APK (#1-5, #19 draft) |
| W2 | Oct 19-25 | **M2 Greybox**: rig + locomotion switching in simulator, greybox rooms, lever/gate, first real build on Quest (#6-9, #11-13 greybox) |
| W3 | Oct 26-Nov 1 | **M3 Playable**: full trial loop, layouts, logging, trail, operator panel; Ashley pulls a real CSV and analysis runs on it (#10, #14-18) |
| W4 | Nov 2-8 | **M4 Feature complete**: props/art, practice room, analysis done on fake data, form final, layout balance (#21, #22) |
| W5 | Nov 9-15 | **M5 Pilot**: 1-2 pilot participants, fix list, freeze `v1.0` APK by Sunday (#23) |
| W6 | Nov 16-22 | **M6 Data collection 1**: P01-P06 (#24-26) |
| W7 | Nov 23-29 | **M7 Data collection 2**: P07-P12 (Thanksgiving week: confirm Ashley's availability) |
| W8 | Nov 30-Dec 4 | **M8 Final**: analysis, report, slides, presentation (#27-29) |
No feature work after the v1.0 freeze; only crash fixes, and every rebuild gets a new version number on the lobby panel and in the CSVs.

## 7. Branching and PR rules
- `main` is protected: PR only, 1 approval, CI green. No direct pushes.
- Branch names: `<name>/<issue#>-<short-desc>`, e.g. `yulisa/11-room1-greybox`, `sai/21-analysis`.
- Reviews: Mahanyas reviews everyone; Sai or Yulisa reviews Mahanyas's PRs. Room PRs include a screenshot. Script PRs pass CI.
- `.github/CODEOWNERS`: `Main.unity` and `PlayerRig.prefab` -> Mahanyas; `Room_1/2`, `PracticeRoom` -> Yulisa; `Room_3`, `Kit_*`, `Lever/Gate/ExitDoor` -> Sai; `analysis/` -> Sai; `docs/OPERATOR_CHECKLIST.md` -> Ashley.
- **Main.unity**: only Mahanyas commits changes to it. Everyone else changes their own prefab; changes inside a prefab show up in Main automatically. If you need Main changed (new object, moved room), open an issue or ask in the group chat.
- Pull `main` before opening Unity, keep PRs small and short-lived (merge within 2-3 days), never edit `.unity`/`.prefab` YAML by hand.
- No secrets, APKs, or participant names in the repo. APKs go in GitHub Releases.

## 8. Risks and mitigations
| Risk | Mitigation |
|---|---|
| Only one headset, with a remote teammate | XR Device Simulator for all Editor testing; Ashley tests every weekly build with a 10-minute checklist (#18); hello-world sideload in W1 so dev-mode problems surface early; ask whether CSU lends a Quest (open question). |
| Quest build fails / app won't install | Pin Unity patch + packages; Android module installed on the build laptop in W1; first device build in W2 not W5; keep the last good APK in Releases; Ashley's dev mode set up in W1. |
| Motion-sickness dropouts | Seated, 5-min breaks, sickness-prone screening question, stop rule in script, joystick speed moderate (1.5 m/s, pilot-tuned); recruit 12 so losing 1-3 still leaves 9. Analysis handles partial participants (Friedman needs complete rows, so report how many were dropped). |
| Scene merge conflicts | One build scene, one owner; rooms are prefabs with one owner each; sandbox scenes per person; CODEOWNERS; YAMLMerge configured as backup. |
| Unity version mismatch | Exact version in CLAUDE.md, CONTRIBUTING.md, and ProjectVersion.txt; CI fails if ProjectVersion.txt changes without the doc; "never click Upgrade" rule. |
| Layouts not equally hard | Lever spots at similar path distance from each room's entrance; pilot times compared by layout; layout is crossed with method by the Latin square anyway. |
| Timer/logging bug ruins data | Pipeline tested on fake data first, then on a real pilot CSV in W3 and W5; flush per trial; never overwrite files. |
| Code written in the cloud fails to compile in Unity | Keep XRI usage small, Core logic engine-free and CI-tested; Mahanyas opens the project right after each script PR and fixes compile errors before merge. |

## 9. Open questions (please answer)
1. **Final presentation date** and any course check-in deadlines? I assumed the week of Nov 30.
2. **IRB / consent**: does Dr. Arefin require a consent form or any approval for classmates/friends as participants?
3. **Seated or standing?** I recommend seated on a non-swivel chair (safer with joystick; levers at about 1.0-1.2 m so they are reachable). Head position is still logged either way.
4. **Lever interaction**: near-only (must walk up and grab/press it) or allow the ray from a distance? I recommend near-only so finding = reaching.
5. **Joystick speed**: fixed 1.5 m/s OK (pilot can tune)? And snap turn 45 degrees in all conditions, comfort vignette off for all?
6. **Grid cell size**: 1.0 m with 0.15 m hysteresis OK?
7. **Data storage**: commit anonymized CSVs to `data/raw/` in this repo (my pick), or keep them only in Drive?
8. **Ashley's setup**: is her Quest 3 already in developer mode, and does she have a Windows/Mac laptop for MQDH? Is she available Thanksgiving week? Is anyone else ever near her for a headset hand-off?
9. **GitHub usernames** for Yulisa, Sai, Ashley (for CODEOWNERS, assignees, and inviting them as collaborators).
10. **Branches**: the repo has no `main` yet. OK if I push the hygiene commit to `baira/laughing-cori-q1gext` and you create `main` from it on GitHub (or I create `main` from it)? Also OK to turn on branch protection?
11. **Latin square**: keep the approved 3-order square (simple, matches the deck), or with 12 people use all 6 orders (2 each) to also balance carryover? I recommend keeping the approved design.
12. **Demographics**: add age range, prior VR experience, and motion-sickness susceptibility to the baseline form? (Recommended, needs no extra code.)
