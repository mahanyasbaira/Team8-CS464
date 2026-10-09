# Contributing (Team 8)

Short version: install the exact Unity version, install Git LFS, set up UnityYAMLMerge once, then work on a branch and open a PR into `main`.

## 1. One-time setup

### Install
1. **Git** and **Git LFS**: https://git-lfs.com. Then run once:
   ```
   git lfs install
   ```
2. **Unity Hub**, then the exact editor version in `ProjectSettings/ProjectVersion.txt` (Unity 6.3 LTS, 6000.3.x). In Hub use *Installs > Install Editor > Archive* if the exact patch isn't listed. Add these modules:
   - **Android Build Support** (with OpenJDK and Android SDK & NDK Tools). Required for whoever builds the APK, optional for everyone else.
3. (Optional) Unity's agent plugin (Unity CLI + editor skills), see `docs/PLAN.md` R5. Commit before letting any tool change scenes or prefabs, and review the diff.

Ashley (headset) does **not** need Unity. She needs the Meta Horizon phone app (developer mode on), Meta Quest Developer Hub (MQDH) on her laptop, and optionally Node.js for `npx metavr@latest`.

### Clone
```
git clone https://github.com/mahanyasbaira/Team8-CS464.git
cd Team8-CS464
git lfs pull
```

### Set your name and the Unity merge tool (in the repo folder)
```
git config user.name "Your Name"
git config user.email "you@example.com"
git config merge.unityyamlmerge.name "UnityYAMLMerge"
```
Then the driver line for your OS (replace `<ver>` with the version in ProjectVersion.txt):

Windows (Git Bash):
```
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/<ver>/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
```
macOS:
```
git config merge.unityyamlmerge.driver '"/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/Tools/UnityYAMLMerge" merge -p %O %B %A %A'
```
This only helps when a merge happens anyway. The real fix is not editing the same scene/prefab as someone else (see below).

### Open the project
Open the repo folder from Unity Hub (*Add > Add project from disk*). If Hub asks to upgrade or change version, **cancel** and install the right version instead.

Check once: *Edit > Project Settings > Editor*: Asset Serialization = **Force Text**, Version Control mode = **Visible Meta Files**.

### First-time project creation (only once, Mahanyas, issue #3)
Until `ProjectSettings/` is committed, the repo only has scripts. To turn it into a Unity project:
1. Unity Hub > *New project* > **Universal 3D** template, newest 6000.3.x, saved in a temporary folder. Let it open, then close Unity.
2. Copy `Packages/`, `ProjectSettings/` and `Assets/Settings/` from that temp project into the repo folder.
3. Hub > *Add > Add project from disk* > the repo folder, open it.
4. *Window > Package Manager* > Unity Registry: install **XR Interaction Toolkit** (3.4.x) and import its **Starter Assets** and **XR Device Simulator** samples; install **OpenXR Plugin** and **Unity OpenXR: Meta**. Our scripts show compile errors until XRI and Starter Assets are in; that's expected.
5. Put the exact editor version (from `ProjectSettings/ProjectVersion.txt`) into `.unity-version`, then commit everything Unity created on a branch.

### See the greybox world
Menu **Team8 > Build Greybox**. It creates plain Room_1/2/3, PracticeRoom and Lobby prefabs in `Assets/_Project/Prefabs/Rooms/` and opens `Scenes/Sandbox/Sandbox_Greybox.unity`. Fly around the Scene view (right mouse + WASD). Running it again never overwrites a room prefab that already exists, so room owners can safely edit theirs.

## 2. Who owns what
| Thing | Owner |
|---|---|
| `Main.unity`, `PlayerRig`, StudyManagers, OperatorPanel, scripts, builds | Mahanyas |
| `Room_1`, `Room_2`, `PracticeRoom`, Lobby geometry | Yulisa |
| `Room_3`, Exit, `Kit_*` pieces, Lever/Gate/ExitDoor visuals, `analysis/` | Sai |
| Headset testing, Google Form, `docs/OPERATOR_CHECKLIST.md`, sessions, data upload | Ashley |

Rules:
- Only Mahanyas commits changes to `Main.unity`. Edit your room in **prefab mode** or in your own `Scenes/Sandbox/Sandbox_<YourName>.unity`. Prefab changes show up in Main automatically.
- Need something changed in Main? Open an issue or say it in the group chat.
- Never hand-edit `.unity` / `.prefab` / `.meta` files in a text editor.

## 3. Testing without a headset
Use the **XR Device Simulator** (XRI sample, already in the project). Drop the `XR Device Simulator` prefab into your sandbox scene and press Play. Keyboard/mouse drives the head and controllers. Remove it before committing Main (it's only meant for sandboxes; Mahanyas handles Main).

## 4. Day-to-day workflow
1. Pick or get assigned a GitHub issue.
2. Update first, **before opening Unity**:
   ```
   git checkout main
   git pull
   ```
3. Branch: `<name>/<issue#>-<short-desc>`, e.g. `yulisa/11-room1-greybox`.
   ```
   git checkout -b yulisa/11-room1-greybox
   ```
4. Work, save in Unity (Ctrl/Cmd+S, and *File > Save Project*), then commit:
   ```
   git add -A
   git status        # make sure you only changed your own files
   git commit -m "room 1 greybox walls + floor"
   git push -u origin yulisa/11-room1-greybox
   ```
5. Open a PR into `main` on GitHub. Put `Closes #11` in the description. Room PRs: add a screenshot.
6. Reviewer: Mahanyas reviews everyone; Sai or Yulisa reviews Mahanyas. CI must be green.
7. Merge within 2-3 days. Small PRs beat big ones.

If `git status` shows changes to files you didn't mean to touch (often `Main.unity` or `ProjectSettings/`), discard them before committing:
```
git restore Assets/_Project/Scenes/Main.unity
```

## 5. Code checks (no Unity needed)
```
# C# core logic
dotnet test tests/dotnet

# Python analysis
python -m pip install -r analysis/requirements.txt
python -m pytest analysis
python analysis/fake_data.py --n 12 --out /tmp/fake
python analysis/analyze.py --data /tmp/fake --out /tmp/results
```
CI runs the same checks on every PR.

## 6. Data
- Participant IDs only (`P01`..`P12`). No names, emails, or video of faces in the repo.
- Session folders go in `data/raw/`, the Google Form export goes in `data/forms/ssq.csv`.

## 7. Project rules (short version)
Folder layout:
```
Assets/_Project/Scripts/Core      plain C#, no UnityEngine (asmdef Team8.Core), tested in CI
Assets/_Project/Scripts/Runtime   MonoBehaviours (no asmdef)
Assets/_Project/Prefabs           Rooms/, Interactables/, Player/, Kit/
Assets/_Project/Scenes            Main.unity (only build scene), Sandbox/
Assets/Tests/EditMode             NUnit tests for Core (also run by tests/dotnet in CI)
analysis/                         Python analysis + fake data generator
data/raw/<session>/               pulled CSVs (anonymous P-codes only)
docs/                             PLAN.md, OPERATOR_CHECKLIST.md, proposal
```
- Pinned versions: Unity 6.3 LTS (exact patch in `ProjectSettings/ProjectVersion.txt`, mirrored in `.unity-version`), XR Interaction Toolkit 3.4.x, OpenXR + Unity OpenXR: Meta (no Oculus XR plug-in, no Meta XR SDK), URP, Android, package id `edu.colostate.cs464.team8`. Never click "Upgrade".
- Never edit `.unity`, `.prefab`, `.asset` or `.mat` files as text. Use the Unity Editor.
- Never change an existing `.meta` file (its GUID is how scenes find the asset). New scripts: commit the `.meta` Unity makes together with the `.cs`.
- Logic that can be pure C# goes in `Core/` so CI tests it.
- CSV columns are defined in `docs/PLAN.md` section 2. Change the spec, the C# writer and `analysis/` together.
- Commit messages: short, lowercase is fine (e.g. `add lever trigger + gate open`).
