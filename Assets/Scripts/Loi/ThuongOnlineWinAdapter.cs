using System;
using System.Collections.Generic;
using System.Linq;
using Thuong.Online;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adapter chuyên trách quản lý trạng thái thức tỉnh của Bạch Khuyển (WhiteHound) và điều kiện chiến thắng trong Thương Online.
/// 
/// Đảm bảo các nguyên tắc:
/// 1. Xác định chính xác trạng thái thức tỉnh thực tế của Bạch Khuyển (isWhiteHoundAwakened hoặc bị cắn đêm nay).
///    Tuyệt đối không suy luận bừa từ việc DogSpirit đã chết.
/// 2. Giữ nguyên roleType là RoleType.WhiteHound (không ghi đè thành DogSpirit làm hỏng ThuongMatchOutcome và ResultView).
/// 3. Khi thức tỉnh và còn sống:
///    - Thuộc phe Monster (faction = FactionType.Monster).
///    - Tính vào số lượng Ma Sói còn sống (aliveMonsters).
///    - CHẶN HOÀN TOÀN Dân Làng chiến thắng, kể cả khi tất cả Khuyển Tinh (DogSpirit) khác đã chết.
/// 4. Khi chưa thức tỉnh và còn sống:
///    - Thuộc phe Dân Làng (faction = FactionType.Villager).
///    - Tính vào số Dân Làng còn sống (aliveVillagers).
///    - Cho phép Dân Làng chiến thắng theo luật hiện tại khi quái vật bị tiêu diệt hết.
/// 5. Khi Bạch Khuyển đã chết:
///    - Không tính vào số Ma Sói hay Dân Làng còn sống.
/// 6. Hỗ trợ giải quyết thứ tự sự kiện trong đêm: cắn thức tỉnh trước khi kiểm tra chết và điều kiện thắng.
/// 7. Reset sạch sẽ trạng thái khi bắt đầu ván mới.
/// 8. TUYỆT ĐỐI KHÔNG CHỈNH SỬA CODE THƯƠNG (Assets/Scripts/Thuong/).
/// 9. KHÔNG DÙNG REFLECTION.
/// </summary>
public class ThuongOnlineWinAdapter : MonoBehaviour
{
    public static ThuongOnlineWinAdapter Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
        EnsureWinEvaluatorInstalled();
    }

    public static void EnsureInstance()
    {
        if (Instance != null) return;
        var existing = FindAnyObjectByType<ThuongOnlineWinAdapter>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("[ThuongOnlineWinAdapter]");
        Instance = go.AddComponent<ThuongOnlineWinAdapter>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureWinEvaluatorInstalled();
    }

    /// <summary>
    /// Kiểm tra người chơi có phải vai trò Bạch Khuyển hay không.
    /// </summary>
    public static bool IsWhiteHound(PlayerData player)
    {
        if (player == null) return false;
        return player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf;
    }

    /// <summary>
    /// Kiểm tra người chơi Bạch Khuyển đã thực sự thức tỉnh hay chưa (dựa trên flag hoặc bị cắn trong đêm).
    /// Tuyệt đối KHÔNG suy đoán từ việc các Ma Sói khác đã chết.
    /// </summary>
    public static bool IsWhiteHoundAwakened(PlayerData player)
    {
        if (!IsWhiteHound(player)) return false;

        if (player.isWhiteHoundAwakened) return true;
        if (player.faction == FactionType.Monster) return true;

        if (NetworkRoleActionBridge.Instance != null &&
            NetworkRoleActionBridge.Instance.HasWhiteHoundBeenBittenTonight(player.playerID))
        {
            return true;
        }

        if (RoleManager.Instance?.playerRoles != null &&
            RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole role) &&
            role != null && role.faction == FactionType.Monster)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Đồng bộ trạng thái Bạch Khuyển trên toàn bộ danh sách người chơi:
    /// - Nếu thức tỉnh: faction = Monster, isWhiteHoundAwakened = true, roleType giữ nguyên WhiteHound.
    /// - Nếu bị DogSpirit cắn đêm nay: đảm bảo isAlive = true (cú cắn để thức tỉnh chứ không làm chết).
    /// - Nếu chưa thức tỉnh: faction = Villager, isWhiteHoundAwakened = false, roleType giữ nguyên WhiteHound.
    /// </summary>
    public static void SynchronizeWhiteHoundState()
    {
        if (PlayerManager.Instance?.players == null) return;

        bool stateChanged = false;

        foreach (PlayerData p in PlayerManager.Instance.players)
        {
            if (p == null) continue;
            if (!IsWhiteHound(p)) continue;

            bool wasBittenTonight = NetworkRoleActionBridge.Instance != null &&
                                    NetworkRoleActionBridge.Instance.HasWhiteHoundBeenBittenTonight(p.playerID);

            // Cú cắn của DogSpirit vào Bạch Khuyển không làm Bạch Khuyển chết:
            if (wasBittenTonight && !p.isAlive)
            {
                if (p.lastDeathCause != DeathCause.Vote && p.lastDeathCause != DeathCause.Poison)
                {
                    p.isAlive = true;
                    NetworkPlayerStateSync.SyncGameplayAliveState(p.playerID, true);
                    stateChanged = true;
                }
            }

            bool awakened = p.isWhiteHoundAwakened || wasBittenTonight || p.faction == FactionType.Monster;

            if (awakened)
            {
                if (!p.isWhiteHoundAwakened || p.faction != FactionType.Monster)
                {
                    p.isWhiteHoundAwakened = true;
                    p.faction = FactionType.Monster;
                    stateChanged = true;
                }

                if (RoleManager.Instance?.playerRoles != null &&
                    RoleManager.Instance.playerRoles.TryGetValue(p.playerID, out BaseRole r))
                {
                    if (r is WhiteHound wh)
                    {
                        wh.BecomeMonster();
                        wh.faction = FactionType.Monster;
                    }
                    else if (r != null)
                    {
                        r.faction = FactionType.Monster;
                    }
                }
            }
            else
            {
                // Chưa thức tỉnh -> Luôn thuộc phe Dân Làng
                if (p.faction != FactionType.Villager || p.isWhiteHoundAwakened)
                {
                    p.isWhiteHoundAwakened = false;
                    p.faction = FactionType.Villager;
                    stateChanged = true;
                }

                if (RoleManager.Instance?.playerRoles != null &&
                    RoleManager.Instance.playerRoles.TryGetValue(p.playerID, out BaseRole r) && r != null)
                {
                    r.faction = FactionType.Villager;
                }
            }

            p.NotifyChanged();
        }

        if (stateChanged)
        {
            NetworkRoleSync.SyncAllRoleSyncs();
        }
    }

    /// <summary>
    /// Kiểm tra xem Dân Làng có thể chiến thắng hay không dựa trên danh sách người chơi thực tế.
    /// Trả về false NGAY LẬP TỨC nếu còn bất kỳ Ma Sói nào (kể cả Bạch Khuyển thức tỉnh) còn sống.
    /// </summary>
    public static bool CanVillagersWin(IReadOnlyList<PlayerData> players)
    {
        if (players == null || players.Count == 0) return false;

        int aliveMonsters = 0;
        int aliveVillagers = 0;
        int aliveThirdParty = 0;

        foreach (var player in players)
        {
            if (player == null || !player.isAlive || player.isDisconnected ||
                NetworkGameManager.IsPlayerLeft((ulong)player.playerID))
            {
                continue;
            }

            bool isWh = IsWhiteHound(player);
            bool awakenedWh = isWh && IsWhiteHoundAwakened(player);

            bool isMonster = awakenedWh ||
                             player.faction == FactionType.Monster ||
                             player.roleType == RoleType.DogSpirit ||
                             player.roleType == RoleType.SerpentSpirit ||
                             player.roleType == RoleType.Ogre;

            bool isThirdParty = !isMonster && !isWh && (
                player.faction == FactionType.Neutral ||
                player.roleType == RoleType.Madman ||
                player.roleType == RoleType.Jester ||
                player.roleType == RoleType.Lover ||
                player.roleType == RoleType.FoxSpirit ||
                player.roleType == RoleType.Piper ||
                player.roleType == RoleType.Killer ||
                player.roleType == RoleType.SerialKiller ||
                player.roleType == RoleType.DeathHerald);

            if (isMonster)
            {
                aliveMonsters++;
            }
            else if (isThirdParty)
            {
                aliveThirdParty++;
            }
            else
            {
                // Dân làng thường và Bạch Khuyển chưa thức tỉnh đều tính là Dân Làng sống
                aliveVillagers++;
            }
        }

        // Nếu còn bất kỳ quái vật nào (kể cả Bạch Khuyển thức tỉnh) -> CHẶN DÂN LÀNG THẮNG
        if (aliveMonsters > 0)
        {
            return false;
        }

        // Dân làng chỉ thắng khi hết quái vật, hết phe thứ ba và còn ít nhất một dân làng/Bạch Khuyển chưa thức tỉnh sống
        return aliveMonsters == 0 && aliveVillagers > 0 && aliveThirdParty == 0;
    }

    /// <summary>
    /// Đánh giá kết quả ván đấu theo luật Thương Online (ThuongMatchOutcome) phục vụ kiểm thử và xác thực.
    /// </summary>
    public static string EvaluateOnlineOutcomeForTest(IReadOnlyList<PlayerData> players, int day = 1, GamePhase phase = GamePhase.DayResolution, bool dayFinished = true)
    {
        if (players == null || players.Count == 0) return "NoPlayers";
        var state = new List<ThuongWinPlayer>();
        foreach (var p in players)
        {
            if (p == null) continue;
            state.Add(new ThuongWinPlayer(
                p.playerID, p.playerName, 0, p.roleType, p.faction, p.isAlive, p.wasVotedOut,
                p.loverID, p.isWhiteHoundAwakened, false
            ));
        }
        var settings = new ThuongOnlineWinSettings { eliminationWin = true, minimumPlayersForParity = 1, specialWinVillageLimit = 2 };
        var outcome = ThuongMatchOutcome.Evaluate(state, settings, day, phase, 0, 7, dayFinished);
        return outcome != null ? outcome.WinnerId : "Ongoing";
    }

    /// <summary>
    /// Đảm bảo WinConditionManager trong scene có MatchEvaluator chuẩn, ngăn ngừa fallback VillagerWin lỗi ở dòng 115.
    /// </summary>
    public static void EnsureWinEvaluatorInstalled()
    {
        var game = GameRoleManager.Instance;
        var wcm = WinConditionManager.Instance;
        if (game == null || wcm == null) return;

        // Nếu WinConditionManager đã có evaluator từ ThuongOnlineSession thì để nguyên
        if (wcm.HasMatchEvaluator(game)) return;

        // Chỉ cài đặt nếu cùng scene theo yêu cầu của WinConditionManager
        if (game.gameObject.scene == wcm.gameObject.scene)
        {
            wcm.InstallMatchEvaluator(game, EvaluateMatchOutcome);
        }
    }

    /// <summary>
    /// Hàm đánh giá điều kiện chiến thắng chuẩn xác, không dùng Reflection, không ghi đè roleType,
    /// bảo toàn toàn bộ quy tắc game:
    /// - Bạch Khuyển thức tỉnh còn sống: Dân Làng KHÔNG THỂ thắng.
    /// - Bạch Khuyển chưa thức tỉnh còn sống: Dân Làng THẮNG khi hết quái vật.
    /// - Bạch Khuyển đã chết: không tính vào phe sống.
    /// - Parity và solo win được bảo toàn đầy đủ.
    /// </summary>
    public static void EvaluateMatchOutcome(bool dayFinished)
    {
        var game = GameRoleManager.Instance;
        if (game == null || game.currentState == GameState.GameOver) return;
        if (NetworkGameOverManager.HasPendingHunterDeathShot()) return;

        // Trước khi tính thắng thua, đồng bộ trạng thái Bạch Khuyển
        SynchronizeWhiteHoundState();

        if (PlayerManager.Instance == null || PlayerManager.Instance.players == null ||
            PlayerManager.Instance.players.Count == 0)
        {
            return;
        }

        // 1. Kẻ Điên bị vote loại ngày 1
        if (dayFinished && game.currentDay == 1 &&
            PlayerManager.Instance.players.Exists(p => p != null && p.roleType == RoleType.Madman &&
                                                      !p.isAlive && p.wasVotedOut))
        {
            game.MadmanWin();
            NetworkGameOverManager.Instance?.TriggerGameOver("Madman");
            return;
        }

        // 2. Cáo Tinh đã mê hoặc đủ
        if (WinConditionManager.Instance != null && WinConditionManager.Instance.FoxSpiritHasWon())
        {
            game.FoxSpiritWin();
            NetworkGameOverManager.Instance?.TriggerGameOver("Fox Spirit");
            return;
        }

        // 3. Tiến độ nhiệm vụ 100%
        if (TaskManager.Instance != null && TaskManager.Instance.progress >= 100)
        {
            game.VillagerWin();
            NetworkGameOverManager.Instance?.TriggerGameOver("Villagers");
            return;
        }

        // Đếm số lượng người sống theo từng phe
        int aliveMonsters = 0;
        int aliveVillagers = 0;
        int aliveThirdParty = 0;
        int aliveKillers = 0;
        int aliveAwakenedWhiteHounds = 0;
        int unawakenedWhiteHounds = 0;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive || player.isDisconnected ||
                NetworkGameManager.IsPlayerLeft((ulong)player.playerID))
            {
                continue;
            }

            bool isWh = IsWhiteHound(player);
            bool awakenedWh = isWh && IsWhiteHoundAwakened(player);

            bool isMonster = awakenedWh ||
                             player.faction == FactionType.Monster ||
                             player.roleType == RoleType.DogSpirit ||
                             player.roleType == RoleType.SerpentSpirit ||
                             player.roleType == RoleType.Ogre;

            bool isThirdParty = !isMonster && !isWh && (
                player.faction == FactionType.Neutral ||
                player.roleType == RoleType.Madman ||
                player.roleType == RoleType.Jester ||
                player.roleType == RoleType.Lover ||
                player.roleType == RoleType.FoxSpirit ||
                player.roleType == RoleType.Piper ||
                player.roleType == RoleType.Killer ||
                player.roleType == RoleType.SerialKiller ||
                player.roleType == RoleType.DeathHerald);

            if (isMonster)
            {
                aliveMonsters++;
                if (awakenedWh) aliveAwakenedWhiteHounds++;
            }
            else if (isThirdParty)
            {
                aliveThirdParty++;
                if (player.roleType == RoleType.Killer || player.roleType == RoleType.SerialKiller)
                    aliveKillers++;
            }
            else if (isWh && !awakenedWh)
            {
                unawakenedWhiteHounds++;
                aliveVillagers++;
            }
            else
            {
                aliveVillagers++;
            }
        }

        // 4. Cặp đôi là 2 người sống sót cuối cùng
        if (LoversAreLastTwoAlive())
        {
            game.LoversWin();
            NetworkGameOverManager.Instance?.TriggerGameOver("Lovers");
            return;
        }

        // 5. Phe Bạch Khuyển (WhiteWolf) thắng riêng khi chỉ còn 1 Bạch Khuyển thức tỉnh và <= 2 dân làng
        if (aliveAwakenedWhiteHounds == 1 && (aliveMonsters - aliveAwakenedWhiteHounds) == 0 &&
            aliveKillers == 0 && aliveVillagers <= 2)
        {
            game.WhiteWolfWin();
            NetworkGameOverManager.Instance?.TriggerGameOver("White Wolf");
            return;
        }

        // 6. Sát Nhân thắng riêng khi chỉ còn Sát Nhân và <= 2 dân làng
        if (aliveKillers == 1 && aliveMonsters == 0 && aliveVillagers <= 2)
        {
            game.KillerWin();
            NetworkGameOverManager.Instance?.TriggerGameOver("Killer");
            return;
        }

        // 7. Phe thứ 3 là người sống sót cuối cùng
        if (aliveThirdParty > 0 && aliveMonsters == 0 && aliveVillagers == 0)
        {
            if (aliveKillers > 0)
            {
                game.KillerWin();
                NetworkGameOverManager.Instance?.TriggerGameOver("Killer");
            }
            else if (PlayerManager.Instance.players.Exists(p => p != null && p.isAlive &&
                     (p.roleType == RoleType.FoxSpirit || p.roleType == RoleType.Piper)))
            {
                game.FoxSpiritWin();
                NetworkGameOverManager.Instance?.TriggerGameOver("Fox Spirit");
            }
            else if (PlayerManager.Instance.players.Exists(p => p != null && p.isAlive &&
                     (p.roleType == RoleType.Madman || p.roleType == RoleType.Jester)))
            {
                game.MadmanWin();
                NetworkGameOverManager.Instance?.TriggerGameOver("Madman");
            }
            else
            {
                game.KillerWin();
                NetworkGameOverManager.Instance?.TriggerGameOver("ThirdParty");
            }
            return;
        }

        // 8. Hết số ngày tối đa (MaxDay) mà quái vật còn sống
        int maxDay = WinConditionManager.Instance != null ? WinConditionManager.Instance.MaxDay : 7;
        if (dayFinished && game.currentDay >= maxDay && aliveMonsters > 0)
        {
            game.WerewolfWin();
            NetworkGameOverManager.Instance?.TriggerGameOver("Werewolves");
            return;
        }

        // 9. Ma Sói thắng theo parity: aliveMonsters >= aliveVillagers + aliveThirdParty (khi không còn Killer)
        int nonMonsters = aliveVillagers + aliveThirdParty;
        if (aliveMonsters > 0 && aliveKillers == 0 && aliveMonsters >= nonMonsters)
        {
            game.WerewolfWin();
            NetworkGameOverManager.Instance?.TriggerGameOver("Werewolves");
            return;
        }

        // 10. Dân Làng thắng:
        // TUYỆT ĐỐI CHỈ KHI aliveMonsters == 0 (tất cả Sói và Bạch Khuyển thức tỉnh đã chết)
        // và còn Dân Làng (kể cả Bạch Khuyển chưa thức tỉnh)
        if (aliveMonsters == 0 && aliveVillagers > 0 && aliveThirdParty == 0)
        {
            game.VillagerWin();
            NetworkGameOverManager.Instance?.TriggerGameOver("Villagers");
            return;
        }

        // 11. Tất cả người chơi đều chết
        if (aliveMonsters == 0 && aliveVillagers == 0 && aliveThirdParty == 0)
        {
            NetworkGameOverManager.Instance?.TriggerGameOver("Villagers");
        }
    }

    private static bool LoversAreLastTwoAlive()
    {
        if (PlayerManager.Instance?.players == null) return false;

        PlayerData first = null;
        PlayerData second = null;
        int count = 0;

        foreach (var p in PlayerManager.Instance.players)
        {
            if (p == null || !p.isAlive) continue;
            count++;
            if (count > 2) return false;
            if (first == null) first = p;
            else second = p;
        }

        return count == 2 && first.loverID == second.playerID && second.loverID == first.playerID;
    }

    /// <summary>
    /// Reset toàn bộ dữ liệu WhiteHound và chuẩn bị cho ván đấu mới.
    /// </summary>
    public static void ResetForNewMatch()
    {
        if (PlayerManager.Instance?.players != null)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (p == null) continue;
                if (IsWhiteHound(p))
                {
                    p.isWhiteHoundAwakened = false;
                    p.faction = FactionType.Villager;
                    p.roleType = RoleType.WhiteHound;
                }
            }
        }

        if (NetworkRoleActionBridge.Instance != null)
        {
            NetworkRoleActionBridge.Instance.ClearBittenWhiteHounds();
        }
    }
}
