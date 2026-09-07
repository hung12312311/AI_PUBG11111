# AIOK

Phiên bản đầu tiên: **v1 (1.0.0)**.

## Chạy ứng dụng

Mở `bin/Build/CouldBeAimmyV2.exe`. Giữ nguyên toàn bộ thư mục `bin/Build` để có đủ thư viện, model và cấu hình. Tên executable được giữ để tương thích với các cấu hình hiện có.

## Biên dịch

Windows, .NET 8 SDK:

```powershell
dotnet restore Aimmy2.csproj
dotnet build Aimmy2.csproj -p:Platform=x64
```

Đầu ra duy nhất: `bin/Build`. TensorRT yêu cầu môi trường CUDA/TensorRT tương ứng.

## Nội dung bản đầu tiên

Theo yêu cầu lưu toàn bộ dự án, bản v1 bao gồm cả mã nguồn, bản chạy, model, config, `.vs`, `obj` và các bản lưu trong `scratch`. `scratch` chứa lịch sử phát triển và build cũ; bản chạy hiện tại ở `bin/Build`.

`AIOK-v1-files.csv` liệt kê đường dẫn, dung lượng và SHA-256 của các file trong bản chụp này (ngoại trừ chính danh sách và dữ liệu quản lý `.git`).

Bản này đã biên dịch; chưa được kiểm thử chạy trong lần phát hành này.

Các lần cập nhật tiếp theo tiếp tục trên nhánh `main`; tag `v1` giữ nguyên mốc phát hành đầu tiên.
