# AIOK — bản cập nhật custom

Mã nguồn chính: https://github.com/hung12312311/AI_PUBG11111

## Build và chạy

Windows, .NET 8 SDK, cấu hình x64:

```powershell
dotnet restore Aimmy2.csproj
dotnet build Aimmy2.csproj -c Release -p:Platform=x64
```

Mở `bin/Build/CouldBeAimmyV2.exe`. Git giữ model và cấu hình đầu vào trong `bin/Build/bin`; thư viện NuGet và chương trình được tạo lại khi build. Giữ nguyên thư mục đầu ra khi chạy. TensorRT cần môi trường CUDA/TensorRT phù hợp; file engine phụ thuộc phần cứng và phiên bản runtime.

## Các thay đổi hiện tại

- Cập nhật capture, thông tin model, ONNX/TensorRT và bảng hiệu suất.
- Giao diện Việt/Anh, lưu kích thước cửa sổ và độ nhạy riêng theo capture → kích thước ảnh → model.
- Bắn từng viên có 5 mức; phát tiếp theo dùng mức 5. Sửa trạng thái ghì liên tục và lực bù con lăn.
- Bỏ build/cache khỏi Git; vẫn giữ dữ liệu runtime và các bản backup trong lịch sử.

Xem [báo cáo cập nhật](MERGE_REPORT.md), [tài liệu](Documentation.md) và các báo cáo cụ thể trong `reports/`.

## Kiểm chứng và giới hạn

Release gần nhất: 0 lỗi, 250 cảnh báo. Bộ kiểm tra UI, tọa độ chuột, độ nhạy, bắn từng viên và lực ghì liên tục đã chạy qua. Chưa xác nhận cảm giác WGC/ghì tâm trực tiếp trong game. Driver ddxoft vẫn có lỗi cài đặt trên máy đang thử; không coi là đã sửa. Chưa xác nhận toàn bộ hạng mục trong master prompt hoàn tất.

Bản v1 và backup trước cập nhật giữ nguyên; xem cách quay lại trong MERGE_REPORT.md.