using System;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Thuong;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ThuongPhaseOneChecks
{
    private sealed class DeathObserver : BaseRole
    {
        public int calls;
        public DeathObserver(PlayerData owner) : base(owner) { }
        public override void OnDeath() { calls++; }
    }

    [MenuItem("Tools/Thuong/Check Phase 1 (Alive-Dead)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run outside Play Mode.");

        var oldPlayers = PlayerManager.Instance;
        var oldGame = GameRoleManager.Instance;
        var oldVotes = VoteManager.Instance;
        var oldRoles = RoleManager.Instance;
        var oldDeath = DeathResolver.Instance;
        var oldTasks = TaskManager.Instance;
        var oldNight = NightManager.Instance;
        var scene = EditorSceneManager.NewPreviewScene();
        TaskData task = null;
        try
        {
            var players = Component<PlayerManager>(scene);
            var game = Component<GameRoleManager>(scene);
            var votes = Component<VoteManager>(scene);
            var roles = Component<RoleManager>(scene);
            var death = Component<DeathResolver>(scene);
            var tasks = Component<TaskManager>(scene);
            var night = Component<NightManager>(scene);
            PlayerManager.Instance = players;
            GameRoleManager.Instance = game;
            VoteManager.Instance = votes;
            RoleManager.Instance = roles;
            DeathResolver.Instance = death;
            TaskManager.Instance = tasks;
            NightManager.Instance = night;
            players.CreateTestPlayer(5);

            int notifications = 0;
            players.AliveStateChanged += _ => notifications++;
            Require(players.GetAlivePlayers().Count() == 5, "New players start alive");
            Require(!players.IsAlive(99) && !players.SetAliveState(99, false), "Unknown ID is rejected");
            Require(!JsonUtility.FromJson<PlayerData>("{\"isAlive\":false}").isAlive,
                "Legacy serialized isAlive=false remains dead");

            PlayerData actor = players.GetplayerByID(0);
            actor.roleType = RoleType.Seer;
            roles.playerRoles[0] = new SeerRole(actor);
            var observer = new DeathObserver(actor);
            roles.playerRoles[0] = observer;

            var canvas = Component<Canvas>(scene);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var hud = canvas.gameObject.AddComponent<GameHUD>();
            var roster = canvas.gameObject.AddComponent<PlayerRosterUI>();
            roster.Initialize(hud);
            Require(StateLabel(canvas, 0).text == "ALIVE", "Roster shows initial state");

            var movement = Component<PlayerMovement>(scene);
            movement.playerID = 0;
            movement.SendMessage("Awake");
            var body = movement.GetComponent<Rigidbody2D>();
            game.currentState = GameState.Day;
            body.linearVelocity = new Vector2(3, 0);
            Require(death.TryKillPlayer(0, DeathCause.Monster), "Death changes the canonical state");
            Require(!players.IsAlive(0) && !movement.IsAlive, "Movement reads the same dead state");
            movement.SendMessage("FixedUpdate");
            Require(body.linearVelocity == Vector2.zero, "Dead movement stops");
            Require(StateLabel(canvas, 0).text == "DEAD", "Roster updates from the state event");
            Require(!death.TryKillPlayer(0, DeathCause.Monster) && notifications == 1 && observer.calls == 1,
                "Repeated death does not repeat notifications or OnDeath");

            task = ScriptableObject.CreateInstance<TaskData>();
            tasks.currentTasks.Add(task);
            Require(!tasks.TryCompleteTask(0, task) && !tasks.TryCompleteTask(99, task),
                "Dead or unknown actor cannot complete a task through the API");
            tasks.CompleteTask(task);
            Require(tasks.progress == 0 && !tasks.IsCompleted(task), "Legacy task entry also rejects dead local actor");

            game.currentState = GameState.Voting;
            votes.StartVote();
            Require(!votes.TryVote(0, 1) && !votes.TryVote(1, 0) && votes.GetVotedTarget(0) == -1,
                "Dead voter and dead target are rejected");
            var row = Component<VoteButton>(scene);
            row.voterID = 1;
            row.targetID = 0;
            var label = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(row.transform, false);
            row.Refresh();
            Require(!row.GetComponent<Button>().interactable && label.GetComponent<TMP_Text>().text.Contains("DEAD"),
                "Vote row labels and disables the dead target");

            game.currentState = GameState.Night;
            Require(!roles.UseNightAbility(0, 1, out _), "Dead actor cannot use ability");
            PlayerData livingSeer = players.GetplayerByID(1);
            livingSeer.roleType = RoleType.Seer;
            roles.playerRoles[1] = new SeerRole(livingSeer);
            Require(!roles.UseNightAbility(1, 0, out _), "Ability rejects dead target");

            actor.hasVoted = actor.hasUseNightAction = true;
            Require(players.SetAliveState(0, true) && notifications == 2 && players.GetAlivePlayers().Count() == 5,
                "Restore life updates every reader and emits one event");
            Require(!actor.hasVoted && !actor.hasUseNightAction && StateLabel(canvas, 0).text == "ALIVE",
                "Restored actor has no stale action flags and UI is alive");
            roles.playerRoles[0] = new SeerRole(actor);
            Require(roles.UseNightAbility(0, 1, out _), "Restored actor can use ability");
            game.currentState = GameState.Day;
            Require(tasks.TryCompleteTask(0, task) && tasks.progress == 10, "Living actor can complete a task");
            movement.SetCanMove(true);
            typeof(PlayerMovement).GetField("moveInput", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(movement, Vector2.right);
            movement.SendMessage("FixedUpdate");
            Require(body.linearVelocity.x > 0, "Restored actor can move");
            game.currentState = GameState.Voting;
            votes.StartVote();
            Require(votes.TryVote(0, 1), "Living voter succeeds");
            row.Refresh();
            Require(row.GetComponent<Button>().interactable && label.GetComponent<TMP_Text>().text.Contains("ALIVE"),
                "Living target row becomes usable again");

            players.SetAliveState(2, false);
            canvas.transform.Find("Player Roster Button").GetComponent<Button>().onClick.Invoke();
            SavePreview(canvas, scene, 1600, 900, "Logs/thuong-phase1-roster.png");
            SavePreview(canvas, scene, 960, 540, "Logs/thuong-phase1-roster-small.png");
            CheckAbilityUI(scene, players, game, roles, death);
            players.CreateTestPlayer(20);
            roster.Refresh();
            Require(canvas.transform.Find("Player Roster Window/Player Roster Panel/Viewport/Content").childCount == 20 &&
                StateLabel(canvas, 19).text == "ALIVE", "Roster includes the full lobby after reset");
            Debug.Log("THUONG_PHASE1_PASSED: state migration, death, revival, movement, task, vote, ability and roster.");
        }
        finally
        {
            if (task != null) UnityEngine.Object.DestroyImmediate(task);
            EditorSceneManager.ClosePreviewScene(scene);
            PlayerManager.Instance = oldPlayers;
            GameRoleManager.Instance = oldGame;
            VoteManager.Instance = oldVotes;
            RoleManager.Instance = oldRoles;
            DeathResolver.Instance = oldDeath;
            TaskManager.Instance = oldTasks;
            NightManager.Instance = oldNight;
        }
    }

    private static void CheckAbilityUI(Scene scene, PlayerManager players, GameRoleManager game,
        RoleManager roles, DeathResolver death)
    {
        var ui = Component<RoleAbilityUI>(scene);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Scenes/Thuong/Prototype/Art/PrototypeFont.asset");
        ui.Initialize(font, 0);
        var abilityCanvas = Field<Canvas>(ui, "abilityCanvas");
        SceneManager.MoveGameObjectToScene(abilityCanvas.gameObject, scene);
        PlayerData actor = players.GetplayerByID(0);
        actor.hasUseNightAction = false;
        game.currentState = GameState.Night;
        ui.SendMessage("Update");
        Invoke(ui, "OpenTargets");
        var buttons = Field<System.Collections.Generic.Dictionary<int, Button>>(ui, "targetButtons");
        players.SetAliveState(1, false);
        ui.SendMessage("Update");
        Require(!buttons[1].interactable, "Already-open ability target disables after death");
        players.SetAliveState(1, true);
        ui.SendMessage("Update");
        Require(buttons[1].interactable, "Revived target enables in the open list");
        players.SetAliveState(0, false);
        ui.SendMessage("Update");
        Require(!Field<GameObject>(ui, "targetView").activeSelf &&
            !Field<Button>(ui, "useButton").interactable &&
            Field<TMP_Text>(ui, "playerLabel").text.Contains("DEAD"),
            "Dead local actor closes selection and disables its living-only action");
        players.SetAliveState(0, true);
        ui.SendMessage("Update");
        Require(Field<Button>(ui, "useButton").interactable &&
            Field<TMP_Text>(ui, "playerLabel").text.Contains("ALIVE"), "Revival refreshes the ability UI");

        actor.roleType = RoleType.Hunter;
        roles.playerRoles[0] = new HunterRole(actor);
        Require(death.TryKillPlayer(0, DeathCause.Monster) && actor.hasHunterTrap,
            "Current main's Hunter death callback still grants its trap");
        game.currentState = GameState.Discussion;
        ui.SendMessage("Update");
        Require(Field<Button>(ui, "hunterTrapButton").interactable,
            "Hunter's intentional post-death action remains enabled");
        Invoke(ui, "OpenHunterTrapTargets");
        ui.SendMessage("Update");
        Require(buttons[1].interactable, "Dead Hunter can select a living trap target");
        Invoke(ui, "SelectTarget", 1);
        Require(actor.hunterTargetID == 1 && !actor.hasHunterTrap && !actor.isAlive,
            "Hunter trap executes without reviving its actor");
        UnityEngine.Object.DestroyImmediate(ui.gameObject);
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);

    private static void Invoke(object instance, string name, params object[] arguments) =>
        instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(instance, arguments);

    private static T Component<T>(Scene scene) where T : UnityEngine.Component
    {
        var go = new GameObject(typeof(T).Name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.AddComponent<T>();
    }

    private static Text StateLabel(Canvas canvas, int id) => canvas.transform
        .Find("Player Roster Window/Player Roster Panel/Viewport/Content/Roster Player " + id + "/State")
        .GetComponent<Text>();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Phase 1 failed: " + message);
    }

    private static void SavePreview(Canvas canvas, Scene scene, int width, int height, string path)
    {
        var camera = Component<Camera>(scene);
        var texture = new RenderTexture(width, height, 24);
        var previous = RenderTexture.active;
        var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = new Vector3(0, 0, -10);
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .09f, .1f);
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            Color32[] pixels = screenshot.GetPixels32();
            Color32 background = pixels[0];
            Require(pixels.Count(pixel => Math.Abs(pixel.r - background.r) +
                Math.Abs(pixel.g - background.g) + Math.Abs(pixel.b - background.b) > 15) > 1000,
                "Roster screenshot contains visible UI");
            System.IO.File.WriteAllBytes(path, screenshot.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = null;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }
}
