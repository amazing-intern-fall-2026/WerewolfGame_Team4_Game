# Test nhân vật offline trong phần Thương

Mở `Assets/Scenes/Thuong/Prototype/ThuongGameplayPrototype.unity` bằng Unity 6000.6.0f1. Trước khi nhấn **Play**, đặt **PlayerManager → Prototype Lobby Size** trong Inspector bằng số người muốn thử. Script test đã gắn trên object `Systems` trong scene. Đồng đội có cùng dự án chỉ cần mở scene này để thử; không cần chạy menu hay sửa Build Settings.

Khi Play, script tạo đúng số nhân vật trong **Prototype Lobby Size** từ object Player sẵn có (SpriteRenderer, Rigidbody2D, collider và PlayerMovement). Mỗi nhân vật có ID và vai riêng; các object `Test Player 1` đến `Test Player N` xuất hiện trong Hierarchy. Ví dụ đặt **10** sẽ tạo **10 nhân vật**. `RoleManager` gán ngẫu nhiên vai cho đúng 10 người trước khi nhân vật xuất hiện. Không tạo bảng UI mới.

| Phím | Nhân vật điều khiển | Vai |
| --- | --- | --- |
| F1–F12 | Player 1–12 | Ngẫu nhiên mỗi ván |
| PageDown/PageUp | Nhân vật kế tiếp/trước đó | Dùng được khi lobby trên 12 người |

Các nhân vật còn lại đứng trong scene. UI vai/kỹ năng và người bỏ phiếu có sẵn của prototype sẽ theo nhân vật được chọn, nhưng script test không tạo thêm UI.

Console in seed cùng danh sách `[ROLE]` cho từng người. Dòng `[Thuong offline test]` xác nhận số nhân vật đã tạo. Vai được rút từ 17 class Role cụ thể trong dự án; các tên enum chưa có class riêng không được rút. Hồ vai giữ ít nhất một Sói và một Dân làng khi có từ hai người, sau đó rút ngẫu nhiên từ mọi vai đặc biệt và xáo trộn. Lobby 4 người cũng có thể gặp vai của Nhật.

Test nhanh: nhấn **OK / BẮT ĐẦU** trên thẻ vai, đi lại bằng WASD/phím mũi tên, làm nhiệm vụ bằng E. Đến ban đêm, chuyển đến người có kỹ năng chọn mục tiêu, mở **ROLE / KỸ NĂNG** rồi thử. Đến lượt bỏ phiếu, đổi nhân vật để thử từng người bỏ phiếu. Nhấn R để chạy lại scene với cùng số người và một lần phân vai mới; dừng Play để thoát chế độ test.

Đây là các nhân vật trên **một máy**, mỗi lần chỉ điều khiển một nhân vật. Người thật kết nối vào cùng trận là bước online tiếp theo; script này chưa tạo kết nối mạng. `PlayerManager` không đặt trần số người chơi; `Prototype Lobby Size` chỉ có mức tối thiểu 0. Bảng vote có sẵn trong scene chỉ có 5 nút mục tiêu, nên khi thử nhiều hơn 5 người, các Player từ 6 trở đi chưa có nút để được chọn trong bảng đó.

Các class Role của Nhật đã được đưa vào hồ phân vai. Một số class hiện mới ghi log hoặc thiếu luật hoàn chỉnh (Pháp Sư, Thợ Săn, Dệt Duyên, Sói Trắng, Đứa Trẻ, Hồ Ly); việc xuất hiện trong hồ vai chưa đồng nghĩa kỹ năng và điều kiện thắng của chúng đã chơi trọn vẹn. Phần test offline giữ đúng hành vi hiện có và chờ chốt luật trước khi nối những cơ chế còn thiếu.

Nếu đặt **Prototype Lobby Size** từ 100 trở lên, Console ghi `[Thuong stress]` với thời gian tạo nhân vật trong `Start`, sau đó ghi FPS trung bình và frame chậm nhất trong 5 giây tiếp theo. Đây là số đo Play Mode trên máy đang chạy; số người được gán vai vẫn được xác nhận bằng dòng `[ROLE]`.
