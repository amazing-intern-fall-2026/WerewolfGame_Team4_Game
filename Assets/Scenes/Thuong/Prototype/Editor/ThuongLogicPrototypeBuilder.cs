using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class ThuongLogicPrototypeBuilder
{
    public const string Root = "Assets/Scenes/Thuong/Prototype/LogicPhase1To4";
    public const string ScenePath = Root + "/WerewolfLogicPrototype.unity";
    [MenuItem("Tools/Thuong/Create Logic Phase 1-4 Scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Logic prototype exists; open it instead of overwriting.");
        Directory.CreateDirectory(Root + "/Roles");
        AssetDatabase.Refresh();
        var doctor = Role("Doctor", RoleType.VillageGuardian, FactionType.Villager, AbilityType.Protect);
        doctor.allowSelfTarget = true;
        doctor.effectTicksOnPhase = GamePhase.Discussion;
        var wolf = Role("Wolf", RoleType.DogSpirit, FactionType.Monster, AbilityType.Kill);
        wolf.targetFactionRule = TargetFactionRule.OtherFaction;
        var villager = Role("Villager", RoleType.Villager, FactionType.Villager, AbilityType.None);
        EditorUtility.SetDirty(doctor); EditorUtility.SetDirty(wolf);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.transform.position = new Vector3(0,0,-10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f,.09f,.115f);
        var systems = new GameObject("Systems").transform;
        var players = Add<PlayerManager>(systems);
        players.CreateTestPlayer(4);
        for (int i = 0; i < 4; i++)
        {
            var player = players.players[i];
            player.playerName = $"Player P{i + 1:00}";
            player.roleDefinition = i == 0 ? doctor : i == 1 ? wolf : villager;
            player.roleType = player.roleDefinition.roleType;
            player.faction = player.roleDefinition.faction;
        }
        var phase = Add<GameRoleManager>(systems);
        phase.manualLogicPrototype = true; phase.currentPhase = GamePhase.Setup; phase.currentDay = 0;
        var roles = Add<RoleManager>(systems);
        var death = Add<DeathResolver>(systems);
        var status = Add<StatusEffectSystem>(systems); status.deathSystem = death;
        var night = Add<NightManager>(systems);
        var ability = Add<AbilitySystem>(systems); ability.phaseController = phase; ability.nightSystem = night;
        var vote = Add<VoteManager>(systems);
        var win = Add<WinConditionManager>(systems);
        var winData = new SerializedObject(win);
        winData.FindProperty("useEliminationWin").boolValue = true;
        winData.ApplyModifiedPropertiesWithoutUndo();
        var match = Add<ThuongLogicMatchController>(systems);
        match.players = players; match.phaseController = phase; match.roles = roles; match.deathSystem = death;
        match.statusSystem = status; match.abilitySystem = ability; match.nightSystem = night;
        match.voteSystem = vote; match.winSystem = win;
        var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
        var ui = canvas.AddComponent<ThuongLogicPrototypeUI>(); ui.match = match; ui.BuildUI();
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("THUONG_LOGIC_SCENE_CREATED: " + ScenePath);
    }
    private static T Add<T>(Transform parent) where T : Component
    { var go = new GameObject(typeof(T).Name); go.transform.SetParent(parent); return go.AddComponent<T>(); }
    private static ThuongRoleDefinition Role(string name, RoleType type, FactionType faction, AbilityType ability)
    {
        string path = Root + "/Roles/" + name + ".asset";
        var role = AssetDatabase.LoadAssetAtPath<ThuongRoleDefinition>(path);
        if (role != null) return role;
        role = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
        role.roleID = name.ToLowerInvariant(); role.displayName = name;
        role.roleType = type; role.faction = faction; role.abilityType = ability;
        AssetDatabase.CreateAsset(role, path);
        return role;
    }
}
