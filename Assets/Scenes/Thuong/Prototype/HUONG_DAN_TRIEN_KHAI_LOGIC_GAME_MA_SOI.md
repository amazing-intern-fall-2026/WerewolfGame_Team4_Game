# Hướng dẫn triển khai logic game Ma Sói trong phần của Thường

Tài liệu này chuyển 13 phase của bản kế hoạch thành các bước thực hiện được trên project hiện tại. Phạm vi **file được tạo hoặc sửa** chỉ gồm `Assets/Scripts/Thuong/` và `Assets/Scenes/Thuong/`. Lộ trình dưới đây ưu tiên prototype offline `Assets/Scenes/Thuong/Prototype/ThuongGameplayPrototype.unity`; không coi các hệ thống mạng hoặc scene của thành viên khác là đã hoàn thành.

> Phase 1 đã được tích hợp theo API hiện tại trên `main`: `PlayerManager`, `RoleManager`, `VoteManager`, `votePower` và `GameState.Night`. Xem [README - Phase 1](README.md#phase-1-trạng-thái-sốngchết) để chạy thử và kiểm tra. Prototype hiện lưu lobby 20 người. Không chạy công cụ tạo lại scene khi scene đã tồn tại.

## 1. Điểm xuất phát thực tế

### Môi trường và cách chạy

1. Mở thư mục project bằng **Unity 6000.6.0f1**. Package liên quan đã có: Input System, uGUI, TextMeshPro, Unity Test Framework và Netcode for GameObjects.
2. Mở `Assets/Scenes/Thuong/Prototype/ThuongGameplayPrototype.unity` ở chế độ **Single**, chờ compile xong và nhấn **Play**. Không cần thêm scene vào Build Profile để thử trong Editor.
3. Trong `Systems/PlayerManager`, đặt `Prototype Lobby Size` trước khi Play. Scene hiện lưu giá trị **20**, trong khi bảng vote chỉ có **5** hàng mục tiêu. Khi thử vote nguyên bản, dùng lobby 4-5 người; khi đã làm Phase 7, thay bảng vote động trước khi kiểm thử lobby lớn.
4. Điều khiển theo `README.md` và `TEST_4_VAI_OFFLINE.md` cùng thư mục: F1-F12 hoặc PageUp/PageDown đổi nhân vật, WASD/mũi tên di chuyển, E làm nhiệm vụ, R chơi lại. Đây là nhiều nhân vật trên **một máy**, không phải nhiều client mạng.
5. `Tools > Thuong > Create Prototype Scene` trong `ThuongPrototypeBuilder.cs` chỉ dành cho lúc **chưa có** scene. Hàm `Build()` hiện ném lỗi nếu scene đã tồn tại. Chỉnh scene hiện tại trực tiếp; không xóa/tạo lại để làm theo tài liệu.

### Những gì đã có và chỗ còn thiếu

| Thành phần | Đường dẫn trong project | Hiện trạng và việc phải làm |
| --- | --- | --- |
| Dữ liệu player | `Assets/Scripts/Thuong/Players-thuongnh/PlayerData.cs`, `PlayerStatus.cs`, `PlayerManager.cs` | Đã có `isAlive`, `roleType`, `faction`, `votePower`, `hasVoted`, `hasUseNightAction`; status còn là các bool rời rạc, chưa có vòng đời hiệu ứng. |
| Xử lý chết | `Assets/Scripts/Thuong/Death-thuongnh/DeathResolver.cs`, `DeathCause.cs` | Đã có `TryKillPlayer`, chặn vote đối với Idiot, chặn Monster khi Protected. Chưa lưu nguyên nhân, kết quả bị chặn hay phát sự kiện cho UI. |
| Luồng game | `Assets/Scripts/Thuong/GameFlow-thuongnh/GameRoleManager.cs`, `GameState.cs`, `GamePhase.cs`, `DayTimer.cs` | Luồng hiện tại: RoleReveal -> Day -> Night -> Discussion -> Voting -> Day. Đã có GameOver. Đặt các mốc thêm/xóa effect đúng chỗ chuyển phase. |
| Vote | `Assets/Scripts/Thuong/Voting-thuongnh/VoteManager.cs`, `VoteButton.cs` | Đã chặn player chết, vote lặp và target chết; dùng `votePower` trực tiếp; chưa có CannotVote hoặc ExtraVote tạm thời. Scene có 5 `VoteButton` tĩnh. |
| Kỹ năng/đích | `Assets/Scripts/Thuong/Roles-thuongnh/RoleManager.cs`, `RoleActionRules.cs`, `BaseRole.cs` | Đã kiểm tra Night, còn sống, đã dùng và không tự chọn. Chưa có bộ quy tắc theo role, chưa chặn Silenced. Có `RoleManger.cs` (sai chính tả) gần như trùng code; prototype đang gắn `RoleManager`, không gắn cả hai. |
| Đêm/bảo vệ | `Assets/Scripts/Thuong/Night-thuongnh/NightManager.cs` | Đã gom mục tiêu Sói, Guardian và Killer; phần bảo vệ bị kiểm tra cả tại NightManager lẫn DeathResolver. Phải gom thành một quyết định trong DeathResolver. |
| Thắng/kết thúc | `Assets/Scripts/Thuong/GameFlow-thuongnh/WinConditionManager.cs`, `GameRoleManager.cs` | Thắng bằng nhiệm vụ 100% và hết ngày 7 đã có; loại hết Sói/số Sói áp đảo chỉ xét khi `useEliminationWin` bật. Một số luật phe ba mới là logic thử nghiệm, cần kiểm chứng lại. |
| HUD | `Assets/Scripts/Thuong/UI-thuongnh/GameHUD.cs`, `RoleAbilityUI.cs` | HUD scene có tiến độ, phase, timer, MeetingPanel, ResultPanel. `RoleAbilityUI` tự tạo **Role Ability Canvas khi chạy**; không tìm nó trong scene lúc chưa Play. Phase 1 đã có danh sách Alive/Dead cho toàn lobby; chưa có bảng kết quả từng player. |
| Test hiện có | `Assets/Scenes/Thuong/Prototype/Editor/ThuongPrototypeChecks.cs`, `VoteClickRegression.cs` | Có kiểm tra prototype batchmode và `Tools > Thuong > Check Vote Click`. Mở rộng các test này hoặc thêm test nhỏ trong `Editor/` khi thay đổi luồng chung. |

`RoleCatalog` trong `RolePoolBuilder.cs` có 17 class role cụ thể và có thể rút role từ `Assets/Scripts/Nhat/`. `DeathResolver` còn gọi `NetworkPlayerStateSync` của Lợi và `LoverManager` của Nhật. **Chỉ sửa mã bên Thường** theo phạm vi yêu cầu; các lệnh gọi hiện có được giữ tương thích. Với `CursedRole` ở ngoài thư mục Thường, có thể chặn ở `RoleActionRules` và xử lý hiệu ứng mới trước khi gọi `role.TryUseNightAbility` để không phải sửa file của Nhật. Phần đồng bộ mạng thực sự cần một kế hoạch tích hợp riêng sau prototype offline; đừng diễn giải kết quả local là đã đồng bộ qua mạng.

## 2. Quy ước kỹ thuật trước khi tạo file

- **ID**: `playerID` trong code là 0-based; UI hiển thị `Player {id + 1}`. Dùng ID ổn định cho mọi death record, effect, vote và target; không dùng chỉ số trong list làm ID sau khi xáo role.
- **Nguồn sự thật**: `PlayerData.isAlive` là trạng thái sống/chết duy nhất; chỉ khởi tạo/reset ở đầu ván và thay đổi qua `DeathResolver` khi ván đang chạy. UI chỉ đọc. Không cho một role hoặc button tự gán `isAlive = false`.
- **Đầu vào gameplay**: mọi thao tác đi từ UI/input -> manager -> validator -> thay đổi dữ liệu -> event/UI. Nút bị khóa là phản hồi cho người chơi, **không** thay cho kiểm tra trong manager.
- **Role source**: prototype dùng `RoleManager.Instance`; không thêm `RoleManger` vào scene. Nếu code còn fallback `RoleManger`, giữ tương thích lúc chuyển đổi nhưng test đường `RoleManager` chính.
- **Thời điểm kiểm tra thắng**: sau khi một chuỗi chết hoàn tất, sau ResolveNight, sau ResolveVote, sau nhiệm vụ đạt ngưỡng và khi kết thúc ngày cuối. Không kiểm tra trong `Update()` mỗi frame.
- **Luật còn cần nhóm chốt**: có cho Guardian tự bảo vệ không (hiện **không**), Protected chặn nguồn nào (hiện Monster và NightManager còn chặn Killer), số đêm của Cursed, hòa phiếu, Idiot sau khi thoát chết có mất quyền vote không, phe ba ưu tiên thắng thế nào. Ghi luật chốt vào test trước khi code nhánh tương ứng. Ví dụ trong hướng dẫn dùng: hòa phiếu = không ai chết; Protected chỉ chặn Monster; CannotVote/ExtraVote hết sau vòng Voting; Silenced chặn kỹ năng đêm hiện tại; Cursed kích hoạt sau 2 lần ResolveNight. Đây là **đề xuất để lập trình**, không khẳng định luật chính thức.

### Danh sách file nên tạo

Tạo các file C# bằng **Assets > Create > Scripting > MonoBehaviour Script** cho component, hoặc tạo C# script thường cho enum/data/service. Tên file phải trùng tên public class/enum; đặt đúng thư mục dưới `Assets/Scripts/Thuong/`. Nếu tạo bằng file manager, quay lại Unity để Unity sinh `.meta` và compile. Không thêm `.asmdef` riêng giữa chừng vì hiện các script đang dùng `Assembly-CSharp` và tham chiếu nhiều role của Nhật/Lợi.

| File mới đề xuất | Mục đích | Gắn lên GameObject? |
| --- | --- | --- |
| `Death-thuongnh/DeathRecord.cs`, `DeathResult.cs` | Lưu target ID, `DeathCause`, ngày/đêm và kết quả Killed/Blocked/Invalid/AlreadyDead. | Không. |
| `Players-thuongnh/StatusEffectType.cs`, `StatusEffectInstance.cs` | Enum Protected, CannotVote, ExtraVote, Silenced, Cursed; dữ liệu source ID, giá trị cộng phiếu, mốc hết hạn. | Không. |
| `Players-thuongnh/StatusEffectManager.cs` | API Add/Remove/Has/GetExtraVotes/TickAtBoundary, bảo đảm mỗi player quản lý effect riêng. | Có: `Systems/StatusEffectManager`. |
| `Roles-thuongnh/TargetRules.cs`, `AbilityUseValidator.cs` | Kiểm tra target theo role và quyền dùng kỹ năng; trả `bool` + lý do cho UI. | Không, trừ khi chọn kiến trúc MonoBehaviour có state. |
| `UI-thuongnh/PlayerRosterUI.cs`, `UI-thuongnh/VoteListUI.cs`, `UI-thuongnh/MatchResultUI.cs` | Cập nhật roster, danh sách vote động, bảng kết quả. | Có: trên `Canvas` hoặc panel tương ứng. |
| `Assets/Scenes/Thuong/Prototype/UI/VoteRow.prefab` | Một hàng vote tái sử dụng cho mọi player. | Prefab, không gắn vào Systems. |

`StatusEffectInstance` nên là `[Serializable]` để thấy trong Inspector; `StatusEffectManager` nên thao tác trên danh sách effect của từng `PlayerData`, không lưu danh sách thứ hai không đồng bộ. Có thể giữ các bool cũ trong `PlayerStatus` một thời gian để chuyển đổi, nhưng sau khi di chuyển mọi call site, không dùng cả bool cũ lẫn effect mới làm hai nguồn sự thật. `PlayerData.isCursed` cũng là dữ liệu cũ cần được thay bằng hiệu ứng Cursed hoặc một adapter đọc effect mới.

### Giao diện API gợi ý để các phase khớp nhau

```csharp
// Gợi ý hợp đồng, chưa phải code hoàn chỉnh để dán nguyên file.
DeathResult TryKillPlayerDetailed(int targetID, DeathCause cause, int sourceID = -1);
bool TryKillPlayer(int targetID, DeathCause cause); // giữ wrapper cho code đang gọi
bool HasEffect(int playerID, StatusEffectType type);
int GetExtraVotes(int playerID);
bool CanUseNightAbility(int actorID, out string reason);
bool CanTarget(int actorID, int targetID, RoleType role, out string reason);
bool CanVote(int voterID, int targetID, out string reason);
```

Giữ `TryKillPlayer` kiểu `bool` để `VoteManager`, `NightManager` và các role cũ không gãy API; phương thức `Detailed` phục vụ UI và test để phân biệt chết thật với được cứu. Khi tạo event, phát `PlayerDied(DeathRecord)` sau khi cập nhật dữ liệu; event `AttackBlocked` là nhánh khác, không phát `PlayerDied`. Với chết dây chuyền như Lover, đặt cơ chế chống xử lý trùng và chỉ kiểm tra thắng sau khi chuỗi đã kết thúc.

## 3. Setup scene và UI nền

### Hierarchy đang có

```text
ThuongGameplayPrototype
├─ Main Camera
├─ Map
├─ Player                         (template để clone khi Play)
├─ TaskStations                   (6 station)
├─ Systems
│  ├─ GameRoleManager
│  ├─ PlayerManager
│  ├─ RoleManager
│  ├─ TaskManager
│  ├─ DayTimer
│  ├─ NightManager
│  ├─ VoteManager
│  ├─ DeathResolver
│  ├─ WinConditionManager
│  └─ (component ThuongFourRoleOfflineTest nằm trên chính Systems)
├─ Canvas                         (GameHUD, GraphicRaycaster, CanvasScaler)
│  ├─ TopBar
│  ├─ TaskPanel
│  ├─ MeetingPanel                 (Title, Hint, 5 Vote Player rows hiện tại)
│  └─ ResultPanel                  (ResultText, RestartHint)
└─ EventSystem                    (InputSystemUIInputModule)
```

`Role Ability Canvas` xuất hiện ở runtime do `GameHUD.Start()` thêm/khởi tạo `RoleAbilityUI`. `PlayerNameTag` cũng được tạo/cấu hình lúc clone nhân vật. Khi muốn chỉnh UI role, sửa `RoleAbilityUI.BuildUI()` và phần `RefreshRole`/`CanUse`, không cố kéo object runtime vào scene asset.

### Các bước thao tác trong Unity

1. Tạo một bản scene thử mới **trong `Assets/Scenes/Thuong/`** nếu cần so sánh nhiều nhánh; nếu giữ một scene, chỉnh `ThuongGameplayPrototype.unity` hiện có và ghi rõ từng thay đổi. Không chạy `Create Prototype Scene` để tái tạo.
2. Chọn `Systems`: thêm component `StatusEffectManager` sau khi script compile. Giữ **một** instance cho mỗi manager. `GameRoleManager.BeginGame()` yêu cầu TaskManager, DayTimer, PlayerManager, NightManager, VoteManager, DeathResolver, WinConditionManager; nếu thêm dependency mới, bổ sung check lỗi tại đây.
3. Chọn `Systems/PlayerManager`: đặt `Prototype Lobby Size` theo bộ test. Test nhỏ 4-5 người để xem vote; test danh sách động 10 và 20 người; trả lại giá trị mong muốn sau khi kiểm thử. `Players` rỗng trước Play là bình thường vì được tạo runtime.
4. Chọn `Systems/GameRoleManager`: xác nhận `Night Duration=20`, `Discussion Duration=8`, `Voting Duration=10`, `Role Reveal Duration=25`. Chọn `Systems/DayTimer`: `Day Duration=45`. Đây là giá trị scene hiện lưu, có thể giảm tạm lúc thử rồi phục hồi.
5. Chọn `Systems/WinConditionManager`: hiện `Max Day=7`, `Use Elimination Win=false`. Phase 12 phải chốt luật rồi mới bật/đổi flag này, vì bật ngay sẽ thay đổi cách kết thúc prototype cũ.
6. Chọn `Canvas`: giữ `Screen Space - Overlay`, `CanvasScaler = Scale With Screen Size`, reference resolution `1600 x 900`, Match `0.5`. Giữ `GraphicRaycaster` và một `EventSystem` với `InputSystemUIInputModule`; kiểm tra `Image.raycastTarget` của lớp phủ để không che nút vote.
7. Bổ sung `Canvas/PlayerRosterPanel` (phase 1), `MeetingPanel/Scroll View` (phase 7) và các hàng trong `ResultPanel` (phase 13) theo hướng dẫn UI riêng bên dưới. Nút chọn và danh sách dùng `Button`, `Image`, `TextMeshProUGUI`; nội dung dài phải có `ScrollRect` + `RectMask2D`.
8. Khi đã gắn script/prefab/reference trong Inspector, **Save Scene** ngoài Play Mode. Các object tạo bởi code khi Play sẽ biến mất sau khi Stop; chỉ các object dựng trong Edit Mode mới được lưu.

### Setup UI cụ thể

**Roster sống/chết (Phase 1):** Tạo `Canvas/PlayerRosterPanel` với `Image` nền, `Header` và `Scroll View/Viewport/Content`. Đặt `VerticalLayoutGroup` và `ContentSizeFitter` trên Content; mỗi hàng có `Name`, `Role` (chỉ hiện khi được phép lộ), `State` và có thể có icon. Tạo một `Button` mở/đóng ở vùng trống bên trái Footer hoặc vị trí không đè TaskPanel/MeetingPanel/Role Ability; chỉnh anchor và thử ở 1600x900 lẫn cửa sổ hẹp. Gắn `PlayerRosterUI` lên panel, kéo Content/row prefab hoặc tạo hàng bằng code. Đọc `PlayerManager.Instance.players`, refresh khi phân vai, khi `PlayerDied`, khi đổi local player; không chỉ refresh trong `Start()`. Người chết có nhãn **ĐÃ CHẾT**, màu/độ mờ và trạng thái nút phù hợp. Đừng hiện role thật cho người còn sống nếu luật bí mật vai yêu cầu che.

**Vote động (Phase 7-8):** Trong Edit Mode, kéo một `MeetingPanel/Vote Player 1` ra `Assets/Scenes/Thuong/Prototype/UI/` để tạo `VoteRow.prefab`. Row gồm `Image`, `Button`, `VoteButton`, `Name` và `State/Reason` nếu cần. Tạo `MeetingPanel/Scroll View` với `Viewport` có `RectMask2D`; `Content` dùng `VerticalLayoutGroup`, `ContentSizeFitter (Vertical = Preferred Size)`. `VoteListUI` tạo một row theo từng `PlayerData` và gán `targetID` từ dữ liệu, `voterID` từ nhân vật đang điều khiển. Bỏ hoặc tắt năm row cũ **sau khi** danh sách động chạy đúng, tránh hai nút cùng mục tiêu. Sửa `ThuongFourRoleOfflineTest.SetLocalPlayer()` để báo `VoteListUI` đổi voter; mảng `voteButtons` cache lúc Start hiện không tự biết row tạo sau đó. `VoteButton` gọi cùng `VoteManager.CanVote/TryVote`, hiển thị lý do nếu CannotVote/đã chết/đã bỏ phiếu. Hiển thị tổng phiếu có trọng số ở kết quả vote, không tiết lộ phiếu từng người trước lúc ResolveVote nếu luật không cho.

**Role Ability (Phase 5-6, 9):** `RoleAbilityUI` đang dùng `HasActiveAbility`, `CanUse`, `OpenTargets`, `SelectTarget`. Cho UI đọc validator chung của RoleManager. Mở danh sách chỉ gồm mục tiêu hợp lệ **theo role**, hoặc để hàng không hợp lệ nhưng disabled kèm lý do. Nếu bị Silenced, hiển thị lý do và khóa nút dùng kỹ năng. Sau khi bấm, manager vẫn phải kiểm tra lại vì phase/target có thể thay đổi giữa lúc mở panel và lúc click. Phần `HasActiveAbility` hiện chỉ liệt kê một số role đã có UI; các role passive tiếp tục chỉ hiện mô tả.

**Game Over (Phase 13):** `ResultPanel` hiện chỉ có `ResultText` và `RestartHint`. Thêm `Scroll View/Content` với một row cho mỗi player: tên, phe/role thật (nếu luật cho xem cuối trận), Alive/Dead, nguyên nhân chết và ngày/đêm bị loại. `MatchResultUI` nhận winner + danh sách death record, dựng snapshot **một lần khi GameOver**, không tạo lại mỗi frame. Gắn nút `Chơi lại` dùng cùng hành vi reload scene của phím R qua một hàm chung; nút `Quay về` chỉ tạo khi đã có scene/menu đích hợp lệ trong phần Thường. Không gán tên scene chưa tồn tại.

## 4. Triển khai theo thứ tự phụ thuộc

Mỗi phase dưới đây gồm code cần sửa/tạo, setup và kiểm tra. Sau mỗi phase: chờ Unity compile, kiểm tra Console không có exception, mở đúng scene, Play test đường chính và trường hợp từ chối. Nên ghi lại một commit riêng cho mỗi nhóm phase đã chạy ổn, nếu nhóm đang dùng Git.

### Phase 1 - Trạng thái sống/chết

1. Giữ `PlayerData.isAlive` làm trạng thái chuẩn. `PlayerManager.CreateTestPlayer` và `RoleManager.AssignRole` được phép đặt `true` ở **đầu ván**; trong trận không ai ngoài `DeathResolver` được đặt `false`. Tìm các chỗ gán bằng `rg -n 'isAlive\s*=' Assets/Scripts/Thuong` trước mỗi lần chỉnh.
2. Thêm API đọc an toàn ở `PlayerManager`: `GetplayerByID` hiện có; có thể thêm `IsAlive(int id)` và `GetAlivePlayers()` để tránh lặp logic null. Không đổi ID khi player chết, không xóa player khỏi `players` vì vote/result cần lịch sử.
3. `PlayerMovement.IsAlive`, `TaskStation` và `VoteManager` đã đọc `isAlive`; rà thêm `RoleManager`, `RoleAbilityUI`, `VoteButton`, các callback role. Nhân vật chết dừng di chuyển, task, kỹ năng và vote từ **manager**. Thêm roster theo setup UI trên.
4. Test: player sống làm task/vote/ability; đổi sang chết qua DeathResolver trong test; sau đó mọi thao tác bị từ chối, roster và nametag cùng hiển thị trạng thái. Reload scene thì tất cả lại sống.

### Phase 2 - Hệ thống chết cơ bản

1. Giữ `DeathCause` hiện có (`Vote`, `Monster`, `Poison`, `Trap`, `Killer`, `Lover`, `DeathHerald`); thêm `Curse` hoặc nguyên nhân khác **ở cuối enum** để tránh đổi giá trị serialized cũ. Định nghĩa `DeathRecord` gồm `targetID`, `cause`, `sourceID` (nếu có), `day`, `phase`/`nightIndex`. `DeathResult` nên phân biệt ít nhất `Killed`, `Blocked`, `AlreadyDead`, `InvalidTarget`.
2. Trong `DeathResolver.TryKillPlayerDetailed`: lấy player; từ chối ID sai/đã chết; áp passive/Protected; chỉ ở nhánh Killed mới đặt `isAlive=false`, ghi record đúng một lần, gọi sync/`role.OnDeath()`/`LoverManager` và phát event. Giữ `TryKillPlayer(...) => result == Killed` cho code cũ. Nếu passive `OnDeath` tạo thêm death, bảo vệ chống lặp theo ID.
3. `VoteManager.ResolveVote` và `NightManager.ResolveNight` đã gọi DeathResolver. Rà các role trong phần Thường để không gán `isAlive` trực tiếp. Với code role ngoài phần Thường, kiểm tra call site khi tích hợp; không chỉnh file ngoài phạm vi.
4. Test bốn trường hợp: ID không tồn tại, mục tiêu chết trước, Vote giết, Monster giết. Mỗi người chỉ có một record/sự kiện chết; voter/target trong UI cập nhật ngay.

### Phase 3 - Nền tảng Status Effect

1. Tạo `StatusEffectType` và `StatusEffectInstance`. Một instance nên chứa `type`, `sourcePlayerID`, `stacks/value` (ExtraVote), `appliedDay`, `appliedNight`, `expiryBoundary` hoặc `remainingTriggers`. Dùng một cách đếm nhất quán, không vừa dựa trên `Time.time` vừa dựa trên phase.
2. Tạo `StatusEffectManager` với `AddEffect`, `RemoveEffect`, `HasEffect`, `GetExtraVotes`, `TickAtBoundary`. Mỗi lần Add phải có chính sách trùng: Protected không cộng dồn, CannotVote/Silenced có thể làm mới hạn, ExtraVote cộng giá trị hay lấy max theo luật; Cursed không nhân nhiều timer ngoài ý muốn. Reject ID không tồn tại và nguồn không hợp lệ nếu luật yêu cầu.
3. Gắn manager lên `Systems`. Nếu `PlayerStatus` chứa list effect, khởi tạo list cho từng `PlayerData` trong `CreateTestPlayer`; thêm kiểm tra null cho dữ liệu cũ trong Inspector. Không reset effect khi `RoleManager.AssignRole` chạy trừ khi bắt đầu ván mới.
4. Chọn mốc rõ: `NightManager.StartNight` mở chu kỳ đêm; `ResolveNight` áp damage rồi hết Protected/Silenced của đêm; `VoteManager.StartVote` mở vòng phiếu; `ResolveVote` áp phiếu rồi hết CannotVote/ExtraVote. Viết test effect của hai player độc lập, refresh hạn đúng một lần, và effect vẫn có hiệu lực cho đến **sau** hành động cuối chu kỳ.

### Phase 4 - Protected

1. `GuardianRole.TryUseNightAbility` đã gọi `NightManager.SetProtectedTarget`. Tại `ResolveNight`, đánh dấu Protected lên đúng target còn sống **trước** khi xử lý các cuộc tấn công.
2. Bỏ nhánh `protectedPlayer == target` tự chặn trong `NightManager`; mọi cú đánh đều gọi `DeathResolver.TryKillPlayerDetailed`. DeathResolver quyết định cause nào bị Protected chặn; nếu chặn thì trả `Blocked`, không tạo DeathRecord, tùy luật có tiêu hao effect ngay hay sau cả đêm. Với đề xuất Monster-only, Killer vẫn đi qua và không bị chặn.
3. Kết thúc `ResolveNight` mới dọn Protected còn lại. Không xóa ở `StartDay` trước khi kết quả đêm xử lý xong. HUD kết quả đêm nên lấy `DeathResult`: có người chết/được cứu/không có cuộc tấn công là ba tình huống khác nhau.
4. Test: Guardian bảo vệ đúng mục tiêu Sói -> sống; bảo vệ người khác -> mục tiêu chết; Killer tấn công người Protected -> theo chính sách cause đã chốt; target chết rồi không nhận Protected; Guardian không thể chọn cùng mục tiêu hai đêm liền như code hiện tại.

### Phase 5 - Kiểm tra mục tiêu

1. Trong `TargetRules.CanTarget(actorID, targetID, role, out reason)`, kiểm tra actor/target có tồn tại, target còn sống, cho phép tự chọn hay không, faction được phép chọn, quy tắc riêng role (ví dụ Guardian không chọn lại `lastTarget`). Dùng `RoleType`/`FactionType` đã có; đặt rule dưới `Assets/Scripts/Thuong/Roles-thuongnh/`.
2. Đưa đoạn kiểm tra `playerID == targetID` và target null/chết từ `RoleManager.UseNightAbility` sang validator. Gọi validator **trước** `RoleActionRules.TryUseNightAbility`; giữ phản hồi `out feedback` cho UI. Khi chuẩn bị cho vai mới, thêm cấu hình role rule vào một chỗ, tránh copy/paste trong từng role.
3. `RoleAbilityUI.OpenTargets` hiện lọc sống và khác mình theo luật chung. Thay bằng cùng `CanTarget`; refresh danh sách khi player đổi hoặc phase đổi. Kiểm tra lại ở `SelectTarget` qua manager; không tin targetID do button gửi lên.
4. Test self-target, ID sai, target đã chết, phe không hợp lệ, Guardian chọn lặp, và target hợp lệ. Nếu nhóm quyết định Guardian được tự bảo vệ, đổi rule + UI + test cùng lúc.

### Phase 6 - Điều kiện sử dụng kỹ năng

1. `AbilityUseValidator.CanUseNightAbility` kiểm tra: game đang `GameState.Night`, actor có role được gán, actor còn sống, role có `HasNightAbility`, chưa `hasUseNightAction`, không có effect Silenced. Thứ tự kiểm tra quyết định lý do UI nhận được; thống nhất trong một hàm.
2. `RoleManager.UseNightAbility` gọi validator -> TargetRules -> RoleActionRules/role thực thi -> chỉ sau khi thành công mới đặt `hasUseNightAction=true`. `NightManager.StartNight` hiện reset cờ này; giữ reset đúng một lần mỗi đêm.
3. `RoleAbilityUI.CanUse` dùng validator này để khóa nút và hiển thị lý do. Cần sửa `useButton.interactable` hiện chỉ dựa trên `HasActiveAbility`; dù UI sai trạng thái, manager vẫn từ chối.
4. Test sai phase, chết, passive role, dùng lặp cùng đêm, Silenced, target sai, và kỹ năng hợp lệ. Đặc biệt kiểm tra role trả `false` không tiêu hao lượt.

**Mốc ưu tiên:** Hoàn thành Phase 1-6 rồi chạy một vòng đầy đủ RoleReveal -> Day -> Night -> Discussion -> Voting -> Day và regression đang có trước khi tiếp tục.

### Phase 7 - Cannot Vote

1. `VoteManager.CanVote` và `TryVote` phải từ chối voter chết, CannotVote, đã vote, sai phase, target chết/không tồn tại. `VoteButton.Update` chỉ đọc cùng `CanVote`; không giữ một bộ điều kiện khác.
2. Dùng `VoteListUI` và Scroll View động như phần setup UI, vì 5 row hiện tại khiến lobby 20 người không thể vote mọi mục tiêu. Khi đổi local player F-key, hàng vote phải đọc đúng voter mới.
3. Cho CannotVote hết hiệu lực **sau** `ResolveVote` (hoặc mốc luật chốt). `StartVote` chỉ reset `hasVoted`; không xóa effect trước khi người chơi vote.
4. Test thử gọi `VoteManager.TryVote` trực tiếp với CannotVote và bấm UI: cả hai đều bị chặn, `choices`/tổng phiếu không đổi. Test xóa effect rồi vòng sau vote được.

### Phase 8 - Extra Vote

1. Giữ `PlayerData.votePower` là phiếu cơ bản (`MayorRole.OnGameStart` hiện đặt 2). Tính sức nặng lúc bỏ phiếu bằng `Mathf.Max(1, voter.votePower) + StatusEffectManager.GetExtraVotes(voterID)`; quy định min/max nếu effect có giá trị âm.
2. Lưu phiếu bằng một record per voter (`voterID`, `targetID`, `weight`) hoặc đảm bảo `choices` và `votes` luôn nhất quán. Không cộng effect vào `votePower` vĩnh viễn. Nếu cho đổi phiếu trong tương lai, trừ weight cũ trước khi cộng mới.
3. `ResolveVote` dùng tổng weight để tìm người nhiều phiếu nhất, xử lý hòa theo luật; Result/Meeting UI hiển thị tổng sau effect. Hết vòng vote mới dọn ExtraVote.
4. Test phiếu thường 1, Mayor 2, thường +1 = 2, Mayor +1 = 3; vòng sau trở về cơ bản; hòa phiếu không làm chết ai nếu giữ luật đề xuất.

### Phase 9 - Silenced

1. **Tách nghĩa trước khi dùng:** `PlayerManager.LockPlayers/UnlockPlayers` hiện gán `player.status.isSilenced` cho **mọi** người khi hết ngày/bắt đầu ngày, dù đó là khóa theo phase. `PlayerMovement` đã tự chặn di chuyển ngoài Day, còn AbilityUseValidator tự kiểm tra Night. Bỏ việc dùng `isSilenced` cho phase lock; nếu cần khóa movement riêng, dùng cờ khác.
2. Chuyển Silenced sang `StatusEffectManager`. Chỉ actor có effect mới bị chặn kỹ năng; `RoleManager` và `RoleAbilityUI` cùng đọc validator. Không reset toàn lobby ở `StartDay` khi effect còn hạn theo luật.
3. Hết hạn tại `ResolveNight` (theo luật đề xuất) hoặc mốc đã chốt. Test một người bị câm, người khác vẫn dùng kỹ năng; người bị câm không tiêu hao action khi thử; đêm sau dùng được.

### Phase 10 - Cursed

1. `CursedRole` của Nhật hiện chỉ đặt `target.isCursed=true`, không có đếm lượt/kích hoạt. Để giữ phạm vi Thường, thêm nhánh `RoleType.Cursed` ở `RoleActionRules.TryUseNightAbility` hoặc adapter dưới `Assets/Scripts/Thuong/`: Add Cursed effect vào target, trả feedback, **không gọi tiếp** implementation cũ cho nhánh này. Đảm bảo action chỉ tiêu hao khi Add thành công.
2. Effect Cursed chứa số lần `ResolveNight` còn lại, không phụ thuộc frame rate. Quyết định rõ đêm đặt có tính là lần đầu không. Ví dụ: đặt đêm N, sau ResolveNight của N còn 1, sau ResolveNight của N+1 kích hoạt.
3. Khi hết hạn, gọi `DeathResolver.TryKillPlayerDetailed(target, DeathCause.Curse, sourceID)`. Nếu target đã chết, xóa Cursed mà không tạo death thứ hai. Nếu Protected không chặn Curse theo luật đã chốt, kiểm tra nguyên nhân trước khi chặn.
4. Test hết hạn đúng mốc, target chết trước, nguồn chết trước (luật còn tác dụng hay không cần chốt), nhiều lời nguyền trên cùng target, và kết quả UI/DeathRecord.

### Phase 11 - Hoàn thiện hệ thống chết

1. Gộp toàn bộ nhánh Vote, Monster, Killer, Poison, Trap, Lover, Curse vào `DeathResolver`. Một bảng chính sách `DeathCause -> bị chặn bởi Protected? -> passive nào xét?` dễ kiểm tra hơn nhiều `if` rải rác.
2. Kiểm tra thứ tự: hợp lệ/còn sống -> bảo vệ -> passive role -> xác nhận chết -> cập nhật `isAlive` -> lưu nguyên nhân -> sync/event -> hiệu ứng sau chết -> kiểm tra thắng **sau chuỗi**. Tránh phát sự kiện chết nếu Idiot thoát vote hoặc Protected cứu.
3. Với `LoverManager` bên ngoài phần Thường, giữ lệnh gọi hiện có nhưng test chết dây chuyền: A chết -> B chết `Lover`; mỗi người đúng một record. Xác nhận không gọi win condition giữa A và B.
4. Test bảng từng cause, bị chặn/không bị chặn, chết trùng, callback OnDeath, UI và `NetworkPlayerStateSync.SyncGameplayAliveState` không lỗi ở offline (không có NetworkManager). Việc test offline không xác nhận mạng thực.

### Phase 12 - Điều kiện thắng

1. Chốt luật rồi sửa `WinConditionManager.CheckWinCondition`. Hiện có thắng nhiệm vụ 100%, ngày 7, vài nhánh phe ba, còn elimination bị cổng `useEliminationWin=false`. Dùng danh sách `PlayerManager.players` lọc `isAlive`, phân loại theo `FactionType` và vai đặc biệt; xác định thứ tự ưu tiên nếu nhiều điều kiện đồng thời đúng.
2. Quy tắc đề xuất để test: dân thắng khi không còn Sói **và** không còn mối đe dọa theo luật; Sói thắng khi số Sói sống >= tổng người sống thuộc phe đối địch còn liên quan; phe ba có predicate riêng, không suy từ `aliveThirdParty` gộp chung. Điều kiện nhiệm vụ 100% và giới hạn ngày 7 phải được chốt thứ tự ưu tiên so với elimination.
3. Gọi check sau ResolveNight và ResolveVote như flow hiện tại; khi Phase 11 có death dây chuyền, check sau chuỗi. `TaskManager.CompleteTask` hiện gọi `VillagerWin()` trực tiếp ở 100%; nếu muốn một đầu mối, đổi thành gọi WinConditionManager theo luật mới. Không thêm polling trong HUD/Update.
4. Test bảng tình huống: 0 Sói, Sói áp đảo, còn phe ba, ngày cuối, nhiệm vụ 100%, cùng lúc có hai điều kiện, player chết dây chuyền. Mỗi trường hợp chỉ có một winner và GameOver đúng một lần.

### Phase 13 - Game Over và UI kết quả

1. `GameRoleManager.EndGame` hiện đã đặt `Winner`, `GameState.GameOver`, dừng DayTimer và khóa player. Sau khi Phase 9 tách Silenced, bỏ việc dùng `LockPlayers` như hiệu ứng; mọi entry gameplay (vote, ability, task, movement, phase transition) phải từ chối khi GameOver.
2. `GameHUD` đang bật `ResultPanel` khi GameOver. Thêm `MatchResultUI`/row động và death record vào panel theo setup UI; hiển thị rõ phe thắng, vai, sống/chết, nguyên nhân, ngày/đêm, và trạng thái cứu/bị chặn nếu cần trong log trận.
3. Dùng cùng một hàm reload scene cho nút Chơi lại và phím R của `PrototypeControls`, tránh logic hai nơi khác nhau. Trước khi quyết định cho người chơi bấm quay về menu, xác nhận scene đích và Build Profile.
4. Test winner hiện một lần; timer đứng yên; vote/ability/task không đổi dữ liệu; R/nút chơi lại tạo ván mới với trạng thái/effect/death record rỗng; UI không giữ kết quả ván cũ.

## 5. Ma trận kiểm thử sau khi hoàn tất

| Tình huống | Cách tạo trong prototype | Kết quả mong đợi |
| --- | --- | --- |
| Player chết | Gọi DeathResolver với ID hợp lệ trong một test Editor/Play Mode | `isAlive=false`, có một record, nametag/roster đổi, movement/task/vote/ability bị chặn. |
| Bảo vệ | Guardian chọn đúng người bị Sói nhắm | Không có DeathRecord cho target; có kết quả Blocked/được cứu; effect hết đúng mốc. |
| Chọn sai mục tiêu | Self, ID không tồn tại, target chết, role rule sai | Manager trả false + lý do, không tiêu hao action. |
| Skill sai phase/đã dùng/Silenced | Gọi API trực tiếp và qua UI | Cả hai đường bị từ chối giống nhau; effect chỉ ảnh hưởng player được gán. |
| CannotVote | Gán effect trước Voting | Button disabled; gọi API trực tiếp vẫn false; tổng phiếu không đổi. |
| ExtraVote | So sánh thường, Mayor, có effect | Weight đúng và hết hạn; `votePower` gốc không đổi. |
| Cursed | Gán trong đêm và chuyển nhiều đêm | Tick đúng số lần, chỉ trigger một lần qua DeathResolver. |
| Điều kiện thắng | Sắp xếp alive/faction/task/ngày theo từng luật | Winner duy nhất, GameOver, timer/action dừng. |
| Lobby lớn | Đặt `Prototype Lobby Size=10` rồi `20` | Roster, target list, vote list cuộn được, có đủ ID; không có nút trùng/mất click. |
| Restart | Bấm R và nút chơi lại sau GameOver | Ván mới không giữ effect, phiếu hoặc death record. |

Giữ test regression hiện có: `Tools > Thuong > Check Vote Click` (ngoài Play Mode) và check Play Mode trong `ThuongPrototypeChecks.cs` khi chạy batchmode; log pass mong đợi là `THUONG_PROTOTYPE_PLAYMODE_PASSED`. Sau khi thay vote tĩnh bằng vote động, cập nhật chính các check này để chúng tìm row mới. Các phím E/R cần thử thủ công khi Game view có focus theo ghi chú của README. Nếu tạo test mới, đặt trong `Assets/Scenes/Thuong/Prototype/Editor/`, dùng scene preview hoặc Play Mode và tránh ghi đè scene asset khi test.

## 6. Thứ tự làm việc thực tế và bàn giao

1. Chốt 6 luật mơ hồ ở mục 2; ghi ví dụ input/output cho từng luật. Việc này quyết định API effect/death và tránh phải viết lại test.
2. Hoàn thành Phase 1-2: trạng thái, death record/result, roster tối thiểu. Chạy test chết, task, vote, reload.
3. Hoàn thành Phase 3-4: effect manager, Protected chỉ xử lý ở DeathResolver. Chạy test kết quả đêm.
4. Hoàn thành Phase 5-6: validator target/ability, nối `RoleManager` và `RoleAbilityUI`. Chạy một vòng game đầy đủ.
5. Hoàn thành Phase 7-9: vote list động, CannotVote, ExtraVote, Silenced; kiểm tra lobby 4/10/20 người.
6. Hoàn thành Phase 10-11: Cursed và death pipeline đầy đủ, gồm Lover/passive/blocked.
7. Hoàn thành Phase 12-13: luật thắng được chốt, bảng kết quả, restart, toàn bộ regression.

Trước mỗi lần bàn giao: scene mở được bằng Unity 6000.6.0f1; không có Missing Script/Inspector reference; các script mới nằm trong `Assets/Scripts/Thuong/`, UI/prefab/test trong `Assets/Scenes/Thuong/`; scene được lưu ngoài Play Mode; Console không có exception; ma trận test đã ghi kết quả và luật đã chốt. Nếu một tính năng cần sửa mã ở `Assets/Scripts/Nhat` hoặc `Assets/Scripts/Loi` để chạy online/chung scene, ghi rõ là **phụ thuộc tích hợp ngoài phạm vi**, không âm thầm sửa các thư mục đó.
