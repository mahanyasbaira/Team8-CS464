# Building the project (Team8 menu)

Everything is built from the **Team8** menu in Unity, in this order. Each step is safe to re-run.

| Step | Menu | What it does | Who |
|---|---|---|---|
| 1 | Team8 > 1 Setup Project | Installs OpenXR, Unity OpenXR: Meta, Test Framework; imports XRI Starter Assets + XR Device Simulator; new Input System; Android app id. Then opens XR Plug-in Management. | once per project (Mahanyas) |
| 1b | (by hand) | XR Plug-in Management > Android tab: tick **OpenXR**, tick **Meta Quest** feature group, Project Validation > **Fix All**. Restart Unity. | once |
| 2 | Team8 > 2 Build Greybox | Room_1/2/3, PracticeRoom, Lobby prefabs + `Sandbox_Greybox` scene. Never overwrites existing room prefabs. | once |
| 2b | Team8 > 2b Dress Rooms | Themes the rooms (English bedroom / antique attic / Victorian kitchen): hiding kits around each lever spot with the same geometry in every room, a smaller lever, furniture along the walls (same count per room), wall decor, 6 grabbable decoys. Uses the CC0 kit from `tools/fetch_assets.py` when it is there, simple built shapes otherwise. Skips rooms that are already dressed. | after the kit is fetched |
| 2b | Team8 > 2b Redo Generated Rooms | Deletes the rooms the dresser made (`DressedRoom.generated` ticked) and runs 2 + 2b again. Use this once to swap the old warehouse/office/workshop rooms for the new themes. Hand-dressed rooms are kept. | when the dresser changes |
| 2c | Team8 > 2c Build Building | `Building.prefab`: south corridor, front hall, east wing, closed doors (same in every room), ceilings, roof, light panels. Room prefabs are not touched. | any time |
| - | Team8 > Check Triangle Budget | Room + building must stay under 750k triangles; drops decoy props first if a room is over. | after dressing |
| 3 | Team8 > 3 Build Player Rig | `PlayerRig.prefab` (variant of the Starter Assets rig) with LocomotionSwitcher, PositionLogger, TeleportTrail; snap turn 45 on the right stick only; vignette off. | Mahanyas |
| 4 | Team8 > 4 Build Main Scene | `Main.unity`: rooms + rig + StudyManagers + operator panel, all references wired, set as the only build scene. | Mahanyas only |
| 4b | Team8 > 4b Bake Lighting | Area lights under every light panel, light probes, box-projected reflection probes, lightmaps (Progressive CPU), occlusion culling, Quest URP settings. No light has real-time shadows. | after 4 |
| 5 | Team8 > 5 Build APK | Quest settings (IL2CPP, ARM64, min API 32) and builds `Builds/Team8-v<version>.apk`. | Mahanyas |

## Testing in the Editor without a headset
*Edit > Project Settings > XR Interaction Toolkit*: tick **Use XR Device Simulator in scenes**. Then open `Main.unity` and press Play. Keyboard/mouse drive the head and controllers (the simulator shows its key help in the Game view).

## Release checklist
1. *Edit > Project Settings > Player*: bump **Version** (e.g. 0.1.0 -> 0.2.0). It shows on the operator panel and is written to every CSV.
2. Team8 > 5 Build APK.
3. GitHub > Releases > new release `v0.2.0`, attach the APK (never commit APKs).
4. Tell Ashley; she installs it with MQDH and runs the device checklist (issue #18).

## Building in the cloud (GitHub Actions)
`.github/workflows/unity-build.yml` runs the same steps on GitHub's servers with no screen:
fetch the CC0 kit, SetupPackages, Configure XR (OpenXR + Meta Quest), BuildWorld (greybox, dressing, building, budget check, rig, Main.unity, light bake), EditMode tests, APK.
It uploads the APK and a top-down map (`layout.png`) as the run's artifact and commits the generated Unity files back to the branch, so on your laptop you just `git pull` and open `Main.unity`.

One-time setup (repo owner):
1. On a Mac where you've signed in to Unity: copy the contents of `/Library/Application Support/Unity/Unity_lic.ulf`.
2. GitHub > Settings > Secrets and variables > Actions > New repository secret: `UNITY_LICENSE` (the file contents), `UNITY_EMAIL`, `UNITY_PASSWORD`.
3. Actions > unity-build > Run workflow (or push to a `baira/...` branch).

The first run takes 20-40 minutes (it downloads the Unity image); later runs reuse the cached `Library/`. Main.unity is regenerated and re-baked on every run. Tick **rebuild** when running it by hand to also regenerate the dressed rooms (only ones marked `DressedRoom.generated`) and the PlayerRig.
