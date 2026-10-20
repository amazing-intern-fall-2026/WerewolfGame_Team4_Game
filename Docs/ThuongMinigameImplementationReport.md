# Báo cáo triển khai minigame — 19/10/2026

Nhánh: `codex/thuong-minigame-integration`, bắt đầu từ `57290e4`. Đã fetch `main`, `thuong`, `Bảo` trước khi triển khai; không có ref mới. Không push hoặc merge vào main/thuong trong lần triển khai này.

## Đã triển khai

- Nhập riêng nguồn Trapsweeper, commit `f6bf12c`. `git diff origin/Bảo HEAD -- Assets/Minigames/Trapsweeper Assets/Minigames/Trapsweeper.meta` không có khác biệt tại thời điểm commit nhập nguồn.
- Khung runtime, sáu controller nhiệm vụ, catalog, sáu prefab và bốn TaskData mới, commit `1593f2b`. Người dùng đã chọn thêm Thu gom trứng, Pha chế thuốc, Bảo vệ khu vực, Gỡ bẫy. Gom củi/Thu hoạch gắn với TaskData hiện có; bốn task cũ khác giữ cách tương tác E.
- Server quản lý lượt, khóa task/ngày, sequence, hạn dùng, khoảng cách, sống/chết và điều kiện thắng. Đã chặn CompleteTask cũ đối với task có minigame. Không nhận boolean thắng từ client, không gửi bàn bẫy/seed ẩn.
- Camera/RenderTexture/local physics scene riêng; cùng EventSystem/AudioListener với trận; không pause/reload active scene. Chuyển phase, bị loại, task hết hiệu lực, rời phiên, mất mạng và kết thúc ván đóng/vô hiệu hóa lượt.
- Bộ cài asset, kiểm tra luật/dependency, kiểm tra LAN và kiểm tra rendering có menu trong Unity. Hướng dẫn cấu hình ở `Docs/ThuongMinigameIntegration.md`.

## Kết quả đã chạy

Unity `6000.6.0f1` trên máy hiện tại:

| Kiểm tra | Kết quả |
| --- | --- |
| Biên dịch runtime và Editor | Đạt; không có lỗi C# |
| Biên dịch script Windows Player (Development) | Đạt, `THUONG_MINIGAME_PLAYER_SCRIPTS_PASSED` |
| Luật sáu game, vòng đời, khóa/replay/sai chủ, timeout, potion, traps, wire, GUID/dependency/LFS | Đạt 325 assertions trong lần chạy LAN cuối; số assertions thay đổi theo flood mở ô của bàn ngẫu nhiên |
| Hồi quy gameplay offline | Đạt 57 assertions |
| Minigame với host + 24 client LAN loopback | Đạt 21 assertions; thưởng từ thao tác client, gửi lặp/sai chủ, tranh lượt, khóa di chuyển, task hoàn tất từ hệ thống khác, timer/active scene, bị loại, sang đêm, gói muộn và cleanup |
| Hồi quy online lobby/role/Ready/vote/results/disconnect/rehost | Đạt 11.556 checks, gồm các điều kiện được kiểm tra lại trong khi chờ mạng |
| Render sáu cửa sổ, bounds/aspect pointer, cắm cờ/mở ô qua viewport và cleanup sau mỗi lượt | Đạt 44 assertions, đã xem ảnh sáu game |

Ảnh và log nằm trong `Logs/Minigames`, `Logs/OnlineUIForm`, `Logs/Minigames-install.log`, `Logs/Minigames-online.log`, `Logs/Minigames-presentation.log`. Đây là output tạm không commit.

Log Editor có exception của package Multiplayer PlayMode khi đọc cấu hình trong Play Mode và cảnh báo hàng đợi UTP khi chạy nhiều transport trong cùng tiến trình. Các assertion kết quả bên trên đã hoàn tất; không coi lần chạy này là đo hiệu năng hay kiểm chứng mạng Internet thực tế.

## Còn cần xác minh trước khi merge main

- Hai bản ứng dụng đầy đủ trên host/client riêng, Relay có độ trễ/mất gói, voice/Vivox đang gọi thật và mất kết nối giữa các máy.
- Chuột/touch trên thiết bị thật, đổi kích thước cửa sổ và hiệu năng 25 người trên nhiều máy.
- Cân bằng thời lượng/độ khó trong ngày 45 giây; kết thúc ván khi minigame cuối đưa tiến độ lên 100%.
- Chưa đóng gói/chạy thử file executable hoàn chỉnh; bước Windows Player đã chạy là biên dịch script.

Giữ nguyên ngoài commit: thay đổi có sẵn của `PrototypeFont.asset`, Vivox `Settings.json`, `Assets/Settings/PlayMode*`, và file plan có sẵn `Docs/ThuongMinigameMergePlan.md`. Không sửa scene/prefab demo, role Hunter, scene Lợi, BuildSettings, TagManager hay input settings.
