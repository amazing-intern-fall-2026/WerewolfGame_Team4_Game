# Thắng / thua và UI kết quả Thương Online

## Phạm vi

Chỉ kích hoạt khi `ThuongOnlineSession` gắn bộ xét luật vào `GameRoleManager` và `WinConditionManager` cùng scene `OnlineSceneGamePlay`. Runtime mạng nằm trong DontDestroyOnLoad, nên kiểm tra phạm vi dựa trên scene của gameplay manager. Callback được gỡ khi runtime bị hủy.

Không sửa script role trong `Assets/Scripts/Nhat`, hệ thống mạng trong `Assets/Scripts/Loi`, scene / prefab, hoặc asset UI dùng chung. Các scene không gắn bộ xét mới tiếp tục chạy nguyên luật và bảng kết quả cũ, kể cả prototype offline của Thương.

Khi tích hợp main mới có `NetworkGameOverManager` tự khởi tạo toàn cục, session Thương tạm tắt manager đó trước Start để tránh xét luật khác hoặc chồng hai màn hình kết quả. Trạng thái bật được khôi phục khi rời scene / hủy runtime. Lớp tương thích chỉ nằm trong mã Thương; source của manager bên Lợi giữ nguyên so với main.

## Luật mặc định và thứ tự ưu tiên

Server xét sau toàn bộ xử lý đêm, vote + bẫy + hiệu ứng, khi chuyển ngày / đêm / vote và trong LateUpdate sau nhiệm vụ, kỹ năng, chuyển phe hoặc chết dây chuyền ngoài thời gian thu thập hành động đêm. Khi đêm đang nhận hành động, đợi giải quyết hết đòn đánh và hiệu ứng rồi mới xét (Hồ Ly không thắng sớm trước đòn đánh đang chờ có thể giết mình). Không kết luận ở giữa callback chết. Chỉ có một kết quả cố định cho mỗi ván.

| Ưu tiên | Kết quả | Điều kiện |
| --- | --- | --- |
| 1 | Hòa | Tất cả đã chết sau khi xử lý hết hiệu ứng. |
| 2 | Kẻ Điên thắng riêng | Bị vote loại trong ngày 1; xét sau toàn bộ vote. |
| 3 | Cặp đôi thắng riêng | Là đúng hai người cuối cùng, liên kết tình nhân hai chiều. |
| 4 | Hồ Ly thắng riêng | Còn sống và `FoxSpiritRole.HasWon` đạt: đã mê hoặc ít nhất 2 người, toàn bộ người đã mê hoặc đều còn sống. Giữ điều kiện của script hiện có; xét trực tiếp thay vì chờ ghi đè thắng phe. |
| 5 | Bạch Khuyển thức tỉnh thắng riêng | Chỉ một Bạch Khuyển thức tỉnh sống, không còn sói thường / sát nhân, tối đa 2 dân còn sống. |
| 6 | Sát nhân thắng riêng | Chỉ một sát nhân sống, không còn sói thường / Bạch Khuyển thức tỉnh, tối đa 2 dân còn sống. |
| 7 | Dân làng thắng | Tổng nhiệm vụ đạt 100%. |
| 8 | Dân làng thắng | Vẫn có dân sống và không còn sói thường, Bạch Khuyển thức tỉnh hoặc sát nhân sống. |
| 9 | Ma Sói thắng | Chỉ còn sói thường sống (kể cả phòng thử 2 người). Hoặc sói sống ≥ tổng người sống ngoài phe, không còn Bạch Khuyển thức tỉnh / sát nhân, phòng ban đầu có ít nhất 3 người. |
| 10 | Ma Sói thắng / hòa do hết ngày | Kết thúc vote ngày tối đa (mặc định 7), nhiệm vụ chưa hoàn tất: còn sói thường → sói thắng; không còn sói và chưa ai đạt luật ưu tiên → hòa. Không cho phe đã tuyệt diệt thắng do hết ngày. |
| 11 | Hòa do bế tắc | Chỉ còn trung lập không thể đạt điều kiện riêng. Nếu Hồ Ly còn đủ 2 mục tiêu sống để tiếp tục mê hoặc, cho chơi đến khi thắng hoặc hết ngày tối đa. |

Thắng phe tính cả đồng đội đã chết, theo phe ở cuối ván. Thắng riêng chỉ tính ID đạt điều kiện (Điên là ngoại lệ cần bị vote chết). Bạch Khuyển chưa thức tỉnh thuộc dân; khi thức tỉnh là phe cạnh tranh riêng, không cộng vào sói thường. Chuyển phe được lấy từ trạng thái cuối sự kiện.

Phòng 2 người là phòng thử: vẫn xét nhiệm vụ, loại phe và ngày tối đa, nhưng tắt thắng do cân bằng quân số. Trước khi gửi thẻ role, online giảm số sói nếu bộ random có thể tạo thế cân bằng ngay từ đầu (ví dụ 2 sói / 2 người khác). Ưu tiên giữ Dog Spirit có đòn tấn công từ đêm đầu. Bộ role của các scene khác không đổi.

`ThuongOnlineWinSettings` trên session có `eliminationWin`, `minimumPlayersForParity` (tối thiểu 3) và `specialWinVillageLimit` (mặc định 2). Ngày tối đa lấy từ `WinConditionManager.MaxDay` hiện có. Cờ `useEliminationWin` cũ trong scene được giữ nguyên cho luật cũ; online dùng `winSettings.eliminationWin` mặc định bật. Cấu hình được gửi trong snapshot để client mô tả đúng luật của host. Hướng dẫn kỹ năng được giữ; điều kiện thắng được bổ sung trong thẻ role online, riêng mô tả thắng cũ của Hồ Ly được thay cho đúng thời điểm xử lý đêm.

## Dữ liệu và hiển thị

- `ThuongMatchOutcome.cs`: lấy một bản trạng thái và xét luật, trả `ThuongMatchResult` cố định: phe / role thắng, lý do, ngày / phase, danh sách tên, avatar, role, phe cuối, sống / chết và thắng / thua.
- `ThuongOnlineEndgame.cs`: chỉ server kết luận; khóa timer, di chuyển và hành động, gửi snapshot ngay. Lối gọi thắng trực tiếp cũ trong nhiệm vụ / role được chuyển qua bộ xét trong LateUpdate khi có bộ luật online.
- `ThuongOnlineResultView.cs`: Canvas runtime riêng, dùng phong cách giấy / gỗ hiện có. Mỗi người thấy **CHIẾN THẮNG / THẤT BẠI / HÒA**, phe thắng và lý do, role / phe của mình, bảng cuộn tối đa 25 người và nút **RỜI PHÒNG**.
- **XEM VOTE CUỐI** chuyển bảng sang số phiếu có trọng số và người bị vote loại; **VỀ BẢNG KẾT QUẢ** quay lại kết quả trận. Vote cuối không tự che màn hình kết thúc.
- Người chơi rời sau GameOver không làm xóa kết quả của những người còn lại. Khi đã nhận kết quả, client vẫn giữ màn hình đó nếu chủ phòng rời; bấm Rời phòng mới dọn kết quả.
- Snapshot chuyển sang **`thuong.online.snapshot.v4`**. Có cờ `hasResult`; chỉ sau GameOver mới có dữ liệu role của mọi người. Host và client cần cùng phiên bản mới. Mỗi snapshot hoàn chỉnh gồm cả kết quả vote và kết quả trận.

## Xác nhận thực hiện

Biên dịch C# và rà phạm vi thay đổi; chưa chạy Play Mode / LAN cho bản thắng thua này. Bộ kiểm tra lobby / vote hiện có đã được cập nhật định dạng v4 và giữ một sói sống trong kịch bản kiểm tra vote để tránh vô tình kích hoạt thắng do hết phe đối địch.
