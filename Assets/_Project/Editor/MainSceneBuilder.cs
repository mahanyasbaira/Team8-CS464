using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Team8 > 4 Build Main Scene
// Builds Scenes/Main.unity from the room prefabs + PlayerRig and wires everything
// the TrialManager needs, plus the operator panel in the lobby (issues #15, #16).
// Rooms stay prefab instances, so later edits to a room prefab still show up here.
public static class MainSceneBuilder
{
    const string MainPath = "Assets/_Project/Scenes/Main.unity";
    const string RoomFolder = "Assets/_Project/Prefabs/Rooms";
    const string RigPath = "Assets/_Project/Prefabs/Player/PlayerRig.prefab";

    [MenuItem("Team8/4 Build Main Scene", priority = 4)]
    public static void Build()
    {
        var names = new[] { "Room_1", "Room_2", "Room_3", "PracticeRoom", "Lobby" };
        var missing = names.Where(n => Load(RoomFolder + "/" + n + ".prefab") == null).ToList();
        if (Load(RigPath) == null) missing.Add("PlayerRig");
        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("Team8", "Missing prefabs: " + string.Join(", ", missing) +
                "\nRun Team8 > 2 Build Greybox and Team8 > 3 Build Player Rig first.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainPath) != null &&
            !EditorUtility.DisplayDialog("Team8", "Main.unity already exists. Rebuild it from the prefabs?", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        light.shadows = LightShadows.None; // cheap on Quest

        var rooms = names.Select(n => (GameObject)PrefabUtility.InstantiatePrefab(Load(RoomFolder + "/" + n + ".prefab"))).ToArray();
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(Load(RigPath));

        var managers = new GameObject("StudyManagers");
        var session = managers.AddComponent<SessionConfig>();
        var layouts = managers.AddComponent<LayoutLoader>();
        var trials = managers.AddComponent<TrialManager>();

        WireRig(rig, rooms[0]);
        WireLayouts(layouts, rooms);
        WireTrials(trials, session, layouts, rig, rooms);

        var events = new GameObject("EventSystem");
        events.AddComponent<EventSystem>();
        events.AddComponent<XRUIInputModule>();

        BuildOperatorPanel(rooms[4], session, trials);
        BuildStartButton(rooms[3], trials);

        EditorSceneManager.SaveScene(scene, MainPath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainPath, true) };
        Debug.Log("[Main] built " + MainPath + " and set it as the only build scene");
    }

    static void WireRig(GameObject rig, GameObject room1)
    {
        var logger = rig.GetComponent<PositionLogger>();
        logger.zones = Object.FindObjectsByType<RoomZone>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(z => z.roomIndex).ToArray();
        logger.gridOrigin = room1.transform; // room 1's outer corner = (0, 0)
        PrefabUtility.RecordPrefabInstancePropertyModifications(logger);
    }

    static void WireLayouts(LayoutLoader layouts, GameObject[] rooms)
    {
        layouts.rooms = new LayoutLoader.RoomLayout[3];
        for (int i = 0; i < 3; i++)
        {
            var t = rooms[i].transform;
            layouts.rooms[i] = new LayoutLoader.RoomLayout
            {
                name = "Room " + (i + 1),
                lever = rooms[i].GetComponentInChildren<LeverController>(true).transform,
                spotA = Find(t, "LeverSpot_A"),
                spotB = Find(t, "LeverSpot_B"),
                spotC = Find(t, "LeverSpot_C"),
            };
        }
    }

    static void WireTrials(TrialManager trials, SessionConfig session, LayoutLoader layouts, GameObject rig, GameObject[] rooms)
    {
        trials.session = session;
        trials.layouts = layouts;
        trials.locomotion = rig.GetComponent<LocomotionSwitcher>();
        trials.logger = rig.GetComponent<PositionLogger>();
        trials.trail = rig.GetComponentInChildren<TeleportTrail>(true);
        trials.rig = rig.GetComponent<XROrigin>();

        trials.levers = new LeverController[3];
        for (int i = 0; i < 3; i++)
        {
            trials.levers[i] = rooms[i].GetComponentInChildren<LeverController>(true);
        }
        trials.gates = new[]
        {
            Find(rooms[0].transform, "Gate_1").GetComponent<GateController>(),
            Find(rooms[1].transform, "Gate_2").GetComponent<GateController>(),
        };
        trials.exitDoor = rooms[2].GetComponentInChildren<ExitDoor>(true);
        trials.practiceLever = rooms[3].GetComponentInChildren<LeverController>(true);

        trials.startSpawn = Find(rooms[0].transform, "StartSpawn");
        trials.practiceSpawn = Find(rooms[3].transform, "PracticeSpawn");
        trials.lobbySpawn = Find(rooms[4].transform, "LobbySpawn");
    }

    // ---------- UI ----------

    static void BuildOperatorPanel(GameObject lobby, SessionConfig session, TrialManager trials)
    {
        var spot = Find(lobby.transform, "OperatorPanelSpot");
        var canvas = WorldCanvas("OperatorPanel", spot, new Vector2(900, 700));
        var panel = canvas.gameObject.AddComponent<OperatorPanel>();
        panel.session = session;
        panel.trials = trials;

        var info = Label(canvas.transform, "Info", "", new Vector2(0, 110), new Vector2(860, 440), 26, TextAnchor.UpperLeft);
        panel.info = info;

        // two rows of buttons under the text
        MakeButton(canvas.transform, "< ID", new Vector2(-330, -180), panel.PrevParticipant);
        MakeButton(canvas.transform, "ID >", new Vector2(-110, -180), panel.NextParticipant);
        MakeButton(canvas.transform, "< Trial", new Vector2(110, -180), panel.StartTrialDown);
        MakeButton(canvas.transform, "Trial >", new Vector2(330, -180), panel.StartTrialUp);
        MakeButton(canvas.transform, "Practice", new Vector2(-220, -280), panel.Practice);
        MakeButton(canvas.transform, "Start", new Vector2(0, -280), panel.StartTrial);
        MakeButton(canvas.transform, "Abort", new Vector2(220, -280), panel.Abort);
    }

    static void BuildStartButton(GameObject practiceRoom, TrialManager trials)
    {
        var spot = Find(practiceRoom.transform, "StartButtonSpot");
        var canvas = WorldCanvas("PracticeStartButton", spot, new Vector2(500, 260));
        Label(canvas.transform, "Hint", "Ready? Press Start.", new Vector2(0, 70), new Vector2(480, 80), 34, TextAnchor.MiddleCenter);
        MakeButton(canvas.transform, "Start", new Vector2(0, -40), trials.StartTrial, new Vector2(300, 100));
    }

    static Canvas WorldCanvas(string name, Transform spot, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetPositionAndRotation(spot.position, spot.rotation);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one * 0.002f; // 900 px wide = 1.8 m
        go.AddComponent<TrackedDeviceGraphicRaycaster>();

        var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
        bg.transform.SetParent(go.transform, false);
        Stretch(bg.rectTransform);
        bg.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        return canvas;
    }

    static Text Label(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = size;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = Color.white;
        t.text = text;
        return t;
    }

    static void MakeButton(Transform parent, string label, Vector2 pos, UnityAction onClick, Vector2? size = null)
    {
        var go = new GameObject("Button " + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size ?? new Vector2(200, 80);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.45f, 0.85f);
        var button = go.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(button.onClick, onClick);

        var text = Label(go.transform, "Label", label, Vector2.zero, rt.sizeDelta, 30, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ---------- helpers ----------

    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    static Transform Find(Transform root, string name)
    {
        var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
        if (t == null) throw new System.Exception("[Main] couldn't find '" + name + "' under " + root.name);
        return t;
    }
}
