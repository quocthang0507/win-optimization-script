# Windows System Maintenance Tool

Windows System Maintenance Tool là bộ công cụ dọn dẹp, tối ưu và sửa lỗi Windows. Repo hiện có hai giao diện sử dụng:

- **WinUI app** trong `src/app`: giao diện hiện đại có dashboard, scan/preview, xác nhận theo mức rủi ro và report.
- **PowerShell CLI** trong `src/cli`: script console hiện có, phù hợp khi cần chạy nhanh bằng PowerShell.

## Tính năng chính

### WinUI app

- Dashboard hiển thị trạng thái hệ thống: Thông tin Windows version, trạng thái Administrator, Uptime, dung lượng trống ổ đĩa, Pending Reboot và các bản cập nhật WinGet khả dụng.
- Chế độ dọn dẹp (Cleanup) hỗ trợ Scan / Preview trước khi thực thi thực tế.
- Bộ phân tích dung lượng (Storage Analyzer) giống TreeSize: Quét thư mục/ổ đĩa bất kỳ trực quan, hiển thị các thư mục/file dung lượng lớn nhất, biểu đồ phân bổ loại file, và đề xuất dọn dẹp.
- Giao tiếp đa tiến trình (IPC) qua Windows Named Pipes (`WinOptimizationApp_Runner`) để đồng bộ trạng thái và gửi lệnh an toàn.
- Mô hình Runner-Module: Tiến trình Runner quản lý ứng dụng, có thể khởi động lại UI Module nếu gặp sự cố, đồng thời xử lý các tác vụ nền.
- Cảnh báo chạy dưới quyền Administrator (Admin Warning Banner) và hỗ trợ tự động yêu cầu nâng quyền (UAC elevation) khi thực hiện các chức năng phân tích/dọn dẹp hệ thống sâu.
- Huy hiệu mức độ rủi ro (Risk Badge) trực quan: `Safe`, `Medium`, `High`.
- Lưu báo cáo dọn dẹp chi tiết (JSON và Log) trong thư mục `logs/` ở thư mục gốc của repo.
- Startup Inventory cho phép xem thông tin các chương trình khởi động cùng Windows.
- Xem trước và cập nhật ứng dụng qua WinGet Update Preview.
- Hỗ trợ đa ngôn ngữ hoàn chỉnh (tiếng Anh và tiếng Việt) chuyển đổi tức thì trong Settings.

### PowerShell CLI

CLI hiện có các nhóm chức năng ổn định:

- Quick Cleanup: Các tệp tạm, bộ nhớ đệm trình duyệt, Thùng rác (Recycle Bin), clipboard.
- Deep Cleanup: Disk Cleanup chính chủ, cache lập trình viên (NuGet, NPM, pip, Yarn), Windows Update cache, Event Logs, Windows.old.
- Optimization: Tối ưu hóa ổ đĩa, tắt chế độ ngủ đông (hibernation).
- Repair: Quét và sửa lỗi hệ thống bằng DISM, SFC, khởi động lại Explorer.
- Software: Chạy nâng cấp ứng dụng qua `winget upgrade --all`, biên dịch script sang EXE.
- Network: Xóa bộ nhớ đệm DNS.
- Privacy: Xóa lịch sử PowerShell, danh sách file mở gần đây trên Start/Taskbar.
- Info: Uptime của hệ thống.

## Kiến trúc Runner-Module & IPC

Ứng dụng WinUI 3 áp dụng mô hình kiến trúc hai tiến trình:

1. **Runner Mode (Daemon)**: Khi chạy `WinOptimizationApp.exe` không có tham số, nó sẽ đóng vai trò là **Runner** (tiến trình quản lý chính). Runner sẽ:
   - Khởi tạo `IpcServer` và lắng nghe kết nối Named Pipe tại `\\.\pipe\WinOptimizationApp_Runner`.
   - Chạy các dịch vụ nền (Cleanup, SystemStatus, Winget, Startup).
   - Khởi chạy tiến trình UI con với đối số `--ui`.
   - Giám sát vòng đời tiến trình UI (tự động dọn dẹp IPC khi UI thoát).
2. **UI Mode (Module)**: Khi chạy với đối số `--ui`, ứng dụng sẽ khởi động giao diện người dùng WinUI 3. UI sẽ:
   - Kết nối tới Named Pipe của Runner bằng `IpcClient`.
   - Gửi yêu cầu qua định dạng tin nhắn JSON-serialized (`IpcMessage`) và hiển thị tiến trình, dữ liệu trả về từ Runner.

Sự tách biệt này đảm bảo nếu tiến trình UI gặp lỗi hoặc cần nâng cấp đặc quyền (UAC elevation), nó có thể kết nối lại liền mạch tới Runner nền mà không làm gián đoạn trạng thái hệ thống.

## Cấu trúc project

```text
docs/
  implementation_plan.md        # Kế hoạch chi tiết và trạng thái triển khai
src/
  app/
    WinOptimizationApp.csproj   # File dự án WinUI 3
    App.cs                      # Application class
    Program.cs                  # Điểm khởi đầu chính (phân luồng Runner / UI Mode)
    MainWindow.cs               # Cửa sổ chính và điều hướng Navigation
    Models/                     # Chứa các thực thể dữ liệu (Data models)
      AppSettings.cs
      AppTheme.cs
      AppWinUiStyle.cs
      DiskItem.cs
      DiskScanResult.cs
      ...
    Views/                      # Các trang giao diện người dùng WinUI 3
      BasePage.cs               # Lớp cơ sở chứa Admin Warning Banner dùng chung
      DashboardPage.cs
      MaintenancePage.cs
      StoragePage.cs            # Giao diện Storage Analyzer
      StartupPage.cs
      UpdatesPage.cs
      HistoryPage.cs
      SettingsPage.cs
    Services/                   # Các dịch vụ logic và giao tiếp IPC
      IpcClient.cs              # Client kết nối Named Pipe gửi yêu cầu từ UI
      IpcServer.cs              # Server Named Pipe xử lý yêu cầu ở Runner
      IpcMessages.cs            # Định nghĩa các gói tin IPC
      CleanupService.cs
      DiskAnalysisService.cs
      LocalizationService.cs    # Dịch vụ Việt hóa / Anh hóa toàn bộ UI
      ...
  cli/
    Utilities.ps1               # Script PowerShell tối ưu hệ thống
    Utilities.exe               # Executable biên dịch từ script
logs/
  maintenance-*.json            # Báo cáo dạng JSON
  maintenance-*.log             # Log chi tiết quá trình chạy
```

`src/app/Services` đóng vai trò là core engine cho WinUI app. Kế hoạch tiếp theo là tách phần này sang `src/core` thành một class library dùng chung cho WinUI, CLI và test.

## Yêu cầu hệ thống

- Windows 10 hoặc Windows 11.
- PowerShell 5.1 trở lên cho CLI.
- .NET SDK 10 trở lên để biên dịch WinUI app.
- Quyền Administrator (để thực hiện các tác vụ dọn dẹp hệ thống, DISM/SFC, hoặc quét ổ đĩa đầy đủ).

## Hướng dẫn chạy và biên dịch

### Chạy WinUI app

Từ thư mục gốc (root) của kho lưu trữ:

```powershell
dotnet restore .\src\app\WinOptimizationApp.csproj
dotnet run --project .\src\app\WinOptimizationApp.csproj
```

### Biên dịch WinUI app

```powershell
dotnet build .\src\app\WinOptimizationApp.csproj -c Release
```

### Chạy PowerShell CLI

Mở PowerShell với quyền Administrator, rồi chạy:

```powershell
.\src\cli\Utilities.ps1
```

Hoặc chạy file EXE trực tiếp nếu đã build:

```powershell
.\src\cli\Utilities.exe
```

## Hướng dẫn An toàn

- **Xem trước (Preview/Scan)**: Luôn sử dụng nút `Scan` hoặc `Preview` để xem trước dung lượng tệp tin và các thư mục bị ảnh hưởng trước khi thực hiện dọn dẹp thực tế.
- **Dọn dẹp lưu trữ**: Khi sử dụng Storage Analyzer, mọi thư mục/tệp tin được chọn xóa sẽ được đưa qua hộp xác nhận `Cleanup Review` trước khi thực sự bị di chuyển vào Thùng rác (Recycle Bin) hoặc xóa vĩnh viễn.
- **Trình duyệt**: Đóng tất cả trình duyệt trước khi dọn dẹp bộ nhớ đệm trình duyệt để tránh xung đột khoá tệp.
- **Điểm khôi phục (Restore Point)**: Đối với các tác vụ có mức rủi ro `High` (như xóa Event Logs hoặc Windows.old), hệ thống sẽ tạo một điểm khôi phục (System Restore Point) trước khi chạy.
- **Báo cáo**: Lịch sử dọn dẹp được ghi lại đầy đủ tại thư mục `logs/` giúp bạn có thể theo dõi và đối chiếu khi cần thiết.

## Quy trình phát hành và cập nhật

- Ứng dụng WinUI kiểm tra GitHub Releases khi khởi động thông qua `https://api.github.com/repos/quocthang0507/win-optimization-script/releases/latest`.
- Nếu có bản cập nhật mới, một hộp thoại trong ứng dụng sẽ hiện ra kèm theo tệp ZIP hoặc liên kết trang phát hành.
- Đẩy một tag có tên `vX.Y.Z` để kích hoạt luồng `.github/workflows/release-windows.yml`.
- Quy trình này sẽ chạy test, biên dịch ứng dụng độc lập `win-x64`, tạo file ZIP, tính toán mã băm SHA256, và tạo hoặc cập nhật GitHub release.
- Cấu hình chạy của VS Code `Release: Bump Version and Push Tag` sẽ thực thi script `scripts/Release.ps1`, giúp tự động cập nhật metadata phiên bản, chạy test, biên dịch, commit, gắn tag, và đẩy tag lên GitHub.
- Lưu ý: Bạn cần có một working tree sạch trước khi chạy script cập nhật phiên bản.

### Các bước phát hành chi tiết

1. Kiểm tra trạng thái thư mục làm việc.

```powershell
git status --short
```

Hãy commit hoặc stash các thay đổi không liên quan trước. Script phát hành sẽ tự động dừng nếu phát hiện working tree chưa sạch.

2. Chạy test và biên dịch trước khi chuẩn bị phát hành.

```powershell
dotnet test .\src\WinOptimizationApp.Tests\WinOptimizationApp.Tests.csproj
dotnet build .\src\app\WinOptimizationApp.csproj
```

3. Chọn phiên bản kế tiếp theo Semantic Versioning.

Nhập `X.Y.Z` (không có chữ `v` ở trước) cho script. Ví dụ:

- Patch: `0.2.1`
- Minor: `0.3.0`
- Major: `1.0.0`

4. Tạo commit phát hành và gắn tag ở máy cục bộ.

```powershell
.\scripts\Release.ps1 -Version 0.2.1
```

Thao tác này sẽ cập nhật `src/app/WinOptimizationApp.csproj`, chạy test, biên dịch local, tạo commit `Release v0.2.1` và tạo tag `v0.2.1`.

5. Đẩy commit phát hành và tag lên GitHub.

```powershell
git push origin HEAD
git push origin v0.2.1
```

Hoặc thực hiện gộp chung bước 4 và 5:

```powershell
.\scripts\Release.ps1 -Version 0.2.1 -Push
```

6. Đợi GitHub Actions chạy xong.

Mở:

```text
https://github.com/quocthang0507/win-optimization-script/actions
```

Workflow `Release Windows App` cần phải hoàn tất thành công.

7. Kiểm tra trên GitHub Release.

Mở:

```text
https://github.com/quocthang0507/win-optimization-script/releases
```

Xác nhận bản release bao gồm:

- `WinOptimizationApp-vX.Y.Z-win-x64.zip`
- `WinOptimizationApp-vX.Y.Z-win-x64.zip.sha256`

8. Xác minh mã băm (checksum) ở máy cục bộ sau khi tải ZIP về.

```powershell
Get-FileHash .\WinOptimizationApp-v0.2.1-win-x64.zip -Algorithm SHA256
Get-Content .\WinOptimizationApp-v0.2.1-win-x64.zip.sha256
```

Hai giá trị băm này phải khớp nhau hoàn toàn.

9. Kiểm tra tính năng phát hiện cập nhật trong app.

Chạy một phiên bản cũ của app. Khi khởi động, app sẽ kiểm tra GitHub Release và hiện hộp thoại cập nhật nếu tag trên GitHub mới hơn `InformationalVersion` hiện tại của app.

10. Phát hành thủ công (chỉ khi thực sự cần).

Biên dịch phát hành thủ công mà không cần tạo GitHub Release:

```powershell
dotnet publish .\src\app\WinOptimizationApp.csproj --configuration Release --runtime win-x64 --self-contained true
```

Thư mục đầu ra được lưu tại:

```text
src/app/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/
```

### Các lối tắt phát hành trong VS Code

- `Run and Debug` -> `Release: Bump Version and Push Tag`: Hiển thị hộp thoại nhập `X.Y.Z`, sau đó chạy toàn bộ script phát hành với cờ `-Push`.
- `Tasks: Run Task` -> `publish WinUI app release`: Biên dịch một bản Release ở máy cục bộ mà không cần tạo tag hoặc đẩy lên GitHub.

## Roadmap & Kế hoạch

Chi tiết kế hoạch triển khai và tiến trình hiện tại có thể xem tại [docs/implementation_plan.md](docs/implementation_plan.md).

## Lời cảm ơn (Acknowledgments)

- Xin gửi lời cảm ơn đến đội ngũ phát triển [Microsoft WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) và .NET.
- Cảm ơn cộng đồng mã nguồn mở và những người dùng đã đóng góp ý tưởng để cải thiện và hoàn thiện công cụ này.

---

Phát triển bởi [quoct](https://github.com/quoct)
