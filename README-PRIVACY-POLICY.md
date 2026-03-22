# Hướng Dẫn Deploy Privacy Policy lên GitHub Pages

## Bước 1: Chuẩn bị file

File `privacy-policy.html` đã được tạo sẵn với đầy đủ nội dung theo yêu cầu của Facebook.

## Bước 2: Tùy chỉnh thông tin

Mở file `privacy-policy.html` và thay đổi các thông tin sau:

- **Email liên hệ:** `privacy@yourcompany.com` → email thực của bạn
- **Số điện thoại:** `+84 xxx xxx xxx` → số điện thoại thực
- **Địa chỉ công ty:** `[Địa chỉ công ty của bạn]` → địa chỉ thực
- **Facebook Page:** `facebook.com/yourpage` → link page thực của bạn
- **Tên công ty:** `[Tên Công Ty]` → tên công ty/shop của bạn

## Bước 3: Tạo GitHub Repository

### Option 1: Repository mới (Khuyến nghị)

1. Tạo repository public mới trên GitHub với tên: `privacy-policy`
2. Clone repository về máy:
   ```bash
   git clone https://github.com/[username]/privacy-policy.git
   cd privacy-policy
   ```
3. Copy file `privacy-policy.html` vào thư mục và đổi tên thành `index.html`:
   ```bash
   cp privacy-policy.html index.html
   ```
4. Commit và push:
   ```bash
   git add index.html
   git commit -m "Add privacy policy"
   git push origin main
   ```

### Option 2: Sử dụng repository hiện tại

1. Tạo branch mới cho GitHub Pages:
   ```bash
   git checkout --orphan gh-pages
   git rm -rf .
   ```
2. Copy file privacy policy:
   ```bash
   cp privacy-policy.html index.html
   ```
3. Commit và push:
   ```bash
   git add index.html
   git commit -m "Add privacy policy for GitHub Pages"
   git push origin gh-pages
   ```

## Bước 4: Enable GitHub Pages

1. Vào repository trên GitHub
2. Click **Settings** → **Pages**
3. Trong **Source**, chọn:
   - **Branch:** `main` (hoặc `gh-pages` nếu dùng option 2)
   - **Folder:** `/ (root)`
4. Click **Save**

## Bước 5: Lấy URL

Sau vài phút, GitHub Pages sẽ deploy xong. URL sẽ có dạng:
```
https://[username].github.io/privacy-policy/
```

Hoặc nếu dùng repository hiện tại:
```
https://[username].github.io/[repo-name]/
```

## Bước 6: Cấu hình Facebook App

1. Vào **Facebook App Dashboard**
2. Chọn **App Settings** → **Basic**
3. Tìm mục **Privacy Policy URL**
4. Nhập URL từ bước 5
5. Click **Save Changes**
6. Thử chuyển app sang **Live mode**

## Bước 7: Verify

Kiểm tra xem Privacy Policy đã hoạt động:
1. Mở URL trong trình duyệt
2. Đảm bảo trang hiển thị đúng
3. Kiểm tra HTTPS (GitHub Pages tự động enable)
4. Test trên mobile để đảm bảo responsive

## Custom Domain (Tùy chọn)

Nếu bạn có domain riêng:

1. Tạo file `CNAME` trong repository:
   ```
   privacy.yourdomain.com
   ```
2. Cấu hình DNS:
   - Type: `CNAME`
   - Name: `privacy`
   - Value: `[username].github.io`
3. Đợi DNS propagate (5-30 phút)
4. URL mới: `https://privacy.yourdomain.com`

## Lưu ý quan trọng

- ✅ Repository phải là **public** (GitHub Pages miễn phí chỉ cho public repo)
- ✅ File phải tên là `index.html` để truy cập trực tiếp qua URL root
- ✅ HTTPS tự động được enable bởi GitHub Pages
- ✅ Cập nhật ngày "Cập nhật lần cuối" mỗi khi sửa đổi
- ✅ Backup file này trong repository chính của bạn

## Troubleshooting

### Lỗi 404 Not Found
- Đợi 5-10 phút sau khi enable GitHub Pages
- Kiểm tra branch và folder settings
- Đảm bảo file tên là `index.html`

### Facebook không chấp nhận URL
- Đảm bảo URL dùng HTTPS
- Kiểm tra trang có thể truy cập công khai (không cần login)
- Xóa cache trình duyệt và thử lại

### Trang không responsive trên mobile
- File HTML đã có responsive CSS sẵn
- Kiểm tra viewport meta tag có đúng không

## Cập nhật sau này

Khi cần cập nhật Privacy Policy:
1. Sửa file `index.html` trong repository
2. Cập nhật ngày "Cập nhật lần cuối"
3. Commit và push
4. GitHub Pages tự động deploy trong vài phút

## Liên hệ

Nếu gặp vấn đề, tham khảo:
- [GitHub Pages Documentation](https://docs.github.com/en/pages)
- [Facebook App Review](https://developers.facebook.com/docs/app-review)
