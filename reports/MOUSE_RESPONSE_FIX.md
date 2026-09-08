# Khôi phục phản hồi chuột và recoil — 08/09/2026

Nguyên nhân đã xác định của aim chậm: CalculateCoordinates trong đợt chuẩn hóa tọa độ đã ép scaleX=scaleY=1 rồi cộng ROI, trong khi các profile MouseSensitivity hiện hữu được hiệu chỉnh theo phép nhân screen/capture của code cũ. Ví dụ 1920×1080 / capture256: hệ số ngang cũ 7.5, dọc 4.21875 trước smoothing/clamp. Giữ nguyên sensitivity nhưng bỏ hệ số làm aim yếu đi, khiến tương quan với recoil thay đổi.

Thay đổi:
- Khôi phục CalculateCoordinates từ commit 409acd3. Chú thích rõ đây là không gian phản hồi aim cũ, không phải tọa độ vật lý để vẽ ESP.
- Không thay MouseManager, MouseSensitivityProfiles hoặc config người dùng.
- Hoàn nguyên RecoilManager đúng byte bản 409acd3: bỏ stage reset theo đổi slot/scope đã thêm ở lượt trước vì chưa chứng minh giải quyết triệu chứng và làm khác hành vi cũ.
- Giữ sửa race trong WeaponSlotManager: scan nền không áp lại slot cũ sau phím đổi súng.

Kiểm tra:
- Release build PASS 0 lỗi, 250 cảnh báo.
- 48 trường hợp CalculateCoordinates so với code đóng băng từ 409acd3: 2 slot × 4 capture sizes × 2 chế độ offset × 3 vị trí aim, tất cả trùng X/Y.
- Các kiểm tra UI, gate ESP, mapping slot và scan pending tiếp tục PASS.
- Git diff so với 409acd3 cho MouseManager, MouseSensitivityProfiles, RecoilManager rỗng.
- Config hiện tại AI FPS Limit=0, Capture Size=0; không có giới hạn FPS mới đang bật.

Giới hạn: kiểm tra số học không phát sinh input chuột; chưa xác nhận trực tiếp cảm giác aim/recoil trong game. Không khẳng định đã loại hết mọi nguyên nhân recoil. ESP/capture vẫn dùng tọa độ vật lý riêng, không khôi phục parser/capture cũ.
