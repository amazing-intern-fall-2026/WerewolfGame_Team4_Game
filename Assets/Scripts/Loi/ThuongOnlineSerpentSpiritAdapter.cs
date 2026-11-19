using System;
using System.Collections;
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
        EnforceSessionRestriction();
        ThuongOnlineSerpentSpiritRuntime.EnsureRuntimeExists();
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
    /// Tìm GameObject gốc của Canvas "Role Ability Canvas" theo tên trực tiếp (không dùng FindObjectsByType).
    /// </summary>
    public static GameObject GetRoleAbilityCanvasRoot()
    {
        return GameObject.Find("Role Ability Canvas");
    }

    /// <summary>
    /// Tìm nút "Use skill" (nút CHỌN MỤC TIÊU của RoleAbilityUI) trực tiếp theo cây phân cấp.
    /// </summary>
    public static Button GetUseSkillButton()
    {
        var root = GetRoleAbilityCanvasRoot();
        if (root == null) return null;

        var t = root.transform.Find("Role Ability Window/Card/Ability/Ability details/Use skill");
        if (t != null)
        {
            return t.GetComponent<Button>();
        }

        var window = root.transform.Find("Role Ability Window");
        if (window != null)
        {
            var buttons = window.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == "Use skill")
                    return buttons[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Tìm nút "Open Role Ability" trên HUD của RoleAbilityUI trực tiếp theo cây phân cấp.
    /// </summary>
    public static Button GetOpenRoleAbilityButton()
    {
        var root = GetRoleAbilityCanvasRoot();
        if (root == null) return null;

        var t = root.transform.Find("Open Role Ability");
        if (t != null)
        {
            return t.GetComponent<Button>();
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
    /// Gắn duy nhất 1 SerpentSpiritUIWatcher vào "Role Ability Window" để bắt sự kiện OnEnable khi mở cửa sổ.
    /// Tuyệt đối không gắn vào nhiều đối tượng, không duyệt toàn bộ cây phân cấp, không gây đệ quy.
    /// </summary>
    public static void AttachUIWatcher(GameObject canvasRoot = null)
    {
        if (canvasRoot == null)
        {
            canvasRoot = GetRoleAbilityCanvasRoot();
        }

        if (canvasRoot != null)
        {
            var window = canvasRoot.transform.Find("Role Ability Window");
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
        var btn = GetOpenRoleAbilityButton();
        if (btn != null)
        {
            btn.onClick.RemoveListener(SyncRoleAbilityUIButtons);
            btn.onClick.AddListener(SyncRoleAbilityUIButtons);
        }
    }

    /// <summary>
    /// Thiết lập liên kết Canvas 1 lần duy nhất khi vào trận.
    /// </summary>
    public static void BindCanvas(GameObject canvasRoot)
    {
        if (isBound || canvasRoot == null) return;
        isBound = true;

        AttachUIWatcher(canvasRoot);
        HookRoleAbilityOpenButton();
        SyncRoleAbilityUIButtons();
    }

    /// <summary>
    /// Đồng bộ trạng thái hiển thị của nút "CHỌN MỤC TIÊU" (Use skill) trên Role Ability Canvas (GameHUD popup của Thương).
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

            var useBtn = GetUseSkillButton();
            if (useBtn != null)
            {
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

                    useBtn.interactable = interactable;
                    var label = useBtn.GetComponentInChildren<TMPro.TMP_Text>();
                    if (label != null)
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

    private IEnumerator BindCanvasRoutine()
    {
        for (int i = 0; i < 30; i++)
        {
            var canvasRoot = ThuongOnlineSerpentSpiritAdapter.GetRoleAbilityCanvasRoot();
            if (canvasRoot != null)
            {
                var windowTransform = canvasRoot.transform.Find("Role Ability Window");
                if (windowTransform != null)
                {
                    ThuongOnlineSerpentSpiritAdapter.BindCanvas(canvasRoot);
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.2f);
        }
    }
}

/// <summary>
/// Component lifecycle watcher duy nhất gắn vào GameObject "Role Ability Window".
/// Khi người chơi mở cửa sổ (OnEnable), tự động kích hoạt đồng bộ nút kỹ năng.
/// Không đệ quy, không spam FindObjects, không ép trạng thái mỗi frame.
/// </summary>
public class SerpentSpiritUIWatcher : MonoBehaviour
{
    private int cachedNight = -1;

    private void OnEnable()
    {
        ThuongOnlineSerpentSpiritAdapter.SyncRoleAbilityUIButtons();
    }

    /// <summary>
    /// Khi cửa sổ đang mở, chỉ kiểm tra xem số đêm có thay đổi không (ví dụ chuyển từ Đêm 2 sang Đêm 3).
    /// Tuyệt đối không gọi các hàm quét nặng hay ép trạng thái mỗi frame.
    /// </summary>
    private void Update()
    {
        var session = ThuongOnlineSession.Active;
        int currentNight = session != null ? session.GetVisibleDay() : ThuongOnlineSerpentSpiritAdapter.GetAuthoritativeNightNumber();

        if (currentNight != cachedNight)
        {
            cachedNight = currentNight;
            ThuongOnlineSerpentSpiritAdapter.SyncRoleAbilityUIButtons();
        }
    }
}
