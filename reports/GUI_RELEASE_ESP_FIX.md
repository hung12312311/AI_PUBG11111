# Sửa GUI / cửa sổ Release / ESP — 08-09-2026

- Khối trắng là Expander nâng cao và Inspector bị ảnh hưởng theme MaterialDesign Light. Đã dùng template tối riêng cho Expander, ToggleButton header, ComboBox, ComboBoxItem và TextBox; có lời giải thích chức năng. Đã render mở rộng dưới theme MaterialDesign Light thật, kiểm tra reports/gui-dashboard-expanded.png.
- SettingsMenuControl được khởi tạo khi Application.Current.MainWindow vẫn trỏ tới splash của Release. Các handler trước đây sửa kích thước splash, không phải cửa sổ chính. Đã chuyển sang owning _mainWindow. PreloadMainWindowAsync lấy kích thước đã restore thay vì arrange mặc định 670×444.
- Thêm Show FPS / Inference trong ESP Config, khóa thao tác khi Show Detected Player tắt. Overlay chỉ hiển thị nếu cả hai công tắc bật. FPS lấy số lượt suy luận theo khoảng thời gian, Inference là thời gian suy luận trung bình của phiên.

Kiểm tra:
- Release build PASS: 0 lỗi / 250 cảnh báo.
- UiRegressionChecks PASS: slider sửa đúng owning window khi MainWindow của Application vẫn là splash; save/restore kích thước 1030×820; đủ bốn tổ hợp công tắc ESP.
- GUI expanded render PASS dưới theme sáng của app: nền tối, header/ô nhập/ô chọn đọc được.

Recoil: CHƯA XÁC ĐỊNH NGUYÊN NHÂN / CHƯA KHẲNG ĐỊNH ĐÃ SỬA.
- InputLogic/RecoilManager.cs không khác mốc fb893fc hoặc 409acd3. MouseManager cũng không bị sửa trong đợt tích hợp hiện tại.
- Red Dot trong config output và backup Git đều: S1 10 / 0.25s, S2 11.3 / 0.8s, S3 15.35 / 0.85s, S4 15.6. Các tham số Tap cũng khớp.
- Backup config cũ hơn scratch/config-backup-20260907-125959 có Strength=70, Step=1, Delay=2, Multi=5, chưa có 4 stage. Chưa có bằng chứng đây là bản người dùng muốn đối chiếu.
- Vòng recoil cộng lực mỗi Sleep(5), có khả năng phụ thuộc nhịp scheduler; chưa có phép đo chứng minh nhịp thay đổi trên bản cũ của người dùng. Không sửa hệ số, không ghi đè config bằng backup khác.
- Đã hỏi đường dẫn EXE cũ đang được dùng làm mốc so sánh để tiếp tục kiểm tra.

## Bổ sung: slot, vị trí cửa sổ và ba ô ESP
- Metadata Scope_V7_160: 0=8x,1=6x,2=4x,3=3x,4=2x,5=chamdo,6=morong; khớp mapping hiện có, không tự đảo slot/ROI.
- Sửa race: quét nền đọc/ap dụng active slot trong cùng khóa với đổi slot bằng phím, tránh áp lại slot cũ.
- Đổi scope/slot trong khi giữ bắn reset thời điểm bắt đầu stage và phần pixel lẻ. Không thay lực cấu hình.
- Reveal Release giữ tâm splash khi cửa sổ chính khác kích thước, thay vì dùng cùng góc trên trái gây lệch xuống/phải.
- ESP gồm ba ô Chụp màn hình (ms), Suy luận (ms), FPS suy luận.
- Release build 0 lỗi. UiRegressionChecks 6 PASS, gồm kiểm tra scope riêng từng slot và scan chờ không ghi đè slot vừa chọn.
- Chưa kiểm thử recoil trực tiếp trong game; chưa khẳng định hai lỗi sửa được là toàn bộ nguyên nhân triệu chứng người dùng.
