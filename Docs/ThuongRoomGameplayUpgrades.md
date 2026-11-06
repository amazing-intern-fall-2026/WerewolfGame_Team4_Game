# Nhiệm vụ, thiết lập phòng và bản đồ chỉ dẫn — Thương Online

Áp dụng cho `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`.

## Cách dùng

1. Tạo phòng và tập hợp người chơi. Host bấm **LUẬT VÁN** ở lobby trước khi phân vai.
2. Chọn **Cơ bản**, **Tiêu chuẩn** hoặc **Đầy đủ**. Giữ thời gian tự đề xuất theo số người hoặc dùng nút − / + để chỉnh từng giai đoạn, rồi **ÁP DỤNG**. Dòng tóm tắt luật hiện trên lobby của cả phòng.
3. Phân vai, đọc role, Ready và bắt đầu như trước. Thiết lập được khóa sau khi phân vai, mở lại khi host chọn chơi lại về lobby.
4. Trong ban ngày, bấm **Tab** mở sơ đồ nhiệm vụ, chọn một task để xem đường đi. Sơ đồ có vị trí bản thân, các điểm nhiệm vụ, tên khu và thông tin trạm. Các map chưa định nghĩa khu riêng dùng tên phương hướng/trung tâm.
5. Tới gần trạm, HUD hiển thị **E · tên nhiệm vụ**, số người đang làm, trạm đầy, nhiệm vụ đã góp hoặc không đủ thời gian. Mỗi người đóng góp mỗi loại task tối đa một lần mỗi ngày.

## Cân bằng nhiệm vụ

Nhiệm vụ là mục tiêu chung của cả phòng, có nhiều lượt đóng góp. Số người tham gia được chốt ở đầu mỗi ngày theo những người còn sống và còn kết nối.

- Mục tiêu ngày: `max(4, 2 × số người)`, giới hạn bởi số loại task thực tế × số người.
- Số loại task hoạt động: 4–6, giới hạn bởi catalog/map. Phòng đông dùng đủ sáu loại.
- Sức chứa mỗi loại: `ceil(số người / số loại đang hoạt động)`, đồng thời không mở nhiều lượt hơn số đóng góp còn thiếu.
- Mỗi người chỉ có một lượt minigame đang chạy; mỗi loại chỉ được đóng góp một lần/người/ngày.
- Hoàn tất toàn bộ mục tiêu ngày mang lại tối đa **40% tiến độ cả ván**, chia đều cho từng lượt. Phòng 25 người có 50 lượt, mỗi lượt +0,8%; phòng hai người có bốn lượt, mỗi lượt +10%.
- Hủy, thất bại, mất quyền tham gia hoặc hết hạn không cộng điểm. Host vẫn kiểm tra khoảng cách, địa hình, epoch/ngày, chủ sở hữu, sequence và kết quả minigame.
- Người chơi lại cùng một loại task đã góp sẽ bị từ chối. Khi đủ mục tiêu một loại, trạm đó đóng cho cả nhóm. Sang ngày mới, giới hạn cá nhân và lượt ngày được xóa; tổng tiến độ ván được giữ.

Chế độ cooperative chỉ được bật khi phiên online Thương phân vai. Prototype/offline mặc định vẫn giữ bốn task chung, mỗi task dùng `TaskData.progressValue` như cũ. Không sửa các asset task hoặc minigame nguồn để tăng thưởng.

## Preset và thời lượng

| Preset | Pool role |
| --- | --- |
| Cơ bản | Villager, DogSpirit, Seer, VillageGuardian |
| Tiêu chuẩn (mặc định) | Cơ bản + Mayor, Idiot, Hunter, Shaman, Cursed |
| Đầy đủ | Toàn bộ 17 role cụ thể trong catalog hiện có |

Pool vẫn có giới hạn phe theo số người; không có nghĩa mỗi role trong preset đều xuất hiện ở mọi phòng. Ngoại hình độc lập với role bí mật.

| Số người | Ngày | Đêm | Thảo luận | Vote |
| --- | --- | --- | --- | --- |
| 2–5 | 90s | 20s | 30s | 20s |
| 6–12 | 120s | 30s | 60s | 30s |
| 13–25 | 150s | 30s | 90s | 45s |

Khi chọn chỉnh tay: ngày 60–300s, đêm 15–120s, thảo luận 15–180s, vote 15–120s. Các con số là cấu hình khởi đầu để playtest; chưa phải kết luận cân bằng từ thử người chơi.

Thiết lập được lưu trong phiên phòng, giữ qua nút chơi lại. Chỉ host thay đổi được và chỉ trước phân vai. Client nhận bản sao cấu hình; không có endpoint cho client sửa luật của host.

## Chỉ dẫn và đồng bộ

- Tab hiển thị sơ đồ địa hình có tỷ lệ, vị trí bản thân, pin nhiệm vụ và bảng chú giải riêng để tránh chồng nhãn.
- Đường dẫn dùng A* trên lưới collision của map, giữ lại các đoạn thẳng đã kiểm tra địa hình; không tự di chuyển người chơi.
- Gợi ý mặc định ưu tiên task còn chỗ gần người chơi. Người chơi có thể chọn task khác trong sơ đồ.
- Số lượt đã góp/mục tiêu, số người đang làm/sức chứa được host đồng bộ. Dấu hoàn thành của cá nhân chỉ gửi về đúng người đó.
- Sơ đồ không thêm vị trí người chơi khác, role, phe, mục tiêu kỹ năng hoặc thông tin bí mật.
- HUD chỉ dẫn ẩn khi ở lobby, đọc role, chết, chơi minigame, mở emote, họp hoặc kết thúc ván.
- Chơi lại/rời phòng/đổi map xóa bảng task và đường dẫn cũ; cấu hình phòng vẫn giữ qua replay.

Connection prefix hiện tại là **`THUONG8|`**. Host/client phải cùng bản build. Packet `thuong.online.room-state.v1` mang cấu hình và bảng nhiệm vụ kèm epoch/ngày/revision. Xem `Docs/ThuongOnlineRecovery.md` cho luồng kết nối lại.

Host kiểm tra bảng nhiệm vụ cùng nhịp snapshot. Khi dữ liệu đổi, host gộp thay đổi trong một nhịp gửi và gửi bản mới sau snapshot để client đã nhận đúng map và epoch. Phòng ổn định chỉ gửi bảng hiện tại cho người vừa vào. Mỗi client chỉ nhận cờ hoàn thành cá nhân của mình.

## Kiểm tra

- **Tools → Thuong → Room Upgrades → Check Rules Tasks And Routes**: preset theo số người, quota/phần thưởng/giới hạn cá nhân/reset, sức chứa minigame và route trên các map.
- **Tools → Thuong → Room Upgrades → Check 25 Online Players And UI** chạy scene thật với host và 24 kết nối LAN cùng máy. Kịch bản kiểm tra luật phòng cho người vào sau và qua replay, đường đi trên bản đồ của host, 25 lượt đồng thời, người thứ sáu chờ tại trạm đầy, hai lượt hoàn thành đồng thời và chống thưởng trùng. Khi chạy có đồ họa, bộ kiểm tra xuất ảnh 1280×720 và 1920×1080.
- `Logs/RoomUpgrades/online-metrics.txt` ghi số gói và byte payload bảng nhiệm vụ khi phòng đứng yên và khi có thao tác, thời gian chờ và thời gian hiển thị chỗ trống trong kịch bản có kiểm soát. Số byte này chỉ tính payload bảng nhiệm vụ. Kịch bản kiểm tra mốc 40%, 80%, 100% bằng cách điền quota trực tiếp qua `TaskManager` sau lượt kiểm tra qua mạng.
- Khi playtest với người chơi, ghi thời gian từ lúc thấy trạm đầy đến lúc bắt đầu được, số ngày và thời gian thực đến 100%, cùng số người hoàn thành mỗi ngày. Đo ở các cỡ phòng nhỏ, vừa và 25 người trên Relay để đánh giá cân bằng thực tế.
- Kết quả nằm trong `Logs/RoomUpgrades`. Kiểm tra LAN cùng máy không thay thế nghiệm thu Relay trên nhiều máy, độ trễ thật và cân bằng với người chơi.

Các lỗi xét thắng offline/QA và các vấn đề nền khác đã nêu trong `Docs/Reports/2026-11-02-ThuongMainSyncReview.md` cần được theo dõi riêng.

### Kết quả xác minh ngày 02/11/2026

- Đã đạt **1.401 kiểm tra** cho luật phòng, pool role, nhiệm vụ và route; **365 assertions** minigame cũ; **53 kiểm tra** emote. Kết quả được ghi trong `Logs/RoomUpgrades/build.log` trước bước build.
- Chưa hoàn tất kiểm tra online 25 kết nối và kiểm tra hình ảnh. Các lượt Editor trước bị lỗi khởi tạo/reload Play Mode và timeout trước khi vào kịch bản online; không tính là đạt.
- Build Windows đã được bắt đầu nhưng bị dừng trước khi có kết quả cuối. Không xác nhận executable cũ trong `Logs/Maps/Player` là bản của thay đổi này.
- Theo yêu cầu của người dùng do máy lag, đã dừng Unity QA, runner và các tiến trình con của tác vụ. Không chạy lại Unity hoặc build để hoàn tất báo cáo. Hai điều chỉnh cuối về số chỗ còn lại trên UI và giới hạn tần suất tìm đường chỉ được rà soát mã nguồn sau khi dừng.
- Đã hoàn nguyên bốn asset/cấu hình Unity tự ghi khi build, lưu các resource performance test tự sinh dưới `Logs/RoomUpgrades/GeneratedResources`, giữ nguyên hai file font local có từ trước.
