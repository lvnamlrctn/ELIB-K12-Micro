# DTOs — Request & Response

## Common

### SearchRequest — Internal (base cho tất cả Internal search)

```csharp
public class SearchRequest
{
    public string? Keyword   { get; set; }
    // DepartmentId KHÔNG có ở đây — BaseRepository tự lấy từ JWT claim
    public string? PortalId  { get; set; }
    public string? Language  { get; set; }
    public int     PageIndex { get; set; } = 1;
    public int     PageSize  { get; set; } = 10;
}
```

### PublicSearchRequest — Public (base cho tất cả Public search)

```csharp
public class PublicSearchRequest
{
    public string? Keyword      { get; set; }
    public string? PortalId     { get; set; }
    public string? Language     { get; set; }
    public long?   DepartmentId { get; set; }  // Client tự truyền — optional, không bắt buộc
    public int     PageIndex    { get; set; } = 1;
    public int     PageSize     { get; set; } = 10;
}
```

> **Sự khác biệt chính giữa Internal và Public search:**
> - `SearchRequest` (Internal): không có `DepartmentId` — server tự lấy từ JWT claim; không bắt buộc filter `Status` (trả cả bản ghi ẩn để quản trị).
> - `PublicSearchRequest` (Public): có `DepartmentId` — client tự truyền (optional), không truyền thì không filter; `Status = 2` **bắt buộc**, enforce tại `PublicBaseRepository.BuildQuery`.

### ChangeStatusRequest

```csharp
public class ChangeStatusRequest
{
    public Guid PublicId { get; set; }
    public int  Status   { get; set; }
}
```

### ApiResponse\<T\>

```csharp
public class ApiResponse<T>
{
    public bool   Success    { get; set; }
    public string Message    { get; set; }
    public T?     Data       { get; set; }
    public int    StatusCode { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items      { get; set; }
    public int     TotalCount { get; set; }
    public int     PageIndex  { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages { get; }   // computed: ceil(TotalCount / PageSize)
}
```

---

## Auth

### LoginRequest

```json
{ "loginName": "admin", "password": "admin123" }
```

### LoginResponse

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "fullName": "Quản trị viên",
  "loginName": "admin",
  "userId": 1,
  "departmentId": 3,
  "expiration": "2026-04-26T16:00:00Z"
}
```

> `departmentId` trả về để client biết user thuộc phòng ban nào. Token đã chứa claim `"DepartmentId"` để `BaseRepository` tự đọc — không cần client truyền lại trong các request Internal.

---

## Internal DTOs

> Tất cả Internal request **không có** `DepartmentId` — server tự lấy từ JWT claim.

### AttachFile

```json
// AttachFileRequest
{ "name": "Báo cáo Q1.pdf", "url": "/uploads/bao-cao-q1.pdf", "fileSize": 1048576, "newsId": 42 }

// AttachFileSearchRequest
{ "keyword": "báo cáo", "newsId": 42, "pageIndex": 1, "pageSize": 10 }
```

### Category

```json
// CategoryRequest
{
  "name": "Tin tức", "parentId": null, "level": 1, "status": 2, "order": 1,
  "portalId": "portal1", "isLogin": 0, "description": "Danh mục tin tức",
  "keyword": "tin tức", "pageTitle": "Tin tức", "metaDescription": "Danh mục tin tức",
  "language": "vi-VN", "link": "/tin-tuc"
}

// CategorySearchRequest
{ "keyword": "tin", "portalId": "portal1", "language": "vi-VN", "pageIndex": 1, "pageSize": 10 }
```

### Menu

```json
// MenuRequest
{
  "name": "Trang chủ", "menuType": 1, "link": "/", "friendUrl": "trang-chu",
  "sortOrder": 1, "status": 2, "openType": "_self", "portalId": "portal1",
  "language": "vi-VN", "parentId": null, "linkType": "internal",
  "subId": null, "isLogIn": 0, "icon": "fa-home"
}
```

### MenuType

```json
// MenuTypeRequest
{ "name": "Menu chính", "description": "Menu điều hướng chính", "language": "vi-VN", "portalId": "portal1", "code": "MAIN_MENU" }
```

### Module

```json
// ModuleRequest
{
  "name": "Quản lý tin tức", "link": "/admin/news", "icon": "fa-newspaper",
  "parentId": null, "language": "vi-VN", "portalId": "portal1", "group": 1,
  "sortOrder": 1, "moduleCode": "NEWS", "type": "page", "folderPage": "news", "status": 2
}
```

### ModuleRoles

```json
// ModuleRolesRequest
{
  "rolesId": 1, "moduleId": 7, "portalId": "portal1", "language": "vi-VN",
  "can_access": 1, "can_add": 1, "can_edit": 1, "can_delete": 0, "can_view": 1
}

// ModuleRolesSearchRequest
{ "rolesId": 1, "moduleId": 7, "portalId": "portal1", "pageIndex": 1, "pageSize": 10 }
```

### News

```json
// NewsRequest
{
  "title": "Tiêu đề bài viết", "brief": "Tóm tắt ngắn gọn",
  "content": "<p>Nội dung đầy đủ HTML</p>", "images": "/uploads/img.jpg",
  "thumb": "/uploads/thumb.jpg", "startTime": "2026-04-26T00:00:00", "endTime": null,
  "categoryId": 2, "eventId": null, "portalId": "portal1", "language": "vi-VN",
  "keyword": "từ khoá", "author": "Tác giả", "source": "Nguồn", "types": "news",
  "status": 2, "allowComment": 1, "metaTitle": "SEO Title",
  "metaKeyword": "SEO keywords", "metaDescription": "SEO description"
}

// NewsSearchRequest
{
  "keyword": "tin tức", "portalId": "portal1", "language": "vi-VN",
  "categoryId": 2, "status": 1, "types": "news", "pageIndex": 1, "pageSize": 10
}
```

### Photo

```json
// PhotoRequest
{
  "name": "Tên ảnh", "brief": "Mô tả ảnh", "image": "/uploads/photo.jpg",
  "link": null, "postion": "top", "photoAlbumId": 3, "width": 800, "height": 600,
  "status": 2, "portalId": "portal1", "sortOrder": 1, "language": "vi-VN", "types": "gallery"
}

// PhotoSearchRequest
{ "keyword": "tên ảnh", "photoAlbumId": 3, "status": 1, "portalId": "portal1", "language": "vi-VN", "pageIndex": 1, "pageSize": 20 }
```

### PhotoAlbum

```json
// PhotoAlbumRequest
{
  "portalId": "portal1", "name": "Album sự kiện", "description": "Ảnh sự kiện khai trương",
  "image": "/uploads/album-cover.jpg", "code": "EVENT_2026", "language": "vi-VN",
  "status": 2, "sortOrder": 1, "types": "event", "postions": "home", "isSpecial": 1
}

// PhotoAlbumSearchRequest
{ "keyword": "sự kiện", "status": 1, "portalId": "portal1", "language": "vi-VN", "pageIndex": 1, "pageSize": 10 }
```

---

## Public DTOs — `ELIBAPI.API.Public/DTOs/`

> Public request **có** `DepartmentId` (client tự truyền, optional). Public response **không có** audit trail.

### PublicNewsSearchRequest

```json
{
  "keyword": "tin tức",
  "portalId": "portal1",
  "language": "vi-VN",
  "departmentId": 3,        // optional — không truyền thì không filter
  "categoryId": 2,
  "types": "news",
  "pageIndex": 1,
  "pageSize": 10
}
```

### PublicNewsResponse

```json
{
  "id": 42,
  "title": "Tiêu đề bài viết",
  "brief": "Tóm tắt",
  "content": "<p>Nội dung</p>",
  "images": "/uploads/img.jpg",
  "thumb": "/uploads/thumb.jpg",
  "startTime": "2026-04-26T00:00:00",
  "endTime": null,
  "categoryId": 2,
  "portalId": "portal1",
  "language": "vi-VN",
  "keyword": "từ khoá",
  "author": "Tác giả",
  "source": "Nguồn",
  "types": "news",
  "status": 2,
  "allowComment": 1,
  "totalView": 150,
  "metaTitle": "SEO Title",
  "metaKeyword": "SEO keywords",
  "metaDescription": "SEO description"
  // Không có: isDelete, createdRowBy, updateRowBy, createdRowDate, updatedRowDate, publicId, departmentId
}
```

### PublicMenuSearchRequest

```json
{
  "keyword": "trang chủ",
  "portalId": "portal1",
  "language": "vi-VN",
  "departmentId": 3,        // optional
  "menuType": 1,
  "parentId": null,
  "pageIndex": 1,
  "pageSize": 50
}
```

### PublicMenuResponse

```json
{
  "id": 1,
  "name": "Trang chủ",
  "menuType": 1,
  "link": "/",
  "friendUrl": "trang-chu",
  "sortOrder": 1,
  "status": 2,
  "openType": "_self",
  "portalId": "portal1",
  "language": "vi-VN",
  "parentId": null,
  "linkType": "internal",
  "subId": null,
  "isLogIn": 0,
  "icon": "fa-home"
  // Không có: isDelete, createdRowBy, updateRowBy, createdRowDate, updatedRowDate, publicId, departmentId
}
```

### PublicCategorySearchRequest

```json
{
  "keyword": "tin tức",
  "portalId": "portal1",
  "language": "vi-VN",
  "departmentId": 3,        // optional
  "parentId": null,
  "level": 1,
  "pageIndex": 1,
  "pageSize": 50
}
```

### PublicCategoryResponse

```json
{
  "id": 5,
  "name": "Tin tức",
  "parentId": null,
  "level": 1,
  "status": 2,
  "order": 1,
  "portalId": "portal1",
  "isLogin": 0,
  "description": "Danh mục tin tức",
  "keyword": "tin tức",
  "pageTitle": "Tin tức",
  "metaDescription": "Danh mục tin tức",
  "language": "vi-VN",
  "link": "/tin-tuc"
  // Không có: isDelete, createdRowBy, updateRowBy, createdRowDate, updatedRowDate, publicId, departmentId
}
```

### PublicPhotoSearchRequest

```json
{
  "keyword": "tên ảnh",
  "portalId": "portal1",
  "language": "vi-VN",
  "departmentId": 3,        // optional
  "photoAlbumId": 3,
  "types": "gallery",
  "pageIndex": 1,
  "pageSize": 20
}
```

### PublicPhotoResponse

```json
{
  "id": 10,
  "name": "Tên ảnh",
  "brief": "Mô tả ảnh",
  "image": "/uploads/photo.jpg",
  "link": null,
  "postion": "top",
  "photoAlbumId": 3,
  "width": 800,
  "height": 600,
  "status": 2,
  "portalId": "portal1",
  "sortOrder": 1,
  "language": "vi-VN",
  "types": "gallery"
  // Không có: isDelete, createdRowBy, updateRowBy, createdRowDate, updatedRowDate, publicId, departmentId
}
```

---

## Response mẫu

### Thành công — Search có phân trang (Internal hoặc Public)

```json
{
  "success": true,
  "message": "Success.",
  "data": {
    "items": [ /* PublicXxxResponse hoặc Entity */ ],
    "totalCount": 100,
    "pageIndex": 1,
    "pageSize": 10,
    "totalPages": 10
  },
  "statusCode": 200
}
```

### Lỗi — Sai phòng ban — Internal Update/Delete (403)

```json
{ "success": false, "message": "You do not have permission to modify data from another department.", "data": null, "statusCode": 403 }
```

### Lỗi — Not Found (404)

```json
{ "success": false, "message": "Data not found.", "data": null, "statusCode": 404 }
```

---

## DTO mẫu — Schema `Ebook`

> Tất cả entity trong schema `Ebook` áp dụng cùng pattern: `{Entity}Request`, `{Entity}SearchRequest`, `Public{Entity}Response`.
> Class name dùng tên class C# (ví dụ `IntroBooks`, `EbookCollection`, `EbookItem`).

### IntroBooks (ví dụ tiêu biểu)

```json
// IntroBooksRequest
{
  "title": "Giới thiệu sách mới tháng 4/2026",
  "brief": "Tóm tắt nội dung cuốn sách...",
  "noidung": "<p>Nội dung chi tiết giới thiệu</p>",
  "image": "/uploads/introbooks/cover.jpg",
  "submited": "2026-04-27T00:00:00",
  "portalId": "portal1",
  "language": "vi-VN",
  "order": 1,
  "introBookCategoryId": 3,
  "bibId": 1042
}

// IntroBooksSearchRequest
{
  "keyword": "sách mới",
  "introBookCategoryId": 3,
  "portalId": "portal1",
  "language": "vi-VN",
  "pageIndex": 1,
  "pageSize": 10
}
```

### PublicIntroBooksResponse

```json
{
  "id": 15,
  "title": "Giới thiệu sách mới tháng 4/2026",
  "brief": "Tóm tắt nội dung cuốn sách...",
  "noidung": "<p>Nội dung chi tiết</p>",
  "image": "/uploads/introbooks/cover.jpg",
  "submited": "2026-04-27T00:00:00",
  "portalId": "portal1",
  "language": "vi-VN",
  "order": 1,
  "introBookCategoryId": 3,
  "bibId": 1042
  // Không có: isDelete, createdRowBy, updateRowBy, createdRowDate, updatedRowDate, publicId, departmentId
}
```

### EbookCollection (ví dụ cây)

```json
// EbookCollectionRequest
{
  "name": "Tài liệu khoa học tự nhiên",
  "parentId": null,
  "level": 1,
  "status": 2,
  "sortOrder": 1,
  "allowdownload": 1,
  "images": "/uploads/collections/science.jpg",
  "portalId": "portal1",
  "language": "vi-VN",
  "link": "/bst/khoa-hoc-tu-nhien"
}

// EbookCollectionSearchRequest
{ "keyword": "khoa học", "parentId": null, "level": 1, "status": 2, "portalId": "portal1", "language": "vi-VN", "pageIndex": 1, "pageSize": 20 }
```

---

## DTO mẫu — Schema `EOffice`

> Tất cả entity trong schema `EOffice` áp dụng cùng pattern: `{Entity}Request`, `{Entity}SearchRequest`, `Public{Entity}Response`.

### Document (ví dụ tiêu biểu)

```json
// DocumentRequest
{
  "name": "Nghị định 01/2026/NĐ-CP về quản lý thư viện",
  "brief": "Trích yếu nội dung nghị định...",
  "documentTypeId": 2,
  "agencyId": 5,
  "issueDate": "2026-01-15T00:00:00",
  "expireDate": null,
  "typeId": 1,
  "status": 2,
  "sign": "01/2026/NĐ-CP",
  "topicId": 3,
  "govDocNumber": "01/2026",
  "portalId": "portal1",
  "language": "vi-VN"
}

// DocumentSearchRequest
{
  "keyword": "nghị định",
  "documentTypeId": 2,
  "agencyId": 5,
  "topicId": 3,
  "status": 2,
  "portalId": "portal1",
  "language": "vi-VN",
  "pageIndex": 1,
  "pageSize": 10
}
```

### PublicDocumentResponse

```json
{
  "id": 88,
  "name": "Nghị định 01/2026/NĐ-CP về quản lý thư viện",
  "brief": "Trích yếu nội dung nghị định...",
  "documentTypeId": 2,
  "agencyId": 5,
  "issueDate": "2026-01-15T00:00:00",
  "expireDate": null,
  "typeId": 1,
  "status": 2,
  "sign": "01/2026/NĐ-CP",
  "topicId": 3,
  "govDocNumber": "01/2026",
  "portalId": "portal1",
  "language": "vi-VN"
  // Không có: isDelete, createdRowBy, updateRowBy, createdRowDate, updatedRowDate, publicId, departmentId
}
```

---

## DTO mẫu — Schema `Evaluate`

> Tất cả entity trong schema `Evaluate` áp dụng cùng pattern: `{Entity}Request`, `{Entity}SearchRequest`, `Public{Entity}Response`.

### MonHoc (ví dụ tiêu biểu — có trường tiếng Việt)

```json
// MonHocRequest
{
  "maMon": "CNTT101",
  "tenMon": "Lập trình hướng đối tượng",
  "soTinChi": 3,
  "degreeId": 1,
  "knowledgeId": 2,
  "optionId": 1,
  "nguoiBienSoan": "TS. Nguyễn Văn A",
  "active": 1,
  "attachment": "/uploads/evaluate/cntt101-deco.pdf",
  "note": null
}

// MonHocSearchRequest
{
  "keyword": "lập trình",
  "degreeId": 1,
  "knowledgeId": 2,
  "active": 1,
  "pageIndex": 1,
  "pageSize": 10
}
```

### PublicMonHocResponse

```json
{
  "id": 45,
  "maMon": "CNTT101",
  "tenMon": "Lập trình hướng đối tượng",
  "soTinChi": 3,
  "degreeId": 1,
  "knowledgeId": 2,
  "optionId": 1,
  "nguoiBienSoan": "TS. Nguyễn Văn A",
  "active": 1,
  "attachment": "/uploads/evaluate/cntt101-deco.pdf",
  "note": null
  // Không có: isDelete, createdRowBy, updateRowBy, createdRowDate, updatedRowDate, publicId, departmentId
}
```

### NganhHoc (ví dụ cây)

```json
// NganhHocRequest
{
  "majorsName": "Công nghệ thông tin",
  "parentId": null,
  "programId": 1,
  "amountStudent": 120,
  "sortOrder": 1,
  "portalId": "portal1",
  "language": "vi-VN",
  "majorsCode": "7480201",
  "status": 2
}
```

---

## DTO mẫu — Schema `PrintBook`

> Tất cả entity trong schema `PrintBook` áp dụng cùng pattern: `{Entity}Request`, `{Entity}SearchRequest`, `Public{Entity}Response`.
> Các entity `BibData`, `BibXML`, `BibDataOrder`, `BibXMLOrder`, `BibOrder` thường chỉ dùng qua `Bib` — ít khi có API riêng.

### Bib (ví dụ tiêu biểu — biểu ghi thư mục)

```json
// BibRequest
{
  "mfn": null,
  "status": "CXL",
  "bib_worksheet_id": 1,
  "bib_type_id": 2,
  "marc_status": "n",
  "url": "/uploads/covers/bib-1042.jpg",
  "images": "/uploads/covers/bib-1042.jpg",
  "ebookId": null,
  "docNum": null
}

// BibSearchRequest
{
  "keyword": "lập trình",
  "bib_type_id": 2,
  "status": "CXL",
  "pageIndex": 1,
  "pageSize": 20
}
```

### PublicBibResponse

```json
{
  "bibid": 1042,
  "mfn": 1042,
  "status": "CXL",
  "bib_worksheet_id": 1,
  "bib_type_id": 2,
  "marc_status": "n",
  "url": "/uploads/covers/bib-1042.jpg",
  "images": "/uploads/covers/bib-1042.jpg",
  "ebookId": null
  // Không có: isDelete, createdRowBy, updateRowBy, createdRowDate, updatedRowDate, publicId, departmentId
}
```

### Barcode (ví dụ)

```json
// BarcodeRequest
{
  "barcode": "VN-0001042-001",
  "barcodeNumber": 1,
  "store": 1,
  "bibId": 1042,
  "status": "AVAIL"
}

// BarcodeSearchRequest
{ "keyword": "VN-0001042", "bibId": 1042, "status": "AVAIL", "store": 1, "pageIndex": 1, "pageSize": 20 }
```

### BookOut (cho mượn sách — ví dụ luồng lưu hành)

```json
// BookOutRequest
{
  "readerId": 123,
  "barcode": "VN-0001042-001",
  "borrowDate": "2026-04-27T08:00:00",
  "dueDate": "2026-05-27T08:00:00",
  "circPlace": 1,
  "note": null
}

// BookOutSearchRequest
{
  "keyword": null,
  "readerId": 123,
  "status": "LOAN",
  "circPlace": 1,
  "pageIndex": 1,
  "pageSize": 20
}
```

### Store (kho sách)

```json
// StoreRequest
{
  "name": "Kho A - Tầng 1",
  "postion": "A1",
  "storeTypeId": 1,
  "code": "KHO-A1",
  "images": null
}

// StoreSearchRequest
{ "keyword": "kho A", "storeTypeId": 1, "pageIndex": 1, "pageSize": 10 }
```

### CircPlace (địa điểm lưu hành)

```json
// CircPlaceRequest
{
  "name": "Phòng đọc chính",
  "cir_Type": 1,
  "limit_Book": 5,
  "work_Session": "07:00-21:00",
  "access_Request_Validtime": 3
}

// CircPlaceSearchRequest
{ "keyword": "phòng đọc", "pageIndex": 1, "pageSize": 10 }
```

### Supplier (nhà cung cấp)

```json
// SupplierRequest
{
  "name": "NXB Giáo Dục Việt Nam",
  "address": "81 Trần Hưng Đạo, Hà Nội",
  "email": "contact@nxbgd.vn",
  "website": "nxbgd.vn",
  "mobile": "024-38221552",
  "mst": "0100110190"
}

// SupplierSearchRequest
{ "keyword": "giáo dục", "pageIndex": 1, "pageSize": 10 }
```

### PolicyCirc (chính sách mượn)

```json
// PolicyCircRequest
{
  "readerType": 1,
  "circPlace": 1,
  "numberOfDate": 30,
  "numberOfRenew": 2,
  "numberOfBook": 5,
  "numberOfRequest": 3,
  "muontrung": 0,
  "store": 1
}

// PolicyCircSearchRequest
{ "readerType": 1, "circPlace": 1, "pageIndex": 1, "pageSize": 10 }
```
