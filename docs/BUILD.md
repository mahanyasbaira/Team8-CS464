# Building the project (Team8 menu)

Everything is built from the **Team8** menu in Unity, in this order. Each step is safe to re-run.

| Step | Menu | What it does | Who |
|---|---|---|---|
| 1 | Team8 > 1 Setup Project | Installs OpenXR, Unity OpenXR: Meta, Test Framework; imports XRI Starter Assets + XR Device Simulator; new Input System; Android app id. Then opens XR Plug-in Management. | once per project (Mahanyas) |
| 1b | (by hand) | XR Plug-in Management > Android tab: tick **OpenXR**, tick **Meta Quest** feature group, Project Validation > **Fix All**. Restart Unity. | once |
| 2 | Team8 > 2 Build Greybox | Room_1/2/3, PracticeRoom, Lobby prefabs + `Sandbox_Greybox` scene. Never overwrites existing room prefabs. | once; rooms then owned by Yulisa/Sai |
| 3 | Team8 > 3 Build Player Rig | `PlayerRig.prefab` (variant of the Starter Assets rig) with LocomotionSwitcher, PositionLogger, TeleportTrail; snap turn 45 on the right stick only; vignette off. | Mahanyas |
| 4 | Team8 > 4 Build Main Scene | `Main.unity`: rooms + rig + StudyManagers + operator panel, all references wired, set as the only build scene. | Mahanyas only |
| 5 | Team8 > 5 Build APK | Quest settings (IL2CPP, ARM64, min API 32) and builds `Builds/Team8-v<version>.apk`. | Mahanyas |

## Testing in the Editor without a headset
*Edit > Project Settings > XR Interaction Toolkit*: tick **Use XR Device Simulator in scenes**. Then open `Main.unity` and press Play. Keyboard/mouse drive the head and controllers (the simulator shows its key help in the Game view).

## Release checklist
1. *Edit > Project Settings > Player*: bump **Version** (e.g. 0.1.0 -> 0.2.0). It shows on the operator panel and is written to every CSV.
2. Team8 > 5 Build APK.
3. GitHub > Releases > new release `v0.2.0`, attach the APK (never commit APKs).
4. Tell Ashley; she installs it with MQDH and runs the device checklist (issue #18).
