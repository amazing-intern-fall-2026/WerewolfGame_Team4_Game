# Tích hợp kế hoạch logic vào scene online của Thương

Scene sử dụng: `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`.

`ThuongOnlineBootstrap` tự khởi tạo phiên online cho scene này. Phiên online tìm các manager trong chính scene, bổ sung và nối `StatusEffectSystem` / `AbilitySystem` khi thiếu. Không cần kéo thêm reference trong Inspector hoặc chỉnh scene của nhóm khác.

## Luồng đã nối

- Host xử lý sống/chết, hiệu ứng, mục tiêu, lượt kỹ năng, kết quả đêm, kiểm phiếu và điều kiện thắng. Client chỉ gửi yêu cầu; host kiểm tra lại.
- Hiệu ứng cá nhân gồm Protected, CannotVote, ExtraVote, Silenced và Cursed. Client nhận loại, độ mạnh, số lượt còn lại, phase dùng kỹ năng, quyền dùng và danh sách mục tiêu hợp lệ của chính mình. Nguồn gây hiệu ứng và vai của người khác không được thêm vào gói công khai trong trận.
- Cursed và Shaman cũ được thích ứng trong `RoleActionRules` của Thương; số bùa của Shaman vẫn do vai cũ quản lý. Bạch Khuyển cập nhật phe trong dữ liệu trận khi thức tỉnh. Các file vai của Nhật không cần sửa.
- Phiếu tăng thêm được cộng khi kiểm phiếu; CannotVote chặn phiếu mới và loại phiếu mất hiệu lực trước khi đếm. Phiếu gốc không bị sửa.
- Chết theo chuỗi hoàn tất trước khi xét thắng. Kết quả cuối truyền riêng vai, phe, thắng/thua và nguyên nhân bị loại để dựng bảng kết quả trên các máy.
- Asset `NeutralWinRule` gán cho vai trung lập cũng được host xét trong luồng online; nhãn kết quả và người đạt điều kiện được đồng bộ tới client.
- Chủ phòng có nút **CHƠI LẠI** trên màn hình kết quả. Các kết nối và lựa chọn nhân vật/bản đồ được giữ; vai, hiệu ứng, phiếu, nhiệm vụ và kết quả được xóa. Chủ phòng phân vai lại, từng người đọc vai và Ready trước khi bắt đầu.
- Mỗi lần phân vai hoặc chơi lại có mã ván mới. Yêu cầu kỹ năng, vote và minigame của ván cũ bị từ chối trước khi cập nhật bộ đếm yêu cầu của ván mới.

## Phạm vi cách ly

Mã thay đổi của luồng này nằm trong `Assets/Scripts/Thuong`, kiểm thử trong `Assets/Scenes/Thuong/Prototype/Editor` và tài liệu trong `Docs`.

Manager kết quả toàn cục của Lợi được tạm ngưng trong khi scene Thương sở hữu trận và khôi phục khi rời scene. Không sửa mã nguồn manager đó. Những chỉnh sửa scene, font, `NetworkPhaseSync` và ProjectSettings đã tồn tại trước yêu cầu được giữ nguyên.

Các packet mới: `thuong.online.action.v3`, `thuong.online.snapshot.v6`, `thuong.online.private.v3`, `thuong.minigame.request.v2`, `thuong.minigame.state.v2`; dấu phiên bản kết nối là `THUONG6|`. Host và client cần cùng bản build mới.

## Kiểm tra

Trong Unity: **Tools → Thuong → Online → Check Complete Logic With 25 Players** (`ThuongOnlineLogicChecks.Run`). Bộ này mở scene online thật, dùng host và 24 kết nối LAN, kiểm tra lobby/bản đồ/phân vai/vote, minigame, rồi chạy hiệu ứng, kỹ năng nhiều lượt, nguyên nhân chết, kết quả, chơi lại và chặn gói của ván cũ. Hai kết nối dùng trực tiếp bộ đọc packet client của production.

Kết quả logic online tại `Logs/ThuongOnlineLogic/result.txt`; kết quả lobby và minigame tại các thư mục `Logs/ThuongOnline*` và `Logs/ThuongMinigames*`. Bản sao project kiểm thử nằm dưới `Logs/LogicPlan/VerificationProject` để không đóng phiên Unity đang mở.

Kiểm thử LAN trên cùng máy không xác nhận dịch vụ đăng nhập, Relay qua Internet, voice hoặc bản build executable trên nhiều máy.

Kết quả ngày 22/10/2026: 96 kiểm tra logic, 14 kiểm tra thao tác offline trong Play Mode, 57 kiểm tra tích hợp scene, 355 kiểm tra minigame, 21 kiểm tra minigame với 25 kết nối LAN và 39 kiểm tra logic online đều đạt. Bộ lobby/bản đồ/vote/rời và tạo lại phòng cũng hoàn tất. Ảnh `Logs/ThuongOnlineLogic/online-result.png` đã được kiểm tra trực quan.

Đối chiếu 498 tệp mã nguồn, scene, prefab và cấu hình ngoài Thương không phát hiện thay đổi ngoài dự kiến; báo cáo tại `Logs/ThuongOnlineLogic/scope-verification.json`. Các sửa đổi cũ của người dùng được so với bản chụp đầu phiên, các chỉnh sửa trước đây của tác vụ trong Nhật/Lợi được so với bản gốc Git sau khi khôi phục.


## Đồng bộ với main ngày 23/10/2026

Bản tích hợp dựa trên `795c810` và giữ phần xử lý mất kết nối mới của main. Xung đột ở `PlayerData.CanVote` được kết hợp để chặn cả người mất kết nối và người có CannotVote, kể cả khi quyền mạng vẫn đang bật. Kho người mất kết nối và cơ chế hủy hành động đêm liên quan được giữ nguyên.

Bổ sung 7 kiểm tra hồi quy cho trường hợp kết hợp này; bộ logic có 103 kiểm tra. Các chỉnh sửa local về scene, font, cấu hình và báo cáo ngoài tác vụ không được đưa vào commit tích hợp.
