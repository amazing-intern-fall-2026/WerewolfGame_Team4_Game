# Kế hoạch logic thắng / thua Thương Online

Trạng thái: **đã triển khai cho Thương Online** theo yêu cầu tiếp theo. Các bảng bên dưới giữ lại hiện trạng và đề xuất ở thời điểm lập plan. Luật thực tế, phạm vi và cấu trúc triển khai: `Docs/ThuongOnlineEndgame.md`.

Cập nhật sau đó: bỏ giới hạn 25 giây cho role online. Nút Phân vai chuyển thành Xem phân vai; người chơi tự mở thẻ, bấm OK rồi Ready. Các đề cập timeout ở phần plan bên dưới là lịch sử trước thay đổi này; luồng hiện tại xem `Docs/ThuongOnlineUIForm.md`.

## 1. Hiện trạng đã đọc trong mã nguồn

| Điều kiện | Hành vi hiện có |
| --- | --- |
| Tổng tiến độ nhiệm vụ đạt 100% | Gọi dân làng thắng. Thanh nhiệm vụ trong ngày đạt 100% không tự thắng. |
| Kết thúc vote của ngày 7 | Gọi phe sói thắng nếu chưa kết thúc trước đó. |
| Điên bị vote loại trong ngày 1 | Gọi Điên thắng khi giải quyết vote. |
| Hai người yêu nhau là hai người cuối cùng | Gọi cặp đôi thắng. |
| Bạch Khuyển đã thức tỉnh còn sống, không còn sói thường, còn tối đa 2 dân | Gọi Bạch Khuyển thắng. Chưa loại trừ sát nhân hoặc các role trung lập khác trong điều kiện này. |
| Sát nhân còn sống, không còn sói thường, còn tối đa 2 dân | Gọi sát nhân thắng. Chưa loại trừ Bạch Khuyển thức tỉnh. |
| Cáo tinh còn sống và ít nhất 2 mục tiêu đã mê hoặc đều còn sống | `FoxSpiritRole.HasWon` trả true; hiện chỉ được dùng để ghi đè kết quả khi một phe khác kết thúc ván. |
| Không còn sói / số sói đạt thế cân bằng | Có nhánh mã xử lý nhưng **`useEliminationWin = 0`** trong `OnlineSceneGamePlay`, nên đang tắt. |
| Tất cả cùng chết | Chế độ gameplay tích hợp chưa có kết quả hòa rõ ràng. |

Luồng kết quả đã tồn tại: `GameRoleManager.EndGame` → `GameOver` → snapshot chứa `Winner` → `ThuongOnlinePresentation.RefreshHud` hiện bảng kết quả. Hiện chưa có dữ liệu kết quả riêng về lý do thắng, danh sách người thắng và kết quả cá nhân.

Nguồn chính: `WinConditionManager.cs`, `GameRoleManager.cs`, `TaskManager.cs`, các script role trong `Assets/Scripts/Nhat`, `ThuongOnlineSession.cs`, `ThuongOnlinePresentation.cs` và cấu hình scene online.

## 2. Luật đề xuất để thống nhất trước khi viết code

Đề xuất dùng **nhiệm vụ + loại phe**; giữ mốc ngày 7 làm giới hạn thời gian. Quy tắc sau chỉ là bản đề xuất, không tự bật trong scene.

| Phe / role | Điều kiện đề xuất |
| --- | --- |
| Dân làng | Tổng nhiệm vụ đạt 100%, hoặc không còn mối đe dọa: sói thường, sát nhân và Bạch Khuyển thức tỉnh. Người chết thuộc phe thắng vẫn được ghi nhận thắng. |
| Sói thường | Đạt thế cân bằng: số sói sống ≥ tổng số người sống ngoài phe; không còn sát nhân / Bạch Khuyển thức tỉnh đang cạnh tranh. Hoặc hết ngày 7 khi nhiệm vụ chưa đạt 100% và vẫn còn sói sống. |
| Cặp đôi | Đúng hai người còn sống, có liên kết tình nhân hai chiều. Kết quả riêng ưu tiên hơn thắng phe do quân số. |
| Điên | Bị vote loại trong ngày 1, sau khi hoàn tất hiệu ứng kéo theo của vote. |
| Bạch Khuyển thức tỉnh | Giữ ngưỡng hiện có: còn sống, hết sói thường, tối đa 2 dân; bổ sung không còn sát nhân hoặc Bạch Khuyển đối địch khác. |
| Sát nhân | Giữ ngưỡng hiện có: còn sống, hết sói thường, tối đa 2 dân; bổ sung không còn Bạch Khuyển thức tỉnh hoặc sát nhân đối địch khác. |
| Cáo tinh | Tách thành điều kiện riêng được kiểm tra trực tiếp. Đề xuất giữ yêu cầu từ script: Cáo còn sống và ít nhất 2 mục tiêu mê hoặc đều còn sống. Cần chốt có giữ ngưỡng này hay yêu cầu mê hoặc toàn bộ người còn sống. |
| Hòa | Không còn người sống, hoặc cùng lúc có những kết quả riêng không thể phân định sau toàn bộ hiệu ứng. Nếu chỉ còn role trung lập nhưng chưa đạt luật riêng, phải có quy tắc kết thúc hữu hạn. |

Các điểm cần chốt khi triển khai: bật thắng theo loại phe hay tiếp tục chỉ nhiệm vụ / ngày 7; điều kiện Cáo tinh; ngưỡng thắng của Bạch Khuyển / sát nhân; ưu tiên cặp đôi so với Điên khi cùng phát sinh sau vote. Không suy ra các luật này từ tên role.

## 3. Thứ tự xử lý đề xuất

1. Server giải quyết trọn sự kiện: đòn đánh, bảo vệ, chết dây chuyền do tình nhân, bẫy thợ săn, vote và chuyển phe.
2. Lấy một bản trạng thái cuối sự kiện; không kết luận thắng giữa chuỗi chết.
3. Không còn ai sống → hòa.
4. Điên bị vote ngày 1 → thắng riêng.
5. Cặp đôi là hai người cuối → thắng riêng.
6. Cáo tinh đạt luật đã thống nhất → thắng riêng.
7. Bạch Khuyển / sát nhân đạt luật riêng; điều kiện phải loại trừ xung đột giữa hai bên.
8. Nhiệm vụ 100% → dân thắng.
9. Điều kiện loại phe / thế cân bằng → kết quả phe.
10. Hết ngày tối đa → áp dụng kết quả giới hạn thời gian đã chốt, không tự cho phe đã tuyệt diệt thắng.

Thứ tự này cũng là đề xuất. Một lần kết thúc ván chỉ tạo một kết quả; mọi yêu cầu tới sau bị từ chối.

## 4. Các bước triển khai sau khi chốt luật

1. **Dữ liệu luật và kết quả:** cấu hình bật loại phe, ngày tối đa, luật role đặc biệt; kết quả chứa loại kết quả, lý do, phe / ID người thắng, ngày và giai đoạn kết thúc. Tách người thắng theo phe và thắng riêng.
2. **Bộ xét thắng duy nhất:** tập trung trong `WinConditionManager`; đếm theo phe hiện tại và trạng thái thức tỉnh, xử lý người đã chết, các phe cạnh tranh và hòa. `TaskManager` cũng đi qua bộ xét này thay vì gọi kết thúc riêng.
3. **Điểm kích hoạt:** sau hoàn thành nhiệm vụ, xử lý trọn đêm, xử lý trọn vote + bẫy, chết dây chuyền, chuyển phe và khi kết thúc ngày tối đa. Giữ server là nơi duy nhất kết luận trong online.
4. **Khóa ván:** dừng timer, di chuyển, nhiệm vụ, kỹ năng và vote; không chuyển sang ngày mới; giữ kết quả cố định. Mất kết nối được phân biệt với kết quả thắng / thua (hiện tại mất người sau phân vai đóng cả phòng).
5. **Đồng bộ và UI:** truyền kết quả có phiên bản; mọi client hiện cùng phe thắng và lý do; mỗi người thấy Thắng / Thua / Hòa theo danh sách thắng. Hiện danh sách tên, role và trạng thái cuối ván; kết quả vote cuối vẫn đọc được và có nút đóng rõ ràng. Không lộ role trước khi kết thúc.
6. **Cân bằng lobby:** rà lại bộ role cho 2–25 người. Với luật thế cân bằng, 2 người hoặc một số bộ role nhỏ có thể thắng ngay khi bắt đầu; giới hạn phòng chơi chính thức hoặc dành phòng quá nhỏ cho chế độ thử, thay vì bỏ qua kiểm tra thắng đầu ván.

## 5. Tiêu chí kiểm tra khi triển khai

- Dân thắng do nhiệm vụ, thắng do hết mối đe dọa; sói thắng do quân số và do ngày tối đa.
- Từng role đặc biệt, role đã chết không được thắng riêng trừ luật Điên, thức tỉnh / chuyển phe được tính đúng.
- Vote hòa / không có phiếu, bẫy và tình nhân chết dây chuyền, tất cả chết, chỉ còn trung lập.
- Hai điều kiện xảy ra cùng lượt, kết quả không bị ghi đè và không xét giữa chuỗi hiệu ứng.
- Host / client cùng kết quả, không nhận hành động sau GameOver, kết quả vote cuối không che vô thời hạn bảng kết thúc.
- Phòng nhỏ không kết thúc ngoài dự kiến; phòng 25 người và tên tiếng Việt vẫn nhận đủ dữ liệu.

## 6. Sửa Ready trong lượt hiện tại

- Trước phân vai: hiển thị rõ **CHỜ PHÂN VAI**.
- Sau OK hoặc hết 25 giây: cho Ready ngay khi đã có role riêng; không đợi cờ xác nhận đọc role quay về từ server mới mở nút.
- Server nhận Ready như xác nhận đã đọc role, đồng bộ lại trạng thái; vẫn cấm bắt đầu trước khi mọi người đọc role / Ready.
- Xác nhận đọc role là thao tác lobby, không phụ thuộc ngày / phase gameplay trong gói yêu cầu.
- Tắt nhận chuột cùng Canvas đang ẩn để UI lobby không bị lớp gameplay / role chặn.

Phần Ready được sửa trước. Luật thắng / thua được triển khai trong lượt tiếp theo, tách khỏi hành vi offline và các phần khác.
