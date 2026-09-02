# Kiểm thử UI/UX bằng computer-use — 2026-09-02

## Phạm vi và môi trường

- Kiểm thử trực tiếp bản Debug WinUI hiện có; About hiển thị phiên bản 0.4.2. Mã nguồn đối chiếu tại commit `3211aeb`.
- Tài khoản không nâng quyền; cửa sổ mặc định khoảng 1215 × 576 và cửa sổ phóng to khoảng 2560 × 1392 theo ảnh chụp của công cụ.
- Kiểm tra bằng chuột, cuộn, nhập từ khóa, phím Esc, ảnh chụp và cây UI Automation. Đã thử English/Light và Tiếng Việt/Dark ở các luồng trọng tâm.
- Đây là smoke test UI/UX, không phải kiểm thử toàn bộ nghiệp vụ. Không xóa file/registry, cài/gỡ/nâng cấp phần mềm, áp dụng preset, đổi startup, chạy sửa hệ thống hoặc khôi phục snapshot.
- Chỉ chạy các phép đọc: phân tích 1-click, quét Bloatware, đọc thông tin gói WinGet, quét danh sách phần mềm đã cài. Các xác nhận đều được hủy.
- Sau kiểm thử đã trả ngôn ngữ về English, giao diện Light; giữ WinUI style Default và widget tắt như ban đầu. Không sửa mã ứng dụng trong lượt này.

## Các vấn đề đã tái hiện

### UX-01 — P1: Chi tiết tác vụ trong xác nhận 1-click không hiển thị

**Tái hiện:** Cleanup → Analyze 1-click với lựa chọn mặc định → xem hộp xác nhận, không nhấn Clean & boost. Lặp lại với Tiếng Việt/Dark.

**Quan sát:** Hộp thoại hiện tổng số file/dung lượng và cảnh báo trình duyệt đang mở, nhưng phần liệt kê từng tác vụ trống. Cây truy cập vẫn chứa các dòng Temporary files/File tạm, Browser cache/Cache trình duyệt và DNS cache/Cache DNS. Lỗi xảy ra ở cả hai cấu hình hiển thị đã thử.

**Ảnh hưởng:** Người dùng được yêu cầu xác nhận xóa dữ liệu nhưng không đọc được từng hành động và dung lượng tương ứng. Đây là ưu tiên cao nhất.

**Đối chiếu mã:** `src/app/Views/MaintenancePage.cs:701` gán `Foreground = ... ? Brush(...) : null` cho các dòng không có mức High. Khác với bỏ thuộc tính để kế thừa màu chữ, giá trị null được gán trực tiếp vào brush vẽ chữ.

**Hướng sửa / nghiệm thu:** Không gán foreground null; dùng màu chữ theo theme cho trường hợp thông thường. Mọi dòng tác vụ phải nhìn thấy ở Light/Dark, cả Safe/Medium/High; xác nhận mặc định vẫn là Hủy.

### UX-02 — P2: Hủy 1-click nhưng tiến trình vẫn báo đang phân tích

**Tái hiện:** Mở xác nhận như UX-01 → Hủy → quan sát lại sau khi hộp thoại đã đóng và nút Analyze bật trở lại.

**Quan sát:** Thanh tiến trình đầy vẫn hiện; dòng `Analyzing DNS cache (3/3)...` hoặc `Đang phân tích Cache DNS (3/3)...` không đổi. Đồng thời thanh trạng thái toàn app báo Ready/Sẵn sàng và nút Stop/Dừng bị vô hiệu hóa.

**Ảnh hưởng:** Trạng thái mâu thuẫn, dễ tưởng tác vụ vẫn chạy ngầm hoặc app bị kẹt. Đã tái hiện hai lần, không chỉ trong khung hình chuyển tiếp.

**Đối chiếu mã:** `MaintenancePage.cs:649` trả về ngay khi từ chối xác nhận; khối finally tại dòng 671 chỉ phục hồi nút, không ẩn/reset `_oneClickProgress` hoặc cập nhật trạng thái cục bộ.

**Hướng sửa / nghiệm thu:** Chuẩn hóa trạng thái Idle/Analyzing/AwaitingConfirmation/Running/Cancelled/Completed/Failed; mọi đường thoát phải kết thúc tiến trình. Sau Hủy phải ghi rõ chưa dọn dữ liệu. Cần kiểm thử cả ngoại lệ và nút Dừng.

### UX-03 — P2: Thanh điều khiển chưa thích ứng cửa sổ mặc định

**Tái hiện:** Mở Storage Analyzer trong cửa sổ mặc định, sau đó phóng to để so sánh; mở Startup trong cửa sổ mặc định.

**Quan sát:** Ô Drive or folder trong Storage chỉ còn khoảng 50 px nhìn thấy, nhãn bị cắt thành `Drive or`; phóng to thì ô nhập rộng bình thường. Ở Startup, dòng `Only items that can be changed` bị cắt ở mép phải.

**Ảnh hưởng:** Khó kiểm tra đúng thư mục cần quét hoặc đọc đầy đủ bộ lọc, dù đang dùng kích thước app mở mặc định.

**Đối chiếu mã:** `StoragePage.cs:109` đặt bảy điều khiển trên một Grid một hàng; sáu cột Auto cạnh cột đường dẫn. `StartupPage.cs:72` dùng StackPanel ngang với nhiều chiều rộng tối thiểu, không xuống dòng.

**Hướng sửa / nghiệm thu:** Chia đường dẫn thành hàng riêng; cho nhóm tùy chọn/nút xuống hàng theo chiều rộng nội dung. Kiểm tra lại cả English/Vietnamese ở cửa sổ mặc định, phóng to và kích thước nhỏ được hỗ trợ; không cắt trường nhập, nhãn hay nút.

### UX-04 — P2: Bản tiếng Việt chưa dịch nội dung nghiệp vụ quan trọng

**Tái hiện:** Settings → Language → Tiếng Việt → Tối ưu hóa; mở xác nhận 1-click.

**Quan sát:** Khung điều hướng đã dịch nhưng nhóm `Gaming`, tên `Enable Game Mode` và mô tả tinh chỉnh vẫn tiếng Anh. Hộp xác nhận 1-click còn cảnh báo `msedge.exe is running; close it for a more complete cleanup.`

**Ảnh hưởng:** Người dùng không hiểu rõ tác động/cảnh báo trước các hành động hệ thống. Đây không chỉ là tên thương hiệu cần giữ nguyên.

**Đối chiếu mã:** `OptimizePage.cs:128`, `:159`, `:162` hiển thị trực tiếp Category/Title/Description; `CleanupService.cs:494` tạo cảnh báo tiếng Anh từ lớp nghiệp vụ.

**Hướng sửa / nghiệm thu:** Ánh xạ chuỗi bằng ID ổn định, trả warning code + tham số từ service để UI dịch. Dịch tên nhóm, mô tả, cảnh báo và tìm kiếm theo nội dung đã dịch. Không dịch tên gói/nhãn do nhà phát hành cung cấp một cách tùy tiện.

### UX-05 — P2: Nút biểu tượng và nhiều điều khiển thiếu tên truy cập

**Tái hiện:** Đọc cây UI Automation của Updates, Software Installer, System Tweaks và Settings.

**Quan sát:** Ba nút ở hàng gói Updates chỉ là `button`; nút mở trang của ứng dụng trong Installer cũng không có tên. Công tắc tinh chỉnh chỉ có `Off`/`On` thay vì tên chức năng; các ComboBox Language/Theme/WinUI style trong Settings không có tên trong cây truy cập.

**Ảnh hưởng:** Người dùng trình đọc màn hình khó phân biệt nút xem chi tiết với nút nâng cấp, hoặc công tắc đang điều khiển thiết lập nào. Chưa chạy Narrator; kết luận dựa trên metadata UIA, không phải đo trải nghiệm với mọi công cụ trợ năng.

**Đối chiếu mã:** `BasePage.cs:157` tạo IconButton và chỉ gắn ToolTip ở dòng 166, không gắn AutomationProperties.Name. Các nhãn của Settings và Tweaks là TextBlock tách rời điều khiển.

**Hướng sửa / nghiệm thu:** Gắn tên truy cập có ngữ cảnh, ví dụ `Nâng cấp Docker Desktop`, `Chi tiết Docker Desktop`; dùng Name/LabeledBy cho dropdown/công tắc. Kiểm tra thứ tự Tab và tên đọc bằng Narrator trước phát hành.

### UX-06 — P2: Chi tiết giấy phép gói cập nhật bị cắt

**Tái hiện:** Updates → biểu tượng chi tiết của Docker Desktop → đợi tải xong.

**Quan sát:** Dòng License dài chạy ngang ra ngoài thẻ và bị cắt; không có xuống dòng hoặc cách đọc đầy đủ ngay trong phần chi tiết.

**Đối chiếu mã:** `UpdatesPage.cs:396` tạo StackPanel ngang cho giấy phép; TextBlock ở dòng 399 không bật TextWrapping. Chỉ bật Wrap cũng chưa đủ nếu bố cục ngang vẫn đo nội dung không giới hạn.

**Hướng sửa / nghiệm thu:** Dùng Grid nhãn/nội dung với cột nội dung bị giới hạn chiều rộng, bật Wrap và cho chọn/copy văn bản. Kiểm tra giá trị License/Homepage/Publisher dài, không chỉ gói hiện tại.

### UX-07 — P3: Hộp thoại không theo giao diện sáng của app

**Tái hiện:** Settings đang Light → mở xác nhận 1-click hoặc xem trước preset Balanced.

**Quan sát:** Trang nền sáng nhưng ContentDialog màu tối. Nội dung preset vẫn đọc được, nên lỗi này độc lập với việc mất dòng tác vụ ở UX-01.

**Đối chiếu mã:** `MainWindow.cs:1338` áp dụng RequestedTheme cho root/navigation; các ContentDialog tạo riêng trong `MaintenancePage.cs:722` và `OptimizePage.cs:393` chỉ đặt XamlRoot, không đặt theme tương ứng.

**Hướng sửa / nghiệm thu:** Dùng một factory/helper tạo dialog với theme hiện hành; kiểm tra dialog, flyout và lỗi xác nhận ở Light/Dark/System.

### UX-08 — P2: Khuyến nghị khởi động lại dẫn sang cập nhật ứng dụng

**Tái hiện:** Dashboard trên máy đang có pending reboot.

**Quan sát:** `Restart when convenient` đi kèm nút `Scan Updates`, giống nút của khuyến nghị cập nhật ứng dụng. Người dùng không được chỉ dẫn rõ cách xử lý pending restart.

**Đối chiếu mã:** `DashboardHealthCheckService.cs:221` gán action tag `updates` cho `reboot.restart`; `DashboardPage.cs:556` đổi nhãn mọi action này thành Scan Updates và handler điều hướng tới trang WinGet. Đích hành động được xác nhận bằng đọc mã, không nhấn nút khởi động lại hay chạy cập nhật.

**Hướng sửa / nghiệm thu:** Tách action của pending reboot khỏi WinGet. Có thể dùng hướng dẫn khởi động lại thủ công hoặc mở đúng trang Windows Update; tuyệt đối không tự khởi động lại máy.

### UX-09 — P3: Nút chính của 1-click nằm quá xa phần đầu

**Tái hiện:** Vào Cleanup lần đầu ở cửa sổ mặc định, các nhóm giữ trạng thái mở mặc định.

**Quan sát:** Màn hình đầu chỉ hiện tiêu đề, cảnh báo admin và hai lựa chọn đầu. Phải cuộn qua nhóm khuyến nghị và tăng hiệu suất mới thấy Analyze 1-click; khi xem lựa chọn ở đầu trang thì không thấy nút chính/tóm tắt lựa chọn.

**Đối chiếu mã:** `MaintenancePage.cs:474` mở sẵn hai nhóm; nút phân tích được thêm sau toàn bộ nhóm ở dòng 492.

**Hướng sửa / nghiệm thu:** Đưa nút phân tích và tóm tắt số mục đã chọn lên đầu thẻ hoặc dùng thanh hành động cố định. Vẫn giữ luồng xem trước/xác nhận, không biến nút thành thao tác xóa ngay.

## Những phần hoạt động trong phạm vi đã thử

| Khu vực | Thao tác và kết quả |
| --- | --- |
| Dashboard | Nạp được sức khỏe hệ thống, RAM, ổ đĩa và khuyến nghị. Có UX-08. |
| Cleanup | Phân tích 1-click trả về tổng và cảnh báo; Hủy không chạy cleanup. Có UX-01/02/09. |
| Bloatware | Quét hoàn tất, nút được bật lại; dữ liệu hiện tại trả về danh sách rỗng và Remove bị khóa. Chưa xác minh tính đầy đủ của bộ phát hiện ứng dụng. |
| Storage | Mở trang, so sánh cửa sổ thường/phóng to. Có UX-03; không chạy lại phân tích ổ đĩa trong lượt này. |
| Software Installer | Tìm `zz-uiux-no-match` cho 0/15 và thông báo rỗng đúng; Install bị khóa. Nút Select visible còn bật khi không có kết quả, là điểm tinh chỉnh nhỏ. |
| Updates | Nạp một gói từ cache, mở thông tin nhà phát hành/giấy phép/mô tả thành công. Không nhấn Upgrade. Có UX-05/06. |
| Startup | Nạp sẵn 14 mục cùng trạng thái/tổng số. Không bật/tắt mục nào. Có UX-03. |
| System Tweaks | Xem trước Balanced hiển thị 7 thay đổi, 2 mục không đổi; Esc đóng hộp thoại và điều khiển bật lại. Không áp dụng. Có UX-04/05/07. |
| Repair | Kiểm tra danh sách, mô tả rủi ro và cảnh báo admin. Không chạy DNS/DISM/SFC. |
| Toolbox | Chuyển được cả ba tab; quét Clean Uninstaller trả về 421 ứng dụng, không gỡ. Không chạy registry cleanup hoặc tối ưu mạng. |
| History | Trạng thái `No reports yet` hiển thị rõ. Không có báo cáo/snapshot để thử chi tiết hoặc hoàn tác trong phiên này. |
| Settings | Đổi ngôn ngữ/theme hoạt động và lưu được; đã hoàn trả giá trị ban đầu. Có UX-05. |
| Điều hướng | Menu Storage mới mở bắt đầu ở đầu trang dù trước đó Cleanup đang cuộn; quay lại Cleanup giữ được vị trí cũ. History truy cập được bằng cuộn thanh điều hướng khi cửa sổ thấp. |

## Giới hạn và thứ tự xử lý đề nghị

1. Sửa UX-01 và UX-02 trước: khả năng đọc xác nhận và trạng thái kết thúc ảnh hưởng trực tiếp đến sự tin cậy của thao tác dọn dẹp.
2. Sửa UX-03/05/06: bố cục thích ứng, trợ năng và khả năng đọc thông tin dài.
3. Hoàn thiện UX-04/08, sau đó thống nhất theme/dialog và vị trí nút chính.

Chưa kiểm thử luồng nâng cấp/cài/gỡ thật, xóa dữ liệu, quyền Administrator/UAC, restore, lỗi mạng, High Contrast, đổi DPI, Narrator và mọi tổ hợp ngôn ngữ/theme. Cần máy ảo hoặc dữ liệu dùng riêng để kiểm thử các thao tác phá hủy. Không suy diễn tình trạng chậm của ứng dụng từ thời gian chụp/cây UIA của công cụ; chưa đo hiệu suất định lượng.

Không chạy lại build/unit tests vì lượt này chỉ kiểm thử UI và ghi báo cáo, không thay đổi mã ứng dụng.
