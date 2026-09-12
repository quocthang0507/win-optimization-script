# Chính sách dọn dẹp an toàn

## Mặc định 1-click

Chỉ chọn dọn file tạm người dùng và Windows đã được tạo **và** sửa đổi hơn 7 ngày trước. Xem trước và xác nhận vẫn bắt buộc. Downloads, tài liệu, Thùng rác, lịch sử, cookie, phiên trình duyệt, shader cache, Prefetch và thao tác DNS không được chọn mặc định.

Các mục khác nằm trong nhóm nâng cao hoặc hiệu suất, mặc định đóng và không chọn. Không tự áp dụng tinh chỉnh registry, tắt dịch vụ hoặc ứng dụng khởi động trong chế độ mặc định. Các khuyến nghị hiệu suất trên Tổng quan vẫn cần người dùng xem xét.

## Điều kiện bảo vệ

- Preview và thực thi đều kiểm tra trình duyệt đang chạy; thực thi kiểm tra lại giữa các thư mục trình duyệt. Nếu phát hiện trình duyệt mở, dừng phần dọn trình duyệt.
- Không xóa Service Worker CacheStorage/ScriptCache, vì có thể chứa nội dung web ngoại tuyến.
- Không xóa Firefox `places.sqlite` và các file WAL/SHM của nó: cơ sở dữ liệu này chứa cả bookmark. Dọn lịch sử Firefox hoàn chỉnh nên thực hiện trong chính Firefox.
- Chỉ dọn dump ứng dụng `.dmp` đã cũ hơn 14 ngày.
- Không đi qua đường dẫn reparse point/junction/symlink. Kiểm tra lại đường dẫn và tuổi file trước khi xóa.
- Mở file với chế độ chia sẻ hạn chế trước khi xóa để bỏ qua file đang được ứng dụng khác sử dụng. Không ép đóng tiến trình hoặc thay đổi quyền file.
- Dung lượng báo cáo dựa trên file xóa thành công, không lấy tổng preview làm kết quả. Mục bị bỏ qua được đếm; lỗi truy cập được ghi trong báo cáo.

## Giới hạn

Ngưỡng tuổi file là biện pháp thận trọng, không bảo đảm file không còn cần thiết. Các thao tác nâng cao có thể xóa dữ liệu hoặc khiến cache phải tạo lại; dữ liệu đã xóa không được bảo đảm khôi phục bởi restore point. Không tuyên bố tăng tốc chỉ dựa trên số GB đã dọn hoặc việc làm mới DNS.

Kiểm thử thực thi dùng thư mục mẫu riêng, không chạy dọn dữ liệu thật trên máy phát triển. Các luồng tinh chỉnh có snapshot và xác nhận hiện có được giữ nguyên.
