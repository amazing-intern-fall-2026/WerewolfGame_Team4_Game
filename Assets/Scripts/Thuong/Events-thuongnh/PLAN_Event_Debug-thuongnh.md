# Plan random event theo ngày và hiển thị debug

**Dự án:** Werewolf Game – Team 4  
**Phạm vi:** `Assets/Scripts/Thuong`  
**Trạng thái:** Đã được duyệt và triển khai phần lịch event cùng HUD. Đã biên dịch thành công; chưa xác nhận Play Mode và giao diện thực tế. Xem mục 18.

## 1. Mục tiêu

Mỗi ván chọn ngẫu nhiên đúng **một event** và **một ngày xuất hiện**. Lưu lịch ngay đầu ván, không random lại khi đổi ngày hoặc cập nhật UI. Đến ngày đã chọn, hiển thị tên event cùng dòng với ngày.

Ví dụ khi chọn Festival vào ngày 3:

```text
Ngày 1
Ngày 2
Ngày 3-festival event
Ngày 4
```

Plan này thay cơ chế roll xác suất mỗi ngày trong tài liệu event ban đầu bằng cơ chế chọn trước một lịch ngẫu nhiên cho mỗi ván.

## 2. Phạm vi triển khai

### Làm trong giai đoạn này

- Chọn một event trong toàn bộ event hiện có, loại `None`.
- Chọn ngày từ 1 đến số ngày tối đa của ván, bao gồm hai đầu mút.
- Lưu event và ngày đã chọn trong runtime.
- Hiển thị ngày và tên event trên HUD khi đến đúng ngày.
- Bỏ tên event khỏi dòng ngày khi sang ngày tiếp theo.
- Ghi lịch đã chọn vào Console để debug.
- Tạo lịch mới khi chơi lại.

### Để giai đoạn sau

- Hai hoặc ba event trong một ván.
- Weight, Reward Point, xác suất roll mỗi ngày và cooldown.
- Điều kiện lọc theo role hoặc số người sống.
- Hiệu ứng gameplay: giết hai mục tiêu, khóa kỹ năng, miễn chết, delayed death và buff role đặc biệt.
- Popup, icon, ScriptableObject cấu hình event và Canvas mới.
- Đồng bộ multiplayer. Bản này phục vụ prototype debug offline; chưa để các client tự chạy lịch độc lập trong bản online.

## 3. Quy tắc random

```text
Bắt đầu ván
    ↓
Random một event, loại None
    ↓
Random một ngày từ 1 đến MaxDay
    ↓
Lưu lịch một lần
    ↓
Theo dõi currentDay để hiển thị
```

| Enum hiện có | Tên hiển thị |
|---|---|
| `BloodMoon` | `blood moon event` |
| `Fog` | `fog event` |
| `HarvestFestival` | `festival event` |
| `ClearSky` | `clear sky event` |
| `GhostMonth` | `ghost month event` |
| `TuongNight` | `tuong night event` |

- Mỗi event có cơ hội được chọn như nhau.
- Mỗi ngày hợp lệ có cơ hội được chọn như nhau.
- Hai ván liên tiếp có thể trùng event hoặc ngày; không ép kết quả phải khác nhau.
- Không gắn riêng event nào vào một ngày cố định.
- Không đặt random trong `Update()` hoặc hàm cập nhật UI.
- Nếu dùng `Random.Range` với số nguyên, giới hạn trên phải là `totalDays + 1` để ngày cuối cũng được chọn.

**Trận kết thúc sớm:** nếu lịch là ngày 6 nhưng trận kết thúc ngày 3, event chưa xuất hiện. Không dời lịch về ngày kết thúc. Mỗi ván có đúng một event được lên lịch, nhưng không bảo đảm trận kéo dài đến ngày đó.

## 4. Hiển thị HUD

### Dòng ngày

- Có event đúng ngày: `Ngày 3-festival event`.
- Không có event trong ngày: `Ngày 3`.
- Dùng dấu `-` thông thường, không thêm khoảng trắng quanh dấu.
- Không có ngoặc hoặc dấu nháy trong nội dung hiển thị.
- Không hiển thị event tương lai trên dòng ngày.

### Phase và thời gian

HUD hiện dùng `phaseText` để hiển thị cả ngày và phase. Phương án mới:

| Field hiện có | Nội dung mới |
|---|---|
| `phaseText` | Ngày và event của ngày đó |
| `timerText` | Phase và thời gian còn lại |

Ví dụ hai vị trí hiển thị:

```text
Ngày 3-festival event
Night | 15s
```

Giữ vị trí đồng hồ hiện có. Cấu hình chữ ngày và event không xuống dòng, tự giảm cỡ chữ trong giới hạn hợp lý nếu cần. Nếu khung hiện tại không đủ chỗ, báo rõ trước khi đề xuất chỉnh scene ngoài phạm vi được phép.

## 5. Thời gian hiển thị event

Tên event được hiển thị trong toàn bộ vòng có cùng `currentDay`.

Flow prototype hiện tại:

```text
Task → Night → Discussion → Vote → Ngày tiếp theo
```

Nếu lịch là ngày 3, tên event giữ nguyên qua các phase của ngày 3. Khi `currentDay` chuyển sang 4, dòng ngày trở về `Ngày 4`.

Đây là thời gian hiển thị debug. Thời gian áp dụng hiệu ứng gameplay sẽ được xác định riêng ở giai đoạn sau.

## 6. File liên quan

```text
Assets/Scripts/Thuong/
├── Events-thuongnh/
│   ├── EventType.cs
│   ├── EventManagert.cs
│   └── PLAN_Event_Debug-thuongnh.md
├── GameFlow-thuongnh/
│   ├── GameRoleManager.cs
│   └── WinConditionManager.cs
└── UI-thuongnh/
    └── GameHUD.cs
```

| File | Thay đổi dự kiến |
|---|---|
| `EventType.cs` | Dùng enum hiện có, không tạo enum trùng hoặc đổi thứ tự |
| `EventManagert.cs` | Quản lý lịch, random, tra event theo ngày và tên hiển thị |
| `WinConditionManager.cs` | Bổ sung thuộc tính chỉ đọc `MaxDay` |
| `GameRoleManager.cs` | Khởi tạo lịch lúc bắt đầu ván, log lúc đến ngày event |
| `GameHUD.cs` | Hiển thị ngày-event và chuyển phase sang dòng thời gian |

Giữ nguyên tên class `EventManagert`. Không gộp việc đổi tên, đổi namespace hay sửa gameplay vào đợt triển khai này. Giữ nguyên GUID của các script.

## 7. Thiết kế EventManagert

### Dữ liệu runtime

| Thành phần | Vai trò |
|---|---|
| `SelectedEvent` | Event đã được random |
| `ScheduledDay` | Ngày xuất hiện |
| `HasSchedule` | Đã tạo lịch cho ván hay chưa |

Các dữ liệu này không lưu vào `PlayerPrefs` hoặc file.

### InitializeForMatch(int totalDays)

1. Kiểm tra `totalDays >= 1`; cấu hình không hợp lệ thì báo lỗi, không tạo lịch sai.
2. Nếu đã có lịch, giữ nguyên và không random thêm.
3. Lấy các giá trị `GameEventType`, loại `None`.
4. Kiểm tra danh sách còn ít nhất một event.
5. Chọn ngẫu nhiên một event.
6. Chọn ngày ngẫu nhiên từ 1 đến `totalDays`.
7. Lưu kết quả và đánh dấu đã có lịch.
8. Ghi một dòng Console thông báo lịch.

### GetEventForDay(int day)

- Trả `SelectedEvent` nếu đã có lịch và `day == ScheduledDay`.
- Trả `None` nếu chưa có lịch hoặc ngày không khớp.

### GetDisplayName(GameEventType eventType)

Chuyển enum thành tên hiển thị theo bảng ở mục 3. `None` không tạo hậu tố tên event trên HUD.

### ResetForNewMatch()

Xóa lịch và dữ liệu debug của ván trước để chuẩn bị tạo lịch mới khi chơi lại trong cùng scene.

## 8. Dùng số ngày từ WinConditionManager

Tận dụng cấu hình hiện có:

```csharp
[SerializeField, Min(1)]
private int maxDay = 7;
```

Bổ sung thuộc tính chỉ đọc:

```csharp
public int MaxDay => maxDay;
```

| MaxDay lúc bắt đầu ván | Phạm vi random |
|---|---|
| 1 | Ngày 1 |
| 7 | Ngày 1–7 |
| 10 | Ngày 1–10 |

Không tạo cấu hình tổng số ngày thứ hai trong EventManagert. Giữ nguyên luật thắng/thua. MaxDay được đọc lúc khởi tạo lịch; không tự random lại nếu chỉnh Inspector giữa ván.

## 9. Tích hợp GameRoleManager

Khởi tạo lịch trong `BeginGame()`, sau khi kiểm tra manager cần thiết và trước ngày đầu tiên:

```text
Kiểm tra hệ thống
    ↓
Chuẩn bị EventManagert
    ↓
Phân role
    ↓
InitializeForMatch với MaxDay
    ↓
StartDay()
```

- Nếu scene đã có EventManagert thì sử dụng instance hiện có.
- Nếu chưa có thì thêm component lúc runtime vào object chứa GameRoleManager.
- Không lưu thay đổi runtime đó vào scene.
- Không random trong `StartDay()`, `EndDay()` hoặc `FinishVoting()`.
- Giữ nguyên flow ngày/đêm hiện tại.
- Quản lý singleton và `OnDestroy` để không giữ instance cũ khi tải lại scene.

## 10. Tích hợp GameHUD

```text
Đọc currentDay
    ↓
GetEventForDay(currentDay)
    ↓
None → Ngày N
Có event → Ngày N-tên event
```

HUD chỉ đọc kết quả, không chọn event hoặc đổi lịch. Việc cập nhật mỗi frame chỉ cập nhật nội dung hiển thị.

Nếu chưa có manager hoặc chưa có lịch, HUD vẫn hiển thị `Ngày N`, không phát sinh lỗi null.

## 11. Console debug

Ngay khi tạo lịch, ghi một lần:

```text
[Event Debug] Đã chọn festival event | Ngày xuất hiện: 3
```

Khi bắt đầu ngày đã chọn, ghi một lần:

```text
[Event Debug] Ngày 3-festival event
```

Không log mỗi frame và không log lặp khi đổi phase trong cùng ngày. Console cho phép xem lịch tương lai để debug; HUD chỉ hiển thị event khi đến đúng ngày.

## 12. Chơi lại

### Tải lại scene như prototype hiện tại

1. Manager cũ bị hủy và singleton được xóa.
2. Scene tạo manager mới.
3. Bắt đầu ván và tạo lịch mới.

### Chơi lại không tải scene trong tương lai

Luồng bắt đầu ván mới gọi `ResetForNewMatch()` trước khi khởi tạo lịch. Cần đặt lại cả trạng thái ván của GameRoleManager theo cơ chế restart khi cơ chế này được bổ sung.

## 13. Trình tự triển khai sau khi duyệt

1. Kiểm tra code hiện tại và ghi nhận lỗi có sẵn.
2. Bổ sung dữ liệu và hàm lịch random vào EventManagert.
3. Bổ sung thuộc tính `MaxDay` trong WinConditionManager.
4. Nối khởi tạo lịch vào GameRoleManager.
5. Sửa dòng ngày và dòng phase/thời gian trong GameHUD.
6. Thêm log tạo lịch và log đến ngày event.
7. Kiểm tra logic và giao diện trong khả năng môi trường.
8. Kiểm tra thay đổi chỉ nằm trong `Assets/Scripts/Thuong`.
9. Báo kết quả, phần đã kiểm tra và mọi giới hạn chưa kiểm chứng.

Không tự gộp sửa lỗi không liên quan. Nếu lỗi có sẵn cản trở chạy thử, báo rõ lỗi và ảnh hưởng.

## 14. Kiểm thử

| Trường hợp | Kết quả cần đạt |
|---|---|
| Bắt đầu ván | Tạo đúng một lịch |
| Gọi khởi tạo hai lần | Lịch không đổi |
| Chọn event | Không bao giờ chọn None |
| Ván có 7 ngày | Ngày thuộc khoảng 1–7 |
| MaxDay bằng 1 | Event nằm ở ngày 1 |
| Số ngày không hợp lệ | Báo lỗi, không tạo lịch sai |
| Event ở ngày 1 | Hiện ngay trong ngày đầu |
| Event ở ngày cuối | Hiện khi đến ngày cuối |
| Chưa đến ngày event | Chỉ hiện Ngày N |
| Đúng ngày event | Hiện Ngày N-tên event |
| Đổi phase trong ngày event | Tên event giữ nguyên |
| Sang ngày tiếp theo | Bỏ tên event |
| HUD cập nhật nhiều lần | Không thay đổi lịch |
| Chưa có manager hoặc lịch | HUD vẫn hiện ngày, không lỗi null |
| Chơi lại | Khởi tạo lịch mới, không dùng lịch cũ |
| Kết thúc sớm | Không ép event xuất hiện |
| Tên event dài | Cùng dòng, không đè UI |
| Console | Không spam log mỗi frame |
| Kiểm tra phạm vi | Không sửa file ngoài Thuong |

Chạy nhiều ván để quan sát ngày và event thay đổi, nhưng không yêu cầu hai ván liên tiếp phải khác nhau. Kiểm tra các ngày biên bằng dữ liệu kiểm thử kiểm soát được, không chỉ chờ random tình cờ chọn đúng ngày.

## 15. Tiêu chí hoàn thành

- [ ] Mỗi ván lên lịch đúng một event trong sáu event hiện có.
- [ ] Ngày xuất hiện nằm trong thời lượng ván.
- [ ] Lịch không đổi suốt ván.
- [ ] HUD hiện đúng dạng `Ngày 3-festival event` vào đúng ngày.
- [ ] Không có ngoặc hoặc dấu nháy trong dòng hiển thị.
- [ ] Phase và đồng hồ vẫn xem được để debug.
- [ ] Console hiển thị lịch và ngày xuất hiện, không log liên tục.
- [ ] Chơi lại tạo lịch mới.
- [ ] Không thay đổi hiệu ứng gameplay hoặc luật role.
- [ ] Không sửa file ngoài `Assets/Scripts/Thuong`.
- [ ] Kết quả kiểm tra và giới hạn được báo rõ.

## 16. Điều kiện bắt đầu

Điều kiện ban đầu là chỉ triển khai sau khi người dùng duyệt plan. Người dùng đã duyệt bằng yêu cầu “Ok triển khai đi”; kết quả triển khai được ghi ở mục 18.

## 17. Code chi tiết dự kiến

Các đoạn dưới đây là thiết kế đã được duyệt và áp dụng vào các file C#. Đường dẫn trong các mục con tính từ `Assets/Scripts/Thuong/`. Không dán lại thành hàm trùng tên. Phần tên phase ngắn trên đồng hồ được điều chỉnh khi triển khai, xem mục 18. Code chưa được chạy thử trong Unity Play Mode.

### 17.1. Events-thuongnh/EventManagert.cs

**Cách áp dụng:** thay toàn bộ nội dung file hiện có bằng class dưới đây. Giữ file và `.meta` hiện tại để bảo toàn GUID. Không tạo class EventManager thứ hai.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public class EventManagert : MonoBehaviour
{
    public static EventManagert Instance { get; private set; }

    public GameEventType SelectedEvent { get; private set; }
        = GameEventType.None;

    public int ScheduledDay { get; private set; }
    public bool HasSchedule { get; private set; }

    // Một ván chỉ có một ngày event, nên chỉ cần một cờ chống log trùng.
    private bool hasLoggedScheduledDay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Chỉ hủy component trùng, không hủy object có manager khác.
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool InitializeForMatch(int totalDays)
    {
        // Gọi lại trong cùng ván vẫn giữ nguyên lịch đã tạo.
        if (HasSchedule)
            return true;

        // Tránh phạm vi rỗng và tránh tràn khi tính totalDays + 1.
        if (totalDays < 1 || totalDays == int.MaxValue)
        {
            Debug.LogError(
                "[Event Debug] Số ngày phải từ 1 đến int.MaxValue - 1.",
                this);
            return false;
        }

        List<GameEventType> pool = BuildEventPool();

        if (pool.Count == 0)
        {
            Debug.LogError("[Event Debug] Không có event để chọn.", this);
            return false;
        }

        int index = UnityEngine.Random.Range(0, pool.Count);

        SelectedEvent = pool[index];
        ScheduledDay = UnityEngine.Random.Range(1, totalDays + 1);
        HasSchedule = true;
        hasLoggedScheduledDay = false;

        Debug.Log(
            $"[Event Debug] Đã chọn {GetDisplayName(SelectedEvent)}" +
            $" | Ngày xuất hiện: {ScheduledDay}",
            this);

        return true;
    }

    private static List<GameEventType> BuildEventPool()
    {
        var pool = new List<GameEventType>();

        foreach (GameEventType type in Enum.GetValues(typeof(GameEventType)))
        {
            // Contains tránh tăng xác suất nếu enum có alias trùng giá trị.
            if (type != GameEventType.None && !pool.Contains(type))
                pool.Add(type);
        }

        return pool;
    }

    public GameEventType GetEventForDay(int day)
    {
        if (!HasSchedule || day != ScheduledDay)
            return GameEventType.None;

        return SelectedEvent;
    }

    public static string GetDisplayName(GameEventType eventType)
    {
        switch (eventType)
        {
            case GameEventType.BloodMoon:
                return "blood moon event";
            case GameEventType.Fog:
                return "fog event";
            case GameEventType.HarvestFestival:
                return "festival event";
            case GameEventType.ClearSky:
                return "clear sky event";
            case GameEventType.GhostMonth:
                return "ghost month event";
            case GameEventType.TuongNight:
                return "tuong night event";
            case GameEventType.None:
                return string.Empty;
            default:
                // Khi thêm enum mới, bổ sung tên chính thức ở switch này.
                return eventType.ToString().ToLowerInvariant() + " event";
        }
    }

    public void NotifyDayStarted(int day)
    {
        GameEventType todayEvent = GetEventForDay(day);

        if (todayEvent == GameEventType.None || hasLoggedScheduledDay)
            return;

        hasLoggedScheduledDay = true;

        Debug.Log(
            $"[Event Debug] Ngày {day}-{GetDisplayName(todayEvent)}",
            this);
    }

    // Giữ chữ ký hàm cũ để không làm mất liên kết UnityEvent nếu có.
    // Hàm chỉ thông báo ngày event; không random hay áp dụng hiệu ứng.
    public void TryStartEvent()
    {
        var game = GameRoleManager.Instance;

        if (game == null || game.currentState == GameState.GameOver)
            return;

        NotifyDayStarted(game.currentDay);
    }

    public void ResetForNewMatch()
    {
        SelectedEvent = GameEventType.None;
        ScheduledDay = 0;
        HasSchedule = false;
        hasLoggedScheduledDay = false;
    }
}
```

**Giải thích:**

- `InitializeForMatch` trả `bool` để caller biết có tạo/giữ lịch thành công không.
- `BuildEventPool` dùng enum hiện có, nên sáu event đều có thể được chọn dù chưa có hiệu ứng gameplay.
- `GetEventForDay` và `GetDisplayName` không random, không log và không thay đổi dữ liệu.
- `NotifyDayStarted` được gọi từ flow, không gọi từ `Update()` của UI.
- `TryStartEvent` là wrapper tương thích hàm cũ; không phải cổng khởi tạo lịch.
- Không gọi `ResetForNewMatch` ở mỗi ngày; chỉ dùng khi thực sự bắt đầu ván khác.
- Giữ `using UnityEngine`, đồng thời ghi đầy đủ `UnityEngine.Random` để không nhầm với `System.Random`.

### 17.2. GameFlow-thuongnh/WinConditionManager.cs

**Cách áp dụng:** thêm đúng một property ngay dưới field `maxDay`. Không thay các hàm kiểm tra thắng/thua.

```csharp
[SerializeField, Min(1)] private int maxDay = 7;

public int MaxDay => maxDay;
```

`MaxDay` không có setter, nên EventManagert chỉ đọc cấu hình. Không khai báo thêm field `maxDay` nếu field đã có.

### 17.3. GameFlow-thuongnh/GameRoleManager.cs

**A. Thêm hàm PrepareEventManager bên trong class:**

```csharp
private EventManagert PrepareEventManager()
{
    if (EventManagert.Instance != null)
        return EventManagert.Instance;

    // Các manager gameplay của prototype phải nằm trên object đang active.
    // AddComponent gọi Awake và thiết lập Instance khi object đang active.
    return gameObject.AddComponent<EventManagert>();
}
```

Component EventManagert đặt sẵn phải ở object active để `Awake()` đăng ký singleton trước `BeginGame()`. Không bố trí một manager khác trên object inactive rồi kích hoạt giữa ván. Nếu có component trùng được bật muộn, `Awake()` của nó sẽ loại bỏ component trùng.

**B. Thay toàn bộ hàm BeginGame hiện có:**

```csharp
public void BeginGame()
{
    if (started)
        return;

    if (TaskManager.Instance == null ||
        DayTimer.Instance == null ||
        PlayerManger.Instance == null ||
        NightManager.Instance == null ||
        VoteManger.Instance == null ||
        DeathResolver.Instance == null ||
        WinConditionManager.Instance == null)
    {
        Debug.LogError("GameRoleManager: missing gameplay managers.");
        enabled = false;
        return;
    }

    EventManagert events = PrepareEventManager();

    started = true;

    if (RoleManger.Instance != null)
        RoleManger.Instance.AssignRole();

    bool hasEventSchedule = events.InitializeForMatch(
        WinConditionManager.Instance.MaxDay);

    if (!hasEventSchedule)
    {
        // Lỗi phần debug không ngăn flow gameplay hiện có chạy tiếp.
        Debug.LogWarning(
            "[Event Debug] Ván tiếp tục nhưng chưa có lịch event.");
    }

    StartDay();
}
```

Không gọi `ResetForNewMatch()` ở đây: một lịch đã tạo cho ván hiện tại phải được giữ nguyên nếu gọi lại hàm. Luồng restart không tải scene là chức năng riêng, cần reset trạng thái của cả ván, không chỉ lịch event.

**C. Thay toàn bộ hàm StartDay hiện có:**

```csharp
public void StartDay()
{
    if (!started || currentState == GameState.GameOver)
        return;

    SetPhase(GamePhase.DayStart);
    PlayerManger.Instance.UnlockPlayers();
    TaskManager.Instance.StartNewDay();
    DayTimer.Instance.StartTimer();

    // Chỉ kiểm tra/log lịch đã tạo, tuyệt đối không random ở đây.
    if (EventManagert.Instance != null)
        EventManagert.Instance.NotifyDayStarted(currentDay);
}
```

Giữ nguyên `Update`, `SetPhase`, `EndDay`, `StartVoting`, `FinishVoting`, `EndGame` và các hàm còn lại. Nhờ đó flow và điều kiện kết thúc ván không bị thay đổi bởi phần event debug.

### 17.4. UI-thuongnh/GameHUD.cs

Không tạo GameHUD thứ hai. Giữ nguyên các field public và reference hiện có trên scene.

**A. Thêm helper cấu hình text bên trong class:**

```csharp
private void ConfigureSingleLineText(TMP_Text label)
{
    if (label == null)
        return;

    float originalSize = Mathf.Max(1f, label.fontSize);

    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.enableAutoSizing = true;
    label.fontSizeMax = originalSize;
    label.fontSizeMin = Mathf.Min(originalSize, 18f);
    label.overflowMode = TextOverflowModes.Ellipsis;
}
```

Sử dụng API `textWrappingMode` đang có trong package TMP của project. Nếu chữ vẫn quá dài ngay cả ở cỡ tối thiểu, Ellipsis tránh vẽ tràn sang UI khác; đó **chưa đạt tiêu chí hiển thị đầy đủ tên** và phải được báo khi kiểm tra hình ảnh. Không tự sửa scene để nới khung trong phạm vi thay đổi code này.

**B. Thay toàn bộ hàm Start hiện có:**

```csharp
private void Start()
{
    // Phải chạy trước return của meetingPanel.
    ConfigureSingleLineText(phaseText);
    ConfigureSingleLineText(timerText);

    // Giữ phần tìm reference của prototype hiện có.
    if (meetingPanel == null)
        return;

    if (meetingTitle == null)
    {
        meetingTitle = meetingPanel.transform
            .Find("Title")?.GetComponent<TMP_Text>();
    }

    if (votingStatusText == null)
    {
        votingStatusText = meetingPanel.transform
            .Find("Hint")?.GetComponent<TMP_Text>();
    }
}
```

**C. Thêm hàm tạo nội dung ngày:**

```csharp
private string BuildDayLabel(int day)
{
    string dayLabel = $"Ngày {day}";
    EventManagert events = EventManagert.Instance;

    if (events == null)
        return dayLabel;

    GameEventType todayEvent = events.GetEventForDay(day);

    if (todayEvent == GameEventType.None)
        return dayLabel;

    return dayLabel + "-" + EventManagert.GetDisplayName(todayEvent);
}
```

**D. Thêm hàm cập nhật hai dòng HUD:**

```csharp
private void RefreshDayAndTimer(GameRoleManager game)
{
    if (game == null)
        return;

    if (phaseText != null)
        phaseText.text = BuildDayLabel(game.currentDay);

    float seconds = game.currentState == GameState.Day &&
                    DayTimer.Instance != null
        ? DayTimer.Instance.TimeRemaining
        : game.PhaseTimeRemaining;

    if (timerText != null)
    {
        int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        timerText.text = $"{game.currentPhase} | {remainingSeconds}s";
    }
}
```

**E. Trong Update(), chỉ thay đoạn ngày và đồng hồ:**

Tìm đoạn cũ:

```csharp
if (phaseText != null)
    phaseText.text = $"Ngày {game.currentDay} — {game.currentPhase}";

float seconds = game.currentState == GameState.Day && DayTimer.Instance != null
    ? DayTimer.Instance.TimeRemaining : game.PhaseTimeRemaining;

if (timerText != null)
    timerText.text = $"{Mathf.CeilToInt(seconds)}s";
```

Thay toàn bộ đoạn đó bằng:

```csharp
RefreshDayAndTimer(game);
```

Vị trí gọi vẫn sau phần cập nhật tiến độ ngày, trước phần danh sách task. Giữ nguyên các đoạn cập nhật task, vote, meeting và kết quả. Không giữ dòng gán `phaseText.text` cũ ở phía dưới vì nó sẽ ghi đè tên event vừa hiển thị.

### 17.5. Sơ đồ gọi function

```text
GameRoleManager.Start
└── BeginGame
    ├── PrepareEventManager
    ├── RoleManger.AssignRole
    ├── EventManagert.InitializeForMatch(MaxDay)
    │   ├── BuildEventPool
    │   ├── Random event và ngày
    │   └── Log lịch một lần
    └── StartDay
        ├── Các bước bắt đầu ngày hiện có
        └── EventManagert.NotifyDayStarted(currentDay)
            ├── GetEventForDay
            └── Log nếu đúng ngày và chưa log

GameHUD.Start
└── ConfigureSingleLineText cho ngày và đồng hồ

GameHUD.Update
└── RefreshDayAndTimer
    ├── BuildDayLabel
    │   ├── GetEventForDay
    │   └── GetDisplayName
    └── Hiển thị phase và thời gian
```

### 17.6. Kiểm tra code khi triển khai

1. Biên dịch sau khi áp dụng đủ bốn file liên quan. Không tạo enum hoặc class trùng.
2. Kiểm tra không còn dòng HUD cũ ghi đè lên `BuildDayLabel`.
3. Với `MaxDay = 1`, mọi ván đều lên lịch ngày 1 nhưng event vẫn được chọn ngẫu nhiên.
4. Gọi `InitializeForMatch` lại sau khi có lịch: so sánh event/ngày trước và sau, phải giống nhau.
5. Kiểm tra `GetEventForDay(ScheduledDay)` trả event đã chọn; các ngày khác trả `None`.
6. Gọi `NotifyDayStarted` nhiều lần ở ngày đã chọn: chỉ một log xuất hiện.
7. Sau `ResetForNewMatch`, `HasSchedule` phải false, event là `None`, ngày bằng 0; lần khởi tạo tiếp theo được phép tạo lịch mới.
8. Tải lại scene và kiểm tra singleton cũ không còn, lịch mới khởi tạo bình thường.
9. Kiểm tra trên Game view đủ sáu tên event và tên phase dài; không chấp nhận cắt tên bằng dấu ba chấm ở kết quả cuối.

Khi cần tái hiện ngày cuối bằng seed trong kiểm thử, lưu và khôi phục `UnityEngine.Random.state` trong phần test để không làm thay đổi random của gameplay sau test. Không thêm nút ép event hoặc setter lịch vào runtime chỉ để kiểm thử khi chưa có yêu cầu.

**Lưu ý duyệt:** các code block trên mô tả chính xác phần sẽ thêm/thay thế, không tự áp dụng chỉ vì chúng đã xuất hiện trong Markdown. Hiệu ứng role, sửa lỗi không liên quan và online sync vẫn nằm ngoài đợt này.

## 18. Kết quả triển khai

- Đã cập nhật `EventManagert.cs`, `GameRoleManager.cs`, `WinConditionManager.cs` và `GameHUD.cs`.
- Lịch được tạo một lần trong `BeginGame`, loại `None`, chọn ngày từ 1 đến `MaxDay`.
- HUD chỉ hiển thị tên event khi ngày hiện tại khớp lịch; Console ghi lịch đầu ván và thông báo khi đến ngày event.
- Component event được bổ sung lúc runtime nếu scene chưa có; không chỉnh scene hoặc prefab.
- Giữ nguyên `.meta`, tên class và gameplay của role.
- Khung đồng hồ hiện hẹp, nên dùng tên phase ngắn: `Role`, `Day`, `Task`, `Event`, `Talk`, `Vote`, `Count`, `Night`, `Dawn`, `End`. Ví dụ `Talk | 30s`. Hàm `GetCompactPhaseName` trong GameHUD ánh xạ tên hiển thị, không thay đổi enum hay phase thật.
- Đã biên dịch toàn bộ script trong Thuong thành công bằng Roslyn với các reference Unity của project, sau lần chỉnh cuối.
- Bộ kiểm thử tạm cho lịch đã biên dịch, nhưng Windows Application Control chặn thực thi cả khi thử ngoài sandbox. Không ghi nhận các trường hợp runtime là PASS.
- Chưa kiểm tra trực quan trong Game view hoặc Play Mode. Cần xác nhận tên event đầy đủ, không bị ellipsis, và thử chơi lại trong Unity.
- Các thay đổi trong project của đợt này chỉ nằm trong `Assets/Scripts/Thuong`; không chỉnh các thay đổi có sẵn ở ProjectSettings hoặc `.vsconfig`.
