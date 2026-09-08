# Đơn giản hóa tổng quan và giảm lực recoil
- Bỏ Cài đặt model nâng cao và các handler nhập kích thước/backend tương ứng. Giữ Image Size ở Settings và Inspector chỉ đọc.
- Theo yêu cầu giảm lực: ghì liên tục S1–S4 dùng hệ số 0.5 sau phần cộng con lăn. Không sửa giá trị config hiện có; cùng con số cho tổng lượng kéo xấp xỉ một nửa khi cùng số lượt chạy.
- Giữ tích lũy phần lẻ; thanh lực bước 0.01, nút +/- bước 0.01, min 0. Nhập số thập phân bằng dấu chấm hoặc phẩy đã được hỗ trợ.
- Tap không qua hệ số mới.
- Release build PASS 0 lỗi/250 cảnh báo. Test PASS: 2×10 lượt cho 10 đơn vị thay vì20;0.125×16 lượt cho1; lực0 không kéo;0.01 vẫn tích lũy. Các kiểm tra UI/slot/48 tọa độ aim tiếp tục đạt.
- Đã render và kiểm tra dashboard không còn mục nâng cao. Chưa đo recoil trong game.
