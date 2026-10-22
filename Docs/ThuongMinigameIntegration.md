# Tích hợp minigame cho Thương Online

## Phạm vi

- Nguồn Trapsweeper: `origin/Bảo`, commit `74aa3074b2f364c4e34714157ecb01bab0633e06`; đã rà cả commit `6bd18d3`. Chỉ nhập `Assets/Minigames/Trapsweeper` và meta của thư mục. Không lấy thay đổi DefenderGameTiles, Recovery, Potion Making hoặc tetoris.gif.
- Runtime nằm trong `Assets/Scripts/Thuong/Minigames`, namespace `Thuong.Minigames`. Scene demo, role Hunter, scene Lợi và prototype offline giữ hành vi cũ.
- Asset nhiệm vụ nằm trong `Assets/Scenes/Thuong/Prototype/Integration/Minigames`. Catalog được đóng gói qua Resources; không dùng AssetDatabase hoặc InstanceID trong build.
- Bốn nhiệm vụ minigame mới được tạo trong runtime online. Scene vẫn có tổng cộng 10 trạm để không làm hỏng dữ liệu prototype, nhưng pool nhiệm vụ online chỉ gồm sáu task có minigame; các task cũ không có mapping không còn được chọn.

## Cách chơi và cấu hình

Mở `OnlineSceneGamePlay`, Host/Join như trước. Sau khi đọc role, Ready và bắt đầu trận, đến điểm nhiệm vụ đang khả dụng và nhấn E. Mọi nhiệm vụ online đều mở minigame; chỉ khi thắng thử thách mới được cộng `TaskData.progressValue`. Thua, Hủy/Esc hoặc hết hạn không cộng điểm. Nút Thử lại xin một lượt mới.

| Task ID | Nhiệm vụ | Điều kiện mặc định | Giới hạn | Ban ngày còn ít nhất |
| --- | --- | --- | --- | --- |
| `thuong.task.wood` | Gom củi | Chặt đúng 5 khúc, tối đa 2 lần trượt | 18s | 12s |
| `thuong.task.harvest` | Thu hoạch | Thu 8 bó, bỏ qua chướng ngại đỏ, tối đa 1 lần trượt | 23s | 18s |
| `thuong.task.eggs` | Thu gom trứng | Kéo giỏ hứng 10 quả, tối đa 2 lần rơi | 25s | 18s |
| `thuong.task.potion` | Pha chế thuốc | Chọn đúng 3 nguyên liệu để đạt loại được giao | 22s | 8s |
| `thuong.task.defender` | Bảo vệ khu vực | Xoay khiên chặn 10 đợt, tối đa 2 lần trúng | 25s | 18s |
| `thuong.task.traps` | Gỡ bẫy | Bàn 5×5, 3 bẫy, mở 22 ô an toàn; lần đầu và lân cận an toàn | 40s | 15s |

Thời hạn thực tế được giới hạn bởi thời gian ban ngày còn lại. Có thể chỉnh mục tiêu, thời lượng, cửa sổ thao tác, sprite, prefab và vị trí trạm trong catalog. `progressValue` thuộc TaskData; các asset cũ không bị sửa. Công thức dùng ba IngredientData riêng của Thương: hồi phục +2, độc -2, trung tính 0. Tổng điểm ±5 trở lên tạo thuốc mạnh, ±1..4 tạo thuốc thường. Không đổi sức khỏe, vai trò hay hiệu ứng trong trận.

## Vòng đời và online

- Server kiểm tra trận, ngày, sống/chết, khoảng cách, danh sách task hôm nay, hoàn tất và khóa theo task ID/ngày. Mỗi chủ sở hữu chỉ có một lượt. Khóa áp dụng chung nếu nhiều station dùng cùng TaskData.
- `attemptId` mới cho mỗi lượt; kiểm tra chủ sở hữu, ngày, station, sequence và hạn dùng trên từng thao tác. Lượt được tiêu thụ trước khi gọi TaskManager; không có gói client khai báo đã thắng. Đường CompleteTask cũ luôn bị từ chối trong phiên online.
- Chặt/thu hoạch được kiểm tra theo cửa sổ thời gian server. Giỏ/khiên có tốc độ di chuyển/xoay giới hạn và kiểm tra với lịch sinh đợt của server. Không gửi timestamp client để yêu cầu quay ngược thời gian hoặc replay physics.
- Pha thuốc kiểm tra đủ ba chỉ số nguyên liệu và kết quả công thức. Trapsweeper giữ bàn bẫy/RNG trên server, chỉ gửi ô đã mở hoặc cắm cờ; không gửi seed hoặc bàn ẩn.
- Đổi giai đoạn/ngày, bị loại, task không còn khả dụng, rời phiên, mất kết nối hoặc kết thúc ván đều đóng/vô hiệu hóa lượt. Gói trùng, gói cũ hay sai chủ không cộng tiến độ. Server cũng khóa di chuyển; UI không phải lớp bảo vệ duy nhất.
- Hai thông điệp riêng: `thuong.minigame.request.v1` và `thuong.minigame.state.v1`. Snapshot/mask tiến độ hiện có không thêm trường. Cần dùng cùng bản build trên host và client vì catalog/station đã được bổ sung.

## Hiển thị

Controller Thương dùng lại sprite từ tài nguyên minigame gốc, không khởi tạo các manager demo có singleton, pause hoặc reload scene. Cửa sổ có hướng dẫn, tiến độ, thời gian, kết quả, Hủy và Thử lại. Chuột/touch đi qua cùng pointer adapter của RawImage; Trapsweeper có nút Cắm cờ cho touch và chuột phải.

Mỗi lượt có local scene `LocalPhysicsMode.Physics2D`, root riêng, camera trực tiếp và RenderTexture. PhysicsScene2D được simulate trong FixedUpdate, dù luật nhiệm vụ không phụ thuộc va chạm. Không đổi active scene, timeScale, collision matrix, BuildSettings hay cấu hình input. Dùng EventSystem/AudioListener của scene chính; không khởi tạo NetworkManager mới. Dispose tắt camera/root, giải phóng texture, callback, object và unload local scene.

## Công cụ kiểm tra

- `Tools/Thuong/Minigames/Install Assets`: cài catalog/prefab/TaskData. Đã có asset trong repo; chạy lại giữ cấu hình đã cài hoàn tất.
- `Tools/Thuong/Minigames/Check Rules And Assets`: kiểm tra luật sáu game, khóa, replay, sai chủ, thời hạn, công thức, bàn bẫy, wire format, GUID, dependency, LFS và prefab.
- `Tools/Thuong/Minigames/Check Online With 25 Players`: mở Play Mode và mở rộng bộ kiểm tra Thương Online hiện có bằng 24 client LAN thật trong cùng tiến trình Editor. Kiểm tra khóa/task reward, legacy bypass, server movement lock, timer, đổi phase, bị loại, cleanup và giữ hồi quy lobby/role/vote/disconnect.
- `Tools/Thuong/Minigames/Check Rendering And Player Scripts`: biên dịch script Windows Player, render sáu cửa sổ, kiểm tra giới hạn pointer, cắm cờ/mở ô qua viewport, tỷ lệ ảnh và dọn scene/camera sau mỗi lượt.
- Log và ảnh kiểm tra được ghi vào `Logs/Minigames` và `Logs/OnlineUIForm`; không đưa log vào Git.

Kết quả chạy thực tế và những mục chưa xác minh được ghi trong báo cáo triển khai cùng thư mục Docs. Kết nối Relay/voice giữa các máy, touch trên thiết bị và cân bằng độ khó cần được kiểm tra riêng trên build thực tế.
