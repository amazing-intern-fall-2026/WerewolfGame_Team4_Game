# BÁO CÁO CÔNG VIỆC NGÀY 19/10/2026

**Dự án:** WerewolfGame_Team4_Game  
**Hạng mục:** Tích hợp minigame vào nhiệm vụ Thương Online  
**Scene áp dụng:** `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`  
**Nhánh triển khai:** `codex/thuong-minigame-integration`  
**Phiên bản Unity kiểm tra:** `6000.6.0f1`

## 1. Tổng quan

Đã triển khai mã nguồn và asset cho sáu minigame nhiệm vụ của Thương Online. Trước đây, người chơi đứng gần trạm và nhấn E là nhiệm vụ được hoàn tất. Với những nhiệm vụ đã gắn minigame, E hiện gửi yêu cầu bắt đầu lượt chơi; người chơi phải vượt qua thử thách và được server xác thực trước khi tăng tiến độ.

Đã gắn Gom củi và Thu hoạch với hai nhiệm vụ hiện có; bổ sung bốn nhiệm vụ riêng là Thu gom trứng, Pha chế thuốc, Bảo vệ khu vực và Gỡ bẫy theo lựa chọn của người dùng. Lấy nước, Sửa rào, Dọn kho và Thắp đèn tiếp tục giữ cách tương tác cũ. Tổng số trạm online là 10, vẫn nằm trong giới hạn mask 16 bit hiện tại.

Phần triển khai đã được lưu thành ba commit trên nhánh tích hợp. Đã chạy kiểm tra luật, asset, giao diện, biên dịch và LAN loopback. Chưa push hoặc merge vào `thuong`/`main`; các mục cần QA trên build và thiết bị thật được ghi riêng ở mục 6.

## 2. Công việc đã hoàn thành

| Hạng mục | Nội dung thực hiện | Kết quả |
| --- | --- | --- |
| Rà nguồn và nhập Trapsweeper | Fetch lại `main`, `thuong`, `Bảo`; rà hai commit `6bd18d3` và `74aa307`. Chỉ nhập thư mục Trapsweeper cùng `.meta`; kiểm tra GUID, dependency và ảnh Git LFS. | Giữ nguyên nguồn Trapsweeper được chọn; không merge toàn bộ nhánh Bảo. |
| Xây khung minigame | Tạo runtime trong namespace `Thuong.Minigames`, vòng đời Initialize/Begin/Cancel/Dispose, cấu trúc kết quả và input dùng chung. | Sáu game dùng chung cách mở, cập nhật, trả kết quả và giải phóng tài nguyên. |
| Tạo catalog và asset nhiệm vụ | Tạo catalog với ID ổn định được serialize, sáu prefab trình bày, bốn TaskData mới và ba IngredientData riêng cho Thương. | Mapping task → minigame → mục tiêu/thời lượng có thể chỉnh trong Inspector; không phụ thuộc AssetDatabase hoặc InstanceID khi chạy build. |
| Nối tương tác tại trạm | Đổi E thành yêu cầu mở thử thách đối với task có mapping. Tạo bốn trạm mới trong runtime online. | Chưa thắng minigame thì chưa tăng tiến độ; task chưa có mapping giữ hành vi cũ. |
| Quản lý lượt ở server | Lưu attempt ID, chủ sở hữu, task, station, ngày, sequence và thời hạn. Kiểm tra trạng thái trận, người còn sống, khoảng cách và task hôm nay. | Mỗi người chỉ có một lượt; khóa nhiệm vụ theo task/ngày, kể cả khi nhiều station cùng dùng một TaskData. |
| Xác thực thao tác và cộng tiến độ | Kiểm tra nhịp chặt/thu hoạch; vị trí giỏ, góc khiên và tốc độ điều khiển; ba nguyên liệu pha thuốc; thao tác mở/cắm cờ trên bàn bẫy của server. | Không nhận boolean “đã thắng” từ client; tiêu thụ lượt trước khi gọi TaskManager để tránh cộng trùng. |
| Chặn đường hoàn tất cũ | Từ chối CompleteTask trực tiếp cho task có minigame và khi người chơi đang làm thử thách khác. | Client không thể bỏ qua thử thách bằng action hoàn tất nhiệm vụ cũ. |
| Giao diện nhiệm vụ | Hiển thị hướng dẫn, mục tiêu, tiến độ, số lần trượt, thời gian và kết quả; bổ sung Hủy/Esc, Thử lại và chế độ Cắm cờ. | Người chơi có thể kết thúc hoặc xin lượt mới mà không reload scene. |
| Cô lập hiển thị và vật lý | Dùng camera riêng, RenderTexture và local scene với PhysicsScene2D được simulate riêng; pointer được đổi sang tọa độ viewport của cửa sổ. | Giữ active scene online và đồng hồ trận; dùng EventSystem/AudioListener của scene chính. |
| Khóa và trả điều khiển | Khóa di chuyển trên cả client và server trong lúc thực hiện nhiệm vụ. | Thao tác di chuyển gửi lên server không làm nhân vật dịch chuyển khi lượt còn mở. |
| Hủy lượt và cleanup | Vô hiệu hóa lượt khi đổi phase/ngày, bị loại, task không còn khả dụng, rời phiên, mất kết nối hoặc GameOver. Dọn callback, coroutine, object, camera, RenderTexture và local scene. | Gói cũ, sai chủ, gửi lặp hoặc gửi muộn không cộng tiến độ; cửa sổ được đóng khi lượt hết hiệu lực. |
| Hoàn thiện hình ảnh | Dùng lại tài nguyên có sẵn; thay hình bí ngô của prefab nguồn bằng sprite trứng cho nhiệm vụ Thu gom trứng, giữ tỷ lệ sprite và bổ sung đối tượng được bảo vệ ở game phòng thủ. | Hình ảnh phù hợp hơn với nhiệm vụ; các scene demo gốc không bị sửa. |
| Công cụ kiểm tra và tài liệu | Bổ sung menu cài asset, kiểm tra luật/asset, LAN 25 người, render và biên dịch Player; viết hướng dẫn cấu hình và báo cáo kỹ thuật. | Có quy trình kiểm tra lại cùng log/ảnh minh chứng. |

## 3. Sáu minigame đã tích hợp

Các thông số sau là cấu hình ban đầu, có thể điều chỉnh trong catalog.

| Minigame | Nhiệm vụ | Điều kiện hoàn thành | Giới hạn lượt |
| --- | --- | --- | --- |
| Chặt gỗ | Gom củi — nhiệm vụ hiện có | Chặt đúng 5 khúc theo cửa sổ thời gian server; tối đa 2 lần trượt. | 18 giây |
| Thu hoạch | Thu hoạch — nhiệm vụ hiện có | Thu đủ 8 bó lúa, bỏ qua chướng ngại đỏ; tối đa 1 lần trượt. Chạm chướng ngại làm thua lượt. | 23 giây |
| Hứng trứng | Thu gom trứng — nhiệm vụ mới | Di chuyển giỏ để hứng đủ 10 quả; tối đa 2 lần rơi. | 25 giây |
| Pha thuốc | Pha chế thuốc — nhiệm vụ mới | Chọn đúng 3 nguyên liệu để tạo loại thuốc được giao. Server kiểm tra công thức và kết quả. | 22 giây |
| Phòng thủ | Bảo vệ khu vực — nhiệm vụ mới | Xoay khiên chặn đủ 10 đợt; tối đa 2 lần trúng đòn. | 25 giây |
| Trapsweeper | Gỡ bẫy — nhiệm vụ mới | Bàn 5×5 có 3 bẫy; mở hết 22 ô an toàn. Lần mở đầu và vùng lân cận an toàn. | 40 giây |

Thời hạn lượt được giới hạn bởi thời gian ban ngày còn lại. Server từ chối mở nếu không đạt thời gian tối thiểu cấu hình. Thua minigame không giết nhân vật, không thay role và không gọi GameOver của scene demo. Thưởng nhiệm vụ tiếp tục lấy từ `TaskData.progressValue`; minigame không tự quyết định thắng trận.

Đối với Trapsweeper, client chỉ nhận trạng thái ô đã mở/cắm cờ, không nhận seed hoặc toàn bộ bàn bẫy ẩn. Đối với pha thuốc, các nguyên liệu riêng có điểm hồi phục +2, độc -2 và trung tính 0; công thức không tạo hiệu ứng độc/hồi máu lên người chơi trong trận.

## 4. Kết quả kiểm tra đã ghi nhận

Đây là kết quả từ các lần chạy đã lưu log, không phải một lần chạy test mới trong lúc viết báo cáo.

| Nhóm kiểm tra | Kết quả | Phạm vi xác nhận |
| --- | --- | --- |
| Biên dịch runtime và Editor | PASS, không có lỗi C# | Mã runtime, tích hợp online và công cụ Editor. |
| Biên dịch script Windows Player Development | PASS | Có marker `THUONG_MINIGAME_PLAYER_SCRIPTS_PASSED`; tiến trình batch kết thúc mã 0. Chưa phải đóng gói/chạy executable đầy đủ. |
| Luật và asset minigame | PASS, **327 assertions** trong file kết quả hiện có | Luật sáu game, khóa lượt, replay, sai chủ, timeout, công thức, bàn bẫy, wire format, GUID/dependency/LFS. Số assertions thay đổi theo flood mở ô của bàn ngẫu nhiên; lần LAN cuối ghi 325. |
| Hồi quy gameplay offline | PASS, **57 assertions** | Bộ kiểm tra gameplay tích hợp hiện có. |
| Minigame với 25 kết nối LAN | PASS, **21 assertions** | Host + 24 client loopback trong cùng tiến trình Editor: tranh nhiệm vụ, sai chủ, chặn di chuyển, cộng tiến độ từ thao tác client, gói trùng/muộn, task hoàn tất từ hệ thống khác, bị loại, sang đêm và cleanup. |
| Hồi quy online | PASS, **11.556 checks** | Lobby, phân role, Ready, bắt đầu game, vote/kết quả, disconnect và tạo phòng lại. Số đếm có các điều kiện được kiểm tra lặp trong khi chờ mạng, không phải 11.556 ca độc lập. |
| Giao diện và giải phóng tài nguyên | PASS, **44 assertions** | Render sáu cửa sổ, tỷ lệ viewport, giới hạn pointer, cắm cờ/mở ô qua viewport và dọn scene/camera sau mỗi lượt. Đã xem ảnh của sáu game. |

Log có exception từ package Multiplayer PlayMode khi đọc cấu hình trong Play Mode và cảnh báo hàng đợi UTP khi chạy nhiều transport trong cùng tiến trình. Các bộ kiểm tra trên vẫn hoàn tất PASS. Không dùng kết quả loopback để kết luận đã đạt hiệu năng 25 máy hoặc voice/Relay ngoài mạng thực tế.

## 5. Mã nguồn và lịch sử bàn giao

| Commit | Nội dung |
| --- | --- |
| `f6bf12c` | Nhập chọn lọc nguồn Trapsweeper từ Bảo. |
| `1593f2b` | Tích hợp runtime, catalog, prefab và sáu minigame nhiệm vụ có server xác thực. |
| `66a9b3b` | Bổ sung kiểm tra, sửa hình ảnh, xử lý đóng lượt và tài liệu tích hợp. |

Các nhóm file chính:

- Runtime minigame: `Assets/Scripts/Thuong/Minigames/`.
- Nối session online: `Assets/Scripts/Thuong/Online/ThuongOnlineMinigames.cs` và các hook trong session/HUD/kết quả trận.
- Prefab, catalog, nhiệm vụ và nguyên liệu mới: `Assets/Scenes/Thuong/Prototype/Integration/Minigames/`.
- Công cụ kiểm tra: các script `ThuongMinigame*Checks.cs` và `ThuongMinigameInstaller.cs` trong thư mục Editor của prototype Thương.

Không thay luật HunterRole, scene Lợi, scene/prefab demo của các minigame khác, BuildSettings, TagManager hoặc cấu hình input toàn project. Thay đổi có sẵn của font, Vivox, PlayMode và file plan được giữ ngoài ba commit tích hợp.

## 6. Công việc còn cần xác minh trước khi merge main

1. Chạy hai ứng dụng đầy đủ trên host/client riêng; kiểm tra Relay, độ trễ/mất gói, disconnect và voice/Vivox đang hoạt động thật.
2. Chơi thử chuột/touch trên thiết bị thật, thay đổi kích thước cửa sổ và đánh giá hiệu năng 25 người trên nhiều máy.
3. Cân bằng mục tiêu, số lần trượt và thời lượng với ban ngày mặc định 45 giây.
4. Xác nhận minigame cuối đưa tổng tiến độ lên 100%: cửa sổ nhiệm vụ đóng và UI thắng/thua cá nhân của Thương xuất hiện đúng.
5. Đóng gói và chạy executable hoàn chỉnh; rà diff cuối trước khi cập nhật `thuong`/`main` theo quy trình của nhóm.

## 7. Tài liệu và minh chứng

- [Hướng dẫn tích hợp, cấu hình và menu kiểm tra](D:/Project/WerewolfGame_Team4_Game/Docs/ThuongMinigameIntegration.md).
- [Báo cáo kỹ thuật triển khai](D:/Project/WerewolfGame_Team4_Game/Docs/ThuongMinigameImplementationReport.md).
- [Kết quả luật và asset](D:/Project/WerewolfGame_Team4_Game/Logs/Minigames/rules.txt).
- [Kết quả minigame LAN 25 kết nối](D:/Project/WerewolfGame_Team4_Game/Logs/Minigames/online.txt).
- [Kết quả giao diện và cleanup](D:/Project/WerewolfGame_Team4_Game/Logs/Minigames/presentation.txt).
- [Kết quả hồi quy online](D:/Project/WerewolfGame_Team4_Game/Logs/OnlineUIForm/result.txt).
- [Log biên dịch Windows Player scripts cuối](D:/Project/WerewolfGame_Team4_Game/Logs/Minigames-player-final.log).

Ảnh minh chứng của sáu game nằm trong `Logs/Minigames/view-*.png`. Log và ảnh trong `Logs` là output cục bộ, không nằm trong các commit bàn giao.
