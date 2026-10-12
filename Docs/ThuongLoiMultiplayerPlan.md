# Kế hoạch ghép multiplayer của Lợi với prototype top-down của Thương

Ngày khảo sát: 12/10/2026. Trạng thái: kế hoạch, chưa triển khai multiplayer.

Hướng dẫn triển khai chi tiết: [OnlineSceneGameImplementation.md](OnlineSceneGameImplementation.md). Tài liệu này ghi cấu hình scene/prefab, thứ tự khởi tạo, contract request/snapshot, cách nối từng chức năng gameplay, kết nối LAN/Internet và điều kiện kiểm thử.

## 1. Phạm vi và nguyên tắc

Tạo một bản chơi mạng riêng, sử dụng map, art, camera và luật gameplay hiện tại của Thương, kết hợp Netcode và các thành phần tương thích của Lợi. Giữ nguyên các file nguồn, scene, prefab và `.meta` hiện có của cả hai bên tại thời điểm bắt đầu tích hợp.

Theo lựa chọn của người dùng, giữ hai scene gameplay tách biệt:

| Scene | Vai trò | Cách sử dụng |
| --- | --- | --- |
| `Assets/Scenes/Thuong/Prototype/ThuongGameplayPrototype.unity` | Bản offline dự phòng. | Giữ nguyên, mở trực tiếp để thử gameplay và đối chiếu khi bản online gặp lỗi. |
| `Assets/Integration/ThuongLoi/Scenes/OnlineSceneGame.unity` | Scene gameplay online hoàn toàn mới, tích hợp Thương và Lợi. | Dựng riêng, vào qua lobby mạng; tất cả hành động ảnh hưởng ván chơi được server xử lý và đồng bộ. |

Scene online không tải scene offline theo kiểu additive, không có nút chuyển chế độ offline/online trong cùng scene và không gọi scene offline khi kết nối thất bại. Mỗi scene có `.unity` và `.meta` riêng. Giữ bản offline làm mốc đối chiếu trong suốt quá trình triển khai.

Scene dự phòng vẫn tham chiếu asset và script hiện có. Trước khi triển khai, cần lưu snapshot phiên bản nền gồm scene, prefab, script và art phụ thuộc để có thể khôi phục đầy đủ; riêng file scene không đóng băng các asset được nó tham chiếu. Đây là bước dự kiến trong plan, chưa tạo snapshot hoặc commit trong lượt lập kế hoạch.

Chỉ thêm file vào `Assets/Integration/ThuongLoi/`, `Docs/` và thư mục công cụ riêng nếu cần. Không sửa `Assets/Scripts/Loi`, `Assets/Scenes/Loi`, `Assets/Prefabs/Loi`, `Assets/Scripts/Thuong`, `Assets/Scenes/Thuong`, `Packages` hoặc `ProjectSettings` trong phần tích hợp này. Các thay đổi art top-down đã làm trước đó là trạng thái nền, không hoàn tác hay gom lẫn vào commit tích hợp.

Hai phần đã nằm chung trên nhánh `main`; việc cần làm là nối chúng khi chạy. Không cần merge lại nguyên các nhánh lịch sử để đạt mục tiêu này.

Không thể bảo đảm mọi lần merge sau này đều không có conflict. Tuy nhiên, thay đổi chỉ thêm ở thư mục riêng giúp tránh tranh chấp trực tiếp trên file của hai tác giả. Conflict về hành vi khi chạy phải được giải quyết bằng cơ chế điều phối và kiểm thử dưới đây.

## 2. Những điểm đã kiểm tra trong code hiện tại

| Điểm nối | Hiện trạng | Quyết định tích hợp |
| --- | --- | --- |
| Chuyển scene | `NetworkUI` tải cứng `Lobby`; `LobbyManager.StartGame()` tải cứng `Game`; `SceneLoader` quay về `MainMenu`. | Scene tích hợp dùng tên riêng và callback mới. Không gọi các hàm chuyển scene cũ. |
| Danh sách người chơi | Thương tạo lobby offline qua `prototypeLobbySize` và `ThuongFourRoleOfflineTest`; Lợi đăng ký theo client thật. | Bản mới đặt số người offline bằng 0, bỏ spawner offline và đăng ký roster mạng trước khi bắt đầu. |
| Chia role | `NetworkRoleManager` gán `PlayerRole`; `GameRoleManager.BeginGame()` gọi `RoleManager.AssignRole()` theo `RoleType`. | Chỉ dùng hệ thống chia role của Thương trên server. Không đưa `NetworkRoleManager` và UI dùng role cũ vào scene mới. |
| Đồng hồ | `DayTimer` của Thương kết thúc ngày; `NetworkGameTimer` có đồng hồ `DayStart` 10 giây và `NetworkPhaseTimerController` cũng gọi `EndDay()`. | Chỉ `GameRoleManager` + `DayTimer` của Thương điều khiển thời gian. Adapter mạng chỉ gửi thời gian và trạng thái để hiển thị. |
| Bỏ phiếu | `NetworkPlayerVote` cho đổi phiếu và gọi `StartVote()` để dựng lại; `VoteManager.TryVote()` của Thương chặn người đã vote. | RPC mới gọi trực tiếp `TryVote()`, giữ luật một phiếu mỗi lượt của prototype. Không dùng component vote cũ trong prefab tích hợp. |
| Tiên Tri | `NetworkRoleActionBridge` gửi `target.roleType` chính xác; `SeerRole` của Thương chỉ trả Dog Spirit / không phải Dog Spirit. | Adapter mới dùng kết quả `UseNightAbility(..., out feedback)` và chỉ gửi riêng cho người yêu cầu. |
| Di chuyển | `PlayerController` của Lợi chạy input/physics ở owner; `PlayerMovement` của Thương chạy physics và khóa ngoài ban ngày. | Một movement adapter mới xử lý input owner và physics server; không để hai controller cùng chạy. |
| ID | Các bridge của Lợi và hook tử vong của Thương hiện giả định `playerID == (int)clientId`. | Bản đầu giữ cùng ID, kiểm tra phạm vi int và trùng ID trước khi nhận người chơi. Không tự chuyển sang ID thứ tự 0..N khi vẫn tái sử dụng các bridge này. |

Netcode, Transport và Input System đã có trong project; không cần thêm package hoặc sửa cấu hình input chung cho kế hoạch này.

## 3. Cấu trúc file mới dự kiến

```text
Assets/Integration/ThuongLoi/
  Scenes/
    ThuongLoiMainMenu.unity
    ThuongLoiLobby.unity
    OnlineSceneGame.unity
  Prefabs/
    NetworkMedievalPlayer.prefab
    IntegrationNetworkPrefabs.asset
  Runtime/
    IntegrationSessionFlow.cs
    IntegrationRelayConnector.cs
    IntegrationLobbyState.cs
    IntegrationMatchBootstrap.cs
    IntegrationMatchSync.cs
    IntegrationNetworkTypes.cs
    IntegrationMovement.cs
    IntegrationGameplayRequests.cs
    IntegrationPlayerPresentation.cs
    IntegrationHudPresenter.cs
    IntegrationStationPresenter.cs
    IntegrationWorldPresenter.cs
    IntegrationGameConfig.cs
  Data/
    IntegrationGameConfig.asset
  Editor/
    IntegrationSceneBuilder.cs
    IntegrationBuild.cs
    IntegrationChecks.cs
```

Tên là đề xuất, có thể gộp những adapter nhỏ khi triển khai. Runtime dùng namespace riêng `Team4.Integration.ThuongLoi`, tránh tên trùng các manager hiện có. Không thêm asmdef buộc hai phần gốc phải đổi assembly.

`OnlineSceneGame` được dựng từ đầu với cấu trúc phục vụ multiplayer: map và camera, network bootstrap, server gameplay root, network player, UI client và các adapter. Lấy các thành phần cần thiết cùng tham chiếu art, task asset, role catalog và animation của Thương; không sao chép nguyên scene offline rồi giữ lại các hệ thống thử offline. Scene offline dự phòng không bị ghi đè. Prefab mạng là variant của prefab Lợi, với cấu hình riêng và visual dùng art Thương; mọi override chỉ nằm trong variant. Không Apply override ngược về prefab gốc.

Build bằng script truyền danh sách scene trực tiếp cho `BuildPipeline.BuildPlayer`, hoặc build profile riêng được lưu trong thư mục tích hợp. Không sửa `ProjectSettings/EditorBuildSettings.asset`. Cấu hình NetworkManager và danh sách network prefab nằm trong scene/asset mới; mọi máy dùng cùng bản build và danh sách này.

## 4. Luồng chạy và quyền quyết định

```text
Menu tích hợp → Lobby tích hợp → chờ tất cả máy tải Game tích hợp
                                      ↓
Server chốt roster → spawn player → đăng ký PlayerData → BeginGame một lần
                                      ↓
Gameplay Thương trên server → snapshot / kết quả riêng → UI và art trên client
```

### Khởi tạo

- Giữ gameplay root chưa hoạt động cho tới khi xác định server/client. Client không khởi động các manager gameplay offline.
- Trên server, khởi tạo manager với `prototypeLobbySize = 0`; giữ `GameRoleManager` chưa chạy `Start` và giữ `DayTimer` dừng trong lúc dựng roster.
- Chờ tất cả client hoàn tất tải scene bằng sự kiện scene của Netcode. Đăng ký đủ người chơi rồi mới cho `GameRoleManager` bắt đầu. Không dùng `manualLogicPrototype` để chặn auto-start, vì biến này cũng chặn `BeginGame()` và làm tắt logic tích hợp.
- Sinh network player cho từng ván, không dùng lại player object đã gửi role ở ván trước: `NetworkRoleSync` hiện chỉ gửi role một lần trên mỗi instance.
- Loại khỏi scene mới các script test mạng dùng phím debug, spawner offline, input đổi P1..P20 và chức năng R reload cục bộ.

### Tái sử dụng phần Lợi

Ưu tiên dùng Netcode/Transport, cấu hình kết nối, `NetworkManagerPersistent`, `NetworkPhaseSync`, `NetworkRoleSync` và `NetworkPlayerStateSync` khi kiểm tra vòng đời cho thấy tương thích. `LobbyManager` có thể dùng cho danh sách người tham gia, nhưng nút Start của scene mới phải trỏ đến `IntegrationSessionFlow`.

Các component có hành vi không khớp được loại khỏi scene/variant mới và thay bằng adapter. File gốc vẫn tồn tại và chạy như trước trong scene gốc. Khi thiết kế variant phải kiểm tra cả `OnNetworkSpawn`, không chỉ tắt `Update` của component cạnh tranh.

### Di chuyển, art và camera

- Chỉ owner đọc WASD/gamepad và gửi hướng di chuyển. Server kiểm tra sender, trạng thái sống, phase, tốc độ và va chạm rồi cập nhật Rigidbody2D. `NetworkTransform` trong variant được cấu hình theo server.
- Giữ tốc độ, collider chân, gravity bằng 0 và quy tắc chỉ đi trong ban ngày của prototype Thương.
- Đồng bộ skin, hướng nhìn, đi/đứng/chết. Skin dùng chỉ số roster riêng cho hình ảnh, không suy ra role và không lấy client ID làm thứ tự hiển thị.
- Dùng lại cast, sprite, AnimatorController và depth sort. Presenter mới đọc trạng thái mạng: không để visual client phụ thuộc vào `PlayerManager` hoặc `PlayerMovement` offline.
- Camera mỗi máy gọi `ThuongTopDownCamera.Follow()` với player của chính máy đó. Camera không phải NetworkObject. Adapter màu đêm đọc phase mạng thay vì manager offline.

### Nhiệm vụ, kỹ năng, vote và HUD

- Client gửi ý định qua `IntegrationGameplayRequests`. Server lấy actor từ sender; không tin actor ID do client cung cấp.
- Nhiệm vụ: gửi mã station, server kiểm tra khoảng cách bằng vị trí server, phase và trạng thái sống rồi gọi `TaskManager.TryCompleteTask()`. Hai người hoàn thành cùng nhiệm vụ chỉ cộng tiến độ một lần.
- Kỹ năng: server gọi API và validator hiện có của Thương. Gửi feedback riêng cho requester; không truyền role chính xác của mục tiêu trong kết quả Tiên Tri. Có lệnh riêng cho chọn bùa Shaman và đặt bẫy Hunter; Hunter được thao tác sau khi chết trong Discussion nếu luật hiện có cho phép.
- Vote: server gọi `VoteManager.TryVote()`; gửi phản hồi và kết quả theo luật prototype, không reset toàn bộ phiếu khi có request mới.
- Snapshot công khai gồm phase, ngày, thời gian còn lại, nhiệm vụ đang hoạt động/đã xong, tiến độ, tên/skin và trạng thái sống, kết quả vote và kết quả ván. Mã nhiệm vụ dùng bảng cố định trong cấu hình tích hợp; không gửi tham chiếu Unity object trực tiếp qua mạng.
- Role, phe và thông tin kỹ năng riêng chỉ gửi cho owner/người được phép biết. Cần bổ sung trạng thái riêng khi role biến đổi hoặc kỹ năng được mở khóa; không chỉ dựa vào role gửi lúc đầu.
- `PhaseTimeRemaining`, tập nhiệm vụ đã xong và `Winner` không có public setter phù hợp để biến các manager offline thành bản sao client. HUD mới đọc snapshot mạng trực tiếp; không dùng reflection sửa private field và không dựng toàn bộ role của người khác trên client.
- Giữ giấy/gỗ, portrait, bố cục và thông tin gameplay của giao diện Thương. Không chạy các nút offline đang gọi trực tiếp `RoleManager`, `VoteManager` hoặc `SkipRoleReveal()` trên client. Nút OK chỉ xác nhận người đó đã đọc role; việc kết thúc reveal vẫn do server điều phối.

## 5. Các mốc triển khai

| Mốc | Công việc | Điều kiện hoàn thành |
| --- | --- | --- |
| 1. Tách bản tích hợp | Lưu snapshot nền offline, dựng scene online mới, variant mới, cấu hình mạng, build script, lưu fingerprint file gốc. | Offline dự phòng mở và chạy độc lập; scene online có GUID riêng; diff triển khai chỉ thêm trong thư mục cho phép. |
| 2. Lobby và spawn | Host/client kết nối, tải scene đồng bộ, chốt roster, spawn, camera và movement top-down. | Host + 2 client thấy cùng người chơi; mỗi máy chỉ điều khiển chính mình; không sinh 20 bot offline. |
| 3. Luồng ván | Server bắt đầu một lần, chia role một lần, snapshot phase/ngày/timer/role riêng. | Máy client không tự random role/nhiệm vụ hoặc tự chuyển phase; thời lượng giữ theo prototype. |
| 4. Gameplay và giao diện | Nhiệm vụ, kỹ năng, death, vote, tiến độ, thắng/thua, animation và màu đêm. | Một ván hoàn chỉnh chạy trên nhiều máy với luật Thương; toàn bộ role hiện có được rà soát về trạng thái công khai/riêng. |
| 5. Vòng đời và đóng gói | Thoát, hủy ván, chơi lại, build nhiều máy và kiểm tra file nguồn. | Chơi lại không giữ role/vote/timer cũ; scene của hai tác giả vẫn chạy độc lập. |

Bản đầu khóa người mới tham gia khi ván đã bắt đầu; chưa hỗ trợ reconnect giữa ván hoặc host migration. Nếu có người mất kết nối trong ván, hủy ván và đưa những máy còn kết nối về lobby, không tự tính bên thắng. Nếu host thoát, các client về menu tích hợp. Đây là chính sách đề xuất để tránh phát sinh luật mới trong hai phần gốc.

## 6. Kiểm tra bắt buộc trước khi nhận bản tích hợp

- Host + ít nhất 2 client: roster, spawn, ownership, camera, di chuyển 4 hướng, va chạm và animation. Dùng ít nhất 4 người cho bài kiểm tra luật với bộ role cốt lõi; không giới hạn role pool chính thức về 4 role.
- Request sai phase/lượt phase, giả actor ID, mục tiêu không tồn tại, task ngoài khoảng cách, spam request và task bị hai người hoàn thành cùng lúc đều được server kiểm tra đúng luật. Người chết bị chặn các hành động không được phép; HunterTrap hợp lệ sau khi chết vẫn được nhận.
- Không có client nhận role của người khác; Tiên Tri chỉ nhận thông tin prototype cho phép. Không suy ra role từ skin hoặc tên object được gửi cho client.
- Đủ chu trình RoleReveal → Day → Night → Discussion → Voting → ngày tiếp theo; death và win chỉ xử lý một lần. Nhiệm vụ ngày mới reset, tiến độ toàn ván giữ lại.
- Timer không bị bộ đếm 10 giây của Lợi kết thúc ngày sớm; vote lần hai không thay đổi phiếu đầu tiên.
- Ngắt kết nối và chơi lại không để singleton, event listener, RPC hoặc role của ván cũ tồn tại.
- Chạy LAN trước, sau đó Relay giữa hai mạng khác nhau để xác nhận chơi Internet. Cấu hình phần Lợi hiện dùng địa chỉ loopback 127.0.0.1; chưa có luồng Relay trong script gameplay hiện tại.
- Chạy lại regression offline của Thương và kiểm tra menu/lobby/game gốc của Lợi. So sánh fingerprint: các file gốc và shared settings không thay đổi so với nền tích hợp.

## 7. Ranh giới Git

Khi thực sự triển khai, dùng nhánh `codex/thuong-loi-integration` từ trạng thái nền đã ghi nhận. Commit art hiện có và commit lớp multiplayer phải tách phạm vi rõ ràng; không dùng `git add .` để gom cả workspace.

Chỉ stage đường dẫn tích hợp và tài liệu mới. Mọi asset/scene/prefab mới phải có `.meta` riêng và GUID duy nhất, còn tham chiếu asset gốc giữ GUID hiện tại. Trước khi merge, kiểm tra danh sách file thay đổi, `.meta`, compile, regression và chạy nhiều máy.

Nếu hai tác giả tiếp tục đổi API hoặc asset nguồn, phải rà lại tính tương thích của adapter và variant. Git có thể merge sạch nhưng prefab variant vẫn bị ảnh hưởng bởi thay đổi prefab nền; bài kiểm tra tích hợp là điều kiện nhận bản cập nhật.

## 8. File tham chiếu chính

- `Assets/Scripts/Loi/NetworkUI.cs`, `LobbyManager.cs`, `SceneLoader.cs`: chuyển scene cứng.
- `Assets/Scripts/Loi/NetworkRoleManager.cs`, `NetworkRoleSync.cs`: chia role cũ và gửi role riêng.
- `Assets/Scripts/Loi/NetworkGameTimer.cs`, `NetworkPhaseTimerController.cs`: đồng hồ DayStart riêng.
- `Assets/Scripts/Loi/NetworkPlayerVote.cs`, `NetworkRoleActionBridge.cs`: khác biệt luật vote và kết quả Tiên Tri.
- `Assets/Scripts/Thuong/GameFlow-thuongnh/GameRoleManager.cs`, `DayTimer.cs`: luồng gameplay và thời gian.
- `Assets/Scripts/Thuong/Players-thuongnh/PlayerManager.cs`: lobby mẫu và registry.
- `Assets/Scripts/Thuong/Death-thuongnh/DeathResolver.cs`: hook đồng bộ chết hiện dùng player ID bằng client ID.
- `Assets/Scripts/Thuong/Tasks-thuongnh/TaskManager.cs`, `TaskStation.cs`: API nhiệm vụ và input offline.
- `Assets/Scenes/Thuong/Prototype/ThuongGameplayPrototype.unity`: nền art/camera/gameplay cho scene mới.
