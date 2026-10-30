# Biểu cảm người chơi của Thương

Phạm vi: `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`, dùng `ThuongOnlineSession` và avatar của Thương. Code dùng chung nằm trong `Assets/Scripts/Thuong/Emotes`; kết nối mạng nằm trong partial `Assets/Scripts/Thuong/Online/ThuongOnlineEmotes.cs`. Không sửa prefab, scene hoặc script của thành viên khác.

- Giữ **T** mở vòng chọn tám biểu cảm, rê chuột hoặc dùng phím hướng/WASD để chọn, thả T để gửi.
- **Esc**, trở về vùng tâm rồi thả T, mất focus, đổi phase/epoch, bị loại hoặc bắt đầu minigame sẽ hủy. Di chuyển và thao tác E tạm dừng trong khi vòng chọn mở.
- Biểu cảm hiện trên đầu avatar khoảng 3 giây. Host giới hạn mỗi người gửi tối đa một lần mỗi 1,5 giây.
- Mọi role dùng chung danh mục. Dữ liệu biểu cảm không chứa role, phe, mục tiêu hay lượt kỹ năng.
- Dùng được khi còn sống trong DayStart/Event/Task/Discussion/Voting/Night; không dùng trong Setup/RoleReveal/các bước resolve/GameOver.
- Host xác thực sender từ kết nối, người chơi trong phòng, trạng thái sống, minigame, phase/ngày, match epoch, ID, sequence và cooldown. Client chỉ nhận biểu cảm đã được host xác nhận; thời hạn dùng giờ server.
- Reset khi chuyển ván, rời phòng hoặc đổi scene. UI và mạng không phụ thuộc các component của Lợi.

Kiểm tra bằng `Tools/Thuong/Emotes/Run Checks`. Entry batch `ThuongEmoteSystemChecks.RunAndBuildQa` build đúng scene online của Thương với define `THUONG_EMOTE_QA`; script `Assets/Scenes/Thuong/Prototype/Editor/RunPlayerEmoteChecks.ps1` chạy hai tiến trình host/client. `ThuongEmoteSystemChecks.RenderPreviews` xuất ảnh 1280×720 và 1920×1080. Báo cáo nằm trong `Logs/Emotes/Thuong`.

Các scene offline của Thương chưa được nối vào phần đồng bộ biểu cảm online.

Kiểm chứng ngày 30/10/2026: build đúng `OnlineSceneGamePlay` thành công; 53 kiểm tra Editor và 27 kiểm tra trên hai tiến trình host/client của Thương đạt, không có exception. Đã kiểm tra truyền hai chiều, gắn lên avatar Thương, cooldown trên host, ID/sender sai, hết hạn, đổi phase, hủy vòng chọn, dừng di chuyển và reset. Đây là kiểm tra LAN cùng máy.
