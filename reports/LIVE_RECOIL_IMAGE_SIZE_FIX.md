# Image Size và lực recoil thực tế
- Settings đồng bộ mỗi 500ms từ metadata của từng model đã tải, không chờ khóa GPU, dừng khi tab không hiện. Không đồng bộ chen ngang lúc tải/chỉnh model. Fixed model khóa chọn size; dynamic ONNX cho phép chỉnh.
- Recoil GUI mặc định chọn scope đang dùng, có nút Chỉnh scope đang dùng, cảnh báo khi đang sửa scope khác.
- Hiện giai đoạn S1-S4/Tap, lực gốc, lực sau cộng con lăn khi đang bắn. Phân biệt điều chỉnh nhầm stage với giá trị chưa áp dụng.
- Sửa lực của active scope xóa temporary wheel offset, không để phần cộng ẩn giữ lực cao. Scope khác không xóa offset của scope đang dùng.
- Test PASS: GetSetting của vòng recoil đọc force vừa sửa =2 rồi =0, offset active scope được reset. 48 kiểm tra phản hồi aim cũ và 6 kiểm tra UI/slot trước tiếp tục đạt trước thay đổi nhãn stage cuối. Release build cuối PASS 0 lỗi /250 cảnh báo.
- Chưa kiểm thử kéo chuột trong game; không tuyên bố đã xác định mọi nguyên nhân. Timer và lựa chọn scope không tự đổi lực lưu sẵn.
