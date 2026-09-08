# Báo cáo cập nhật và Git — 08/09/2026

## Mốc lưu trữ

- Repository đích: https://github.com/hung12312311/AI_PUBG11111
- Nhánh gốc và nhánh cập nhật: main.
- Mốc main trước cập nhật: fb893fc4e398cb439bc3f6dfba802d8e3e5f8e24.
- Snapshot custom trước cập nhật: 409acd371beaa1654fb30ce7a8d8cdcf5eb8396e.
- Backup branch: backup/pre-upstream-update-20260908-122300.
- Backup tag: custom-before-upstream-20260908-122300.
- Thời điểm backup ghi trong tên: 08/09/2026 12:23. Đã xác minh branch và tag trên GitHub cùng trỏ tới snapshot trước khi push.
- Remote dùng để push: hung12312311. origin đang trỏ tới repository khác và không được dùng.

## Phạm vi

Đợt lưu này đưa các thay đổi custom đã thực hiện và kiểm tra lên Git. Nguồn upstream tham chiếu: LOCAL, C:/Users/sihun/Downloads/Aimmy2; inventory và diff trong reports/. Không tuyên bố toàn bộ semantic merge/master prompt đã hoàn tất. Các báo cáo riêng mô tả giới hạn kiểm chứng; không coi báo cáo diff là bằng chứng một thay đổi đã tích hợp.

Các thay đổi gồm capture/model pipeline, dashboard và hiệu suất, bản địa hóa Việt/Anh, lưu cài đặt cửa sổ/độ nhạy, recoil 5 phát và sửa trạng thái continuous recoil. Xem reports/CONTINUOUS_RECOIL_REVIEW.md, reports/INDEPENDENT_SENSITIVITY_PROFILES.md và reports/VI_GUI_COMPLETE.md.

## Kiểm tra

Release build gần nhất thành công: 0 lỗi, 250 cảnh báo. General regression qua các kiểm tra UI, 48 trường hợp tọa độ chuột so với bản gốc, 5 mức tap; continuous regression đọc đủ 24 giai đoạn lực từ cấu hình đã lưu. Không gửi input vào game trong các kiểm tra tự động.

NOT TESTED: toàn bộ ma trận GPU/model/hệ điều hành và hiệu quả bám mục tiêu trong game. WGC vẫn cần đánh giá thực tế. Driver ddxoft chưa hoạt động được trên máy thử. Không thay đổi bảo mật Windows để né lỗi driver.

## Dọn Git

Untrack build/cache/.vs/obj/scratch bằng git rm --cached, giữ file đang có trên ổ đĩa. Model, config runtime và DLL ddxoft đầu vào vẫn được theo dõi bằng ngoại lệ .gitignore. Backup driver cũ và build tạm không đưa vào commit mới; backup Git cũ được giữ nguyên. Không rewrite lịch sử nên dung lượng lịch sử cũ vẫn tồn tại.

## Quay lại bản cũ

Tạo checkout riêng để không đè thay đổi đang làm:

```powershell
git worktree add ../Aimmy-before-update custom-before-upstream-20260908-122300
```

Backup chứa bản trước cập nhật, gồm dữ liệu của snapshot cũ. Không force push, không xóa tag/branch backup.