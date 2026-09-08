# Sửa Tổng quan, đo hiệu năng và kiểm tra engine — 08/09/2026

## Nguyên nhân
- Nút Run test cũ tạm dừng AI trong suốt phép đo, tạo thêm một ONNX session và capture session; không hỗ trợ TensorRT. Nó không phải nút bắt đầu nhận diện.
- Bộ cập nhật tổng quan lấy khóa model trên UI thread, có thể chờ GPU hoặc quá trình tải model.

## Thay đổi
- Tổng quan mới: tên model, backend, kiểu dữ liệu input thực, kích thước; ba ô capture / suy luận / FPS, phần nâng cao thu gọn.
- Thay cửa sổ Run test bằng **Đo 5 giây** trực tiếp trong tổng quan. Đếm lượt suy luận của pipeline đang chạy, không tải thêm model, không dừng AI, không tự bật aim/trigger.
- Có tiến trình, nút Hủy, thông báo khi không có lượt suy luận; từ chối kết quả trộn nhiều model/slot.
- Poll metadata không chờ khóa GPU. Hủy phép đo khi rời tổng quan.
- Engine validator chạy warm-up thật và có thể xuất JSON; subprocess có timeout 45 giây.
- Inspector giữ shape khai báo động của engine, đồng thời dùng shape OPT thực tế. Việc thay đổi size engine từ slider cũ vẫn bị chặn để tránh metadata không khớp buffer.

## Kiểm tra
- Release build: PASS, 0 lỗi, 250 cảnh báo (đa số nền tảng Windows/nullability đang có).
- Startup + phép đo: 7 PASS: hai slot thiếu khóa DirectML cũ, reload slot 1, bảo toàn slot 2 khi load lỗi, phép đo không mẫu, hủy nhanh, đếm delta khi producer tiếp tục chạy, UI snapshot không chờ khóa.
- Kiểm tra bộ đếm dùng producer giả lập với thời gian biết trước; không khẳng định đã kiểm thử aim thực tế trong game.
- GUI render ở 480 × 530: reports/gui-dashboard.png, đã kiểm tra trực quan.
- 10/10 engine mới trong Slot1 tải và suy luận thành công trên GPU hiện tại; báo cáo reports/engine-validation-summary.json và từng engine-*.json.
- Slot2 chứa bản engine cùng SHA256 với Slot1.
- Tất cả input engine là kFLOAT (FP32); không suy ra độ chính xác của mọi lớp bên trong engine từ kiểu I/O.
- Engine dynamic: shape OPT 640×640, profile MIN 320×320, MAX 640×640.
- Warm-up dùng ảnh xám tổng hợp, không phải đánh giá độ chính xác nhận diện trong game. Chưa thử engine FP16 vì bộ engine mới chỉ có I/O FP32.

## Cách dùng
Mở bin/Build/CouldBeAimmyV2.exe. Chọn model ở Slot 1 / Slot 2, bật tính năng cần dùng và dùng phím kích hoạt như trước. Nút Đo 5 giây chỉ đo phiên đang chạy. Nếu không có lượt suy luận, giao diện báo rõ và không hiển thị FPS giả.

Tài liệu này ghi riêng đợt sửa phản hồi GUI/test/engine. Không thay thế checklist hoàn tất toàn bộ master prompt; các mục upstream, docs và Git còn phải được rà soát riêng.
