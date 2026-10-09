# Entities & Database Schema

## Tổng quan

Mỗi entity nằm trong **file riêng**, tổ chức theo schema: `ELIBAPI.Core/Entities/{Schema}/{Entity}.cs`

### Schema `cms` — Quản lý nội dung & CMS

| Class | Bảng | Schema | File | Namespace | Mục đích |
|-------|------|--------|------|-----------|----------|
| `ADS` | `ADS` | `cms` | `Entities/Cms/ADS.cs` | `ELIBAPI.Core.Entities.Cms` | Quảng cáo |
| `ADSGroup` | `ADSGroup` | `cms` | `Entities/Cms/ADSGroup.cs` | `ELIBAPI.Core.Entities.Cms` | Nhóm quảng cáo |
| `AttachFile` | `AttachFile` | `cms` | `Entities/Cms/AttachFile.cs` | `ELIBAPI.Core.Entities.Cms` | File đính kèm của bài viết |
| `Banner` | `Banner` | `cms` | `Entities/Cms/Banner.cs` | `ELIBAPI.Core.Entities.Cms` | Banner trang web |
| `Category` | `Category` | `cms` | `Entities/Cms/Category.cs` | `ELIBAPI.Core.Entities.Cms` | Danh mục |
| `Contact` | `Contact` | `cms` | `Entities/Cms/Contact.cs` | `ELIBAPI.Core.Entities.Cms` | Tin liên hệ từ người dùng |
| `ContactGroup` | `ContactGroup` | `cms` | `Entities/Cms/ContactGroup.cs` | `ELIBAPI.Core.Entities.Cms` | Nhóm/phòng ban nhận liên hệ |
| `Counter` | `Counter` | `cms` | `Entities/Cms/Counter.cs` | `ELIBAPI.Core.Entities.Cms` | Đếm lượt truy cập |
| `Customer` | `Customer` | `cms` | `Entities/Cms/Customer.cs` | `ELIBAPI.Core.Entities.Cms` | Khách hàng |
| `EventNews` | `EventNews` | `cms` | `Entities/Cms/EventNews.cs` | `ELIBAPI.Core.Entities.Cms` | Sự kiện tin tức |
| `CmsItem` | `Item` | `cms` | `Entities/Cms/CmsItem.cs` | `ELIBAPI.Core.Entities.Cms` | Mục nội dung chuyên mục |
| `ItemType` | `ItemType` | `cms` | `Entities/Cms/ItemType.cs` | `ELIBAPI.Core.Entities.Cms` | Loại mục nội dung |
| `Link` | `Link` | `cms` | `Entities/Cms/Link.cs` | `ELIBAPI.Core.Entities.Cms` | Liên kết ngoài |
| `LinkGroup` | `LinkGroup` | `cms` | `Entities/Cms/LinkGroup.cs` | `ELIBAPI.Core.Entities.Cms` | Nhóm liên kết |
| `Menu` | `Menu` | `cms` | `Entities/Cms/Menu.cs` | `ELIBAPI.Core.Entities.Cms` | Menu điều hướng |
| `MenuType` | `MenuType` | `cms` | `Entities/Cms/MenuType.cs` | `ELIBAPI.Core.Entities.Cms` | Loại menu |
| `Module` | `Module` | `cms` | `Entities/Cms/Module.cs` | `ELIBAPI.Core.Entities.Cms` | Module/chức năng hệ thống |
| `ModuleRoles` | `ModuleRoles` | `cms` | `Entities/Cms/ModuleRoles.cs` | `ELIBAPI.Core.Entities.Cms` | Quyền của role theo module |
| `News` | `news` | `cms` | `Entities/Cms/News.cs` | `ELIBAPI.Core.Entities.Cms` | Bài viết / tin tức |
| `NewsComment` | `NewsComment` | `cms` | `Entities/Cms/NewsComment.cs` | `ELIBAPI.Core.Entities.Cms` | Bình luận bài viết |
| `Page` | `Page` | `cms` | `Entities/Cms/Page.cs` | `ELIBAPI.Core.Entities.Cms` | Trang tĩnh (page code) |
| `Permission` | `Permission` | `cms` | `Entities/Cms/Permission.cs` | `ELIBAPI.Core.Entities.Cms` | Phân quyền theo user + module |
| `Photo` | `Photo` | `cms` | `Entities/Cms/Photo.cs` | `ELIBAPI.Core.Entities.Cms` | Ảnh trong album |
| `PhotoAlbum` | `PhotoAlbum` | `cms` | `Entities/Cms/PhotoAlbum.cs` | `ELIBAPI.Core.Entities.Cms` | Album ảnh |
| `Roles` | `Roles` | `cms` | `Entities/Cms/Roles.cs` | `ELIBAPI.Core.Entities.Cms` | Vai trò/nhóm quyền |
| `Supportonline` | `Supportonline` | `cms` | `Entities/Cms/Supportonline.cs` | `ELIBAPI.Core.Entities.Cms` | Hỗ trợ trực tuyến |
| `Video` | `Video` | `cms` | `Entities/Cms/Video.cs` | `ELIBAPI.Core.Entities.Cms` | Video |

### Schema `dbo` — Người dùng, bạn đọc & danh mục hệ thống

| Class | Bảng | Schema | File | Namespace | Mục đích |
|-------|------|--------|------|-----------|----------|
| `ChucVu` | `ChucVu` | `dbo` | `Entities/Dbo/ChucVu.cs` | `ELIBAPI.Core.Entities.Dbo` | Chức vụ |
| `Class` | `Class` | `dbo` | `Entities/Dbo/Class.cs` | `ELIBAPI.Core.Entities.Dbo` | Lớp học |
| `ConfigImportReader` | `ConfigImportReader` | `dbo` | `Entities/Dbo/ConfigImportReader.cs` | `ELIBAPI.Core.Entities.Dbo` | Cấu hình import bạn đọc |
| `Course` | `Course` | `dbo` | `Entities/Dbo/Course.cs` | `ELIBAPI.Core.Entities.Dbo` | Khóa/Niên khóa bạn đọc |
| `Currency` | `Currency` | `dbo` | `Entities/Dbo/Currency.cs` | `ELIBAPI.Core.Entities.Dbo` | Loại tiền tệ & tỷ giá |
| `Degree` | `Degree` | `dbo` | `Entities/Dbo/Degree.cs` | `ELIBAPI.Core.Entities.Dbo` | Học vị bạn đọc |
| `Department` | `Department` | `dbo` | `Entities/Dbo/Department.cs` | `ELIBAPI.Core.Entities.Dbo` | Phòng ban/đơn vị |
| `Ethenic` | `Ethenic` | `dbo` | `Entities/Dbo/Ethenic.cs` | `ELIBAPI.Core.Entities.Dbo` | Dân tộc |
| `GroupReader` | `GroupReader` | `dbo` | `Entities/Dbo/GroupReader.cs` | `ELIBAPI.Core.Entities.Dbo` | Nhóm bạn đọc |
| `GroupUser` | `GroupUser` | `dbo` | `Entities/Dbo/GroupUser.cs` | `ELIBAPI.Core.Entities.Dbo` | Nhóm người dùng hệ thống |
| `Nation` | `Nation` | `dbo` | `Entities/Dbo/Nation.cs` | `ELIBAPI.Core.Entities.Dbo` | Quốc tịch |
| `Org` | `Org` | `dbo` | `Entities/Dbo/Org.cs` | `ELIBAPI.Core.Entities.Dbo` | Tổ chức/cơ sở đào tạo (cây) |
| `Prof` | `Prof` | `dbo` | `Entities/Dbo/Prof.cs` | `ELIBAPI.Core.Entities.Dbo` | Chuyên ngành |
| `Reader` | `Reader` | `dbo` | `Entities/Dbo/Reader.cs` | `ELIBAPI.Core.Entities.Dbo` | Bạn đọc/thẻ thư viện |
| `ReaderDelete` | `ReaderDelete` | `dbo` | `Entities/Dbo/ReaderDelete.cs` | `ELIBAPI.Core.Entities.Dbo` | Lưu bạn đọc đã xóa |
| `ReaderInGroup` | `ReaderInGroup` | `dbo` | `Entities/Dbo/ReaderInGroup.cs` | `ELIBAPI.Core.Entities.Dbo` | Bạn đọc trong nhóm (junction) |
| `ReaderTrackingLogin` | `ReaderTrackingLogin` | `dbo` | `Entities/Dbo/ReaderTrackingLogin.cs` | `ELIBAPI.Core.Entities.Dbo` | Theo dõi đăng nhập bạn đọc |
| `ReaderType` | `ReaderType` | `dbo` | `Entities/Dbo/ReaderType.cs` | `ELIBAPI.Core.Entities.Dbo` | Loại bạn đọc |
| `Sip2Log` | `Sip2Log` | `dbo` | `Entities/Dbo/Sip2Log.cs` | `ELIBAPI.Core.Entities.Dbo` | Log giao thức SIP2 |
| `SystemParameter` | `systemparameter` | `dbo` | `Entities/Dbo/SystemParameter.cs` | `ELIBAPI.Core.Entities.Dbo` | Tham số cấu hình hệ thống |
| `UserLog` | `UserLog` | `dbo` | `Entities/Dbo/UserLog.cs` | `ELIBAPI.Core.Entities.Dbo` | Nhật ký hành động người dùng |
| `Users` | `Users` | `dbo` | `Entities/Dbo/Users.cs` | `ELIBAPI.Core.Entities.Dbo` | Người dùng hệ thống |

### Schema `Ebook` — Tài liệu số & quản lý ebook

| Class | Bảng | Schema | File | Namespace | Mục đích |
|-------|------|--------|------|-----------|----------|
| `EbookCollection` | `collection` | `Ebook` | `Entities/Ebook/EbookCollection.cs` | `ELIBAPI.Core.Entities.Ebook` | Bộ sưu tập số (cây) |
| `CollectionPermistionUser` | `CollectionPermistionUser` | `Ebook` | `Entities/Ebook/CollectionPermistionUser.cs` | `ELIBAPI.Core.Entities.Ebook` | Phân quyền BST theo nhóm người dùng |
| `DigType` | `DigType` | `Ebook` | `Entities/Ebook/DigType.cs` | `ELIBAPI.Core.Entities.Ebook` | Loại tài liệu số |
| `EbookAccess` | `Ebook` | `Ebook` | `Entities/Ebook/EbookAccess.cs` | `ELIBAPI.Core.Entities.Ebook` | Log truy cập/đọc ebook |
| `EbookFile` | `EbookFile` | `Ebook` | `Entities/Ebook/EbookFile.cs` | `ELIBAPI.Core.Entities.Ebook` | File vật lý của ebook |
| `EbookLog` | `EbookLog` | `Ebook` | `Entities/Ebook/EbookLog.cs` | `ELIBAPI.Core.Entities.Ebook` | Log tải/xem ebook |
| `IntroBookCategory` | `IntroBookCategory` | `Ebook` | `Entities/Ebook/IntroBookCategory.cs` | `ELIBAPI.Core.Entities.Ebook` | Danh mục giới thiệu sách (cây) |
| `IntroBooks` | `IntroBooks` | `Ebook` | `Entities/Ebook/IntroBooks.cs` | `ELIBAPI.Core.Entities.Ebook` | Bài giới thiệu sách |
| `EbookItem` | `Item` | `Ebook` | `Entities/Ebook/EbookItem.cs` | `ELIBAPI.Core.Entities.Ebook` | Tài liệu số (metadata + trạng thái) |
| `EbookItemXml` | `itemXml` | `Ebook` | `Entities/Ebook/EbookItemXml.cs` | `ELIBAPI.Core.Entities.Ebook` | Dữ liệu mô tả XML của tài liệu số |
| `MetaDataFieldRegistery` | `MetaDataFieldRegistery` | `Ebook` | `Entities/Ebook/MetaDataFieldRegistery.cs` | `ELIBAPI.Core.Entities.Ebook` | Trường metadata (MARC field) |
| `MetadataSchemaRegistry` | `MetadataSchemaRegistry` | `Ebook` | `Entities/Ebook/MetadataSchemaRegistry.cs` | `ELIBAPI.Core.Entities.Ebook` | Schema metadata (Dublin Core, MARC21…) |
| `MetaDataValue` | `MetaDataValue` | `Ebook` | `Entities/Ebook/MetaDataValue.cs` | `ELIBAPI.Core.Entities.Ebook` | Giá trị metadata của từng tài liệu |
| `PolicyDigital` | `PolicyDigital` | `Ebook` | `Entities/Ebook/PolicyDigital.cs` | `ELIBAPI.Core.Entities.Ebook` | Chính sách truy cập số theo loại bạn đọc |
| `PolicyDigitalByCollection` | `PolicyDigitalByCollection` | `Ebook` | `Entities/Ebook/PolicyDigitalByCollection.cs` | `ELIBAPI.Core.Entities.Ebook` | Chính sách truy cập theo BST + loại bạn đọc |
| `EbookSubject` | `Subject` | `Ebook` | `Entities/Ebook/EbookSubject.cs` | `ELIBAPI.Core.Entities.Ebook` | Môn loại DDC/UDC (cây) |
| `TheodoiBienmucEbook` | `TheodoiBienmucEbook` | `Ebook` | `Entities/Ebook/TheodoiBienmucEbook.cs` | `ELIBAPI.Core.Entities.Ebook` | Theo dõi biên mục tài liệu số |
| `EbookTopic` | `Topic` | `Ebook` | `Entities/Ebook/EbookTopic.cs` | `ELIBAPI.Core.Entities.Ebook` | Chủ đề tài liệu số (cây) |

### Schema `EOffice` — Văn bản điện tử

| Class | Bảng | Schema | File | Namespace | Mục đích |
|-------|------|--------|------|-----------|----------|
| `Agency` | `Agency` | `EOffice` | `Entities/EOffice/Agency.cs` | `ELIBAPI.Core.Entities.EOffice` | Cơ quan ban hành (cây) |
| `Document` | `Document` | `EOffice` | `Entities/EOffice/Document.cs` | `ELIBAPI.Core.Entities.EOffice` | Văn bản/tài liệu pháp lý |
| `DocumentFile` | `DocumentFile` | `EOffice` | `Entities/EOffice/DocumentFile.cs` | `ELIBAPI.Core.Entities.EOffice` | File đính kèm văn bản |
| `DocumentType` | `DocumentType` | `EOffice` | `Entities/EOffice/DocumentType.cs` | `ELIBAPI.Core.Entities.EOffice` | Loại văn bản |
| `EofficeTopic` | `Topic` | `EOffice` | `Entities/EOffice/EofficeTopic.cs` | `ELIBAPI.Core.Entities.EOffice` | Chủ đề văn bản (cây) |

### Schema `Evaluate` — Chương trình đào tạo

| Class | Bảng | Schema | File | Namespace | Mục đích |
|-------|------|--------|------|-----------|----------|
| `EvaluateCourse` | `Course` | `Evaluate` | `Entities/Evaluate/EvaluateCourse.cs` | `ELIBAPI.Core.Entities.Evaluate` | Học phần/môn học trong chương trình |
| `CourseOption` | `CourseOption` | `Evaluate` | `Entities/Evaluate/CourseOption.cs` | `ELIBAPI.Core.Entities.Evaluate` | Loại học phần (bắt buộc, tự chọn…) |
| `EvaluateDegree` | `Degree` | `Evaluate` | `Entities/Evaluate/EvaluateDegree.cs` | `ELIBAPI.Core.Entities.Evaluate` | Trình độ đào tạo |
| `Knowledge` | `Knowledge` | `Evaluate` | `Entities/Evaluate/Knowledge.cs` | `ELIBAPI.Core.Entities.Evaluate` | Khối kiến thức |
| `MonHoc` | `MonHoc` | `Evaluate` | `Entities/Evaluate/MonHoc.cs` | `ELIBAPI.Core.Entities.Evaluate` | Môn học (theo chuẩn CTĐT) |
| `NganhHoc` | `NganhHoc` | `Evaluate` | `Entities/Evaluate/NganhHoc.cs` | `ELIBAPI.Core.Entities.Evaluate` | Ngành học |
| `NganhMonHoc` | `NganhMonHoc` | `Evaluate` | `Entities/Evaluate/NganhMonHoc.cs` | `ELIBAPI.Core.Entities.Evaluate` | Ngành — Môn học (junction) |
| `EvaluateProgram` | `Program` | `Evaluate` | `Entities/Evaluate/EvaluateProgram.cs` | `ELIBAPI.Core.Entities.Evaluate` | Chương trình đào tạo |
| `TaiLieu` | `TaiLieu` | `Evaluate` | `Entities/Evaluate/TaiLieu.cs` | `ELIBAPI.Core.Entities.Evaluate` | Tài liệu tham khảo môn học |

### Schema `PrintBook` — Quản lý sách in & nghiệp vụ thư viện

| Class | Bảng | Schema | File | Namespace | Mục đích |
|-------|------|--------|------|-----------|----------|
| `Aacr2Field` | `aacr2_field` | `PrintBook` | `Entities/PrintBook/Aacr2Field.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trường AACR2/MARC |
| `Aacr2Subfield` | `aacr2_subfield` | `PrintBook` | `Entities/PrintBook/Aacr2Subfield.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trường con AACR2/MARC |
| `AbDeliverer` | `ab_deliverer` | `PrintBook` | `Entities/PrintBook/AbDeliverer.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu chuyển giao sách |
| `AbDelivererDetail` | `ab_deliverer_detail` | `PrintBook` | `Entities/PrintBook/AbDelivererDetail.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chi tiết phiếu chuyển giao |
| `AbDelivererStatus` | `ab_deliverer_status` | `PrintBook` | `Entities/PrintBook/AbDelivererStatus.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trạng thái chuyển giao |
| `AbMove` | `ab_move` | `PrintBook` | `Entities/PrintBook/AbMove.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu điều chuyển sách |
| `AbMoveDetail` | `ab_move_detail` | `PrintBook` | `Entities/PrintBook/AbMoveDetail.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chi tiết phiếu điều chuyển |
| `AbOrder` | `ab_order` | `PrintBook` | `Entities/PrintBook/AbOrder.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu đặt mua sách |
| `AbOrderDetail` | `ab_order_detail` | `PrintBook` | `Entities/PrintBook/AbOrderDetail.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chi tiết đặt mua |
| `AbReceipt` | `ab_receipt` | `PrintBook` | `Entities/PrintBook/AbReceipt.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu nhập kho (mua) |
| `AbReceiptDetail` | `ab_receipt_detail` | `PrintBook` | `Entities/PrintBook/AbReceiptDetail.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chi tiết phiếu nhập kho |
| `AbSource` | `ab_source` | `PrintBook` | `Entities/PrintBook/AbSource.cs` | `ELIBAPI.Core.Entities.PrintBook` | Nguồn bổ sung sách |
| `AhReceipt` | `ah_receipt` | `PrintBook` | `Entities/PrintBook/AhReceipt.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu nhập kho (hiến tặng/trao đổi) |
| `Barcode` | `Barcode` | `PrintBook` | `Entities/PrintBook/Barcode.cs` | `ELIBAPI.Core.Entities.PrintBook` | Barcode ấn phẩm (copy) |
| `BarcodeStatus` | `Barcode_Status` | `PrintBook` | `Entities/PrintBook/BarcodeStatus.cs` | `ELIBAPI.Core.Entities.PrintBook` | Danh mục trạng thái barcode |
| `Bib` | `Bib` | `PrintBook` | `Entities/PrintBook/Bib.cs` | `ELIBAPI.Core.Entities.PrintBook` | Biểu ghi thư mục (bibliographic record) |
| `BibType` | `Bib_Type` | `PrintBook` | `Entities/PrintBook/BibType.cs` | `ELIBAPI.Core.Entities.PrintBook` | Loại biểu ghi thư mục |
| `BibWorksheet` | `bib_worksheet` | `PrintBook` | `Entities/PrintBook/BibWorksheet.cs` | `ELIBAPI.Core.Entities.PrintBook` | Mẫu biên mục (worksheet) |
| `BibData` | `BibData` | `PrintBook` | `Entities/PrintBook/BibData.cs` | `ELIBAPI.Core.Entities.PrintBook` | Dữ liệu MARC của biểu ghi |
| `BibDataOrder` | `BibDataOrder` | `PrintBook` | `Entities/PrintBook/BibDataOrder.cs` | `ELIBAPI.Core.Entities.PrintBook` | Dữ liệu MARC biểu ghi đặt mua |
| `BibOrder` | `BibOrder` | `PrintBook` | `Entities/PrintBook/BibOrder.cs` | `ELIBAPI.Core.Entities.PrintBook` | Biểu ghi thư mục (quy trình đặt mua) |
| `BibXML` | `BibXML` | `PrintBook` | `Entities/PrintBook/BibXML.cs` | `ELIBAPI.Core.Entities.PrintBook` | View tổng hợp XML biểu ghi |
| `BibXMLOrder` | `BibXMLOrder` | `PrintBook` | `Entities/PrintBook/BibXMLOrder.cs` | `ELIBAPI.Core.Entities.PrintBook` | View XML biểu ghi đặt mua |
| `BookGroup` | `Book_Group` | `PrintBook` | `Entities/PrintBook/BookGroup.cs` | `ELIBAPI.Core.Entities.PrintBook` | Nhóm sách |
| `BookGroupDetail` | `book_group_detail` | `PrintBook` | `Entities/PrintBook/BookGroupDetail.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chi tiết nhóm sách |
| `BookIn` | `BookIn` | `PrintBook` | `Entities/PrintBook/BookIn.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu trả sách |
| `BookOut` | `BookOut` | `PrintBook` | `Entities/PrintBook/BookOut.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu cho mượn sách |
| `BookRequest` | `BookRequest` | `PrintBook` | `Entities/PrintBook/BookRequest.cs` | `ELIBAPI.Core.Entities.PrintBook` | Yêu cầu mượn sách |
| `Budget` | `Budget` | `PrintBook` | `Entities/PrintBook/Budget.cs` | `ELIBAPI.Core.Entities.PrintBook` | Ngân sách/kinh phí |
| `CFine` | `C_Fine` | `PrintBook` | `Entities/PrintBook/CFine.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phạt vi phạm |
| `CFineMethod` | `c_fine_method` | `PrintBook` | `Entities/PrintBook/CFineMethod.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phương thức thu phạt |
| `CFineType` | `C_Fine_type` | `PrintBook` | `Entities/PrintBook/CFineType.cs` | `ELIBAPI.Core.Entities.PrintBook` | Loại phạt |
| `CPhoto` | `C_photo` | `PrintBook` | `Entities/PrintBook/CPhoto.cs` | `ELIBAPI.Core.Entities.PrintBook` | Dịch vụ photocopy |
| `CQueueStatus` | `c_queue_status` | `PrintBook` | `Entities/PrintBook/CQueueStatus.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trạng thái hàng đợi đặt chỗ |
| `CRenew` | `C_renew` | `PrintBook` | `Entities/PrintBook/CRenew.cs` | `ELIBAPI.Core.Entities.PrintBook` | Yêu cầu gia hạn |
| `CRenewData` | `C_renew_data` | `PrintBook` | `Entities/PrintBook/CRenewData.cs` | `ELIBAPI.Core.Entities.PrintBook` | Dữ liệu lịch sử gia hạn |
| `Cabinet` | `Cabinet` | `PrintBook` | `Entities/PrintBook/Cabinet.cs` | `ELIBAPI.Core.Entities.PrintBook` | Tủ/kệ sách |
| `CheckIn` | `CheckIn` | `PrintBook` | `Entities/PrintBook/CheckIn.cs` | `ELIBAPI.Core.Entities.PrintBook` | Vào cổng thư viện |
| `CheckOut` | `CheckOut` | `PrintBook` | `Entities/PrintBook/CheckOut.cs` | `ELIBAPI.Core.Entities.PrintBook` | Ra cổng thư viện |
| `CircPlace` | `CircPlace` | `PrintBook` | `Entities/PrintBook/CircPlace.cs` | `ELIBAPI.Core.Entities.PrintBook` | Địa điểm lưu hành |
| `ConfigAacr2` | `config_aacr2` | `PrintBook` | `Entities/PrintBook/ConfigAacr2.cs` | `ELIBAPI.Core.Entities.PrintBook` | Cấu hình chuẩn AACR2 |
| `ConfigIsbd` | `Config_Isbd` | `PrintBook` | `Entities/PrintBook/ConfigIsbd.cs` | `ELIBAPI.Core.Entities.PrintBook` | Cấu hình chuẩn ISBD |
| `Countries` | `Countries` | `PrintBook` | `Entities/PrintBook/Countries.cs` | `ELIBAPI.Core.Entities.PrintBook` | Danh mục quốc gia |
| `DBibStatus` | `d_bib_status` | `PrintBook` | `Entities/PrintBook/DBibStatus.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trạng thái biểu ghi |
| `DKey` | `d_key` | `PrintBook` | `Entities/PrintBook/DKey.cs` | `ELIBAPI.Core.Entities.PrintBook` | Từ khóa phụ |
| `DPublisher` | `D_Publisher` | `PrintBook` | `Entities/PrintBook/DPublisher.cs` | `ELIBAPI.Core.Entities.PrintBook` | Nhà xuất bản |
| `DFixField` | `DFixField` | `PrintBook` | `Entities/PrintBook/DFixField.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trường cố định MARC |
| `DFixFieldPost` | `DFixFieldPost` | `PrintBook` | `Entities/PrintBook/DFixFieldPost.cs` | `ELIBAPI.Core.Entities.PrintBook` | Vị trí trong trường cố định |
| `DFixFieldValue` | `DFixFieldValue` | `PrintBook` | `Entities/PrintBook/DFixFieldValue.cs` | `ELIBAPI.Core.Entities.PrintBook` | Giá trị trường cố định |
| `DicAuthor` | `DicAuthor` | `PrintBook` | `Entities/PrintBook/DicAuthor.cs` | `ELIBAPI.Core.Entities.PrintBook` | Từ điển tác giả |
| `DicClass` | `DicClass` | `PrintBook` | `Entities/PrintBook/DicClass.cs` | `ELIBAPI.Core.Entities.PrintBook` | Từ điển phân loại (DDC/UDC) |
| `DicKeyword` | `DicKeyword` | `PrintBook` | `Entities/PrintBook/DicKeyword.cs` | `ELIBAPI.Core.Entities.PrintBook` | Từ điển từ khóa |
| `DicPublisher` | `DicPublisher` | `PrintBook` | `Entities/PrintBook/DicPublisher.cs` | `ELIBAPI.Core.Entities.PrintBook` | Từ điển nhà xuất bản |
| `FixedFieldValue` | `fixed_field_value` | `PrintBook` | `Entities/PrintBook/FixedFieldValue.cs` | `ELIBAPI.Core.Entities.PrintBook` | Giá trị trường cố định của biểu ghi |
| `FrequencyMagazine` | `FrequencyMagazine` | `PrintBook` | `Entities/PrintBook/FrequencyMagazine.cs` | `ELIBAPI.Core.Entities.PrintBook` | Tần suất phát hành báo/tạp chí |
| `Fund` | `Fund` | `PrintBook` | `Entities/PrintBook/Fund.cs` | `ELIBAPI.Core.Entities.PrintBook` | Quỹ/nguồn kinh phí |
| `GeographicAreas` | `GeographicAreas` | `PrintBook` | `Entities/PrintBook/GeographicAreas.cs` | `ELIBAPI.Core.Entities.PrintBook` | Khu vực địa lý |
| `Inventory` | `Inventory` | `PrintBook` | `Entities/PrintBook/Inventory.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu kiểm kê vật lý |
| `InventoryBarcode` | `InventoryBarcode` | `PrintBook` | `Entities/PrintBook/InventoryBarcode.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chi tiết barcode trong kiểm kê |
| `IsbdField` | `isbd_field` | `PrintBook` | `Entities/PrintBook/IsbdField.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trường ISBD |
| `IsbdSubfield` | `isbd_subfield` | `PrintBook` | `Entities/PrintBook/IsbdSubfield.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trường con ISBD |
| `Key` | `Key` | `PrintBook` | `Entities/PrintBook/Key.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chìa khóa tủ/kệ |
| `KeyIn` | `KeyIn` | `PrintBook` | `Entities/PrintBook/KeyIn.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu trả chìa khóa |
| `KeyOut` | `KeyOut` | `PrintBook` | `Entities/PrintBook/KeyOut.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phiếu mượn chìa khóa |
| `KiemKe` | `KiemKe` | `PrintBook` | `Entities/PrintBook/KiemKe.cs` | `ELIBAPI.Core.Entities.PrintBook` | Kiểm kê (legacy) |
| `Language` | `Language` | `PrintBook` | `Entities/PrintBook/Language.cs` | `ELIBAPI.Core.Entities.PrintBook` | Ngôn ngữ tài liệu |
| `LinhVucNghienCuu` | `LinhVucNghienCuu` | `PrintBook` | `Entities/PrintBook/LinhVucNghienCuu.cs` | `ELIBAPI.Core.Entities.PrintBook` | Lĩnh vực nghiên cứu |
| `LogBienMuc` | `Log_BienMuc` | `PrintBook` | `Entities/PrintBook/LogBienMuc.cs` | `ELIBAPI.Core.Entities.PrintBook` | Nhật ký biên mục |
| `LostBook` | `LostBook` | `PrintBook` | `Entities/PrintBook/LostBook.cs` | `ELIBAPI.Core.Entities.PrintBook` | Sách thất lạc/mất |
| `Lydophat` | `Lydophat` | `PrintBook` | `Entities/PrintBook/Lydophat.cs` | `ELIBAPI.Core.Entities.PrintBook` | Lý do phạt |
| `MagazineType` | `MagazineType` | `PrintBook` | `Entities/PrintBook/MagazineType.cs` | `ELIBAPI.Core.Entities.PrintBook` | Loại tạp chí/báo |
| `MarcCodeList` | `marc_code_list` | `PrintBook` | `Entities/PrintBook/MarcCodeList.cs` | `ELIBAPI.Core.Entities.PrintBook` | Bảng mã MARC |
| `MarcField` | `Marc_Field` | `PrintBook` | `Entities/PrintBook/MarcField.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trường MARC chuẩn |
| `MarcIndicator` | `Marc_Indicator` | `PrintBook` | `Entities/PrintBook/MarcIndicator.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chỉ thị MARC |
| `MarcSubField` | `Marc_SubField` | `PrintBook` | `Entities/PrintBook/MarcSubField.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trường con MARC chuẩn |
| `MarcBibLevel` | `MarcBibLevel` | `PrintBook` | `Entities/PrintBook/MarcBibLevel.cs` | `ELIBAPI.Core.Entities.PrintBook` | Cấp độ biểu ghi MARC |
| `MarcRecordType` | `MarcRecordType` | `PrintBook` | `Entities/PrintBook/MarcRecordType.cs` | `ELIBAPI.Core.Entities.PrintBook` | Loại bản ghi MARC |
| `MarcType` | `MarcType` | `PrintBook` | `Entities/PrintBook/MarcType.cs` | `ELIBAPI.Core.Entities.PrintBook` | Loại MARC |
| `MaterialsType` | `MaterialsType` | `PrintBook` | `Entities/PrintBook/MaterialsType.cs` | `ELIBAPI.Core.Entities.PrintBook` | Loại vật liệu/vật mang tin |
| `OrderStatus` | `Order_Status` | `PrintBook` | `Entities/PrintBook/OrderStatus.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trạng thái đặt mua |
| `PartemMagazineDetail` | `PartemMagazineDetail` | `PrintBook` | `Entities/PrintBook/PartemMagazineDetail.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chi tiết mẫu phát hành tạp chí |
| `PatternMagazine` | `PatternMagazine` | `PrintBook` | `Entities/PrintBook/PatternMagazine.cs` | `ELIBAPI.Core.Entities.PrintBook` | Mẫu phát hành tạp chí |
| `PhongBan` | `PhongBan` | `PrintBook` | `Entities/PrintBook/PhongBan.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phòng ban (PrintBook) |
| `PolicyCirc` | `PolicyCirc` | `PrintBook` | `Entities/PrintBook/PolicyCirc.cs` | `ELIBAPI.Core.Entities.PrintBook` | Chính sách lưu hành |
| `PrintBookandDigital` | `PrintBookandDigital` | `PrintBook` | `Entities/PrintBook/PrintBookandDigital.cs` | `ELIBAPI.Core.Entities.PrintBook` | Liên kết sách in — tài liệu số |
| `Private` | `Private` | `PrintBook` | `Entities/PrintBook/Private.cs` | `ELIBAPI.Core.Entities.PrintBook` | Phân quyền role-module |
| `ReceiptStatus` | `Receipt_Status` | `PrintBook` | `Entities/PrintBook/ReceiptStatus.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trạng thái phiếu nhập kho |
| `RecordType` | `RECORDTYPE` | `PrintBook` | `Entities/PrintBook/RecordType.cs` | `ELIBAPI.Core.Entities.PrintBook` | Loại bản ghi (MARC record type) |
| `Roles` | `Roles` | `PrintBook` | `Entities/PrintBook/Roles.cs` | `ELIBAPI.Core.Entities.PrintBook` | Vai trò hệ thống (PrintBook) |
| `Serial` | `Serial` | `PrintBook` | `Entities/PrintBook/Serial.cs` | `ELIBAPI.Core.Entities.PrintBook` | Quản lý ấn phẩm nhiều kỳ |
| `SerialItem` | `SERIALITEM` | `PrintBook` | `Entities/PrintBook/SerialItem.cs` | `ELIBAPI.Core.Entities.PrintBook` | Kỳ phát hành tạp chí |
| `Store` | `Store` | `PrintBook` | `Entities/PrintBook/Store.cs` | `ELIBAPI.Core.Entities.PrintBook` | Kho sách |
| `StoreType` | `StoreType` | `PrintBook` | `Entities/PrintBook/StoreType.cs` | `ELIBAPI.Core.Entities.PrintBook` | Loại kho |
| `SubcriptionStatus` | `SubcriptionStatus` | `PrintBook` | `Entities/PrintBook/SubcriptionStatus.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trạng thái đăng ký tạp chí |
| `Supplier` | `Supplier` | `PrintBook` | `Entities/PrintBook/Supplier.cs` | `ELIBAPI.Core.Entities.PrintBook` | Nhà cung cấp |
| `SystemDescription` | `System_description` | `PrintBook` | `Entities/PrintBook/SystemDescription.cs` | `ELIBAPI.Core.Entities.PrintBook` | Mô tả hệ thống |
| `SystemInfo` | `SystemInfo` | `PrintBook` | `Entities/PrintBook/SystemInfo.cs` | `ELIBAPI.Core.Entities.PrintBook` | Thông tin thư viện |
| `SystemPara` | `SystemPara` | `PrintBook` | `Entities/PrintBook/SystemPara.cs` | `ELIBAPI.Core.Entities.PrintBook` | Tham số hệ thống |
| `Thanhly` | `thanhly` | `PrintBook` | `Entities/PrintBook/Thanhly.cs` | `ELIBAPI.Core.Entities.PrintBook` | Thanh lý tài liệu |
| `TrackingToLibrary` | `trackingtolibrary` | `PrintBook` | `Entities/PrintBook/TrackingToLibrary.cs` | `ELIBAPI.Core.Entities.PrintBook` | Theo dõi bạn đọc vào thư viện |
| `User` | `User` | `PrintBook` | `Entities/PrintBook/User.cs` | `ELIBAPI.Core.Entities.PrintBook` | Người dùng (PrintBook) |
| `WorksheetField` | `worksheet_field` | `PrintBook` | `Entities/PrintBook/WorksheetField.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trường worksheet biên mục |
| `WorksheetSubfield` | `worksheet_subfield` | `PrintBook` | `Entities/PrintBook/WorksheetSubfield.cs` | `ELIBAPI.Core.Entities.PrintBook` | Trường con worksheet biên mục |
| `Z3950Config` | `Z3950Config` | `PrintBook` | `Entities/PrintBook/Z3950Config.cs` | `ELIBAPI.Core.Entities.PrintBook` | Cấu hình kết nối Z39.50 |
| `Z3950Group` | `Z3950Group` | `PrintBook` | `Entities/PrintBook/Z3950Group.cs` | `ELIBAPI.Core.Entities.PrintBook` | Nhóm kết nối Z39.50 |

---

## Các cột Audit Trail (có trong hầu hết bảng)

| Cột | Kiểu | Ý nghĩa |
|-----|------|---------|
| `IsDelete` | `int?` | Soft delete: `2` = đã xoá, `1/null` = chưa xoá |
| `CreatedRowBy` | `long?` | UserId người tạo bản ghi |
| `UpdateRowBy` | `long?` | UserId người cập nhật cuối |
| `CreatedRowDate` | `DateTime?` | Thời điểm tạo |
| `UpdatedRowDate` | `DateTime?` | Thời điểm cập nhật cuối |
| `DepartmentId` | `long?` | Phòng ban sở hữu bản ghi |
| `PublicId` | `Guid` | ID công khai, auto-gen bởi SQL (`newsequentialid()`) |

---

## Chi tiết từng Entity

### dbo.Users

```csharp
[Table("Users", Schema = "dbo")]
public class Users
{
    long     Id             // PK
    string?  FullName       // Họ tên
    string?  LoginName      // Tên đăng nhập (unique)
    string?  Email
    string?  Phone
    long?    DepartmentId
    string?  PortalId       // Portal/site
    string?  Language       // Ngôn ngữ mặc định
    string?  Password       // MD5 hash
    int?     RoleId
    int?     PostionId      // Chức vụ
    int?     Status         // 2=Hoạt động, 1=Không hoạt động
    int?     Sex            // 0=nữ, 1=nam
    string?  Address
    DateTime? BirthDate
    long?    CreatedBy
    long?    UpdateBy
    DateTime? CreatedDate
    string?  Photo          // Avatar URL
    DateTime? LastUpdate
    long?    RoleWinformId
    // + Audit Trail
}
```

### cms.Permission

```csharp
[Table("Permission", Schema = "cms")]
public class Permission
{
    long   Id
    long?  UserId       // FK → dbo.Users.Id
    long?  ModuleId     // FK → cms.Module.Id
    byte?  Can_Access   // 1=có quyền truy cập
    byte?  Can_Add      // 1=có quyền thêm
    byte?  Can_Edit     // 1=có quyền sửa
    byte?  Can_Delete   // 1=có quyền xoá
    byte?  Can_View     // 1=có quyền xem
    string? PortalId
    string? Language
    // + Audit Trail
}
```

**Quan trọng**: `PermissionAttribute` tra cứu bảng này với `(UserId, ModuleId)`.

### cms.AttachFile

```csharp
[Table("AttachFile", Schema = "cms")]
public class AttachFile
{
    long     Id
    string?  Name        // Tên file hiển thị
    string?  Url         // Đường dẫn file
    double?  FileSize    // Kích thước (bytes)
    long?    NewsId      // FK → cms.news.Id
    DateTime? CreatedDate
    // + Audit Trail
}
```

### cms.Category

```csharp
[Table("Category", Schema = "cms")]
public class Category
{
    long    Id
    string? Name
    long?   ParentId        // Danh mục cha (cây)
    long?   Level           // Cấp độ trong cây
    int?    Status          // 2=Xuất bản, 1=Ẩn
    int?    Order           // Thứ tự hiển thị
    string? PortalId
    int?    IsLogin         // 1=yêu cầu đăng nhập
    string? Description
    string? Keyword
    string? PageTitle
    string? MetaDescription
    string? Language
    string? Link            // URL thân thiện
    // + Audit Trail
}
```

### cms.Menu

```csharp
[Table("Menu", Schema = "cms")]
public class Menu
{
    long    Id
    string? Name
    long?   MenuType      // FK → cms.MenuType.Id
    string? Link
    string? FriendUrl     // URL thân thiện
    int?    SortOrder
    int?    Status          // 2=Xuất bản, 1=Ẩn
    string? OpenType      // "_blank", "_self"...
    string? PortalId
    string? Language
    long?   ParentId      // Menu cha (cây)
    string? LinkType
    string? SubId
    int?    IsLogIn       // 1=yêu cầu đăng nhập
    string? Icon
    // + Audit Trail
}
```

### cms.MenuType

```csharp
[Table("MenuType", Schema = "cms")]
public class MenuType
{
    long    Id
    string? Name
    string? Description
    string? Language
    string? PortalId
    string? Code         // Mã định danh loại menu
    // + Audit Trail
}
```

### cms.Module

```csharp
[Table("Module", Schema = "cms")]
public class Module
{
    long    Id
    string? Name
    string? Link
    string? Icon
    long?   ParentId     // Module cha (cây)
    string? Language
    string? PortalId
    long?   Group        // Nhóm module
    int?    SortOrder
    string? ModuleCode   // Mã module dùng trong [Permission("CODE", "action")]
    string? Type
    string? FolderPage
    int?    Status          // 2=Hoạt động, 1=Không hoạt động
    // + Audit Trail
}
```

**Lưu ý**: `ModuleCode` của bảng này là giá trị dùng trong `[Permission("MODULE_CODE", "action")]`. `PermissionAttribute` tra `ModuleCode` → lấy `Id` → tra `cms.Permission`.

### cms.ModuleRoles

```csharp
[Table("ModuleRoles", Schema = "cms")]
public class ModuleRoles
{
    long   Id
    long?  RolesId      // FK → bảng Roles (ngoài scope)
    long?  ModuleId     // FK → cms.Module.Id
    string? PortalId
    string? Language
    byte?  Can_access
    byte?  Can_add
    byte?  Can_edit
    byte?  Can_delete
    byte?  Can_view
    // + Audit Trail
}
```

### cms.news

```csharp
[Table("news", Schema = "cms")]   // tên bảng viết thường
public class News                  // tên class viết hoa
{
    long     Id
    string?  Title
    string?  Brief           // Tóm tắt
    string?  Content         // Nội dung đầy đủ (HTML)
    string?  Images          // URL ảnh chính
    string?  Thumb           // URL ảnh thumbnail
    DateTime? StartTime      // Ngày bắt đầu hiển thị
    DateTime? EndTime        // Ngày kết thúc hiển thị
    DateTime? CreatedDate
    DateTime? LastUpDate
    long?    CreatedBy
    long?    UpdateBy
    long?    CategoryId      // FK → cms.Category.Id
    int?     EventId
    string?  PortalId
    string?  Language
    string?  Keyword
    string?  Author
    string?  Source
    string?  Types
    string?  Clourse
    int?     Status          // 2=Xuất bản, 1=Ẩn/Chưa xuất bản
    int?     AllowComment    // 1=cho phép bình luận
    int?     TotalView       // Số lượt xem
    string?  MetaTitle
    string?  MetaKeyword
    string?  MetaDescription
    string?  MaleAudio       // Audio nam
    string?  FaleAudio       // Audio nữ
    string?  ContentAudio
    string?  BriefAudio
    string?  TitleAudio
    // + Audit Trail
}
```

### cms.Photo

```csharp
[Table("Photo", Schema = "cms")]
public class Photo
{
    long    Id
    string? Name
    string? Brief
    string? Image        // URL ảnh
    string? Link
    string? Postion      // Vị trí (lỗi chính tả trong DB, giữ nguyên)
    long?   PhotoAlbumId // FK → cms.PhotoAlbum.Id
    int?    Width
    int?    Height
    int?    Status          // 2=Xuất bản, 1=Ẩn
    string? PortalId
    int?    SortOrder
    string? Language
    string? Types
    // + Audit Trail
}
```

### cms.PhotoAlbum

```csharp
[Table("PhotoAlbum", Schema = "cms")]
public class PhotoAlbum
{
    long    Id
    string? PortalId
    string? Name
    string? Description
    string? Image        // Ảnh đại diện album
    string? Code
    string? Language
    int?    Status          // 2=Xuất bản, 1=Ẩn
    int?    SortOrder
    string? Types
    string? Postions     // Vị trí (giữ nguyên tên cột DB)
    int?    IsSpecial    // 1=album nổi bật
    // + Audit Trail
}
```

---

## Chi tiết Entity mới — Schema `cms`

### cms.ADS

```csharp
[Table("ADS", Schema = "cms")]
public class ADS
{
    long    Id
    string? Name         // Tên quảng cáo
    string? Image        // Đường dẫn ảnh (varchar 200)
    int?    Width
    int?    Height
    string? Link         // URL đích (nvarchar 200)
    int?    Status       // 2=Hiện, 1=Ẩn
    int?    SortOrder
    long?   AdsGroupId   // FK → cms.ADSGroup.Id
    // + Audit Trail
}
```

### cms.ADSGroup

```csharp
[Table("ADSGroup", Schema = "cms")]
public class ADSGroup
{
    long    Id
    string? Type         // Loại nhóm ADS (nvarchar 50)
    string? Name         // Tên nhóm (nvarchar 100)
    int?    Width
    int?    Height
    int?    Status
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### cms.Banner

```csharp
[Table("Banner", Schema = "cms")]
public class Banner
{
    long    Id
    string? Name         // Tên banner (nvarchar 100)
    string? Url          // URL ảnh banner (nvarchar 200)
    string? Link         // URL đích khi click (nvarchar 200)
    int?    Width
    int?    Height
    int?    SortOrder
    int?    Status       // 2=Hiện, 1=Ẩn
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### cms.Contact

```csharp
[Table("Contact", Schema = "cms")]
public class Contact
{
    long      Id
    string?   Name              // Họ tên người gửi
    string?   Email             // varchar 50
    string?   Content           // Nội dung liên hệ (nvarchar 1000)
    string?   Mobile            // varchar 50
    string?   Address           // nvarchar 200
    DateTime? CreatedDate       // Thời gian gửi
    int?      ContactGroupId    // FK → cms.ContactGroup.Id
    int?      Status
    string?   PortalId
    string?   Language
    string?   Title             // Tiêu đề/chủ đề liên hệ
    // + Audit Trail
}
```

### cms.ContactGroup

```csharp
[Table("ContactGroup", Schema = "cms")]
public class ContactGroup
{
    long    Id
    string? Name         // Tên nhóm (nvarchar 100)
    string? Email        // Email nhận liên hệ (varchar 50)
    int?    SortOrder    // default 0
    string? Description  // nvarchar 200
    int?    Status       // default 1
    string? PortalId
    string? Language     // default 'vie'
    // + Audit Trail
}
```

### cms.Counter

```csharp
[Table("Counter", Schema = "cms")]
public class Counter
{
    long      Id
    long?     Counter    // Số lượt truy cập
    DateTime? Submited   // Thời điểm ghi nhận
    string?   Ip         // IP người dùng (varchar 50)
    // + Audit Trail
}
```

### cms.Customer

```csharp
[Table("Customer", Schema = "cms")]
public class Customer
{
    long    Id
    string? Name         // nvarchar 150
    string? Address      // nvarchar 50
    string? Phone        // varchar 50
    // + Audit Trail
}
```

### cms.EventNews

```csharp
[Table("EventNews", Schema = "cms")]
public class EventNews
{
    long    Id
    string? Name         // Tên sự kiện (nvarchar 200)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### cms.Item (CmsItem)

```csharp
[Table("Item", Schema = "cms")]
public class CmsItem
{
    long    Id
    long?   ItemTypeId   // FK → cms.ItemType.Id
    string? Name         // nvarchar 50
    string? Content      // Nội dung ngắn (nvarchar 500)
    string? Link         // varchar 50
    int?    Status       // 2=Hiện, 1=Ẩn
    string? PortalId
    string? Language
    // + Audit Trail
}
```

**Lưu ý**: Class đặt tên `CmsItem` để tránh trùng với `Ebook.Item`.

### cms.ItemType

```csharp
[Table("ItemType", Schema = "cms")]
public class ItemType
{
    long    Id
    string? Name         // nvarchar 100
    string? Code         // Mã định danh (nvarchar 50)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### cms.Link

```csharp
[Table("Link", Schema = "cms")]
public class Link
{
    long    Id
    string? Name         // nvarchar 300
    string? Link         // URL (varchar 1000)
    string? Description  // nvarchar 200
    string? Images       // varchar 200
    int?    Status
    long?   LinkGroupId  // FK → cms.LinkGroup.Id
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### cms.LinkGroup

```csharp
[Table("LinkGroup", Schema = "cms")]
public class LinkGroup
{
    long    Id
    string? Name         // nvarchar 300
    int?    Status
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### cms.NewsComment

```csharp
[Table("NewsComment", Schema = "cms")]
public class NewsComment
{
    long      Id
    string?   Content      // Nội dung bình luận (nvarchar 4000)
    long?     NewsId        // FK → cms.news.Id
    string?   PortalId
    string?   Language
    string?   Email         // varchar 50
    string?   Name          // Tên người bình luận (nvarchar 50)
    string?   Title         // Tiêu đề bình luận (nvarchar 100)
    DateTime? CreatedDate
    int?      Status        // 2=Duyệt, 1=Chờ duyệt
    // + Audit Trail
}
```

### cms.Page

```csharp
[Table("Page", Schema = "cms")]
public class Page
{
    long    Id
    string? PageCode     // Mã trang (varchar 50)
    string? PageName     // Tên trang (nvarchar 100)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### cms.Roles

```csharp
[Table("Roles", Schema = "cms")]
public class Roles
{
    long    Id
    string? Name         // Tên vai trò (nvarchar 100)
    string? Code         // Mã vai trò (varchar 50)
    string? PortalId
    string? Language
    string? App          // Ứng dụng (varchar 100)
    // + Audit Trail
}
```

### cms.Supportonline

```csharp
[Table("Supportonline", Schema = "cms")]
public class Supportonline
{
    long    Id
    string? Name         // Tên nhân viên hỗ trợ (nvarchar 50)
    string? Yahoo        // varchar 50
    string? Skype        // varchar 50
    string? Facebook     // varchar 50
    string? Email        // varchar 50
    string? Phone        // varchar 50
    string? Mobile       // varchar 50
    string? PortalId
    string? Language
    string? Wellcome     // Tin nhắn chào (nvarchar 200)
    // + Audit Trail
}
```

### cms.Video

```csharp
[Table("Video", Schema = "cms")]
public class Video
{
    int     Id           // int (không phải bigint)
    string? Name         // nvarchar 500
    string? Link         // URL video (varchar 500)
    int     SortOrder    // NOT NULL
    string? Description  // ntext
    DateOnly Submited    // date NOT NULL
    int     Status       // NOT NULL
    string? PortalId
    string? Language
    string? Images       // varchar 500
    string? VideoType    // varchar 50
    // + Audit Trail
}
```

---

## Chi tiết Entity mới — Schema `dbo`

### dbo.ChucVu

```csharp
[Table("ChucVu", Schema = "dbo")]
public class ChucVu
{
    long    Id
    string? Name         // Tên chức vụ (nvarchar 100)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.Class

```csharp
[Table("Class", Schema = "dbo")]
public class Class
{
    long    Id
    string? Name         // Tên lớp (nvarchar 150)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.ConfigImportReader

```csharp
[Table("ConfigImportReader", Schema = "dbo")]
public class ConfigImportReader
{
    int     Id           // int (không phải bigint)
    string? Code         // Mã cột/trường import (nvarchar 50)
    string? Value        // Giá trị ánh xạ (varchar 50)
    // + Audit Trail (không có PK CONSTRAINT riêng trong SQL)
}
```

### dbo.Course

```csharp
[Table("Course", Schema = "dbo")]
public class Course
{
    long    Id
    string? Name         // Tên khóa/niên khóa (nvarchar 150)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.Currency

```csharp
[Table("Currency", Schema = "dbo")]
public class Currency
{
    long    Id
    string? Code         // Mã tiền tệ (nvarchar 100)
    string? Name         // Tên tiền tệ (nvarchar 150)
    double? ExchangeRate // Tỷ giá quy đổi
    int?    Status
    // + Audit Trail
}
```

### dbo.Degree

```csharp
[Table("Degree", Schema = "dbo")]
public class Degree
{
    long    Id
    string? Name         // Tên học vị (nvarchar 150)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.Department

```csharp
[Table("Department", Schema = "dbo")]
public class Department
{
    long    Id
    string? Name         // Tên phòng ban/đơn vị (nvarchar 200)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.Ethenic

```csharp
[Table("Ethenic", Schema = "dbo")]
public class Ethenic
{
    long    Id
    string? Name         // Tên dân tộc (nvarchar 250)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.GroupReader

```csharp
[Table("GroupReader", Schema = "dbo")]
public class GroupReader
{
    long    Id
    string? Name         // Tên nhóm bạn đọc (nvarchar 100)
    string? Code         // Mã nhóm (nvarchar 100)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.GroupUser

```csharp
[Table("GroupUser", Schema = "dbo")]
public class GroupUser
{
    long    Id
    string? Name         // Tên nhóm người dùng (nvarchar 100)
    string? Code         // Mã nhóm (nvarchar 100)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.Nation

```csharp
[Table("Nation", Schema = "dbo")]
public class Nation
{
    long    Id
    string? Name         // Tên quốc tịch (nvarchar 150)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.Org

```csharp
[Table("Org", Schema = "dbo")]
public class Org
{
    long    Id
    string? Name         // Tên tổ chức (nvarchar 300)
    long?   ParentId     // Tổ chức cha (cây)
    long?   Level        // Cấp độ trong cây
    int?    Status       // 2=Hoạt động, 1=Không hoạt động
    int?    Order        // Thứ tự
    string? PortalId
    string? Language     // nvarchar 50
    string? Link         // varchar 200
    // + Audit Trail
}
```

### dbo.Prof

```csharp
[Table("Prof", Schema = "dbo")]
public class Prof
{
    long    Id
    string? Name         // Tên chuyên ngành (nvarchar 250)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### dbo.Reader

```csharp
[Table("Reader", Schema = "dbo")]
public class Reader
{
    long      Id
    string?   FirstName               // Họ & tên đệm (nvarchar 50)
    string?   LastName                // Tên (nvarchar 100)
    string?   Cardno                  // Mã thẻ bạn đọc (varchar 50)
    string?   Email                   // varchar 50
    string?   Phone                   // varchar 50
    string?   Address                 // nvarchar 500
    long?     OrgId                   // FK → dbo.Org.Id
    long?     ReaderTypeId            // FK → dbo.ReaderType.Id
    long?     ClassId                 // FK → dbo.Class.Id
    long?     CourseId                // FK → dbo.Course.Id
    long?     DegreeId                // FK → dbo.Degree.Id
    long?     EthenicId               // FK → dbo.Ethenic.Id
    long?     ProfId                  // FK → dbo.Prof.Id
    double?   Blane                   // Số tiền còn nợ
    DateTime? CreatedDate             // Ngày làm thẻ
    DateTime? ExpireDate              // Ngày hết hạn thẻ
    DateTime? IssueDate               // Ngày cấp thẻ
    DateTime? BirthDate
    string?   Password                // varchar 150
    string?   PortalId
    string?   Language
    string?   Photo                   // Avatar URL (varchar 300)
    DateTime? LasttimeLogin
    DateTime? LastLogin
    DateTime? LastUpdate
    int?      Status                  // 1=Hoạt động (default), 2=Khoá
    int?      Sex                     // 0=nữ, 1=nam
    int?      RequiredChangePassword  // 1=bắt buộc đổi mật khẩu
    int?      IsChangePassword
    long?     CreatedBy
    long?     UpdatedBy
    // + Audit Trail
}
```

### dbo.ReaderDelete

```csharp
[Table("ReaderDelete", Schema = "dbo")]
public class ReaderDelete
{
    // Cùng cấu trúc với dbo.Reader — lưu bản ghi bạn đọc đã xóa
    long      Id
    string?   FirstName
    string?   LastName
    string?   Cardno
    // ... (đầy đủ các trường như Reader)
    // + Audit Trail
}
```

### dbo.ReaderInGroup

```csharp
[Table("ReaderInGroup", Schema = "dbo")]
public class ReaderInGroup
{
    long  Id
    long? ReaderId       // FK → dbo.Reader.Id
    long? GroupReaderId  // FK → dbo.GroupReader.Id
    // + Audit Trail
}
```

### dbo.ReaderTrackingLogin

```csharp
[Table("ReaderTrackingLogin", Schema = "dbo")]
public class ReaderTrackingLogin
{
    long      Id
    long?     ReaderId      // FK → dbo.Reader.Id
    string?   Cardnumber    // varchar 100
    DateTime? LoginTime
    DateTime? LogOutTime
    string?   Ip            // varchar 100
    string?   SessionId     // varchar 100
    // + Audit Trail (không có PK CONSTRAINT riêng)
}
```

### dbo.ReaderType

```csharp
[Table("ReaderType", Schema = "dbo")]
public class ReaderType
{
    long    Id
    string  Name         // NOT NULL (nvarchar 150)
    string  PortalId     // NOT NULL (varchar 100)
    string  Language     // NOT NULL (varchar 100)
    // + Audit Trail
}
```

### dbo.Sip2Log

```csharp
[Table("Sip2Log", Schema = "dbo")]
public class Sip2Log
{
    long      Id
    string?   Request    // nvarchar 1000
    string?   Response   // nvarchar 1000
    DateTime? Submited
    // + Audit Trail
}
```

### dbo.systemparameter (SystemParameter)

```csharp
[Table("systemparameter", Schema = "dbo")]  // tên bảng viết thường
public class SystemParameter
{
    long    Id
    string? Code            // Mã tham số (varchar 300)
    string? DescriptionVn   // Mô tả tiếng Việt (nvarchar 350)
    string? DescriptionEn   // Mô tả tiếng Anh (nvarchar 350)
    string? Value           // Giá trị (nvarchar 4000)
    string? PortalId        // nvarchar 50
    string? Type            // Loại tham số (varchar 50)
    string? Language
    // + Audit Trail
}
```

### dbo.UserLog

```csharp
[Table("UserLog", Schema = "dbo")]
public class UserLog
{
    long      Id
    long?     UserId        // FK → dbo.Users.Id
    string?   Action        // Hành động (ntext)
    DateTime? Submited
    string?   PortalId      // varchar 50
    string?   Ip            // varchar 50
    string?   Application   // Ứng dụng (nvarchar 50)
    string?   Object        // Đối tượng tác động (nvarchar 300)
    string?   ActionType    // Loại hành động (varchar 50)
    // + Audit Trail
}
```

---

## Chi tiết Entity mới — Schema `Ebook`

### Ebook.collection (EbookCollection)

```csharp
[Table("collection", Schema = "Ebook")]  // tên bảng viết thường
public class EbookCollection
{
    long    Id
    string? Name          // nvarchar 300
    long?   ParentId      // BST cha (cây)
    long?   Level
    int?    Status
    int?    SortOrder
    string? PortalId      // varchar 50
    string? Language      // nvarchar 50
    string? Link          // varchar 200
    int?    Allowdownload // 1=cho phép tải
    string? Images        // nvarchar 500
    // + Audit Trail
}
```

### Ebook.CollectionPermistionUser

```csharp
[Table("CollectionPermistionUser", Schema = "Ebook")]
public class CollectionPermistionUser
{
    long Id
    long? CollectionId  // FK → Ebook.collection.Id
    long? GroupUserId   // FK → dbo.GroupUser.Id
    int?  CanAdd
    int?  CanEdit
    int?  CanDelete
    int?  CanView
    // + Audit Trail
}
```

### Ebook.DigType

```csharp
[Table("DigType", Schema = "Ebook")]
public class DigType
{
    long    Id
    string? Code           // varchar 100
    string? DescriptionVn  // nvarchar 200
    string? DescriptionEn  // varchar 100
    string? PortalId
    int?    SortOrder
    string? Language
    // + Audit Trail
}
```

### Ebook.Ebook (EbookAccess)

```csharp
[Table("Ebook", Schema = "Ebook")]  // trùng tên schema, đặt class là EbookAccess
public class EbookAccess
{
    long      Id
    long?     ReaderId    // FK → dbo.Reader.Id
    string?   Cardnumber  // varchar 50
    DateTime? Submited    // Thời điểm truy cập
    long?     Bookid      // FK → Ebook.Item.Id
    int?      Page        // Trang đang đọc
    long?     Size        // Kích thước đã đọc (bytes)
    int?      Type        // Loại hành động (đọc/tải)
    string?   Ip          // varchar 50
    // + Audit Trail
}
```

### Ebook.EbookFile

```csharp
[Table("EbookFile", Schema = "Ebook")]
public class EbookFile
{
    long      Id
    string?   Url                // varchar 2000
    string?   Type               // Loại file (varchar 50)
    int?      IsConvert          // 1=đã convert
    long?     EbookId            // FK → Ebook.Item.Id
    DateTime? CreatedDate
    string?   FileType           // varchar 50
    double?   FileSize
    string?   FileExt            // Phần mở rộng (varchar 50)
    string?   Description        // nvarchar 100
    string?   Source             // Nguồn file (nvarchar 300)
    int?      FormatId
    string?   CheckSumAlgorithm  // varchar 50
    int?      SortOrder
    // + Audit Trail
}
```

### Ebook.EbookLog

```csharp
[Table("EbookLog", Schema = "Ebook")]
public class EbookLog
{
    // Cùng cấu trúc với EbookAccess — log chi tiết lần tải/xem
    long      Id
    long?     ReaderId
    string?   Cardnumber
    DateTime? Submited
    long?     Bookid
    int?      Page
    long?     Size
    int?      Type
    string?   Ip
    // + Audit Trail
}
```

### Ebook.IntroBookCategory

```csharp
[Table("IntroBookCategory", Schema = "Ebook")]
public class IntroBookCategory
{
    long    Id
    string? Name             // nvarchar 100
    long?   ParentId         // Danh mục cha (cây)
    long?   Level
    int?    Status
    int?    Order
    string? PortalId
    int?    IsLogin          // 1=yêu cầu đăng nhập
    string? Description      // nvarchar 150
    string? Keyword          // nvarchar 150
    string? PageTitle        // nvarchar 150
    string? MetaDescription  // nvarchar 150
    string? Language         // nvarchar 50
    string? Link             // nvarchar 200
    // + Audit Trail
}
```

### Ebook.IntroBooks

```csharp
[Table("IntroBooks", Schema = "Ebook")]
public class IntroBooks
{
    long      Id
    string    Title                // NOT NULL (nvarchar 500)
    string?   Brief                // Tóm tắt (ntext)
    string?   Noidung              // Nội dung đầy đủ (ntext)
    string?   Image                // varchar 400
    DateTime? Submited             // Ngày đăng
    string?   PortalId
    string?   Language
    int?      Order
    long?     IntroBookCategoryId  // FK → Ebook.IntroBookCategory.Id
    long?     BibId                // FK → PrintBook.Bib.Bibid
    // + Audit Trail
}
```

### Ebook.Item (EbookItem)

```csharp
[Table("Item", Schema = "Ebook")]  // đặt class là EbookItem để phân biệt
public class EbookItem
{
    long      Id
    DateTime? Submited        // Ngày tạo biểu ghi số
    long?     CreatedBy
    long?     UpdateBy
    long?     CollectionId    // FK → Ebook.collection.Id
    string?   Images          // varchar 200
    int?      TotalView
    int?      TotalDownload
    byte?     Status          // tinyint: 2=xuất bản
    long?     SubjectId       // FK → Ebook.Subject.Id
    DateTime? LastUpdate
    long?     TypeId          // FK → Ebook.DigType.Id
    int?      AllowDownload
    int?      Free            // 1=miễn phí
    long?     TopicId         // FK → Ebook.Topic.Id
    string?   PortalId
    string?   Language
    int?      Show            // 2=hiện (default)
    int?      IndexContent    // 1=đã index nội dung
    int?      Share           // 1=cho phép chia sẻ
    // + Audit Trail
}
```

### Ebook.itemXml (EbookItemXml)

```csharp
[Table("itemXml", Schema = "Ebook")]  // tên bảng viết thường
public class EbookItemXml
{
    long    Id           // PK, FK → Ebook.Item.Id (1:1)
    string? Title        // nvarchar 300
    string? Author       // nvarchar 200
    string? Publisher    // nvarchar 150
    string? PublishDate  // varchar 50
    string? Keyword      // nvarchar 300
    string? Xml          // Mô tả XML (nvarchar 2000)
    string? OtherTitle   // Nhan đề khác (nvarchar 300)
    string? Page         // Số trang (varchar 50)
    string? OldAuthor    // Tác giả bổ sung (nvarchar 200)
    // + Audit Trail
}
```

### Ebook.MetaDataFieldRegistery

```csharp
[Table("MetaDataFieldRegistery", Schema = "Ebook")]
public class MetaDataFieldRegistery
{
    long    MetaDataFieldId    // PK (không phải Id)
    long?   MetaDataSchemaId  // FK → Ebook.MetadataSchemaRegistry.MetadataSchemaId
    string? Field              // Trường MARC (varchar 100)
    string? Subfield           // Trường con (varchar 100)
    string? DescriptionVn      // nvarchar 200
    string? DescriptionEn      // varchar 300
    int?    SortOrder
    int?    Status
    string? Input              // Loại input (varchar 100)
    int?    ExportField        // 1=xuất ra file
    // + Audit Trail
}
```

### Ebook.MetadataSchemaRegistry

```csharp
[Table("MetadataSchemaRegistry", Schema = "Ebook")]
public class MetadataSchemaRegistry
{
    long    MetadataSchemaId  // PK (không phải Id)
    string? NameSpace         // Dublin Core, MARC21... (nvarchar 200)
    string? ShortId           // Tên viết tắt (varchar 200)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### Ebook.MetaDataValue

```csharp
[Table("MetaDataValue", Schema = "Ebook")]
public class MetaDataValue
{
    long    Id
    int?    MetaDataFieldId  // FK → Ebook.MetaDataFieldRegistery.MetaDataFieldId
    string? Value            // Giá trị metadata (nvarchar 3000)
    string? Language         // varchar 100
    long?   ItemId           // FK → Ebook.Item.Id
    string? Value_UnSign     // Giá trị không dấu (varchar 3000)
    int?    SortOrder
    // + Audit Trail
}
```

### Ebook.PolicyDigital

```csharp
[Table("PolicyDigital", Schema = "Ebook")]
public class PolicyDigital
{
    int  Id              // int (không phải bigint)
    int? ReaderTypeid    // FK → dbo.ReaderType.Id
    int? Maxpage         // Số trang tối đa/lần đọc
    float? Maxsize       // Kích thước tối đa (MB)
    int? Maxdocument     // Số tài liệu tối đa/ngày
    // + Audit Trail (không có PK CONSTRAINT riêng)
}
```

### Ebook.PolicyDigitalByCollection

```csharp
[Table("PolicyDigitalByCollection", Schema = "Ebook")]
public class PolicyDigitalByCollection
{
    int  Id              // int
    int? ReaderTypeid    // FK → dbo.ReaderType.Id
    int? CollectionId    // FK → Ebook.collection.Id
    int? Maxpage
    float? Maxsize
    int? Maxdocument
    int? Read            // 1=được phép đọc
    int? Comment         // 1=được phép bình luận
    int? Download        // 1=được phép tải
    // + Audit Trail (không có PK CONSTRAINT riêng)
}
```

### Ebook.Subject (EbookSubject)

```csharp
[Table("Subject", Schema = "Ebook")]
public class EbookSubject
{
    long    Id
    string? Name         // Tên môn loại (nvarchar 300)
    long?   ParentId     // Môn loại cha (cây DDC)
    long?   Level
    int?    Status
    int?    SortOrder
    string? DDC          // Ký hiệu phân loại DDC/UDC (varchar 50)
    string? PortalId
    string? Language     // nvarchar 50
    string? Link         // varchar 200
    // + Audit Trail
}
```

### Ebook.TheodoiBienmucEbook

```csharp
[Table("TheodoiBienmucEbook", Schema = "Ebook")]
public class TheodoiBienmucEbook
{
    long      Id
    int?      UserId      // FK → dbo.Users.Id
    DateTime? Submited    // Thời điểm tác động
    long?     DigId       // FK → Ebook.Item.Id
    string?   Status      // Trạng thái biên mục (varchar 50)
    // + Audit Trail
}
```

### Ebook.Topic (EbookTopic)

```csharp
[Table("Topic", Schema = "Ebook")]
public class EbookTopic
{
    long    Id
    string? Name             // nvarchar 100
    long?   ParentId         // Chủ đề cha (cây)
    long?   Level
    int?    Status
    int?    Order
    string? PortalId
    int?    IsLogin
    string? Description      // nvarchar 150
    string? Keyword          // nvarchar 150
    string? PageTitle        // nvarchar 150
    string? MetaDescription  // nvarchar 150
    string? Language         // nvarchar 50
    string? DDC              // varchar 50
    // + Audit Trail
}
```

---

## Chi tiết Entity mới — Schema `EOffice`

### EOffice.Agency

```csharp
[Table("Agency", Schema = "EOffice")]
public class Agency
{
    long    Id
    string? Name         // Tên cơ quan (nvarchar 500)
    long?   ParentId     // Cơ quan cha (cây)
    long?   Level
    int?    Status
    int?    Order
    string? PortalId
    string? Language     // nvarchar 50
    string? Link         // varchar 200
    // + Audit Trail
}
```

### EOffice.Document

```csharp
[Table("Document", Schema = "EOffice")]
public class Document
{
    long      Id
    string?   Name             // Tên văn bản (nvarchar 1000)
    string?   Brief            // Trích yếu (nvarchar 2000)
    long?     DocumentTypeId   // FK → EOffice.DocumentType.Id
    long?     AgencyId         // FK → EOffice.Agency.Id
    DateTime? IssueDate        // Ngày ban hành
    DateTime? ExpireDate       // Ngày hết hiệu lực
    DateTime? CreatedDate
    int?      TypeId           // Loại văn bản phụ
    int?      Status           // 2=Hiện, 1=Ẩn
    string?   Sign             // Số/Ký hiệu văn bản (nvarchar 100)
    long?     TopicId          // FK → EOffice.Topic.Id
    string?   GovDocNumber     // Số văn bản nhà nước (nvarchar 100)
    string?   PortalId
    string?   Language
    // + Audit Trail
}
```

### EOffice.DocumentFile

```csharp
[Table("DocumentFile", Schema = "EOffice")]
public class DocumentFile
{
    long    Id
    string? Name         // Tên file (nvarchar 500)
    string? Url          // varchar 500
    double? FileSize
    long?   DocumentId   // FK → EOffice.Document.Id
    // + Audit Trail
}
```

### EOffice.DocumentType

```csharp
[Table("DocumentType", Schema = "EOffice")]
public class DocumentType
{
    long    Id
    string? Name         // Tên loại văn bản (nvarchar 50)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### EOffice.Topic (EofficeTopic)

```csharp
[Table("Topic", Schema = "EOffice")]
public class EofficeTopic
{
    long    Id
    string? Name         // Tên chủ đề (nvarchar 500)
    long?   ParentId     // Chủ đề cha (cây)
    long?   Level
    int?    Status
    int?    Order
    string? PortalId
    string? Language     // nvarchar 50
    string? Link         // varchar 200
    // + Audit Trail
}
```

---

## Chi tiết Entity mới — Schema `Evaluate`

### Evaluate.Course (EvaluateCourse)

```csharp
[Table("Course", Schema = "Evaluate")]
public class EvaluateCourse
{
    long    Id
    string? Code             // Mã học phần (nvarchar 100)
    string? Name             // Tên học phần (nvarchar 1000)
    int?    Credit           // Số tín chỉ
    int?    Status
    long?   DegreeId         // FK → Evaluate.Degree.Id
    long?   OptionCourseId   // FK → Evaluate.CourseOption.Id
    string? KnowledgeId      // nchar(10) — mã khối kiến thức
    string? FileUrl          // URL đề cương (nvarchar 1000)
    // + Audit Trail
}
```

### Evaluate.CourseOption

```csharp
[Table("CourseOption", Schema = "Evaluate")]
public class CourseOption
{
    long    Id
    string? Name         // Tên loại học phần (nvarchar 500)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### Evaluate.Degree (EvaluateDegree)

```csharp
[Table("Degree", Schema = "Evaluate")]
public class EvaluateDegree
{
    long    Id
    string? Name         // Tên trình độ đào tạo (nvarchar 150)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### Evaluate.Knowledge

```csharp
[Table("Knowledge", Schema = "Evaluate")]
public class Knowledge
{
    long    Id
    string? Name         // Tên khối kiến thức (nvarchar 500)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### Evaluate.MonHoc

```csharp
[Table("MonHoc", Schema = "Evaluate")]
public class MonHoc
{
    long    Id
    string? MaMon           // Mã môn học (nvarchar 200)
    string? TenMon          // Tên môn học (nvarchar 500)
    int?    SoTinChi        // Số tín chỉ
    long?   DegreeId        // FK → Evaluate.Degree.Id
    long?   KnowledgeId     // FK → Evaluate.Knowledge.Id
    int?    OptionId        // FK → Evaluate.CourseOption.Id
    string? NguoiBienSoan   // Người biên soạn (nvarchar 300)
    int?    Active          // 1=đang áp dụng
    string? Attachment      // File đính kèm (nvarchar 500)
    string? Note            // Ghi chú (nvarchar 700)
    // + Audit Trail
}
```

### Evaluate.NganhHoc

```csharp
[Table("NganhHoc", Schema = "Evaluate")]
public class NganhHoc
{
    long    Id
    string? MajorsName     // Tên ngành (nvarchar 1000)
    long?   ParentId       // Ngành cha (cây)
    long?   ProgramId      // FK → Evaluate.Program.Id
    long?   AmountStudent  // Số sinh viên
    int?    SortOrder
    string? PortalId
    string? Language
    string? MajorsCode     // Mã ngành (nvarchar 200)
    int?    Status
    // + Audit Trail
}
```

### Evaluate.NganhMonHoc

```csharp
[Table("NganhMonHoc", Schema = "Evaluate")]
public class NganhMonHoc
{
    long  Id
    long? MajorId    // FK → Evaluate.NganhHoc.Id
    long? MonHocId   // FK → Evaluate.MonHoc.Id
    // + Audit Trail
}
```

### Evaluate.Program (EvaluateProgram)

```csharp
[Table("Program", Schema = "Evaluate")]
public class EvaluateProgram
{
    long    Id
    string? Name          // Tên chương trình đào tạo (nvarchar 1000)
    string? Description   // Mô tả (nvarchar 4000)
    string? PortalId
    string? Language
    // + Audit Trail
}
```

### Evaluate.TaiLieu

```csharp
[Table("TaiLieu", Schema = "Evaluate")]
public class TaiLieu
{
    long    Id
    long?   BibId           // FK → PrintBook.Bib.Bibid
    string? Title           // Tên tài liệu (nvarchar 1000)
    string? Author          // Tác giả (nvarchar 500)
    string? Publisher       // Nhà xuất bản (nvarchar 500)
    string? Url             // nvarchar 500
    string? PublishDate     // Năm XB (varchar 50)
    int?    LoaiTaiLieu     // 1=sách in, 2=ebook, 3=web...
    string? LanXuatBan      // Lần xuất bản (varchar 50)
    string? Note            // nvarchar 500
    long?   EBookId         // FK → Ebook.Item.Id
    long?   MonHocId        // FK → Evaluate.MonHoc.Id
    // + Audit Trail
}
```

---

## Chi tiết Entity mới — Schema `PrintBook`

### PrintBook.aacr2_field (Aacr2Field)

```csharp
[Table("aacr2_field", Schema = "PrintBook")]  // tên bảng viết thường
public class Aacr2Field
{
    int     Id           // int
    int?    Config_Id    // FK → cấu hình biên mục
    string? Field        // Trường MARC (varchar 50)
    string? Starttp      // varchar 50
    string? Stoptp       // varchar 50
    int?    Fieldindex
    // + Audit Trail
}
```

### PrintBook.aacr2_subfield (Aacr2Subfield)

```csharp
[Table("aacr2_subfield", Schema = "PrintBook")]
public class Aacr2Subfield
{
    int     Id
    int?    Config_Id
    string? Field        // varchar 50
    string? Subfield     // varchar 50
    string? Starttp
    string? Stoptp
    string? Nexttp
    int?    Subfieldindex
    // + Audit Trail
}
```

### PrintBook.ab_deliverer (AbDeliverer)

```csharp
[Table("ab_deliverer", Schema = "PrintBook")]
public class AbDeliverer
{
    long      Id
    long?     Code                // Mã phiếu
    string?   ReceiptName         // Nơi nhận (nvarchar 30)
    string?   ReceiptAddress      // Địa chỉ nhận (nvarchar 150)
    string?   DelivererName       // Người giao (nvarchar 50)
    string?   DelivererAddress    // Địa chỉ giao (nvarchar 150)
    DateTime? DelivererDate       // Ngày giao
    DateTime? ReceiptDate         // Ngày nhận
    int?      UserIddeliverer     // UserId người giao
    int?      UserIdReceipt       // UserId người nhận
    int?      Status
    int?      Store_Id            // Kho nguồn
    int?      Receipt_Id          // FK → ab_receipt.Id
    string?   Note                // nvarchar 300
    int?      Sign                // 1=đã ký (default)
    // + Audit Trail
}
```

### PrintBook.ab_deliverer_detail (AbDelivererDetail)

```csharp
[Table("ab_deliverer_detail", Schema = "PrintBook")]
public class AbDelivererDetail
{
    long  Id
    long? Deliverer_Id  // FK → PrintBook.ab_deliverer.Id
    long? BarcodeId     // FK → PrintBook.Barcode.Id
    int?  Store_Id
    // + Audit Trail
}
```

### PrintBook.ab_deliverer_status (AbDelivererStatus)

```csharp
[Table("ab_deliverer_status", Schema = "PrintBook")]
public class AbDelivererStatus
{
    int     Id
    string? Name  // Tên trạng thái (nvarchar 100)
    // + Audit Trail
}
```

### PrintBook.ab_move (AbMove)

```csharp
[Table("ab_move", Schema = "PrintBook")]
public class AbMove
{
    long      Id
    long?     Code
    string?   ReceiptName
    string?   ReceiptAddress
    string?   DelivererName
    string?   DelivererAddress
    DateTime? DelivererDate
    DateTime? ReceiptDate
    int?      UserIddeliverer
    int?      UserIdReceipt
    int?      Status
    int?      StoreDeliver_Id  // Kho nguồn
    int?      StoreReceipt_Id  // Kho đích
    string?   Note
    // + Audit Trail (không có PK CONSTRAINT riêng)
}
```

### PrintBook.ab_move_detail (AbMoveDetail)

```csharp
[Table("ab_move_detail", Schema = "PrintBook")]
public class AbMoveDetail
{
    long  Id
    long? Move_Id    // FK → PrintBook.ab_move.Id
    long? BarcodeId  // FK → PrintBook.Barcode.Id
    int?  Store_Id
    // + Audit Trail (không có PK CONSTRAINT riêng)
}
```

### PrintBook.ab_order (AbOrder)

```csharp
[Table("ab_order", Schema = "PrintBook")]
public class AbOrder
{
    long      Id
    DateTime? Date_Order       // Ngày đặt mua
    DateTime? Duedate          // Hạn giao sách
    long?     Source_Id        // FK → ab_source.Id
    int?      Supplier_Id      // Nhà cung cấp
    int?      BUDGET_ID        // Nguồn kinh phí
    long?     CREATED_BY
    string?   Order_Name       // Tên phiếu đặt mua (nvarchar 200)
    long?     Code             // Mã phiếu
    int?      Status
    string?   Note             // nvarchar 300
    DateTime? CreatedDate
    int?      PaymentMethodId  // Phương thức thanh toán
    long?     FundId
    int?      Payment_status
    // + Audit Trail
}
```

### PrintBook.ab_order_detail (AbOrderDetail)

```csharp
[Table("ab_order_detail", Schema = "PrintBook")]
public class AbOrderDetail
{
    long    Id
    long?   Order_Id       // FK → PrintBook.ab_order.Id
    long?   Bibid          // FK → PrintBook.Bib.Bibid
    int?    Amount         // Số lượng đặt
    double? Price
    string? CURRENCY       // varchar 50
    double? Rate           // Tỷ giá
    string? Cancel_Reson   // Lý do huỷ (nvarchar 200)
    // + Audit Trail
}
```

### PrintBook.ab_receipt (AbReceipt)

```csharp
[Table("ab_receipt", Schema = "PrintBook")]
public class AbReceipt
{
    long      Id
    DateTime? Receipt_Date    // Ngày nhập kho
    long?     Source_Id       // FK → ab_source.Id
    long?     Supplier_Id
    long?     BUDGET_ID
    long?     CREATED_BY
    string?   Receipt_Name    // Tên phiếu nhập (nvarchar 200)
    long?     Code
    int?      Status
    int?      Payment_Status
    string?   Note
    long?     Store_Id        // Kho nhập
    DateTime? CreatedDate
    int?      PaymentMethodId
    long?     FundId
    // + Audit Trail
}
```

### PrintBook.ab_receipt_detail (AbReceiptDetail)

```csharp
[Table("ab_receipt_detail", Schema = "PrintBook")]
public class AbReceiptDetail
{
    long      Id
    long?     Receipt_Id  // FK → PrintBook.ab_receipt.Id
    long?     Bibid        // FK → PrintBook.Bib.Bibid
    int?      Amount       // Số lượng nhập
    double?   Price
    string?   CURRENCY
    double?   Rate
    long?     Order_Id     // FK → ab_order.Id (nhập theo đặt mua)
    DateTime? Submited
    // + Audit Trail
}
```

### PrintBook.ab_source (AbSource)

```csharp
[Table("ab_source", Schema = "PrintBook")]
public class AbSource
{
    int     Id
    string? Name  // Nguồn bổ sung (nvarchar 100)
    // + Audit Trail
}
```

### PrintBook.ah_receipt (AhReceipt)

```csharp
[Table("ah_receipt", Schema = "PrintBook")]
public class AhReceipt
{
    // Cùng cấu trúc với ab_receipt — dùng cho nhập sách hiến tặng/trao đổi
    long      Id
    DateTime? Receipt_Date
    long?     Source_Id
    long?     Supplier_Id
    long?     BUDGET_ID
    long?     CREATED_BY
    string?   Receipt_Name
    long?     Code
    int?      Status
    int?      Payment_Status
    string?   Note
    long?     Store_Id
    DateTime? CreatedDate
    int?      PaymentMethodId
    long?     FundId
    // + Audit Trail
}
```

### PrintBook.Barcode

```csharp
[Table("Barcode", Schema = "PrintBook")]
public class Barcode
{
    long    Id
    string? Barcode        // Mã barcode (nvarchar 50)
    int?    BarcodeNumber  // Số thứ tự barcode (default 0)
    int?    Store          // Kho lưu trữ
    long?   BibId          // FK → PrintBook.Bib.Bibid
    string? Status         // Trạng thái (varchar 50)
    string? Old_Status     // Trạng thái cũ (varchar 50)
    long?   Receipt_Id     // FK → ab_receipt.Id
    // + Audit Trail
}
```

### PrintBook.Barcode_Status (BarcodeStatus)

```csharp
[Table("Barcode_Status", Schema = "PrintBook")]
public class BarcodeStatus
{
    string  Id             // varchar(5) — PK dạng string (vd: "LOAN", "AVAIL")
    string? CommentStatus  // Mô tả trạng thái (nvarchar 100)
    // + Audit Trail
}
```

### PrintBook.Bib

```csharp
[Table("Bib", Schema = "PrintBook")]
public class Bib
{
    long      Bibid              // PK (không phải Id)
    long?     Mfn                // Machine-readable number
    DateTime? CreatedTime
    DateTime? UpdateTime
    long?     CreatedBy
    long?     UpdateBy
    string?   Status             // varchar 10
    long?     Bib_worksheet_id   // FK → PrintBook.bib_worksheet.Id
    long?     Bib_type_id        // FK → PrintBook.Bib_Type.Id
    string?   MARC_STATUS        // varchar 50
    string?   Url                // URL ảnh bìa (nvarchar 600)
    string?   Images             // nvarchar 600
    long?     EbookId            // FK → Ebook.Item.Id
    long?     DocNum             // Số tài liệu
    // + Audit Trail
}
```

### PrintBook.Bib_Type (BibType)

```csharp
[Table("Bib_Type", Schema = "PrintBook")]
public class BibType
{
    long    Id
    string? Name              // Tên loại biểu ghi (nvarchar 250)
    string? Code              // Mã loại (varchar 50)
    string? Bib_Level         // Bibliographic level (varchar 50)
    string? Record_Type_Code  // varchar 50
    string? Materal_Type      // Loại vật mang tin (varchar 50)
    string? Type              // varchar 50
    // + Audit Trail
}
```

### PrintBook.bib_worksheet (BibWorksheet)

```csharp
[Table("bib_worksheet", Schema = "PrintBook")]  // tên bảng viết thường
public class BibWorksheet
{
    int     Id
    string? Name          // Tên mẫu biên mục (nvarchar 250)
    string? Usmarc        // USMARC format (nvarchar 500)
    int?    Bib_Type_Id   // FK → PrintBook.Bib_Type.Id
    // + Audit Trail
}
```

### PrintBook.BibData

```csharp
[Table("BibData", Schema = "PrintBook")]
public class BibData
{
    long    BibDataId     // PK (không phải Id)
    long?   BibId         // FK → PrintBook.Bib.Bibid
    string? Field         // Trường MARC (nvarchar 3)
    string? SubField      // Trường con (nvarchar 1)
    string? Data          // Giá trị (nvarchar 4000)
    string? Fk            // Foreign key MARC (nvarchar 36)
    string? L1            // Indicator 1 (nvarchar 1)
    string? L2            // Indicator 2 (nvarchar 1)
    string? Title         // Tên trường (nvarchar 255)
    string? DataUnsign    // Giá trị không dấu (nvarchar 4000)
    // + Audit Trail
}
```

### PrintBook.BibDataOrder

```csharp
[Table("BibDataOrder", Schema = "PrintBook")]
public class BibDataOrder
{
    // Cùng cấu trúc với BibData — dùng cho quy trình đặt mua
    long    BibDataId
    long?   BibId        // FK → PrintBook.BibOrder.Bibid
    string? Field
    string? SubField
    string? Data
    string? Fk
    string? L1
    string? L2
    string? Title
    string? DataUnsign
    // + Audit Trail
}
```

### PrintBook.BibOrder

```csharp
[Table("BibOrder", Schema = "PrintBook")]
public class BibOrder
{
    // Cùng cấu trúc với Bib — biểu ghi tạm trong quy trình đặt mua
    long      Bibid           // PK
    long?     Mfn
    DateTime? CreatedTime
    DateTime? UpdateTime
    long?     CreatedBy
    long?     UpdateBy
    string?   Status
    long?     Bib_Worksheet_Id
    long?     Bib_Type_Id
    string?   MARC_STATUS
    string?   Url
    string?   Images
    // + Audit Trail
}
```

### PrintBook.BibXML

```csharp
[Table("BibXML", Schema = "PrintBook")]
public class BibXML
{
    long    BibId              // PK, FK → PrintBook.Bib.Bibid (1:1)
    string? Title              // Nhan đề (nvarchar 2000)
    string? Author             // Tác giả (nvarchar 255)
    string? Publisher          // NXB (nvarchar 255)
    string? PublishDate        // nvarchar 255
    string? Price              // Giá (nvarchar 36)
    string? Page               // Số trang (nvarchar 255)
    int?    Bib_Worksheet_Id
    int?    Bib_Type_Id
    string? Isbd               // Mô tả ISBD (nvarchar 4000)
    string? Accr2              // Mô tả AACR2 (nvarchar 4000)
    string? Keyword            // Từ khoá (nvarchar 300)
    int?    UserId
    string? DDC                // Ký hiệu DDC (nvarchar 300)
    // + Audit Trail
}
```

### PrintBook.BibXMLOrder

```csharp
[Table("BibXMLOrder", Schema = "PrintBook")]
public class BibXMLOrder
{
    // Cùng cấu trúc với BibXML — dùng cho quy trình đặt mua
    long    BibId              // PK, FK → PrintBook.BibOrder.Bibid (1:1)
    string? Title
    string? Author
    string? Publisher
    string? PublishDate
    string? Price
    string? Page
    int?    Bib_Worksheet_Id
    int?    Bib_Type_Id
    string? Isbd
    string? Accr2
    string? Keyword
    int?    UserId
    string? DDC
    // + Audit Trail
}
```

### PrintBook.Book_Group (BookGroup)

```csharp
[Table("Book_Group", Schema = "PrintBook")]
public class BookGroup
{
    long  Id
    long? Bib_Id1    // FK → PrintBook.Bib.Bibid (sách 1)
    long? Bib_Id2    // FK → PrintBook.Bib.Bibid (sách 2)
    int?  Link       // Kiểu liên kết
    int?  Mode
    long? Fieldid    // FK → trường MARC liên kết
    // + Audit Trail
}
```

### PrintBook.book_group_detail (BookGroupDetail)

```csharp
[Table("book_group_detail", Schema = "PrintBook")]
public class BookGroupDetail
{
    long  Id
    long? Book_Group_Id  // FK → PrintBook.Book_Group.Id
    long? Bibid          // FK → PrintBook.Bib.Bibid
    // + Audit Trail
}
```

### PrintBook.BookIn

```csharp
[Table("BookIn", Schema = "PrintBook")]
public class BookIn
{
    long      Id
    long?     ReaderId    // FK → dbo.Reader.Id
    string?   Barcode     // Mã barcode trả
    DateTime? BorrowDate  // Ngày mượn gốc
    DateTime? DueDate     // Hạn trả
    DateTime? ReturnDate  // Ngày trả thực tế
    int?      UserId      // Thủ thư nhận trả
    string?   Note
    long?     BookOutId   // FK → PrintBook.BookOut.Id
    int?      Renew       // Số lần gia hạn
    long?     CircPlace   // FK → PrintBook.CircPlace.Id
    long?     StoreId     // FK → PrintBook.Store.Id
    double?   FineValue   // Tiền phạt (nếu có)
    // + Audit Trail
}
```

### PrintBook.BookOut

```csharp
[Table("BookOut", Schema = "PrintBook")]
public class BookOut
{
    long      Id
    long?     ReaderId    // FK → dbo.Reader.Id
    DateTime? BorrowDate
    DateTime? DueDate     // Hạn trả
    long?     UserId      // Thủ thư cho mượn
    int?      Renew       // Số lần đã gia hạn
    string?   Note
    int?      CircPlace   // FK → PrintBook.CircPlace.Id
    string?   Barcode     // Mã barcode mượn
    long?     RequestId   // FK → PrintBook.BookRequest.Id
    string?   Status      // Trạng thái mượn (varchar 50)
    long?     Reg_Seq_Id  // Sequence đăng ký
    long?     Store       // FK → PrintBook.Store.Id
    long?     Update_By   // UserId cập nhật
    double?   FineValue
    // + Audit Trail
}
```

### PrintBook.BookRequest

```csharp
[Table("BookRequest", Schema = "PrintBook")]
public class BookRequest
{
    long      Id
    long?     ReaderId    // FK → dbo.Reader.Id
    DateTime? BorrowDate
    DateTime? DueDate
    string?   Note
    int?      CircPlace   // FK → PrintBook.CircPlace.Id
    string?   Barcode
    string?   Status      // varchar 50
    long?     UserId
    double?   FineValue
    // + Audit Trail
}
```

### PrintBook.Budget

```csharp
[Table("Budget", Schema = "PrintBook")]
public class Budget
{
    long      Id
    string?   Name       // Tên nguồn kinh phí
    double?   Blance     // Số dư
    DateTime? StartTime
    DateTime? EndTime
    string?   Note
    int?      Status
    // + Audit Trail
}
```

### PrintBook.C_Fine (CFine)

```csharp
[Table("C_Fine", Schema = "PrintBook")]
public class CFine
{
    long      Id
    long      ReaderId        // NOT NULL — FK → dbo.Reader.Id
    DateTime  FineDate        // NOT NULL — Ngày phạt
    string    Fine_type_id    // NOT NULL — FK → PrintBook.C_Fine_type.Code
    DateTime? Returndate
    string?   Note
    double?   Value           // Số tiền phạt
    int?      Fine_method_id  // FK → PrintBook.c_fine_method.Id
    long?     Borrow_Id       // FK → PrintBook.BookOut.Id
    DateTime? BorrowDate
    int?      Created_by
    string?   Barcode
    int?      Lanphat         // Lần phạt
    long?     Bibid
    // + Audit Trail
}
```

### PrintBook.c_fine_method (CFineMethod)

```csharp
[Table("c_fine_method", Schema = "PrintBook")]
public class CFineMethod
{
    int     Id
    string? Name  // Tên phương thức thu phạt
    // + Audit Trail
}
```

### PrintBook.C_Fine_type (CFineType)

```csharp
[Table("C_Fine_type", Schema = "PrintBook")]
public class CFineType
{
    long    Id              // PK (bigint IDENTITY)
    string  Code            // Business key (varchar 50 NOT NULL)
    string? Name
    string? Status_Reg_Id   // FK → trạng thái đăng ký (varchar 50)
    // + Audit Trail
}
```

### PrintBook.C_photo (CPhoto)

```csharp
[Table("C_photo", Schema = "PrintBook")]
public class CPhoto
{
    long      Id
    string?   Barcode     // Barcode tài liệu photo (varchar 50)
    long?     Reader_Id   // FK → dbo.Reader.Id
    int?      Frompage    // Trang bắt đầu
    int?      ToPage      // Trang kết thúc
    double?   Price
    DateTime? PhotoDate
    int?      Copy        // Số bản copy
    int?      Status
    // + Audit Trail
}
```

### PrintBook.c_queue_status (CQueueStatus)

```csharp
[Table("c_queue_status", Schema = "PrintBook")]
public class CQueueStatus
{
    int     Id
    int?    Type    // Loại hàng đợi
    string? Status  // Trạng thái
    // + Audit Trail
}
```

### PrintBook.C_renew (CRenew)

```csharp
[Table("C_renew", Schema = "PrintBook")]
public class CRenew
{
    long      Id
    long?     Borrow_id          // FK → PrintBook.BookOut.Id
    long?     Reg_Seq_Id
    string?   Reg_Id             // varchar 50
    int?      Circ_Place_Id      // FK → PrintBook.CircPlace.Id
    int?      Status_Id
    DateTime? Renew_Date         // Ngày gia hạn
    DateTime? Duedate_Request    // Hạn mới yêu cầu
    int?      Active
    int?      Status_Email       // Đã gửi email xác nhận
    int?      Renew_Date_Num     // Số lần gia hạn
    DateTime? Borrow_Date
    DateTime? Duedate
    long?     Reader_Id
    // + Audit Trail
}
```

### PrintBook.C_renew_data (CRenewData)

```csharp
[Table("C_renew_data", Schema = "PrintBook")]
public class CRenewData
{
    long      Id
    long?     Borrow_id      // FK → PrintBook.BookOut.Id
    long?     Reg_Seq_Id
    string?   Reg_Id         // varchar 50
    DateTime? Renew_Date
    DateTime? Due_Date_Old
    DateTime? Due_Date_New
    DateTime? Borrow_Date
    long?     Reader_Id
    int?      Created_By
    // + Audit Trail
}
```

### PrintBook.Cabinet

```csharp
[Table("Cabinet", Schema = "PrintBook")]
public class Cabinet
{
    long    Id
    string? Name
    string? Code         // varchar 50
    long?   CircPlaceId  // FK → PrintBook.CircPlace.Id
    int?    Status
    string? Note
    // + Audit Trail
}
```

### PrintBook.CheckIn

```csharp
[Table("CheckIn", Schema = "PrintBook")]
public class CheckIn
{
    long      Id
    long?     Readerid      // FK → dbo.Reader.Id
    long?     UserId
    DateTime? CheckInTime
    long?     StoreId       // FK → PrintBook.Store.Id
    // + Audit Trail
}
```

### PrintBook.CheckOut

```csharp
[Table("CheckOut", Schema = "PrintBook")]
public class CheckOut
{
    long      Id
    long?     ReaderId      // FK → dbo.Reader.Id
    long?     UserId
    DateTime? CheckInTime
    DateTime? CheckOutTime
    long?     StoreId       // FK → PrintBook.Store.Id
    long?     CheckInId     // FK → PrintBook.CheckIn.Id
    // + Audit Trail
}
```

### PrintBook.CircPlace

```csharp
[Table("CircPlace", Schema = "PrintBook")]
public class CircPlace
{
    int     Id
    string? Name
    string? Work_Session              // Giờ mở cửa (nvarchar 200)
    int?    Cir_Type                  // Loại lưu hành
    int?    Cir_Work_Follow
    int?    Access_Request_Validtime  // Thời gian hiệu lực đặt chỗ (ngày)
    int?    Limit_Book                // Số sách tối đa mượn 1 lần
    int?    Vitual                    // 1=ảo (không cần vật lý)
    // + Audit Trail
}
```

### PrintBook.config_aacr2 (ConfigAacr2)

```csharp
[Table("config_aacr2", Schema = "PrintBook")]
public class ConfigAacr2
{
    int  Id
    int? Bib_Type_Id   // FK → PrintBook.Bib_Type.Id
    int? Used_Type_Id
    // + Audit Trail
}
```

### PrintBook.Config_Isbd (ConfigIsbd)

```csharp
[Table("Config_Isbd", Schema = "PrintBook")]
public class ConfigIsbd
{
    int  Id
    int? Bib_Type_Id  // FK → PrintBook.Bib_Type.Id
    // + Audit Trail
}
```

### PrintBook.Countries

```csharp
[Table("Countries", Schema = "PrintBook")]
public class Countries
{
    long    Id
    string? Code            // varchar 50
    string? VnDescription
    string? Description     // varchar 100
    string? Region          // varchar 50
    // + Audit Trail
}
```

### PrintBook.d_bib_status (DBibStatus)

```csharp
[Table("d_bib_status", Schema = "PrintBook")]
public class DBibStatus
{
    string  Id          // PK varchar — vd: "CXL", "LOAN"
    string? Name
    string? Opac_Name   // Tên hiển thị OPAC
    string? English
    // + Audit Trail
}
```

**Lưu ý**: PK là `string` (varchar), không phải `long`.

### PrintBook.d_key (DKey)

```csharp
[Table("d_key", Schema = "PrintBook")]
public class DKey
{
    int     Id
    string? Keyword
    string? Description
    // + Audit Trail
}
```

### PrintBook.D_Publisher (DPublisher)

```csharp
[Table("D_Publisher", Schema = "PrintBook")]
public class DPublisher
{
    int     ID         // PK (viết hoa)
    string? Publisher
    string? Place
    // + Audit Trail
}
```

### PrintBook.DFixField

```csharp
[Table("DFixField", Schema = "PrintBook")]
public class DFixField
{
    long    Id
    string? VnDescription
    string? Description     // varchar 200
    string? Field           // varchar 3
    long?   Marc_Type_Id    // FK → PrintBook.MarcType.Id
    long?   Material_Type_Id
    // + Audit Trail
}
```

### PrintBook.DFixFieldPost

```csharp
[Table("DFixFieldPost", Schema = "PrintBook")]
public class DFixFieldPost
{
    long    Id
    int?    PostNumber   // Vị trí bắt đầu
    int?    PostLengh    // Độ dài
    long?   FixFieldId   // FK → PrintBook.DFixField.Id
    string? VnDesciption // (lỗi chính tả trong DB — giữ nguyên)
    string? Description  // varchar 200
    // + Audit Trail
}
```

### PrintBook.DFixFieldValue

```csharp
[Table("DFixFieldValue", Schema = "PrintBook")]
public class DFixFieldValue
{
    long    Id
    long?   FixFieldPostId  // FK → PrintBook.DFixFieldPost.Id
    string? Value
    string? VnDescription
    string? Description
    // + Audit Trail
}
```

### PrintBook.DicAuthor

```csharp
[Table("DicAuthor", Schema = "PrintBook")]
public class DicAuthor
{
    long    Id
    string? DisplayName  // Tên hiển thị
    string? AccessName   // Tên tra cứu (chuẩn hóa)
    string? UnsignName   // Tên không dấu
    string? PortalId     // varchar 50
    string? Language     // varchar 50
    // + Audit Trail
}
```

### PrintBook.DicClass

```csharp
[Table("DicClass", Schema = "PrintBook")]
public class DicClass
{
    long    Id
    long?   ParentId      // Cha trong cây phân loại
    string? Type          // varchar 50
    string? Code          // Ký hiệu phân loại (varchar 50)
    string? VnDescription
    string? Description   // varchar 200
    // + Audit Trail
}
```

### PrintBook.DicKeyword

```csharp
[Table("DicKeyword", Schema = "PrintBook")]
public class DicKeyword
{
    long    Id
    string? DisplayName
    string? AccessName
    string? UnsignName  // varchar 300
    string? PortalId    // varchar 50
    string? Language    // varchar 50
    // + Audit Trail
}
```

### PrintBook.DicPublisher

```csharp
[Table("DicPublisher", Schema = "PrintBook")]
public class DicPublisher
{
    long    Id
    string? DisplayName
    string? AccessName
    string? UnsignName  // varchar 300
    string? PortalId    // varchar 50
    string? Language    // varchar 50
    // + Audit Trail
}
```

### PrintBook.fixed_field_value (FixedFieldValue)

```csharp
[Table("fixed_field_value", Schema = "PrintBook")]
public class FixedFieldValue
{
    long    Id
    long?   Bibid  // FK → PrintBook.Bib.Bibid
    string? Field  // varchar 5
    string? Value  // varchar 50
    // + Audit Trail
}
```

### PrintBook.FrequencyMagazine

```csharp
[Table("FrequencyMagazine", Schema = "PrintBook")]
public class FrequencyMagazine
{
    long    Id
    string? Name
    long?   DV             // Đơn vị tần suất
    int?    SoTrenDV       // Số kỳ / đơn vị
    int?    DVTrenSo       // Đơn vị / kỳ
    string? NgayPhatHanh   // Ngày phát hành (varchar 100)
    int?    Order
    // + Audit Trail
}
```

### PrintBook.Fund

```csharp
[Table("Fund", Schema = "PrintBook")]
public class Fund
{
    long    Id
    string? Name
    string? Manager  // Người quản lý quỹ
    string? Note
    string? Purpose  // Mục đích quỹ
    double? Blane    // Số dư quỹ
    long?   BudgetId // FK → PrintBook.Budget.Id
    // + Audit Trail
}
```

### PrintBook.GeographicAreas

```csharp
[Table("GeographicAreas", Schema = "PrintBook")]
public class GeographicAreas
{
    long    Id
    string? Code           // varchar 50
    string? VnDescription
    string? Description    // varchar 100
    // + Audit Trail
}
```

### PrintBook.Inventory

```csharp
[Table("Inventory", Schema = "PrintBook")]
public class Inventory
{
    long      Id
    string?   InventoryName  // Tên đợt kiểm kê
    DateTime? Submited
    int?      Status
    long?     UserId
    // + Audit Trail
}
```

### PrintBook.InventoryBarcode

```csharp
[Table("InventoryBarcode", Schema = "PrintBook")]
public class InventoryBarcode
{
    long    Id
    string? Barcode           // varchar 50
    long?   StoreId           // FK → PrintBook.Store.Id
    long?   InventoryId       // FK → PrintBook.Inventory.Id
    int?    CheckStoreStatus  // Trạng thái kho
    int?    CheckStatus       // Trạng thái kiểm kê
    int?    CheckBorrow       // Đang mượn
    int?    CheckRegisteter   // Đã đăng ký
    // + Audit Trail
}
```

### PrintBook.isbd_field (IsbdField)

```csharp
[Table("isbd_field", Schema = "PrintBook")]
public class IsbdField
{
    int     Id
    int?    Config_Id    // FK → PrintBook.Config_Isbd.Id
    string? Field        // varchar 10
    string? Starttp      // Ký hiệu mở (varchar 50)
    string? Stoptp       // Ký hiệu đóng (varchar 50)
    int?    Fieldindex
    // + Audit Trail
}
```

### PrintBook.isbd_subfield (IsbdSubfield)

```csharp
[Table("isbd_subfield", Schema = "PrintBook")]
public class IsbdSubfield
{
    int     Id
    int?    Config_Id      // FK → PrintBook.Config_Isbd.Id
    string? Field          // varchar 10
    string? Subfield       // varchar 10
    string? Starttp        // varchar 50
    string? Stoptp         // varchar 50
    string? Nexttp         // varchar 50
    int?    Subfieldindex
    // + Audit Trail
}
```

### PrintBook.Key

```csharp
[Table("Key", Schema = "PrintBook")]
public class Key
{
    int     Id
    string? Machiakhoa   // Mã chìa khóa (varchar 50)
    string? Tenchiakhoa  // Tên/mô tả chìa khóa
    // + Audit Trail
}
```

### PrintBook.KeyIn

```csharp
[Table("KeyIn", Schema = "PrintBook")]
public class KeyIn
{
    long      Id
    long?     Keyid       // FK → PrintBook.Key.Id
    long?     Readerid    // FK → dbo.Reader.Id
    DateTime? BorrowDate
    DateTime? ReturnDate
    long?     Userid
    string?   Note
    // + Audit Trail
}
```

### PrintBook.KeyOut

```csharp
[Table("KeyOut", Schema = "PrintBook")]
public class KeyOut
{
    long      Id
    long?     Keyid       // FK → PrintBook.Key.Id
    long?     Readerid    // FK → dbo.Reader.Id
    DateTime? BorrowDate
    long?     Userid
    string?   Note
    // + Audit Trail
}
```

### PrintBook.KiemKe

```csharp
[Table("KiemKe", Schema = "PrintBook")]
public class KiemKe
{
    int       ID       // PK (int, viết hoa — legacy)
    string?   Dkcb    // Đăng ký cá biệt (varchar 50)
    int?      StoreId
    DateTime? Submited
    // + Audit Trail
}
```

### PrintBook.Language

```csharp
[Table("Language", Schema = "PrintBook")]
public class Language
{
    long    Id
    string? Code           // varchar 10
    string? VnDescription
    string? Description    // varchar 100
    // + Audit Trail
}
```

### PrintBook.LinhVucNghienCuu

```csharp
[Table("LinhVucNghienCuu", Schema = "PrintBook")]
public class LinhVucNghienCuu
{
    long    Id
    string? Name
    string? Ma       // Mã (varchar 50)
    int?    ParentId // Cha trong cây lĩnh vực
    string? GhiChu
    // + Audit Trail
}
```

### PrintBook.Log_BienMuc (LogBienMuc)

```csharp
[Table("Log_BienMuc", Schema = "PrintBook")]
public class LogBienMuc
{
    long      Id
    long?     UserId    // FK → dbo.Users.Id
    DateTime? Submited
    string?   Status
    long?     BibId     // FK → PrintBook.Bib.Bibid
    // + Audit Trail
}
```

### PrintBook.LostBook

```csharp
[Table("LostBook", Schema = "PrintBook")]
public class LostBook
{
    long      Id
    string?   Barcode    // varchar 50
    DateTime? Submited
    long?     Store      // FK → PrintBook.Store.Id
    long?     CreatedBy
    // + Audit Trail
}
```

### PrintBook.Lydophat

```csharp
[Table("Lydophat", Schema = "PrintBook")]
public class Lydophat
{
    int     Id
    string? Name  // Lý do phạt
    // + Audit Trail
}
```

### PrintBook.MagazineType

```csharp
[Table("MagazineType", Schema = "PrintBook")]
public class MagazineType
{
    long    Id
    string? Name
    // + Audit Trail
}
```

### PrintBook.marc_code_list (MarcCodeList)

```csharp
[Table("marc_code_list", Schema = "PrintBook")]
public class MarcCodeList
{
    int     ID     // PK (int, viết hoa)
    string? Loai   // Loại mã
    string? Ma     // Mã
    string? Desvn  // Mô tả tiếng Việt
    string? Desta  // Mô tả tiếng Anh/MARC
    // + Audit Trail
}
```

### PrintBook.Marc_Field (MarcField)

```csharp
[Table("Marc_Field", Schema = "PrintBook")]
public class MarcField
{
    int     Id
    string? Field         // varchar 3
    string? Description   // nvarchar 200
    string? Vndescription
    int?    Repeatable    // 1=lặp được
    int?    MANDATORY     // 1=bắt buộc
    // + Audit Trail
}
```

### PrintBook.Marc_Indicator (MarcIndicator)

```csharp
[Table("Marc_Indicator", Schema = "PrintBook")]
public class MarcIndicator
{
    int     Id
    string? INDICATOR    // varchar 2
    string? Value        // varchar 2
    string? Description
    string? VNDESCRIPTION
    string? Field_Id     // varchar 3
    // + Audit Trail
}
```

### PrintBook.Marc_SubField (MarcSubField)

```csharp
[Table("Marc_SubField", Schema = "PrintBook")]
public class MarcSubField
{
    int     Id
    string? Field         // varchar 3
    string? Subfield      // varchar 1
    string? Description
    string? Vndescription
    int?    Repeatable
    int?    MANDATORY
    // + Audit Trail
}
```

### PrintBook.MarcBibLevel

```csharp
[Table("MarcBibLevel", Schema = "PrintBook")]
public class MarcBibLevel
{
    long    Id
    string? Code           // varchar 5
    string? VnDescription
    string? Description    // varchar 100
    // + Audit Trail
}
```

### PrintBook.MarcRecordType

```csharp
[Table("MarcRecordType", Schema = "PrintBook")]
public class MarcRecordType
{
    long    Id
    string? MarcTypeCode    // varchar 5
    string? RecordTypeCode  // varchar 5
    // + Audit Trail
}
```

### PrintBook.MarcType

```csharp
[Table("MarcType", Schema = "PrintBook")]
public class MarcType
{
    long    Id
    string? Code           // varchar 5
    string? VnDescription
    string? Description    // varchar 100
    // + Audit Trail
}
```

### PrintBook.MaterialsType

```csharp
[Table("MaterialsType", Schema = "PrintBook")]
public class MaterialsType
{
    long    Id
    string? Code           // varchar 5
    string? VnDescription
    string? Description    // varchar 100
    // + Audit Trail
}
```

### PrintBook.Order_Status (OrderStatus)

```csharp
[Table("Order_Status", Schema = "PrintBook")]
public class OrderStatus
{
    int     Id
    string? Name
    // + Audit Trail
}
```

### PrintBook.PartemMagazineDetail

```csharp
[Table("PartemMagazineDetail", Schema = "PrintBook")]
public class PartemMagazineDetail
{
    long    Id
    long?   PatternId  // FK → PrintBook.PatternMagazine.Id
    string? X          // Thành phần biến đổi X (năm/tập)
    string? Y          // Thành phần biến đổi Y (số)
    string? Z          // Thành phần biến đổi Z (phần)
    int?    StepX
    int?    StepY
    int?    StepZ
    int?    RepeatX
    int?    RepeatY
    int?    RepeatZ
    int?    MaxX
    int?    MaxY
    int?    MaxZ
    int?    ResetX
    int?    ResetY
    int?    ResetZ
    // + Audit Trail
}
```

### PrintBook.PatternMagazine

```csharp
[Table("PatternMagazine", Schema = "PrintBook")]
public class PatternMagazine
{
    long    Id
    string? Name
    string? Description
    string? Function  // Hàm tính kỳ kế tiếp
    int?    Order
    // + Audit Trail
}
```

### PrintBook.PhongBan

```csharp
[Table("PhongBan", Schema = "PrintBook")]
public class PhongBan
{
    int     Id
    string? Name
    // + Audit Trail
}
```

### PrintBook.PolicyCirc

```csharp
[Table("PolicyCirc", Schema = "PrintBook")]
public class PolicyCirc
{
    int  Id
    int? ReaderType       // FK → dbo.ReaderType.Id
    int? CircPlace        // FK → PrintBook.CircPlace.Id
    int? NumberOfDate     // Số ngày được mượn
    int? NumberOfRenew    // Số lần gia hạn tối đa
    int? NumberOfBook     // Số sách tối đa đang mượn
    int? NumberOfRequest  // Số yêu cầu đặt chỗ tối đa
    int? Muontrung        // 1=cho mượn trùng
    int? Store            // FK → PrintBook.Store.Id
    // + Audit Trail
}
```

### PrintBook.PrintBookandDigital

```csharp
[Table("PrintBookandDigital", Schema = "PrintBook")]
public class PrintBookandDigital
{
    long  Id
    long? BibId    // FK → PrintBook.Bib.Bibid
    long? EbookId  // FK → Ebook.Item.Id
    // + Audit Trail
}
```

### PrintBook.Private

```csharp
[Table("Private", Schema = "PrintBook")]
public class Private
{
    int  Id
    int? RoleId    // FK → PrintBook.Roles.Id
    int? ModuleId
    // + Audit Trail
}
```

### PrintBook.Receipt_Status (ReceiptStatus)

```csharp
[Table("Receipt_Status", Schema = "PrintBook")]
public class ReceiptStatus
{
    int     Id
    string? Name
    // + Audit Trail
}
```

### PrintBook.RECORDTYPE (RecordType)

```csharp
[Table("RECORDTYPE", Schema = "PrintBook")]
public class RecordType
{
    long    Id
    string? Code           // varchar 5
    string? VnDescription
    string? Description    // varchar 100
    // + Audit Trail
}
```

### PrintBook.Roles

```csharp
[Table("Roles", Schema = "PrintBook")]
public class Roles
{
    byte    Id     // tinyint PK
    string? Name
    string? Code   // varchar 50
    // + Audit Trail
}
```

**Lưu ý**: `Id` kiểu `tinyint` → C# `byte`. Khác với `cms.Roles` (`long Id`).

### PrintBook.Serial

```csharp
[Table("Serial", Schema = "PrintBook")]
public class Serial
{
    long      Id
    long?     BibId        // FK → PrintBook.Bib.Bibid
    DateTime? StartTime
    DateTime? EndTime
    long?     FrequencyId  // FK → PrintBook.FrequencyMagazine.Id
    long?     PatternId    // FK → PrintBook.PatternMagazine.Id
    long?     StoreId      // FK → PrintBook.Store.Id
    string?   Note
    int?      LastX        // Kỳ cuối X
    int?      LastY        // Kỳ cuối Y
    int?      LastZ        // Kỳ cuối Z
    int?      IssueLength
    int?      WeekLength
    long?     SupplierId   // FK → PrintBook.Supplier.Id
    int?      MonthLenght  // (lỗi chính tả — giữ nguyên)
    string?   Locate       // varchar 50
    DateTime? FirstTime
    // + Audit Trail
}
```

### PrintBook.SERIALITEM (SerialItem)

```csharp
[Table("SERIALITEM", Schema = "PrintBook")]
public class SerialItem
{
    long      ID               // PK (viết hoa)
    long?     SUBSCRIPTION_ID  // FK → PrintBook.Serial.Id
    string?   SERIAL_SEQ       // Nhãn kỳ
    int?      SERIAL_SEQ_X
    int?      SERIAL_SEQ_Y
    int?      SERIAL_SEQ_Z
    int?      IS_SPECIAL       // 1=kỳ đặc biệt
    int?      STATUS
    int?      QUANTITY
    DateTime? PLANNED_DATE     // Ngày dự kiến phát hành
    string?   NOTE
    DateTime? PUBLISHED_DATE   // Ngày phát hành thực tế
    DateTime? CLAIM_DATE       // Ngày claim (khi không nhận được)
    int?      CLAIM_COUNT      // Số lần claim
    // + Audit Trail
}
```

### PrintBook.Store

```csharp
[Table("Store", Schema = "PrintBook")]
public class Store
{
    long    Id
    string? Name
    string? Postion      // Vị trí (lỗi chính tả trong DB — giữ nguyên)
    long?   StoreTypeId  // FK → PrintBook.StoreType.Id
    string? Code         // varchar 50
    string? Images
    // + Audit Trail
}
```

### PrintBook.StoreType

```csharp
[Table("StoreType", Schema = "PrintBook")]
public class StoreType
{
    long  Id
    string? Name
    long?   ParentId  // Kho cha (cây)
    // + Audit Trail
}
```

### PrintBook.SubcriptionStatus

```csharp
[Table("SubcriptionStatus", Schema = "PrintBook")]
public class SubcriptionStatus
{
    long    Id
    string? Name
    string? PortalId  // varchar 50
    string? Language  // varchar 50
    // + Audit Trail
}
```

### PrintBook.Supplier

```csharp
[Table("Supplier", Schema = "PrintBook")]
public class Supplier
{
    int     Id
    string? Name
    string? Address
    string? Mobile      // varchar 50
    string? Fax         // varchar 50
    string? Account     // Số tài khoản (varchar 50)
    string? Bank
    string? Mst         // Mã số thuế (varchar 50)
    string? Email       // varchar 50
    string? Website     // varchar 100
    string? Position    // Chức vụ người đại diện
    string? Representative  // Họ tên đại diện
    // + Audit Trail
}
```

### PrintBook.System_description (SystemDescription)

```csharp
[Table("System_description", Schema = "PrintBook")]
public class SystemDescription
{
    int     ID      // PK (int, viết hoa)
    string? Name
    string? Descvn  // Mô tả tiếng Việt
    string? Val     // Giá trị (varchar 200)
    string? Unit    // Đơn vị (varchar 50)
    // + Audit Trail
}
```

### PrintBook.SystemInfo

```csharp
[Table("SystemInfo", Schema = "PrintBook")]
public class SystemInfo
{
    // Không có cột Id — bảng singleton (1 hàng duy nhất)
    string? LibraryName
    string? Address
    string? Tel         // varchar 50
    // + Audit Trail
}
```

**Lưu ý**: Bảng không có cột `Id` / PK IDENTITY — là bảng singleton chứa thông tin thư viện.

### PrintBook.SystemPara

```csharp
[Table("SystemPara", Schema = "PrintBook")]
public class SystemPara
{
    int     Id
    string? Name
    string? Code  // varchar 50
    // + Audit Trail
}
```

### PrintBook.thanhly (Thanhly)

```csharp
[Table("thanhly", Schema = "PrintBook")]
public class Thanhly
{
    int       Id
    long?     BarcodeId  // FK → PrintBook.Barcode.Id
    DateTime? Sumited    // (lỗi chính tả — giữ nguyên)
    int?      UserId
    string?   Note
    // + Audit Trail
}
```

### PrintBook.trackingtolibrary (TrackingToLibrary)

```csharp
[Table("trackingtolibrary", Schema = "PrintBook")]
public class TrackingToLibrary
{
    long      Id
    long?     Readerid  // FK → dbo.Reader.Id
    long?     UserId
    DateTime? Time
    long?     StoreId   // FK → PrintBook.Store.Id
    // + Audit Trail
}
```

### PrintBook.User

```csharp
[Table("User", Schema = "PrintBook")]
public class User
{
    int     Id
    string? LoginName
    string? Password
    string? FullName
    int?    Role
    int?    Department
    int?    Rolecms
    string? Email    // varchar 50
    // + Audit Trail
}
```

**Lưu ý**: Entity này thuộc schema `PrintBook`, khác với `dbo.Users`. Namespace `ELIBAPI.Core.Entities.PrintBook` tránh xung đột.

### PrintBook.worksheet_field (WorksheetField)

```csharp
[Table("worksheet_field", Schema = "PrintBook")]
public class WorksheetField
{
    long    Id
    int?    Bib_Worksheet_Id  // FK → PrintBook.bib_worksheet.Id
    string? Field             // varchar 10
    string? L1                // Indicator 1 (varchar 1)
    string? L2                // Indicator 2 (varchar 1)
    // + Audit Trail
}
```

### PrintBook.worksheet_subfield (WorksheetSubfield)

```csharp
[Table("worksheet_subfield", Schema = "PrintBook")]
public class WorksheetSubfield
{
    long    Id
    long?   Worksheet_Field_Id  // FK → PrintBook.worksheet_field.Id
    string? Subfield            // varchar 1
    string? Value
    // + Audit Trail
}
```

### PrintBook.Z3950Config

```csharp
[Table("Z3950Config", Schema = "PrintBook")]
public class Z3950Config
{
    long    Id
    string? Name
    string? Host
    string? Port          // varchar 20
    string? DatabaseName
    string? Systax        // Cú pháp tìm kiếm (varchar 50)
    string? UserName      // varchar 50
    string? Password      // varchar 50
    string? PortalId      // varchar 50
    string? Language      // varchar 50
    long?   GroupId       // FK → PrintBook.Z3950Group.Id
    string? Url           // varchar 100
    // + Audit Trail
}
```

### PrintBook.Z3950Group

```csharp
[Table("Z3950Group", Schema = "PrintBook")]
public class Z3950Group
{
    long    Id
    string? Name
    string? PortalId  // varchar 50
    string? Language  // varchar 50
    // + Audit Trail
}
```

---

## Quan hệ giữa các bảng

### Schema cms & dbo (hiện có)
```
cms.news ──────────────► cms.Category      (CategoryId)
cms.news ──────────────► cms.AttachFile    (NewsId ở AttachFile)
cms.Photo ─────────────► cms.PhotoAlbum   (PhotoAlbumId)
cms.Menu ──────────────► cms.MenuType     (MenuType)
cms.Permission ─────────► dbo.Users        (UserId)
cms.Permission ─────────► cms.Module       (ModuleId)
cms.ModuleRoles ────────► cms.Module       (ModuleId)
cms.ADS ────────────────► cms.ADSGroup     (AdsGroupId)
cms.Link ───────────────► cms.LinkGroup    (LinkGroupId)
cms.NewsComment ────────► cms.news         (NewsId)
cms.CmsItem ────────────► cms.ItemType     (ItemTypeId)
cms.Contact ────────────► cms.ContactGroup (ContactGroupId)
```

### Schema dbo (bạn đọc)
```
dbo.Reader ─────────────► dbo.Org          (OrgId)
dbo.Reader ─────────────► dbo.ReaderType   (ReaderTypeId)
dbo.Reader ─────────────► dbo.Class        (ClassId)
dbo.Reader ─────────────► dbo.Course       (CourseId)
dbo.Reader ─────────────► dbo.Degree       (DegreeId)
dbo.Reader ─────────────► dbo.Ethenic      (EthenicId)
dbo.Reader ─────────────► dbo.Prof         (ProfId)
dbo.ReaderInGroup ──────► dbo.Reader       (ReaderId)
dbo.ReaderInGroup ──────► dbo.GroupReader  (GroupReaderId)
dbo.ReaderTrackingLogin ► dbo.Reader       (ReaderId)
```

### Schema Ebook
```
Ebook.EbookItem ────────► Ebook.EbookCollection    (CollectionId)
Ebook.EbookItem ────────► Ebook.EbookSubject       (SubjectId)
Ebook.EbookItem ────────► Ebook.EbookTopic         (TopicId)
Ebook.EbookItem ────────► Ebook.DigType            (TypeId)
Ebook.EbookItemXml ─────► Ebook.EbookItem          (Id 1:1)
Ebook.MetaDataValue ────► Ebook.EbookItem          (ItemId)
Ebook.MetaDataValue ────► Ebook.MetaDataFieldRegistery (MetaDataFieldId)
Ebook.MetaDataFieldRegistery ► Ebook.MetadataSchemaRegistry (MetaDataSchemaId)
Ebook.EbookFile ────────► Ebook.EbookItem          (EbookId)
Ebook.IntroBooks ───────► Ebook.IntroBookCategory  (IntroBookCategoryId)
Ebook.IntroBooks ───────► PrintBook.Bib            (BibId)
Ebook.CollectionPermistionUser ► Ebook.EbookCollection (CollectionId)
Ebook.CollectionPermistionUser ► dbo.GroupUser     (GroupUserId)
Ebook.EbookAccess ──────► dbo.Reader               (ReaderId)
Ebook.EbookAccess ──────► Ebook.EbookItem          (Bookid)
Ebook.TheodoiBienmucEbook ► Ebook.EbookItem        (DigId)
```

### Schema EOffice
```
EOffice.Document ───────► EOffice.Agency       (AgencyId)
EOffice.Document ───────► EOffice.DocumentType (DocumentTypeId)
EOffice.Document ───────► EOffice.EofficeTopic (TopicId)
EOffice.DocumentFile ───► EOffice.Document     (DocumentId)
```

### Schema Evaluate
```
Evaluate.EvaluateCourse ► Evaluate.EvaluateDegree   (DegreeId)
Evaluate.EvaluateCourse ► Evaluate.CourseOption     (OptionCourseId)
Evaluate.MonHoc ────────► Evaluate.EvaluateDegree   (DegreeId)
Evaluate.MonHoc ────────► Evaluate.Knowledge        (KnowledgeId)
Evaluate.MonHoc ────────► Evaluate.CourseOption     (OptionId)
Evaluate.NganhHoc ──────► Evaluate.EvaluateProgram  (ProgramId)
Evaluate.NganhMonHoc ───► Evaluate.NganhHoc         (MajorId)
Evaluate.NganhMonHoc ───► Evaluate.MonHoc           (MonHocId)
Evaluate.TaiLieu ───────► Evaluate.MonHoc           (MonHocId)
Evaluate.TaiLieu ───────► PrintBook.Bib             (BibId)
Evaluate.TaiLieu ───────► Ebook.EbookItem           (EBookId)
```

### Schema PrintBook
```
PrintBook.Bib ──────────► PrintBook.BibWorksheet    (Bib_worksheet_id)
PrintBook.Bib ──────────► PrintBook.BibType         (Bib_type_id)
PrintBook.BibXML ───────► PrintBook.Bib             (BibId 1:1)
PrintBook.BibData ──────► PrintBook.Bib             (BibId)
PrintBook.BibOrder ─────► PrintBook.BibWorksheet    (Bib_Worksheet_Id)
PrintBook.BibOrder ─────► PrintBook.BibType         (Bib_Type_Id)
PrintBook.BibXMLOrder ──► PrintBook.BibOrder        (BibId 1:1)
PrintBook.BibDataOrder ─► PrintBook.BibOrder        (BibId)
PrintBook.Barcode ──────► PrintBook.Bib             (BibId)
PrintBook.Barcode ──────► PrintBook.AbReceipt       (Receipt_Id)
PrintBook.AbOrderDetail ► PrintBook.AbOrder         (Order_Id)
PrintBook.AbOrderDetail ► PrintBook.Bib             (Bibid)
PrintBook.AbReceiptDetail ► PrintBook.AbReceipt     (Receipt_Id)
PrintBook.AbReceiptDetail ► PrintBook.Bib           (Bibid)
PrintBook.AbDelivererDetail ► PrintBook.AbDeliverer (Deliverer_Id)
PrintBook.AbDelivererDetail ► PrintBook.Barcode     (BarcodeId)
PrintBook.AbMoveDetail ─► PrintBook.AbMove          (Move_Id)
PrintBook.AbMoveDetail ─► PrintBook.Barcode         (BarcodeId)
PrintBook.Bib ──────────► Ebook.EbookItem           (EbookId)
```
