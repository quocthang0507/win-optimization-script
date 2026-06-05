# Windows System Maintenance Tool

Windows System Maintenance Tool là bộ công cụ dọn dẹp, tối ưu và sửa lỗi Windows. Repo hiện có hai giao diện sử dụng:

- **WinUI app** trong `src/app`: giao diện hiện đại có dashboard, scan/preview, xác nhận theo mức rủi ro và report.
- **PowerShell CLI** trong `src/cli`: script console hiện có, phù hợp khi cần chạy nhanh bằng PowerShell.

## Tính năng chính

### WinUI app

- Dashboard hiển thị trạng thái máy: Windows version, quyền admin, uptime, dung lượng ổ hệ thống, pending reboot và WinGet.
- Cleanup có scan/preview trước khi chạy.
- Storage Analyzer quét ổ đĩa hoặc thư mục, hiển thị thư mục lớn, file lớn, loại file và cleanup candidates.
- Badge mức rủi ro: `Safe`, `Medium`, `High`.
- Chặn hoặc cảnh báo các tác vụ cần Administrator.
- Report sau khi chạy task, lưu vào `logs/` ở root repo.
- Startup inventory dạng read-only.
- WinGet update preview.
- Hỗ trợ giao diện tiếng Việt và tiếng Anh, có thể đổi trong Settings.
- Settings có lối mở Storage Sense và chạy lại CLI ở `src/cli/Utilities.ps1`.

### PowerShell CLI

CLI hiện có các nhóm chức năng:

- Quick Cleanup: temp files, browser cache, Recycle Bin, clipboard.
- Deep Cleanup: Disk Cleanup, developer caches, Windows Update cache, Event Logs, Windows.old.
- Optimization: optimize drives, disable hibernation.
- Repair: DISM, SFC, restart Explorer.
- Software: `winget upgrade --all`, compile script sang EXE.
- Network: flush DNS.
- Privacy: PowerShell history, Start/Taskbar recent list.
- Info: uptime.

## Cấu trúc project

```text
docs/
  implementation_plan.md
src/
  app/
    WinOptimizationApp.csproj
    App.cs
    Program.cs
    MainWindow.cs
    Models/
    Services/
  cli/
    Utilities.ps1
    Utilities.exe
logs/
  maintenance-*.json
  maintenance-*.log
```

`src/app/Services` đang là core engine MVP cho WinUI app. Kế hoạch tiếp theo là tách phần này sang `src/core` để WinUI, CLI và test dùng chung.

## Yêu cầu hệ thống

- Windows 10 hoặc Windows 11.
- PowerShell 5.1 trở lên cho CLI.
- .NET SDK 10 trở lên để build WinUI app.
- Một số tác vụ cần chạy app hoặc PowerShell dưới quyền Administrator.

## Chạy WinUI app

Từ root repo:

```powershell
dotnet restore .\src\app\WinOptimizationApp.csproj
dotnet run --project .\src\app\WinOptimizationApp.csproj
```

Build:

```powershell
dotnet build .\src\app\WinOptimizationApp.csproj
```

## Chạy PowerShell CLI

Mở PowerShell với quyền Administrator, rồi chạy:

```powershell
.\src\cli\Utilities.ps1
```

Hoặc chạy EXE nếu đã build:

```powershell
.\src\cli\Utilities.exe
```

## An toàn khi sử dụng

- Luôn ưu tiên `Scan` hoặc `Preview` trước khi cleanup.
- Trong Storage Analyzer, mọi file/thư mục được chọn để dọn đều đi qua `Cleanup Review`.
- Đóng trình duyệt trước khi dọn browser cache.
- Với task `High`, nên tạo Restore Point trước khi chạy.
- Không chạy các tác vụ repair/optimization khi máy đang cập nhật Windows.
- Report được lưu trong `logs/` để kiểm tra lại kết quả và lỗi.

## Roadmap

Xem kế hoạch chi tiết tại [docs/implementation_plan.md](docs/implementation_plan.md).

Các hướng ưu tiên:

- Tách core engine sang `src/core`.
- Thêm test cho cleanup, disk scan, path validation, WinGet parser và report JSON.
- Tách UI WinUI thành `Views/` và `ViewModels/`.
- Nâng cấp visualization của Storage Analyzer thành treemap/canvas virtualized.
- Mở rộng Startup Manager có backup trước khi enable/disable.
- Hoàn thiện packaging/publish cho WinUI app.

## Disclaimer

Công cụ này có thể thực hiện thay đổi sâu vào hệ thống. Hãy đọc mô tả và mức rủi ro trước khi chạy task. Tác giả không chịu trách nhiệm cho mất dữ liệu hoặc lỗi hệ thống phát sinh từ việc sử dụng sai cách.

---

Phát triển bởi [quoct](https://github.com/quoct)
