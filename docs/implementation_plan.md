# Kế hoạch triển khai cải tiến Windows System Maintenance Tool

## Mục tiêu

Nâng cấp công cụ từ một menu PowerShell chạy tác vụ trực tiếp thành một tiện ích tối ưu Windows an toàn, minh bạch và dễ dùng hơn, tiệm cận trải nghiệm của các công cụ hiện đại như Microsoft PC Manager, Storage Sense, WinGet UI và Sysinternals.

Các nguyên tắc chính:

- Ưu tiên an toàn: luôn cho người dùng biết tác vụ sẽ làm gì trước khi chạy.
- Minh bạch: hiển thị dung lượng dự kiến, trạng thái, lỗi, file bị bỏ qua và báo cáo sau khi chạy.
- Có khả năng khôi phục: backup/restore point cho tác vụ rủi ro cao.
- UI rõ ràng: nhóm chức năng theo workflow, có badge mức rủi ro và kết quả dễ quét.
- Hỗ trợ đa ngôn ngữ tối thiểu tiếng Việt và tiếng Anh.
- Tôn trọng công cụ hệ thống chính chủ: dùng API/cmdlet hoặc CLI chuẩn khi có thể thay vì xóa thủ công.

## Phạm vi và cấu trúc hiện tại

Repo hiện đã được tách thành hai bề mặt sử dụng chính:

- CLI PowerShell hiện có nằm trong `src/cli/Utilities.ps1`.
- App WinUI 3 mới nằm trong `src/app`.
- Report/log runtime được ghi vào `logs/` ở root repo.
- Localization Việt/Anh hiện nằm trong `src/app/Services/LocalizationService.cs`.
- Kế hoạch triển khai và tài liệu kỹ thuật nằm trong `docs/`.

Cấu trúc nguồn hiện tại:

```text
docs/
  implementation_plan.md
src/
  app/
    App.cs
    Program.cs
    MainWindow.cs
    WinOptimizationApp.csproj
    app.manifest
    README.md
    Models/
      AppLanguage.cs
      CleanupTargetPreview.cs
      DashboardStatus.cs
      DiskItem.cs
      DiskScanOptions.cs
      DiskScanProgress.cs
      DiskScanResult.cs
      FileTypeSummary.cs
      MaintenanceTask.cs
      RiskLevel.cs
      StartupEntry.cs
      StorageCleanupCandidate.cs
      StorageCleanupMode.cs
      TaskPreview.cs
      TaskRunResult.cs
      WingetPackage.cs
    Services/
      CleanupService.cs
      CommandRunner.cs
      DiskAnalysisService.cs
      Formatters.cs
      LocalizationService.cs
      MaintenanceCatalog.cs
      MaintenanceExecutionService.cs
      PathService.cs
      ReportService.cs
      RestorePointService.cs
      StartupService.cs
      StorageCleanupService.cs
      SystemStatusService.cs
      WingetService.cs
  cli/
    Utilities.ps1
    Utilities.exe
logs/
  maintenance-*.json
  maintenance-*.log
```

Ghi chú:

- `src/app/bin/` và `src/app/obj/` là output build, không phải source chính.
- `src/app/Services/` đang đóng vai trò core engine MVP cho WinUI app.
- Khi logic ổn định hơn, có thể tách `Models/` và `Services/` sang một class library dùng chung cho WinUI, CLI mới và test.

CLI hiện tại trong `src/cli/Utilities.ps1` có các nhóm:

- Quick Cleanup
- Deep Cleanup
- Optimization
- Repair
- Software
- Network
- Privacy
- Info

Các cải tiến dưới đây được thiết kế để triển khai dần, vẫn giữ được phiên bản console hiện tại trước khi nâng lên GUI.

## Phase 1: Nền tảng an toàn và báo cáo

### 1.1 Thêm mô hình task metadata

Mục tiêu: thay menu item dạng label/action hiện tại bằng cấu trúc giàu thông tin hơn.

Thông tin nên có cho mỗi task:

- `Id`
- `Group`
- `Label`
- `Description`
- `RiskLevel`: `Safe`, `Medium`, `High`
- `RequiresAdmin`
- `RequiresConfirmation`
- `CanPreview`
- `CanRollback`
- `EstimatedImpact`
- `Action`
- `PreviewAction`

Tiêu chí nghiệm thu:

- Menu vẫn chạy được như hiện tại.
- Mỗi tác vụ hiển thị được mức rủi ro.
- Tác vụ rủi ro cao yêu cầu xác nhận trước khi chạy.

### 1.2 Thêm chế độ Scan / Preview

Mục tiêu: trước khi xóa hoặc tối ưu, app có thể quét và báo trước tác động.

Áp dụng trước cho:

- Temporary files
- Browser cache
- Developer caches
- Windows Update cache
- Recycle Bin
- Windows.old

Kết quả preview nên hiển thị:

- Đường dẫn hoặc nguồn dữ liệu
- Dung lượng ước tính
- Số file
- Có thể xóa hay không
- Cảnh báo nếu app liên quan đang mở

Tiêu chí nghiệm thu:

- Người dùng có thể chọn `Scan only` mà không thay đổi hệ thống.
- Tác vụ cleanup hiển thị dung lượng dự kiến trước khi chạy.
- Không có thao tác xóa nào diễn ra trong bước scan.

### 1.3 Thêm logging và report

Mục tiêu: mọi lần chạy có lịch sử rõ ràng.

Đề xuất thư mục:

```text
logs/
  maintenance-YYYYMMDD-HHMMSS.log
  maintenance-YYYYMMDD-HHMMSS.json
```

Thông tin cần log:

- Thời gian bắt đầu/kết thúc
- Windows version/build
- User/admin state
- Task đã chạy
- Dung lượng giải phóng
- Số file xóa thành công
- Số file bỏ qua
- Exception/error message
- Exit code của command bên ngoài

Tiêu chí nghiệm thu:

- Sau mỗi lần chạy task có file log.
- Lỗi không bị nuốt im lặng nếu ảnh hưởng kết quả.
- Report JSON có thể đọc lại để hiển thị trong UI tương lai.

### 1.4 Thêm restore point cho tác vụ rủi ro cao

Mục tiêu: giảm rủi ro khi chạy tác vụ thay đổi sâu.

Áp dụng cho:

- Clear Windows Update cache
- Clear Windows Event Logs
- Remove Windows.old
- Disable Hibernation
- Repair DISM/SFC nếu chạy theo preset repair

Tiêu chí nghiệm thu:

- App kiểm tra System Restore có khả dụng không.
- Nếu tạo restore point thất bại, người dùng được cảnh báo và phải xác nhận tiếp tục.
- Log ghi rõ restore point có được tạo hay không.

## Phase 2: Cải thiện chức năng cleanup

### 2.1 Chuẩn hóa cleanup engine

Mục tiêu: dùng một engine chung cho scan/xóa thay vì mỗi hàm tự xử lý riêng.

Các helper nên có:

- `Get-DirectoryCleanupPreview`
- `Invoke-DirectoryCleanup`
- `Format-Bytes`
- `Test-PathSafeForCleanup`
- `Write-TaskResult`

Tiêu chí nghiệm thu:

- Temp, browser cache, Windows Update cache và developer cache dùng cùng flow.
- Có thống kê file xóa, file lỗi, dung lượng giải phóng.
- Không xóa nếu path rỗng, không tồn tại, hoặc trỏ sai ngoài danh sách cho phép.

### 2.2 Thay cleanup developer cache bằng command chính chủ

Mục tiêu: tránh xóa thô các cache/package store có thể gây tác dụng phụ.

Đề xuất:

- NuGet: `dotnet nuget locals all --clear` nếu có `dotnet`
- pip: `pip cache purge` nếu có `pip`
- npm: `npm cache clean --force` nếu có `npm`
- yarn: `yarn cache clean` nếu có `yarn`

Fallback:

- Chỉ xóa thư mục thủ công khi command chính chủ không có và người dùng xác nhận.

Tiêu chí nghiệm thu:

- App phát hiện tool nào có sẵn.
- App preview command sẽ chạy.
- Kết quả command được log.

### 2.3 Mở rộng browser cache

Mục tiêu: hỗ trợ nhiều trình duyệt và tránh xóa khi browser đang chạy.

Trình duyệt đề xuất:

- Microsoft Edge
- Google Chrome
- Firefox
- Brave
- Opera

Tiêu chí nghiệm thu:

- App phát hiện profile browser.
- Nếu browser đang chạy, app cảnh báo và đề xuất đóng browser.
- Người dùng có thể bỏ chọn từng browser/profile.

### 2.4 Tích hợp Storage Sense

Mục tiêu: bổ sung hướng cleanup bền vững bằng công cụ Windows chính chủ.

Tính năng:

- Hiển thị trạng thái Storage Sense.
- Mở trang Settings liên quan.
- Tùy chọn bật Storage Sense nếu người dùng xác nhận.
- Không tự thay đổi chính sách xóa Downloads nếu chưa hỏi rõ.

Tiêu chí nghiệm thu:

- Có mục `Storage Sense` trong nhóm Cleanup hoặc Optimization.
- App phân biệt rõ giữa mở Settings và thay đổi cấu hình.

## Phase 3: Cải thiện Software, Startup và Repair

### 3.1 Nâng cấp WinGet update flow

Mục tiêu: chuyển từ chạy `winget upgrade --all` trực tiếp sang flow có preview.

Flow đề xuất:

1. Kiểm tra `winget`.
2. Chạy `winget upgrade` để lấy danh sách.
3. Hiển thị package, current version, available version.
4. Cho chọn update tất cả hoặc từng app.
5. Chạy upgrade và log kết quả.

Tùy chọn:

- `--silent`
- `--include-unknown`
- ignore/pin package
- export danh sách app đã cài

Tiêu chí nghiệm thu:

- Không tự update tất cả nếu người dùng chưa xác nhận.
- Có danh sách package trước khi update.
- Log từng package thành công/thất bại.

### 3.2 Thêm Startup Manager cơ bản

Mục tiêu: có tính năng quản lý khởi động kiểu nhẹ, không thay thế Autoruns nhưng đủ an toàn.

Nguồn dữ liệu:

- Startup registry keys
- Startup folder
- Scheduled tasks phổ biến
- Services bên thứ ba có startup automatic

Thông tin hiển thị:

- Name
- Publisher/signature nếu lấy được
- Path
- Startup source
- Enabled/Disabled
- Risk hint

Thao tác:

- Disable
- Enable
- Open file location
- Export list

Tiêu chí nghiệm thu:

- Không xóa entry startup, chỉ disable/enable.
- Trước khi disable service/task có xác nhận.
- Có export backup trước khi thay đổi.

### 3.3 Tổ chức lại Repair Center

Mục tiêu: biến các lệnh repair thành quy trình rõ ràng.

Các mục:

- DISM CheckHealth
- DISM ScanHealth
- DISM RestoreHealth
- SFC Scan
- Windows Update reset
- Network repair: DNS flush, Winsock reset, IP release/renew
- Pending reboot check

Tiêu chí nghiệm thu:

- Người dùng có thể chạy từng bước hoặc preset `Full Repair`.
- Các lệnh dài có progress/status rõ ràng.
- Kết quả được lưu vào report.

## Phase 4: UI console nâng cao

### 4.1 Dashboard console

Mục tiêu: khi mở app, người dùng thấy trạng thái máy thay vì chỉ menu số.

Thông tin đề xuất:

- Windows version/build
- Admin status
- Uptime
- Free disk space
- Pending reboot
- WinGet updates available
- Restore point status
- Last cleanup summary

Tiêu chí nghiệm thu:

- Dashboard tải nhanh.
- Nếu một phép kiểm tra lỗi, dashboard vẫn hiển thị phần còn lại.

### 4.2 Menu chọn bằng phím và bộ lọc

Mục tiêu: cải thiện UX trong console.

Tính năng:

- Chọn bằng số như hiện tại.
- Tìm kiếm task theo tên.
- Lọc theo group.
- Hiển thị badge rủi ro.
- Có shortcut cho preset.

Tiêu chí nghiệm thu:

- Người dùng cũ vẫn có thể dùng menu số.
- Người dùng mới dễ hiểu tác vụ nào an toàn/rủi ro.

### 4.3 Presets

Mục tiêu: gom các tác vụ thường dùng thành workflow.

Preset đề xuất:

- `Safe Cleanup`: temp, recycle bin, DNS cache, clipboard
- `Deep Cleanup`: safe cleanup, browser cache, Windows Update cache, developer cache
- `Repair Windows`: DISM, SFC, pending reboot check
- `Dev Workstation Cleanup`: developer cache, temp, browser cache
- `Privacy Cleanup`: PowerShell history, recent list, clipboard

Tiêu chí nghiệm thu:

- Preset luôn hiển thị danh sách task trước khi chạy.
- Người dùng có thể bỏ chọn task trong preset.
- Tác vụ rủi ro cao vẫn yêu cầu xác nhận riêng.

## Phase 5: GUI hiện đại

Phase này đã có MVP ban đầu trong `src/app` bằng WinUI 3 / Windows App SDK. Mục tiêu tiếp theo là làm chắc engine, giảm logic trong UI code-behind và chuẩn bị tách phần core dùng chung.

### 5.1 Tách core engine khỏi UI

Mục tiêu: để WinUI app, CLI và test dùng chung logic.

Cấu trúc hiện tại:

```text
src/
  app/
    WinOptimizationApp.csproj
    App.cs
    Program.cs
    MainWindow.cs
    app.manifest
    Models/
    Services/
  cli/
    Utilities.ps1
    Utilities.exe
logs/
docs/
  implementation_plan.md
```

Cấu trúc mục tiêu sau refactor:

```text
src/
  app/
    WinOptimizationApp.csproj
    App.cs
    Program.cs
    MainWindow.cs
    Views/
      ShellWindow.cs
      DashboardView.cs
      MaintenanceView.cs
      StorageAnalyzerView.cs
      CleanupReviewView.cs
    ViewModels/
      ShellViewModel.cs
      DashboardViewModel.cs
      MaintenanceViewModel.cs
      StorageAnalyzerViewModel.cs
      CleanupReviewViewModel.cs
  core/
    WinOptimization.Core.csproj
    Models/
      DiskItem.cs
      DiskScanOptions.cs
      DiskScanResult.cs
      StorageCleanupCandidate.cs
    Services/
      CleanupService.cs
      CommandRunner.cs
      DiskAnalysisService.cs
      MaintenanceCatalog.cs
      MaintenanceExecutionService.cs
      ReportService.cs
      RestorePointService.cs
      StartupService.cs
      SystemStatusService.cs
      WingetService.cs
  cli/
    Utilities.ps1
    Utilities.exe
tests/
  WinOptimization.Core.Tests/
docs/
logs/
```

Tiêu chí nghiệm thu:

- Logic cleanup/repair/update không phụ thuộc `Write-Host` hoặc WinUI controls.
- Core trả object kết quả như `TaskPreview`, `TaskRunResult`, `DashboardStatus`.
- WinUI app tham chiếu `src/core` sau khi refactor.
- CLI hiện tại trong `src/cli/Utilities.ps1` vẫn chạy được trong giai đoạn chuyển tiếp.
- Có test cho core mà không cần khởi động WinUI.

### 5.2 Thiết kế GUI theo Windows 11

Hướng đã chọn:

- WinUI 3 / Windows App SDK.
- Target hiện tại: `net10.0-windows10.0.19041.0`.
- App hiện chạy unpackaged với `WindowsPackageType=None`.
- CLI fallback trỏ đến `src/cli/Utilities.ps1` thông qua `PathService`.

Navigation:

- Dashboard
- Cleanup
- Storage Analyzer
- Startup
- Updates
- Repair
- History
- Settings

Ghi chú: Privacy hiện được gom vào Cleanup page trong MVP, nhưng vẫn có thể tách thành page riêng khi số tác vụ privacy tăng.

Form chính cần hỗ trợ chuyển đổi nhanh giữa nhóm chức năng ban đầu và chức năng phân tích dung lượng mới:

- Dùng `NavigationView` làm shell chính, trong đó `Dashboard`, `Cleanup`, `Repair`, `Updates`, `Startup`, `History`, `Settings` là các module maintenance hiện tại.
- Thêm module `Storage Analyzer` ngang cấp với `Cleanup`, không giấu trong Settings.
- Trên Dashboard có quick action `Analyze Storage` để mở thẳng form phân tích ổ đĩa.
- Trong `Storage Analyzer`, có breadcrumb hoặc path bar để chuyển thư mục đang phân tích mà không rời form.
- Khi người dùng chọn file/thư mục để dọn, chuyển sang `Cleanup Review` dạng panel/modal xác nhận, sau đó quay lại kết quả scan để refresh dung lượng.

Nguyên tắc UI:

- Sidebar trái, nội dung chính bên phải.
- Badge rủi ro và trạng thái task rõ ràng.
- Toggle/checkbox cho lựa chọn.
- Progress theo từng task.
- Không dùng layout kiểu landing page.
- Dark/light mode theo system.
- Có keyboard navigation và focus state.

Tiêu chí nghiệm thu:

- Người dùng có thể scan, chạy task và xem report trong GUI.
- Không có task rủi ro cao nào chạy chỉ với một click.
- UI không phụ thuộc vào kích thước cửa sổ cố định.
- Task cần admin được chặn hoặc cảnh báo rõ khi app chưa chạy elevated.
- `History` đọc report từ `logs/`.

### 5.3 Storage Analyzer giống TreeSize

Mục tiêu: bổ sung một form phân tích không gian lưu trữ của ổ đĩa theo phong cách các phần mềm thương mại như TreeSize: quét nhanh, hiển thị cây thư mục theo dung lượng, tìm file lớn và hỗ trợ dọn dẹp an toàn.

Không sao chép giao diện hay thương hiệu của TreeSize. Chỉ học các nguyên tắc UX đã quen thuộc: cây thư mục có cột dung lượng, biểu đồ phân bổ, bộ lọc mạnh, thao tác cleanup có xác nhận và khả năng quay lại kết quả scan.

#### Luồng sử dụng chính

1. Người dùng mở `Storage Analyzer` từ sidebar hoặc quick action trên Dashboard.
2. Chọn ổ đĩa hoặc thư mục gốc để phân tích.
3. App quét dung lượng và cập nhật progress theo thư mục đang xử lý.
4. Kết quả hiển thị theo nhiều chế độ: tree table, treemap, file types, largest files.
5. Người dùng chọn file/thư mục hoặc cleanup candidates.
6. App mở `Cleanup Review` để xác nhận trước khi xóa, recycle hoặc mở vị trí file.
7. Sau khi cleanup, app refresh lại node liên quan và lưu report.

#### Layout đề xuất

```text
Storage Analyzer
├─ Top command bar
│  ├─ Drive selector
│  ├─ Scan / Stop / Refresh
│  ├─ Search
│  ├─ Filter
│  └─ Cleanup Review
├─ Summary strip
│  ├─ Total scanned
│  ├─ Free space
│  ├─ Largest folder
│  ├─ Candidate cleanup size
│  └─ Scan duration
├─ Main split view
│  ├─ Left: folder tree/table
│  └─ Right: details tabs
│     ├─ Treemap
│     ├─ Largest files
│     ├─ File types
│     ├─ Age
│     └─ Cleanup candidates
└─ Bottom status bar
   ├─ Current path
   ├─ Items scanned
   ├─ Errors/skipped
   └─ Last report
```

#### Tree table

Cột nên có:

- Name
- Size
- Allocated
- Percent of parent
- File count
- Folder count
- Last modified
- Owner nếu lấy được không quá tốn thời gian
- Attributes/status

Tương tác:

- Sort theo mọi cột.
- Expand/collapse lazy-loading.
- Double click để drill down.
- Right click context menu: open, open in Explorer, copy path, rescan, add to cleanup review.
- Breadcrumb path bar ở đầu để quay lên nhanh.
- Keyboard navigation và focus state rõ ràng.

#### Treemap và biểu đồ

Yêu cầu UI:

- Treemap hiển thị tỷ lệ dung lượng theo thư mục/file lớn.
- Màu theo loại file hoặc theo folder depth, tránh palette một màu.
- Hover tooltip: name, path, size, percent.
- Click vào ô treemap đồng bộ selection với tree table.
- Không dùng biểu đồ trang trí; mọi visualization phải phục vụ quyết định cleanup.

#### Largest files

Tính năng:

- Danh sách top file lớn nhất trong phạm vi scan.
- Filter theo phần mở rộng, kích thước tối thiểu, ngày sửa đổi.
- Group theo loại: video, archive, installer, disk image, logs, developer artifacts.
- Hành động an toàn: open location, copy path, add to cleanup review.

Không tự động xóa file người dùng. Mọi thao tác xóa phải qua `Cleanup Review`.

#### File types

Tính năng:

- Bảng tổng hợp extension: `.zip`, `.iso`, `.mp4`, `.log`, `.tmp`, `.msi`, `.dmp`.
- Hiển thị total size, count, largest item, last modified range.
- Cho phép click extension để lọc tree/largest files.

#### Cleanup candidates

Nhóm candidate nên có:

- Recycle Bin.
- Windows temp.
- User temp.
- Browser cache.
- Windows Update cache.
- Old logs.
- Crash dumps.
- Installer leftovers trong Downloads.
- Developer caches.

Mỗi candidate cần có:

- Risk level.
- Estimated size.
- Source path.
- Reason.
- Requires admin.
- Can move to Recycle Bin.
- Can permanently delete.
- Exclusions.

Tiêu chí an toàn:

- Mặc định ưu tiên move to Recycle Bin cho file người dùng.
- Permanent delete chỉ dùng cho cache/temp rõ nguồn gốc và vẫn cần xác nhận.
- Không dọn `Downloads`, `Documents`, `Pictures`, project folder hoặc source code folder theo rule tự động.
- Có dry-run/preview trước khi cleanup.
- Có report sau cleanup.

#### Scan engine

Yêu cầu kỹ thuật:

- Quét bất đồng bộ, có cancellation.
- Không block UI thread.
- Có progress theo số item, dung lượng đã cộng dồn và path hiện tại.
- Bỏ qua junction/symlink theo mặc định để tránh vòng lặp.
- Có tùy chọn include hidden/system files.
- Có danh sách skipped/error paths.
- Dùng lazy loading cho tree lớn.
- Cache kết quả scan tạm thời để chuyển tab không phải quét lại.

Model đề xuất:

```text
DiskScanOptions
  RootPath
  IncludeHidden
  IncludeSystem
  FollowReparsePoints
  MinimumFileSize
  ExcludedPaths

DiskItem
  Name
  FullPath
  IsDirectory
  Size
  AllocatedSize
  PercentOfParent
  FileCount
  FolderCount
  LastModified
  Extension
  Children
  ScanStatus

DiskScanResult
  Root
  StartedAt
  FinishedAt
  TotalBytes
  FileCount
  FolderCount
  SkippedCount
  Errors

StorageCleanupCandidate
  Id
  Label
  SourcePath
  EstimatedBytes
  RiskLevel
  CleanupMode
  Reason
```

Service đề xuất:

- `DiskAnalysisService`: scan folder/drive, build tree, aggregate size.
- `DiskItemIndexService`: index extension, largest files, age groups.
- `StorageCleanupCandidateService`: map scan result sang cleanup candidates.
- `StorageCleanupService`: move to Recycle Bin/delete sau khi xác nhận.
- `DiskScanCacheService`: giữ kết quả scan gần nhất.

#### UI/UX chuẩn thương mại

- Màn hình đầu tiên của `Storage Analyzer` là công cụ phân tích thật, không phải landing page.
- Có trạng thái empty/loading/error rõ ràng.
- Scan button có trạng thái `Scan`, `Stop`, `Refresh`.
- Không làm người dùng mất selection khi refresh một node.
- Các số dung lượng phải format nhất quán: KB/MB/GB/TB.
- Cảnh báo rõ ràng khi cần Administrator.
- Hành động nguy hiểm dùng dialog xác nhận có danh sách item và dung lượng.
- Không dùng text dài để giải thích UI; dùng label ngắn, tooltip và trạng thái inline.
- Bảng dữ liệu ưu tiên density vừa phải, dễ scan, không dùng card cho từng file.
- Layout responsive: vẫn dùng được ở width nhỏ bằng cách collapse details panel.

Tiêu chí nghiệm thu:

- Người dùng chọn ổ C: hoặc một thư mục bất kỳ và xem được cây dung lượng.
- Scan có thể hủy giữa chừng.
- Tree table, largest files và file types lấy cùng một kết quả scan.
- Click trong treemap đồng bộ với selection trong tree.
- Có cleanup review trước khi xóa/move to Recycle Bin.
- Không có thao tác xóa nào chạy trực tiếp từ tree hoặc treemap.
- Report cleanup được lưu vào `logs/`.

## Phase 6: Chất lượng, test và phát hành

### 6.1 Test tự động

Mục tiêu: giảm nguy cơ lỗi khi thao tác với file hệ thống.

Test đề xuất:

- Unit test cho `Format-Bytes`
- Unit test cho path validation
- Unit test cho scan không xóa file
- Unit test cho `DiskAnalysisService`: aggregate size, skipped paths, symlink/junction handling.
- Unit test cho `DiskItemIndexService`: largest files, extension grouping, age grouping.
- Unit test cho cleanup review: không xóa nếu chưa confirm.
- Mock command external cho WinGet/dev cache
- Test parse report JSON

Framework:

- xUnit, MSTest hoặc NUnit cho `src/core` C# sau khi tách core.
- Pester cho `src/cli/Utilities.ps1` nếu tiếp tục duy trì CLI PowerShell.

Tiêu chí nghiệm thu:

- Có test cho cleanup engine trước khi mở rộng chức năng.
- Test có thể chạy không cần admin cho phần logic thuần.
- Test không cần khởi động WinUI app.

### 6.2 Cải thiện README và tài liệu

Mục tiêu: repo nhìn chuyên nghiệp và dễ tin cậy hơn.

Việc cần làm:

- Sửa lỗi encoding mojibake trong `README.md`.
- Thêm screenshot hoặc GIF console/GUI.
- Thêm bảng tính năng.
- Thêm phần safety model.
- Thêm hướng dẫn build EXE.
- Thêm changelog.

Tiêu chí nghiệm thu:

- README hiển thị tiếng Việt đúng dấu.
- Có cảnh báo rõ cho tác vụ rủi ro cao.
- Người dùng biết nên chạy `Scan` trước khi cleanup.

### 6.3 Packaging và release

Mục tiêu: phát hành dễ dùng hơn.

Đề xuất:

- Script `.ps1` cho người dùng kỹ thuật.
- EXE signed nếu có thể.
- Release artifact trên GitHub.
- Hash SHA256 cho file tải về.
- Version trong app và report.

Tiêu chí nghiệm thu:

- Người dùng biết version đang chạy.
- Release có checksum.
- Build process có thể lặp lại.

## Thứ tự triển khai khuyến nghị

1. Sửa README encoding và thêm cảnh báo safety.
2. Hoàn thiện metadata/risk catalog trong `src/app/Services/MaintenanceCatalog.cs`.
3. Hoàn thiện logging/report JSON trong `src/app/Services/ReportService.cs`.
4. Mở rộng scan/preview cleanup trong `src/app/Services/CleanupService.cs`.
5. Chuẩn hóa confirmation theo risk level trong WinUI.
6. Tách `src/app/Models` và `src/app/Services` sang `src/core`.
7. Nâng cấp developer cache và browser cache.
8. Nâng cấp WinGet preview flow.
9. Tách UI `MainWindow.cs` thành `Views/` và `ViewModels/`.
10. Tạo shell navigation mới để chuyển nhanh giữa Maintenance và Storage Analyzer.
11. Thêm `StorageAnalyzerView` và `StorageAnalyzerViewModel`.
12. Thêm `DiskAnalysisService` với scan async/cancel/progress.
13. Thêm tree table, largest files, file types và treemap cho Storage Analyzer.
14. Thêm `Cleanup Review` cho file/thư mục/candidate từ Storage Analyzer.
15. Thêm presets.
16. Mở rộng Startup Manager từ read-only sang enable/disable có backup.
17. Thêm test cho `src/core`.
18. Cập nhật packaging/publish cho WinUI app.
19. Thêm release workflow.

## Định nghĩa mức rủi ro

| Mức | Ý nghĩa | Ví dụ |
| --- | --- | --- |
| Safe | Ít khả năng ảnh hưởng hệ thống, có thể chạy thường xuyên | Temp files, DNS cache, clipboard |
| Medium | Có thể làm mất cache/session hoặc cần tải lại dữ liệu | Browser cache, developer cache, Windows Update cache |
| High | Có thể ảnh hưởng khả năng audit, khôi phục, boot hoặc hành vi hệ thống | Event Logs, Windows.old, hibernation, service reset |

## Checklist MVP nâng cấp

- [ ] Menu item có `RiskLevel` và `Description`.
- [ ] Có chế độ scan cho ít nhất temp files, browser cache và recycle bin.
- [ ] Có log text và report JSON cho mỗi task.
- [ ] Tác vụ High risk có xác nhận riêng.
- [ ] README hiển thị tiếng Việt đúng encoding.
- [ ] `winget upgrade` có preview trước khi chạy `--all`.
- [ ] Có summary cuối phiên: dung lượng giải phóng, số task chạy, cảnh báo.
- [ ] Form chính có navigation rõ ràng giữa Maintenance và Storage Analyzer.
- [ ] Storage Analyzer quét được ổ đĩa hoặc thư mục bất kỳ.
- [ ] Storage Analyzer có tree table với size, percent, file count và last modified.
- [ ] Có largest files và file types dùng chung kết quả scan.
- [ ] Có treemap hoặc visualization tỷ lệ dung lượng.
- [ ] Có cleanup review trước khi xóa/move to Recycle Bin từ Storage Analyzer.
- [ ] Scan storage có cancel/progress và không block UI.

## Rủi ro kỹ thuật cần lưu ý

- Một số thao tác cần Administrator và có thể thất bại im lặng nếu dùng `SilentlyContinue` quá rộng.
- Xóa cache khi app đang chạy có thể không giải phóng đủ dung lượng hoặc gây lỗi file lock.
- Restore point có thể bị tắt trên máy người dùng.
- `cleanmgr` là công cụ cũ, nên tránh phụ thuộc hoàn toàn vào nó cho chiến lược dài hạn.
- `winget` output có thể thay đổi theo phiên bản/ngôn ngữ hệ thống, cần parse cẩn thận hoặc ưu tiên output có cấu trúc nếu có.
- GUI nên dùng core engine trả object, không parse lại text output từ console.
- Quét toàn ổ đĩa có thể rất lâu; cần cancellation, progress thật và lazy loading.
- Junction/symlink/reparse point có thể tạo vòng lặp hoặc tính trùng dung lượng; mặc định không follow.
- Một số thư mục hệ thống bị từ chối truy cập; cần hiển thị skipped count thay vì coi là lỗi toàn bộ scan.
- Treemap với hàng trăm nghìn file có thể gây chậm UI; cần giới hạn node render và gom nhóm file nhỏ.
- Cleanup file người dùng có rủi ro cao hơn cleanup cache; mặc định dùng Recycle Bin và luôn qua review.

## Kết quả mong muốn

Sau khi hoàn thành roadmap, app sẽ có trải nghiệm:

1. Người dùng mở app và thấy dashboard tình trạng máy.
2. Người dùng chuyển nhanh giữa Maintenance và Storage Analyzer từ sidebar hoặc quick action.
3. Người dùng bấm scan để xem có thể dọn gì.
4. App hiển thị dung lượng dự kiến và mức rủi ro.
5. Người dùng chọn task hoặc preset.
6. Người dùng mở Storage Analyzer để xem cây dung lượng, file lớn, loại file và candidate cleanup.
7. App đưa mọi thao tác xóa từ Storage Analyzer qua Cleanup Review.
8. App tạo backup/restore point nếu cần.
9. App chạy tác vụ, hiển thị tiến trình rõ ràng.
10. App lưu report và hiển thị kết quả cuối cùng.
