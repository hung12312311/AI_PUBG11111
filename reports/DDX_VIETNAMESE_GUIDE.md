# Tiếng Việt và chẩn đoán ddxoft — 08/09/2026

## Ngôn ngữ
Mở app bản mới trong bin/Build, vào Settings → Language / Ngôn ngữ → English hoặc Tiếng Việt. Nhãn điều khiển đổi ngay, lựa chọn lưu riêng tại bin/Build/bin/language.cfg. Đã chọn Tiếng Việt cho lần mở tiếp theo. Tên model, backend và giá trị lựa chọn kỹ thuật được giữ nguyên. Các nhãn cài đặt chính, ngắm, ESP và recoil đã có bản dịch; thông báo từ DLL bên ngoài vẫn do ddxoft hiển thị.

Chỉ lớp hiển thị thay đổi. ADropdown.SettingKey bảo đảm chọn dropdown vẫn ghi khóa gốc dù nhãn đã dịch. Không thay đổi lực recoil hoặc độ nhạy. Kiểm tra chuyển ngôn ngữ hai chiều, ghi lựa chọn dropdown sau dịch, lưu lựa chọn và bộ hồi quy đều đạt. Release: 0 lỗi, 250 cảnh báo. Chữ có dấu và bộ chọn đã được kiểm tra bằng ảnh render WPF.

## Kết quả kiểm tra bảo vệ Windows
Đã đọc bằng quyền Administrator: VirtualizationBasedSecurityStatus=0, SecurityServicesRunning=[0]. Memory Integrity/VBS không chạy. Dịch vụ WinDefend ở trạng thái Stopped; API Get-MpComputerStatus báo lỗi chung nên không khẳng định các trường trạng thái API không đọc được. Không có phần mềm antivirus khác trong truy vấn SecurityCenter2. Vì các cơ chế này không hoạt động sẵn, không thực hiện lệnh tắt Defender hoặc Memory Integrity. Không đổi firewall, Secure Boot, danh sách chặn driver hoặc kiểm tra chữ ký.

## Cài và dùng gói chính thức
Nguồn: https://github.com/ddxoft/master — README ghi bản miễn phí xác thực qua mạng khi nạp, và yêu cầu tham khảo ví dụ trong gói.

Gói 2026.DD.EV.HVCI.63xxx.7z có hai biến thể:
- 1.simple/dd63330.dll: DLL x64 đã kiểm tra chữ ký hợp lệ, hiện được đặt tên ddxoft.dll bên cạnh app. Ví dụ C# nạp DLL, gọi DD_btn(0), chỉ bật sử dụng khi kết quả bằng 1. App đã áp dụng đúng trình tự này và yêu cầu Administrator. Đóng app trước khi thay DLL, mở CouldBeAimmyV2.exe bằng quyền quản trị, sau đó chọn ddxoft Virtual Input Driver trong Mouse Movement Method của slot cần dùng. Cần mạng cho bước xác thực bản miễn phí.
- 2.hid/ddhid.63340.dll: biến thể riêng đi kèm thư mục drv (INF/CAT/SYS và ddc.exe). install.bat gọi ddc.exe. Không thay riêng DLL HID vào cấu hình simple mà bỏ qua bộ driver tương ứng. Chưa cài hoặc thử biến thể HID trên máy này.

Bản simple vẫn lỗi: DD_btn(0) từng trả 0 rồi -3 dù phép thử chạy Administrator; Windows từng ghi nhận service chạy rồi chuyển sang dừng/xóa. Chưa có nguồn chính thức giải thích mã -3, chưa xác nhận driver hoạt động. Không thể kết luận tắt bảo mật sẽ sửa được. Dùng Mouse Event trong khi driver chưa khởi tạo thành công.

Báo cáo tương tự trên kho tác giả (không phải lời xác nhận nguyên nhân): https://github.com/ddxoft/master/issues/104 và https://github.com/ddxoft/master/issues/103.
Tài liệu Microsoft về nhật ký xác minh driver: https://learn.microsoft.com/en-us/windows-hardware/drivers/install/code-integrity-event-log-messages
