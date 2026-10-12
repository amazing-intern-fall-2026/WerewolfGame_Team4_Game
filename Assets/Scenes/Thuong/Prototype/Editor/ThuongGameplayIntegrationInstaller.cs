using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Thuong;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ThuongGameplayIntegrationInstaller
{
    public const string Root = ThuongPrototypeBuilder.Root + "/Integration";
    public const string CatalogPath = Root + "/GameplayRoles.asset";
    [MenuItem("Tools/Thuong/Integrate Phase 1-4 Into Gameplay")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Logs");
        string backup = "Logs/ThuongGameplayPrototype-before-integration-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".unity";
        File.Copy(ThuongPrototypeBuilder.ScenePath, backup, false);
        var scene = EditorSceneManager.OpenScene(ThuongPrototypeBuilder.ScenePath);
        var game = One<GameRoleManager>(scene);
        if (game.manualLogicPrototype || All<ThuongLogicMatchController>(scene).Any())
            throw new InvalidOperationException("Gameplay must keep one automatic GameRoleManager.");
        var players = One<PlayerManager>(scene);
        var roles = One<RoleManager>(scene);
        var death = One<DeathResolver>(scene);
        var night = One<NightManager>(scene);
        var stations = All<TaskStation>(scene).ToArray();
        int lobbyCount = players.prototypeLobbySize;
        var durations = (game.roleRevealDuration, game.nightDuration, game.discussionDuration, game.votingDuration);
        EnsureFolder(Root); EnsureFolder(Root + "/Roles"); EnsureFolder(Root + "/Prefabs");
        var catalog = AssetDatabase.LoadAssetAtPath<ThuongRoleDefinitionCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ThuongRoleDefinitionCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        AddDefinition(catalog, "Villager", RoleType.Villager, FactionType.Villager, AbilityType.None,
            "DÂN LÀNG", "NỘI TẠI", "Hoàn thành nhiệm vụ, tham gia thảo luận và bỏ phiếu.");
        AddDefinition(catalog, "DogSpirit", RoleType.DogSpirit, FactionType.Monster, AbilityType.Kill,
            "DOGSPIRIT", "TẤN CÔNG", "Cả phe sói có một mục tiêu tấn công mỗi đêm. Lựa chọn gửi sau cùng thay lựa chọn trước.");
        AddDefinition(catalog, "Guardian", RoleType.VillageGuardian, FactionType.Villager, AbilityType.Protect,
            "BẢO VỆ", "BẢO VỆ", "Bảo vệ một người khác trong đêm. Không chọn cùng người trong hai đêm liên tiếp.");
        if (!catalog.Validate(out string error)) throw new InvalidOperationException(error);
        roles.roleDefinitions = catalog;
        var systems = game.transform.parent;
        var status = All<StatusEffectSystem>(scene).SingleOrDefault() ?? Add<StatusEffectSystem>(systems);
        var abilities = All<AbilitySystem>(scene).SingleOrDefault() ?? Add<AbilitySystem>(systems);
        status.deathSystem = death; abilities.phaseController = game; abilities.nightSystem = night;
        game.statusEffects = status; game.abilities = abilities;

        var player = One<PlayerMovement>(scene);
        PrepareCharacter(player, One<ThuongFourRoleOfflineTest>(scene));
        string taskPrefab = Root + "/Prefabs/TaskStation.prefab";
        if (!File.Exists(taskPrefab) && stations.Length > 0)
            PrefabUtility.SaveAsPrefabAsset(stations[0].gameObject, taskPrefab);
        if (players.prototypeLobbySize != lobbyCount ||
            durations != (game.roleRevealDuration, game.nightDuration, game.discussionDuration, game.votingDuration) ||
            stations.Any(station => station == null || station.task == null || station.player != player.transform))
            throw new InvalidOperationException("Integration must preserve lobby, timers and station references.");
        EditorUtility.SetDirty(game); EditorUtility.SetDirty(roles);
        EditorUtility.SetDirty(status); EditorUtility.SetDirty(abilities); EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save gameplay scene.");
        Debug.Log("THUONG_GAMEPLAY_INTEGRATED: original scene upgraded; backup " + backup);
    }

    private static void AddDefinition(ThuongRoleDefinitionCatalog catalog, string filename, RoleType type,
        FactionType faction, AbilityType ability, string name, string skill, string description)
    {
        if (catalog.Find(type) != null) return;
        string path = Root + "/Roles/" + filename + ".asset";
        var definition = AssetDatabase.LoadAssetAtPath<ThuongRoleDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
            definition.roleID = "gameplay-" + filename.ToLowerInvariant(); definition.displayName = name;
            definition.description = description; definition.abilityName = skill;
            definition.roleType = type; definition.faction = faction; definition.abilityType = ability;
            // Gameplay Guardian keeps its legacy self rule; the test Doctor is configured independently.
            definition.allowSelfTarget = false;
            definition.targetFactionRule = type == RoleType.DogSpirit ? TargetFactionRule.OtherFaction : TargetFactionRule.Any;
            definition.sharedNightKill = type == RoleType.DogSpirit;
            definition.forbidConsecutiveNightTarget = type == RoleType.VillageGuardian;
            definition.effectTicksOnPhase = GamePhase.Discussion;
            definition.abilityIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ThuongPrototypeBuilder.Root + "/Art/Resources/RoleAbilityIcon.png");
            AssetDatabase.CreateAsset(definition, path);
        }
        catalog.definitions.Add(definition);
    }
    private static void PrepareCharacter(PlayerMovement player, ThuongFourRoleOfflineTest spawner)
    {
        var appearance = AssetDatabase.LoadAssetAtPath<ThuongCharacterAppearance>(Root + "/CharacterAppearance.asset");
        var oldRenderer = player.GetComponent<SpriteRenderer>();
        if (appearance == null)
        {
            appearance = ScriptableObject.CreateInstance<ThuongCharacterAppearance>();
            appearance.sprite = oldRenderer.sprite; appearance.tint = oldRenderer.color;
            AssetDatabase.CreateAsset(appearance, Root + "/CharacterAppearance.asset");
        }
        var view = player.GetComponentInChildren<ThuongCharacterView>(true);
        if (view == null)
        {
            var visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(Animator), typeof(ThuongCharacterView));
            visual.transform.SetParent(player.transform, false);
            view = visual.GetComponent<ThuongCharacterView>();
            view.spriteRenderer = visual.GetComponent<SpriteRenderer>();
            view.spriteRenderer.sharedMaterial = oldRenderer.sharedMaterial;
            view.spriteRenderer.sortingLayerID = oldRenderer.sortingLayerID;
            view.spriteRenderer.sortingOrder = oldRenderer.sortingOrder;
            view.animator = visual.GetComponent<Animator>();
            oldRenderer.enabled = false;
        }
        view.appearance = appearance; view.ApplyAppearance();
        string path = Root + "/Prefabs/GameplayPlayer.prefab";
        if (!File.Exists(path)) PrefabUtility.SaveAsPrefabAsset(player.gameObject, path);
        spawner.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        EditorUtility.SetDirty(spawner);
    }
    private static T Add<T>(Transform parent) where T : Component
    { var root = new GameObject(typeof(T).Name); root.transform.SetParent(parent); return root.AddComponent<T>(); }
    private static System.Collections.Generic.IEnumerable<T> All<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    private static T One<T>(Scene scene) where T : Component => All<T>(scene).Single();
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
