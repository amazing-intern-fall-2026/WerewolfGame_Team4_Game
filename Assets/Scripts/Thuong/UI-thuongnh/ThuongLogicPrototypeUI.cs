using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ThuongLogicPrototypeUI : MonoBehaviour
{
    public ThuongLogicMatchController match;
    public RectTransform rosterPanel;
    public Text phaseText, statusText, logText;
    public Button startButton, nextButton, useButton, voteButton;
    public Button[] actorButtons, targetButtons;
    public int localActorID;
    public int selectedTargetID = 2;
    private readonly Queue<string> lines = new();
    private bool wired;

    private void OnEnable()
    {
        if (Application.isPlaying && match != null && startButton != null) Initialize();
    }
    private void OnDisable() => Unsubscribe();
    private void Start() => Initialize();
    public void Initialize()
    {
        // Fast Play Mode can restore the flag while UnityEvent runtime listeners were reset.
        // Rebind owned controls on every Start rather than trusting editor-preview state.
        if (wired) Unsubscribe();
        startButton.onClick.RemoveAllListeners();
        nextButton.onClick.RemoveAllListeners();
        useButton.onClick.RemoveAllListeners();
        voteButton.onClick.RemoveAllListeners();
        wired = true;
        startButton.onClick.AddListener(match.StartMatch);
        nextButton.onClick.AddListener(match.AdvanceMatch);
        useButton.onClick.AddListener(UseSelectedAbility);
        voteButton.onClick.AddListener(SubmitSelectedVote);
        for (int i = 0; i < actorButtons.Length; i++)
        {
            actorButtons[i].onClick.RemoveAllListeners();
            targetButtons[i].onClick.RemoveAllListeners();
            int id = match.players.players[i].playerID;
            actorButtons[i].onClick.AddListener(() => SelectActor(id));
            targetButtons[i].onClick.AddListener(() => SelectTarget(id));
        }
        match.MatchStarted += HandleStarted;
        match.MatchEnded += Refresh;
        match.phaseController.PhaseChanged += HandlePhase;
        match.deathSystem.DeathResolved += HandleDeath;
        match.abilitySystem.AbilityResolved += HandleAbility;
        match.statusSystem.EffectRemoved += HandleEffectRemoved;
        var roster = GetComponent<PlayerRosterUI>();
        if (roster == null) roster = gameObject.AddComponent<PlayerRosterUI>();
        roster.InitializeEmbedded(rosterPanel);
        Refresh();
    }
    private void OnDestroy()
    {
        Unsubscribe();
    }
    private void Unsubscribe()
    {
        if (!wired || match == null) return;
        match.MatchStarted -= HandleStarted;
        match.MatchEnded -= Refresh;
        match.phaseController.PhaseChanged -= HandlePhase;
        match.deathSystem.DeathResolved -= HandleDeath;
        match.abilitySystem.AbilityResolved -= HandleAbility;
        match.statusSystem.EffectRemoved -= HandleEffectRemoved;
        wired = false;
    }
    private void Update() { if (wired) Refresh(); }
    private void HandleStarted() { lines.Clear(); logText.text = ""; statusText.text = "Chọn người dùng và mục tiêu."; Refresh(); }
    private void HandlePhase(GamePhase previous, GamePhase current) { statusText.text = "Chọn người dùng và mục tiêu."; Refresh(); }
    private void HandleDeath(DeathResult result) => Append(result.Message);
    private void HandleAbility(AbilityResolution result) => Append(result.Message);
    private void HandleEffectRemoved(PlayerData player, StatusEffectInstance effect) => Append($"{player.DisplayName}: hết {effect.Type}.");
    private void Append(string message)
    {
        lines.Enqueue(message);
        while (lines.Count > 8) lines.Dequeue();
        logText.text = string.Join("\n", lines);
    }
    public void SelectActor(int id) { localActorID = id; statusText.text = "Chọn mục tiêu."; Refresh(); }
    public void SelectTarget(int id) { selectedTargetID = id; statusText.text = "Mục tiêu đã chọn."; Refresh(); }
    public void UseSelectedAbility()
    {
        var result = match.abilitySystem.TryUseAbility(match.players.GetplayerByID(localActorID),
            match.players.GetplayerByID(selectedTargetID));
        statusText.text = result.Message;
        Refresh();
    }
    public void SubmitSelectedVote()
    {
        statusText.text = match.voteSystem.TryVote(localActorID, selectedTargetID) ? "Phiếu đã ghi nhận." : "Không thể bỏ phiếu.";
        Refresh();
    }
    public void Refresh()
    {
        if (match == null || phaseText == null) return;
        string phase = match.phaseController.currentPhase switch
        {
            GamePhase.ResolveNight => "NightResolution",
            GamePhase.Discussion => "DayDiscussion",
            _ => match.phaseController.currentPhase.ToString()
        };
        phaseText.text = $"PHASE {phase}   /   VÒNG {match.phaseController.currentDay}" +
            (match.phaseController.currentState == GameState.GameOver ? $"   /   {match.phaseController.Winner}" : "");
        nextButton.interactable = match.IsMatchRunning;
        startButton.GetComponentInChildren<Text>().text = match.IsMatchRunning ? "BẮT ĐẦU LẠI" : "START MATCH";
        var actor = match.players.GetplayerByID(localActorID);
        var target = match.players.GetplayerByID(selectedTargetID);
        var validation = match.abilitySystem.Validate(actor, target);
        useButton.interactable = validation.IsValid;
        if (validation.FailureReason == ActionFailureReason.ActorDead || validation.FailureReason == ActionFailureReason.Silenced)
            statusText.text = validation.Message;
        voteButton.interactable = match.IsMatchRunning && match.phaseController.currentPhase == GamePhase.Voting &&
            actor != null && target != null && actor.CanVote && target.isAlive && !actor.hasVoted;
        for (int i = 0; i < actorButtons.Length; i++)
        {
            var player = match.players.players[i];
            actorButtons[i].GetComponent<Image>().color = player.playerID == localActorID ? new Color(.2f,.5f,.45f) : new Color(.15f,.24f,.28f);
            targetButtons[i].GetComponent<Image>().color = player.playerID == selectedTargetID ? new Color(.5f,.37f,.16f) : new Color(.15f,.24f,.28f);
            targetButtons[i].interactable = player.isAlive && (actor?.roleDefinition == null ||
                match.phaseController.currentPhase != GamePhase.Night || TargetValidator.Validate(actor, player, actor.roleDefinition).IsValid);
        }
    }

    public void BuildUI()
    {
        var root = transform;
        Panel("Background", root, Vector2.zero, Vector2.one, new Color(.055f,.09f,.115f));
        Label("Title", root, "THƯƠNG / LOGIC MA SÓI / PHASE 1–4", new Vector2(.025f,.925f), new Vector2(.975f,.985f), 28);
        phaseText = Label("PhaseText", root, "PHASE Setup / VÒNG 0", new Vector2(.025f,.855f), new Vector2(.7f,.915f), 22);
        startButton = ButtonAt("StartButton", root, "START MATCH", new Vector2(.72f,.86f), new Vector2(.84f,.915f));
        nextButton = ButtonAt("NextPhaseButton", root, "NEXT PHASE", new Vector2(.855f,.86f), new Vector2(.975f,.915f));
        rosterPanel = Panel("PlayerList", root, new Vector2(.025f,.31f), new Vector2(.445f,.83f), new Color(.09f,.14f,.17f));
        var actionPanel = Panel("AbilityPanel", root, new Vector2(.46f,.31f), new Vector2(.975f,.83f), new Color(.09f,.14f,.17f));
        Label("ActorsTitle", actionPanel, "NGƯỜI DÙNG (LOCAL TEST)", new Vector2(.03f,.86f), new Vector2(.97f,.97f), 22);
        actorButtons = new Button[match.players.players.Count];
        targetButtons = new Button[actorButtons.Length];
        for (int i = 0; i < actorButtons.Length; i++)
        {
            float x = .03f + i * .24f;
            var player = match.players.players[i];
            actorButtons[i] = ButtonAt("Actor " + player.playerID, actionPanel,
                $"P{player.playerID + 1:00}\n{player.roleDefinition.displayName}", new Vector2(x,.64f), new Vector2(x+.22f,.85f));
            targetButtons[i] = ButtonAt("Target " + player.playerID, actionPanel,
                $"P{player.playerID + 1:00}", new Vector2(x,.38f), new Vector2(x+.22f,.51f));
        }
        Label("TargetsTitle", actionPanel, "MỤC TIÊU", new Vector2(.03f,.52f), new Vector2(.97f,.63f), 22);
        useButton = ButtonAt("UseButton", actionPanel, "DÙNG KỸ NĂNG", new Vector2(.03f,.19f), new Vector2(.48f,.33f));
        voteButton = ButtonAt("VoteButton", actionPanel, "BỎ PHIẾU (Voting)", new Vector2(.51f,.19f), new Vector2(.97f,.33f));
        statusText = Label("AbilityStatusText", actionPanel, "Nhấn Start Match.", new Vector2(.03f,.015f), new Vector2(.97f,.17f), 19);
        var logPanel = Panel("EventLog", root, new Vector2(.025f,.055f), new Vector2(.975f,.285f), new Color(.09f,.14f,.17f));
        Label("LogTitle", logPanel, "NHẬT KÝ ABILITY / PROTECTED / DEATH", new Vector2(.015f,.79f), new Vector2(.985f,.98f), 19);
        logText = Label("EventLogText", logPanel, "", new Vector2(.015f,.02f), new Vector2(.985f,.79f), 18);
        logText.alignment = TextAnchor.UpperLeft;
        Label("Hint", root, "Doctor P01 + Wolf P02 → Villager P03. Đổi thứ tự gửi để kiểm tra Protected. Next → DayDiscussion: hết effect.",
            new Vector2(.025f,.005f), new Vector2(.975f,.05f), 17);
    }
    private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return rect;
    }
    private static Text Label(string name, Transform parent, string value, Vector2 min, Vector2 max, int size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.color = Color.white; text.fontSize = size;
        text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = size;
        return text;
    }
    private static Button ButtonAt(string name, Transform parent, string value, Vector2 min, Vector2 max)
    {
        var rect = Panel(name, parent, min, max, new Color(.15f,.24f,.28f));
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        Label("Label", rect, value, new Vector2(.03f,.03f), new Vector2(.97f,.97f), 19).alignment = TextAnchor.MiddleCenter;
        return button;
    }
}
