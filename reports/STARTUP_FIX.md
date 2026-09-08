# Sửa lỗi văng khi khởi động — 2026-09-08

Windows Event Log lúc 12:52–12:53 ghi `KeyNotFoundException`: không có khóa `DirectML` trong `toggleState`. Stack trace đi qua `AIManager.LoadSecondaryModel` và sự kiện `FileManager.ModelListBox_SelectionChanged`.

Nguyên nhân là thay đổi chọn provider ở lần cập nhật trước đã giả định cấu hình custom có toggle `DirectML`. Cấu hình hiện có không chứa toggle này.

## Thay đổi

- `OnnxProviderPreference` đọc khóa bằng `TryGetValue`, giữ chính sách DirectML rồi fallback CPU cho cấu hình cũ. Lựa chọn ONNX Provider rõ ràng vẫn được ưu tiên.
- Dùng cùng bộ phân giải cho Slot 1, Slot 2 và tải lại model; bỏ các truy cập trực tiếp vào toggle thiếu.
- Xử lý exception tại hai sự kiện chọn model của giao diện, khôi phục các toggle trong `finally`, không đánh dấu model Slot 2 đã tải khi thất bại.

## Kiểm tra

- Release x64: PASS, 0 lỗi; vẫn còn cảnh báo của dự án.
- Regression cấu hình thiếu DirectML, lựa chọn CPU và giá trị legacy không hợp lệ: PASS.
- `tests/StartupChecks`: dùng AIManager thật với hai model 256 × 256 có sẵn, mọi toggle điều khiển đầu vào đều tắt trong tiến trình test. Tự tải hai slot, tải lại Slot 1, model Slot 2 không tồn tại giữ nguyên model đang dùng: 3 PASS.
- Đã mở bản `bin/Build/CouldBeAimmyV2.exe` lúc 12:56:41, xác nhận cửa sổ Aimmy tồn tại và tiến trình tiếp tục chạy sau khởi động.

Đây là xác nhận sửa lỗi khởi động nêu trên; không phải xác nhận toàn bộ master prompt đã hoàn tất.
