using Assets.Scripts.Thuong;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Attached to Systems in ThuongGameplayPrototype. PlayerManager's Inspector
// lobby size controls how many characters are spawned at runtime.
[DefaultExecutionOrder(1000)]
public sealed class ThuongFourRoleOfflineTest : MonoBehaviour
{
    private static readonly Key[] SelectPlayerKeys =
    {
        Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6,
        Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12
    };

    private GameHUD hud;
    private RoleAbilityUI abilityUI;
    private PlayerMovement[] scenePlayers;
    private PrototypeControls[] playerControls;
    private TaskStation[] taskStations;
    private VoteButton[] voteButtons;
    private int localPlayerID = -1;
    private int playerCount;
    private double stressSampleStarted = -1;
    private int stressSampleFrames;
    private float stressWorstFrameSeconds;

    private void Awake()
    {
        // Runs after PlayerManager.Awake and before GameRoleManager.Start.
        // Read the Inspector value instead of forcing the four-role example.
        PlayerManager players = PlayerManager.Instance;
        if (players == null || players.prototypeLobbySize < 1)
        {
            Debug.LogError("[Thuong offline test] Prototype Lobby Size must be at least 1.");
            enabled = false;
            return;
        }

        playerCount = players.prototypeLobbySize;
        players.CreateTestPlayer(playerCount);
    }

    private void Start()
    {
        double spawnStarted = Time.realtimeSinceStartupAsDouble;
        PlayerManager players = PlayerManager.Instance;
        RoleManager roles = RoleManager.Instance;
        if (players == null || roles == null ||
            players.players == null || players.players.Count != playerCount)
        {
            Debug.LogError("[Thuong offline test] Lobby count does not match Prototype Lobby Size.");
            enabled = false;
            return;
        }

        // GameRoleManager.Start has already called RoleManager.AssignRole.
        // The prototype only creates scene characters; it never overrides roles.
        for (int id = 0; id < playerCount; id++)
        {
            PlayerData player = players.GetplayerByID(id);
            if (player == null || !roles.playerRoles.ContainsKey(id))
            {
                Debug.LogError("[Thuong offline test] Missing assigned role for player ID " + id);
                enabled = false;
                return;
            }
        }

        if (!SpawnScenePlayers())
        {
            enabled = false;
            return;
        }

        hud = FindAnyObjectByType<GameHUD>();
        abilityUI = hud != null ? hud.GetComponent<RoleAbilityUI>() : null;
        // MeetingPanel starts inactive, so include its existing vote buttons.
        voteButtons = FindObjectsByType<VoteButton>(FindObjectsInactive.Include);
        taskStations = FindObjectsByType<TaskStation>();
        SetLocalPlayer(0);
        Debug.Log("[Thuong offline test] Spawned " + playerCount +
                  " scene players with random roles from RoleManager. F1-F12 switch control.");
        if (playerCount >= 100)
        {
            Debug.Log($"[Thuong stress] Spawn {playerCount} scene players: " +
                      $"{(Time.realtimeSinceStartupAsDouble - spawnStarted) * 1000.0:F1} ms. " +
                      "Sampling frame rate for 5 seconds.");
            stressSampleStarted = Time.realtimeSinceStartupAsDouble;
        }
    }

    private bool SpawnScenePlayers()
    {
        PlayerMovement template = FindAnyObjectByType<PlayerMovement>();
        PrototypeControls templateControls =
            template != null ? template.GetComponent<PrototypeControls>() : null;
        if (template == null || templateControls == null)
        {
            Debug.LogError("[Thuong 4-role test] The prototype Player is missing PlayerMovement or PrototypeControls.");
            return false;
        }

        scenePlayers = new PlayerMovement[playerCount];
        playerControls = new PrototypeControls[playerCount];
        Vector3 center = template.transform.position;
        int columns = Mathf.Min(5, playerCount);
        int rows = Mathf.CeilToInt(playerCount / (float)columns);

        // Clone the existing character, including its sprite, collider, Rigidbody2D
        // and movement script. Keep input off until one character is selected.
        templateControls.enabled = false;
        for (int id = 0; id < playerCount; id++)
        {
            int row = id / columns;
            int column = id % columns;
            int rowLength = Mathf.Min(columns, playerCount - row * columns);
            GameObject character = id == 0
                ? template.gameObject
                : Instantiate(template.gameObject);
            character.name = "Test Player " + (id + 1) + " (" +
                PlayerManager.Instance.GetplayerByID(id).roleType + ")";
            character.transform.position = center +
                new Vector3((column - (rowLength - 1) * 0.5f) * 1.1f,
                    ((rows - 1) * 0.5f - row) * 1.1f, 0f);

            PlayerMovement movement = character.GetComponent<PlayerMovement>();
            PrototypeControls controls = character.GetComponent<PrototypeControls>();
            movement.playerID = id;
            PlayerNameTag.AttachOrCreate(
                character.transform,
                PlayerManager.Instance.GetplayerByID(id));

            movement.SetCanMove(false);
            controls.enabled = false;
            scenePlayers[id] = movement;
            playerControls[id] = controls;
        }

        return true;
    }

    private void Update()
    {
        if (stressSampleStarted >= 0)
        {
            stressSampleFrames++;
            stressWorstFrameSeconds = Mathf.Max(stressWorstFrameSeconds, Time.unscaledDeltaTime);
            double seconds = Time.realtimeSinceStartupAsDouble - stressSampleStarted;
            if (seconds >= 5.0)
            {
                Debug.Log($"[Thuong stress] {playerCount} players: " +
                          $"{stressSampleFrames / seconds:F1} FPS average over {seconds:F1} s; " +
                          $"worst frame {stressWorstFrameSeconds * 1000f:F1} ms.");
                stressSampleStarted = -1;
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        for (int id = 0; id < Mathf.Min(playerCount, SelectPlayerKeys.Length); id++)
        {
            if (!keyboard[SelectPlayerKeys[id]].wasPressedThisFrame) continue;
            SetLocalPlayer(id);
            return;
        }

        // PageUp/PageDown keep every spawned character reachable beyond F12.
        if (keyboard[Key.PageDown].wasPressedThisFrame)
            SetLocalPlayer((localPlayerID + 1) % playerCount);
        else if (keyboard[Key.PageUp].wasPressedThisFrame)
            SetLocalPlayer((localPlayerID - 1 + playerCount) % playerCount);
    }

    private void SetLocalPlayer(int id)
    {
        if (id < 0 || scenePlayers == null || id >= scenePlayers.Length ||
            scenePlayers[id] == null || id == localPlayerID)
            return;

        for (int i = 0; i < scenePlayers.Length; i++)
        {
            if (playerControls[i] != null) playerControls[i].enabled = false;
            if (scenePlayers[i] != null) scenePlayers[i].SetCanMove(false);
        }

        localPlayerID = id;
        scenePlayers[id].SetCanMove(true);
        playerControls[id].enabled = true;
        if (hud != null) hud.localVoterID = id;
        if (abilityUI != null) abilityUI.localPlayerID = id;

        if (taskStations != null)
        {
            foreach (TaskStation station in taskStations)
                if (station != null) station.player = scenePlayers[id].transform;
        }

        if (voteButtons != null)
        {
            foreach (VoteButton voteButton in voteButtons)
            {
                if (voteButton == null) continue;
                voteButton.voterID = id;
                TMP_Text label = voteButton.transform.Find("Name")?.GetComponent<TMP_Text>();
                if (label != null)
                    label.text = "PLAYER " + (voteButton.targetID + 1) +
                                 (voteButton.targetID == id ? "   /   BẠN" : "");
            }
        }

        Debug.Log("[Thuong 4-role test] Controlling Player " + (id + 1) +
                  " (" + PlayerManager.Instance.GetplayerByID(id).roleType + ").");
    }
}
