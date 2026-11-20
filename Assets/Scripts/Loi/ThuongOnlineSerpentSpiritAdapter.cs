using System;
using System.Collections;
using System.Collections.Generic;
using Thuong.Online;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adapter tích hợp dành riêng cho vai trò Xà Tinh (SerpentSpirit) trong Thương Online.
/// 
/// Đảm bảo:
/// 1. Xà Tinh chỉ được tấn công từ Đêm 3 (Night 3) trở đi.
/// 2. Đêm 1 & Đêm 2: Ẩn nút tấn công và các thành phần chọn mục tiêu (không chỉ disable mà UI thực sự không xuất hiện).
/// 3. Server xác thực số đêm thực tế và từ chối mọi yêu cầu tấn công trong Đêm 1 và Đêm 2.
/// 4. Đêm 3 trở đi: Cho phép sử dụng kỹ năng bình thường theo luật.
/// 5. Khi chuyển sang Ngày hoặc phase khác: Ẩn UI kỹ năng.
/// 6. Hoàn toàn Event-driven, KHÔNG dùng Update/LateUpdate polling ép trạng thái liên tục, KHÔNG Reflection, KHÔNG sửa code Thương.
/// 7. Tuyệt đối không recursion, không gọi FindObjectsByType liên tục, không gây tràn bộ nhớ tạm thời (ALLOC_TEMP_TLS / ALLOC_TEMP_MAIN).
/// </summary>
public static class ThuongOnlineSerpentSpiritAdapter
{
    private static bool isListenerRegistered = false;
    private static int simulatedNightForTest = -1;
    private static bool isSyncing = false;
    private static bool isBound = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        EnsureInstalled();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        EnsureInstalled();
        ThuongOnlineSerpentSpiritRuntime.EnsureRuntimeExists();
    }

    public static void EnsureInstalled()
    {
        if (!isListenerRegistered)
        {
            isListenerRegistered = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            NetworkPhaseSync.OnPhaseChangedGlobal -= HandleGlobalPhaseChanged;
            NetworkPhaseSync.OnPhaseChangedGlobal += HandleGlobalPhaseChanged;

            NetworkPhaseSync.OnNightCycleChangedGlobal -= HandleGlobalNightCycleChanged;
            NetworkPhaseSync.OnNightCycleChangedGlobal += HandleGlobalNightCycleChanged;
        }

        EnforceSessionRestriction();
    }

    public static void SetSimulatedNightForTest(int night)
    {
        simulatedNightForTest = night;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isBound = false;
        PurgeGhostRoleAbilityCanvases();
        EnforceSessionRestriction();
        ThuongOnlineSerpentSpiritRuntime.EnsureRuntimeExists();
    }

    /// <summary>
    /// Tiêu hủy toàn bộ Canvas "Role Ability Canvas" bị bake sẵn từ scene file khi scene vừa load.
    /// RoleAbilityUI của GameHUD sẽ tự động tạo Canvas dynamic mới khi khởi tạo.
    /// Loại bỏ triệt để 15 ghost canvases và lỗi tràn bộ nhớ TLS Allocator ALLOC_TEMP_TLS.
    /// </summary>
    public static void PurgeGhostRoleAbilityCanvases()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        var roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var r = roots[i];
            if (r != null && r.name == "Role Ability Canvas")
            {
                UnityEngine.Object.Destroy(r);
            }
        }
    }

    private static void HandleGlobalPhaseChanged(GamePhase phase)
    {
        EnforceSessionRestriction();
        SyncRoleAbilityUIButtons();

        if (NetworkSkillUI.Instance != null)
        {
            NetworkSkillUI.Instance.UpdateSkillUI();
        }
    }

    private static void HandleGlobalNightCycleChanged(int night)
    {
        EnforceSessionRestriction();
        SyncRoleAbilityUIButtons();

        if (NetworkSkillUI.Instance != null)
        {
            NetworkSkillUI.Instance.UpdateSkillUI();
        }
    }

    /// <summary>
    /// Đồng bộ ràng buộc số đêm cho Xà Tinh trong ThuongOnlineSession nếu đang hoạt động.
    /// </summary>
    public static void EnforceSessionRestriction()
    {
        var session = ThuongOnlineSession.Active;
        if (session != null)
        {
            session.EnforceSerpentSpiritNightRestriction();
        }
    }

    /// <summary>
    /// Lấy số đêm authoritative lớn nhất từ các nguồn đồng bộ hiện có.
    /// Tối ưu hóa tối đa: không dùng FindObjectsByType, chỉ truy xuất trực tiếp các singleton.
    /// </summary>
    public static int GetAuthoritativeNightNumber()
    {
        if (simulatedNightForTest > 0)
        {
            return simulatedNightForTest;
        }

        int night = 0;

        // 1. ThuongOnlineSession
        var session = ThuongOnlineSession.Active;
        if (session != null && session.GetVisibleDay() > 0)
        {
            night = Mathf.Max(night, session.GetVisibleDay());
        }

        // 2. NightManager
        if (NightManager.Instance != null && NightManager.Instance.CurrentNightNumber > 0)
        {
            night = Mathf.Max(night, NightManager.Instance.CurrentNightNumber);
        }

        // 3. GameRoleManager
        if (GameRoleManager.Instance != null && GameRoleManager.Instance.currentDay > 0)
        {
            night = Mathf.Max(night, GameRoleManager.Instance.currentDay);
        }

        // 4. NetworkPhaseSync
        if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentNightCycle.Value > 0)
        {
            night = Mathf.Max(night, NetworkPhaseSync.Instance.CurrentNightCycle.Value);
        }

        // 5. HUD nhãn trực tiếp trên GameHUD (ví dụ: "NGÀY 3 / BAN ĐÊM")
        var hud = UnityEngine.Object.FindAnyObjectByType<GameHUD>();
        if (hud != null && hud.phaseText != null && !string.IsNullOrEmpty(hud.phaseText.text))
        {
            var match = System.Text.RegularExpressions.Regex.Match(hud.phaseText.text, @"NGÀY\s*(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int hudDay))
            {
                night = Mathf.Max(night, hudDay);
            }
        }

        return night > 0 ? night : 1;
    }

    /// <summary>
    /// Kiểm tra xem số đêm hiện tại có hợp lệ để Xà Tinh tấn công hay không (từ Đêm 3 trở đi).
    /// </summary>
    public static bool IsSerpentSpiritNightValid(int nightNumber)
    {
        return nightNumber >= 3;
    }

    /// <summary>
    /// Kiểm tra người chơi Xà Tinh có đủ điều kiện tấn công ở đêm hiện tại hay không.
    /// </summary>
    public static bool CanSerpentSpiritAttack(PlayerData player, int nightNumber)
    {
        if (player == null || !player.isAlive) return false;
        if (player.roleType != RoleType.SerpentSpirit) return false;
        return IsSerpentSpiritNightValid(nightNumber);
    }

    /// <summary>
    /// Lấy danh sách tất cả GameObject gốc của Canvas "Role Ability Canvas" trong mọi scene đã load.
    /// </summary>
    public static List<GameObject> GetAllRoleAbilityCanvasRoots()
    {
        var list = new List<GameObject>();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var sc = SceneManager.GetSceneAt(s);
            if (!sc.isLoaded) continue;
            var roots = sc.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var r = roots[i];
                if (r != null && r.name == "Role Ability Canvas" && !list.Contains(r))
                {
                    list.Add(r);
                }
            }
        }

        var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < allCanvases.Length; i++)
        {
            var c = allCanvases[i];
            if (c != null && c.gameObject != null && c.gameObject.name == "Role Ability Canvas" && c.gameObject.scene.isLoaded)
            {
                if (!list.Contains(c.gameObject)) list.Add(c.gameObject);
            }
        }

        return list;
    }

    /// <summary>
    /// Tìm GameObject gốc của Canvas "Role Ability Canvas" theo tên trực tiếp.
    /// </summary>
    public static GameObject GetRoleAbilityCanvasRoot()
    {
        var roots = GetAllRoleAbilityCanvasRoots();
        return roots.Count > 0 ? roots[0] : GameObject.Find("Role Ability Canvas");
    }

    /// <summary>
    /// Lấy danh sách tất cả các nút "Use skill" (CHỌN MỤC TIÊU) trong toàn bộ các Canvas Role Ability.
    /// Tìm kiếm toàn diện kể cả khi GameObject đang bị disable.
    /// </summary>
    public static List<Button> GetAllUseSkillButtons()
    {
        var list = new List<Button>();
        var roots = GetAllRoleAbilityCanvasRoots();
        for (int i = 0; i < roots.Count; i++)
        {
            var root = roots[i];
            if (root == null) continue;

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int b = 0; b < buttons.Length; b++)
            {
                if (buttons[b] != null && buttons[b].gameObject.name == "Use skill" && !list.Contains(buttons[b]))
                {
                    list.Add(buttons[b]);
                }
            }
        }

        if (list.Count == 0)
        {
            var allButtons = Resources.FindObjectsOfTypeAll<Button>();
            for (int i = 0; i < allButtons.Length; i++)
            {
                var b = allButtons[i];
                if (b != null && b.gameObject != null && b.gameObject.name == "Use skill" && b.gameObject.scene.isLoaded)
                {
                    if (!list.Contains(b)) list.Add(b);
                }
            }
        }

        return list;
    }

    /// <summary>
    /// Tìm nút "Use skill" (nút CHỌN MỤC TIÊU của RoleAbilityUI) trực tiếp theo cây phân cấp.
    /// </summary>
    public static Button GetUseSkillButton()
    {
        var buttons = GetAllUseSkillButtons();
        return buttons.Count > 0 ? buttons[0] : null;
    }

    /// <summary>
    /// Tìm nút "Open Role Ability" trên HUD của RoleAbilityUI trực tiếp theo cây phân cấp.
    /// </summary>
    public static Button GetOpenRoleAbilityButton()
    {
        var roots = GetAllRoleAbilityCanvasRoots();
        for (int i = 0; i < roots.Count; i++)
        {
            var root = roots[i];
            if (root == null) continue;

            var t = root.transform.Find("Open Role Ability");
            if (t != null)
            {
                var btn = t.GetComponent<Button>();
                if (btn != null) return btn;
            }
        }

        return null;
    }

    /// <summary>
    /// Kiểm tra toàn diện người chơi cục bộ có phải là Xà Tinh hay không từ tất cả các nguồn hợp lệ.
    /// </summary>
    public static bool IsLocalPlayerSerpentSpirit(RoleAbilityUI abilityUI = null)
    {
        // 1. ThuongOnlineSession
        var session = ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole && session.GetPrivateRole() == RoleType.SerpentSpirit)
            return true;

        // 2. NetworkRoleSync
        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.LocalRole == RoleType.SerpentSpirit)
            return true;

        // 3. NetworkSkillUI
        if (NetworkSkillUI.Instance != null && NetworkSkillUI.Instance.IsSerpentSpirit)
            return true;

        // 4. PlayerManager qua local player ID
        int localId = -1;
        if (abilityUI != null && abilityUI.localPlayerID >= 0)
            localId = abilityUI.localPlayerID;
        else if (session != null && session.HasLocalRole)
            localId = session.LocalPlayerId;
        else if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.LocalClient != null)
            localId = (int)Unity.Netcode.NetworkManager.Singleton.LocalClientId;

        if (localId >= 0 && PlayerManager.Instance != null)
        {
            var p = PlayerManager.Instance.GetplayerByID(localId);
            if (p != null && p.roleType == RoleType.SerpentSpirit)
                return true;
        }

        // 5. RoleManager dictionary
        if (localId >= 0 && RoleManager.Instance != null && RoleManager.Instance.playerRoles != null)
        {
            if (RoleManager.Instance.playerRoles.TryGetValue(localId, out BaseRole role) && role is SerpentSpirit)
                return true;
        }

        // 6. Nhãn trên chính Canvas của RoleAbilityUI
        var root = GetRoleAbilityCanvasRoot();
        if (root != null)
        {
            var roleLabel = root.transform.Find("Role Ability Window/Card/Ability/Ability details/Role");
            if (roleLabel != null)
            {
                var tmp = roleLabel.GetComponent<TMPro.TMP_Text>();
                if (tmp != null && (tmp.text.Contains("XÀ TINH") || tmp.text.Contains("SERPENT SPIRIT")))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gắn SerpentSpiritUIWatcher vào "Role Ability Window" để bắt sự kiện OnEnable và Update khi mở cửa sổ.
    /// Tự động gắn lên tất cả Canvas Role Ability tìm thấy mà chưa có watcher.
    /// </summary>
    public static void AttachUIWatcher(GameObject canvasRoot = null)
    {
        var roots = canvasRoot != null ? new List<GameObject> { canvasRoot } : GetAllRoleAbilityCanvasRoots();
        for (int i = 0; i < roots.Count; i++)
        {
            var r = roots[i];
            if (r == null) continue;
            var window = r.transform.Find("Role Ability Window");
            if (window != null)
            {
                if (window.GetComponent<SerpentSpiritUIWatcher>() == null)
                {
                    window.gameObject.AddComponent<SerpentSpiritUIWatcher>();
                }
            }
        }
    }

    /// <summary>
    /// Hook sự kiện nhấn nút mở popup Role Ability để cập nhật nút kỹ năng ngay khi người chơi mở giao diện.
    /// </summary>
    public static void HookRoleAbilityOpenButton()
    {
        var roots = GetAllRoleAbilityCanvasRoots();
        for (int i = 0; i < roots.Count; i++)
        {
            var r = roots[i];
            if (r == null) continue;
            var t = r.transform.Find("Open Role Ability");
            if (t != null)
            {
                var btn = t.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveListener(SyncRoleAbilityUIButtons);
                    btn.onClick.AddListener(SyncRoleAbilityUIButtons);
                }
            }
        }
    }

    /// <summary>
    /// Thiết lập liên kết Canvas khi vào trận.
    /// </summary>
    public static void BindCanvas(GameObject canvasRoot)
    {
        if (canvasRoot == null) return;

        AttachUIWatcher(canvasRoot);
        HookRoleAbilityOpenButton();
        SyncRoleAbilityUIButtons();
    }

    /// <summary>
    /// Đồng bộ trạng thái hiển thị của nút "CHỌN MỤC TIÊU" (Use skill) trên Role Ability Canvas (GameHUD popup của Thương).
    /// Áp dụng cho toàn bộ các nút Use skill trong tất cả các Canvas Role Ability.
    /// Có cờ bảo vệ chống đệ quy (recursion guard) và không cấp phát bộ nhớ native.
    /// </summary>
    public static void SyncRoleAbilityUIButtons()
    {
        if (isSyncing) return;

        try
        {
            isSyncing = true;

            var abilityUI = UnityEngine.Object.FindAnyObjectByType<RoleAbilityUI>();
            if (!IsLocalPlayerSerpentSpirit(abilityUI)) return;

            EnforceSessionRestriction();

            int night = GetAuthoritativeNightNumber();
            PlayerData local = null;
            if (PlayerManager.Instance != null && abilityUI != null)
            {
                local = PlayerManager.Instance.GetplayerByID(abilityUI.localPlayerID);
            }

            bool isAlive = local == null || local.isAlive;
            bool shouldShow = (night >= 3) && isAlive;

            var buttons = GetAllUseSkillButtons();
            for (int i = 0; i < buttons.Count; i++)
            {
                var useBtn = buttons[i];
                if (useBtn == null) continue;

                if (useBtn.gameObject.activeSelf != shouldShow)
                {
                    useBtn.gameObject.SetActive(shouldShow);
                }

                if (shouldShow)
                {
                    bool interactable = true;
                    var session = ThuongOnlineSession.Active;
                    if (session != null)
                    {
                        interactable = session.CanUseLocalAbility(out _);
                    }
                    else if (abilityUI != null && local != null)
                    {
                        interactable = CanSerpentSpiritAttack(local, night);
                    }

                    if (useBtn.interactable != interactable)
                    {
                        useBtn.interactable = interactable;
                    }

                    var label = useBtn.GetComponentInChildren<TMPro.TMP_Text>();
                    if (label != null && label.text != "CHỌN MỤC TIÊU")
                    {
                        label.text = "CHỌN MỤC TIÊU";
                    }
                }
            }
        }
        finally
        {
            isSyncing = false;
        }
    }
}

/// <summary>
/// Component runtime độc lập tồn tại xuyên suốt để tự động tìm kiếm Canvas và Window của RoleAbilityUI
/// ngay khi GameHUD hoàn tất khởi tạo sau khi vào scene Gameplay.
/// </summary>
public class ThuongOnlineSerpentSpiritRuntime : MonoBehaviour
{
    private static ThuongOnlineSerpentSpiritRuntime instance;

    public static void EnsureRuntimeExists()
    {
        if (instance != null) return;

        var existing = UnityEngine.Object.FindAnyObjectByType<ThuongOnlineSerpentSpiritRuntime>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        var go = new GameObject("[Loi_SerpentSpiritRuntime]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<ThuongOnlineSerpentSpiritRuntime>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(BindCanvasRoutine());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();
        StartCoroutine(BindCanvasRoutine());
    }

    private void LateUpdate()
    {
        ThuongOnlineSerpentSpiritAdapter.SyncRoleAbilityUIButtons();
    }

    private IEnumerator BindCanvasRoutine()
    {
        // Liên tục kiểm tra và bind Canvas của RoleAbilityUI xuyên suốt trận đấu
        while (true)
        {
            var canvasRoots = ThuongOnlineSerpentSpiritAdapter.GetAllRoleAbilityCanvasRoots();
            for (int c = 0; c < canvasRoots.Count; c++)
            {
                var canvasRoot = canvasRoots[c];
                if (canvasRoot != null)
                {
                    ThuongOnlineSerpentSpiritAdapter.BindCanvas(canvasRoot);
                }
            }
            yield return new WaitForSeconds(1.0f);
        }
    }
}

/// <summary>
/// Component lifecycle watcher duy nhất gắn vào GameObject "Role Ability Window".
/// Khi người chơi mở cửa sổ (OnEnable), tự động kích hoạt đồng bộ nút kỹ năng.
/// Khi cửa sổ đang mở, kiểm tra và đảm bảo nút kỹ năng hiển thị đúng ở Đêm 3 trở đi.
/// </summary>
public class SerpentSpiritUIWatcher : MonoBehaviour
{
    private int cachedNight = -1;

    private void OnEnable()
    {
        ThuongOnlineSerpentSpiritAdapter.SyncRoleAbilityUIButtons();
    }

    private void Update()
    {
        var session = ThuongOnlineSession.Active;
        int currentNight = session != null ? session.GetVisibleDay() : ThuongOnlineSerpentSpiritAdapter.GetAuthoritativeNightNumber();

        if (currentNight != cachedNight)
        {
            cachedNight = currentNight;
            ThuongOnlineSerpentSpiritAdapter.SyncRoleAbilityUIButtons();
            return;
        }

        if (currentNight >= 3)
        {
            var useBtn = transform.Find("Card/Ability/Ability details/Use skill");
            if (useBtn != null && !useBtn.gameObject.activeSelf)
            {
                ThuongOnlineSerpentSpiritAdapter.SyncRoleAbilityUIButtons();
            }
        }
    }
}
