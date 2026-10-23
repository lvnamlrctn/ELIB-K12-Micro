import { translateError } from './i18n';
import { HttpClient, HttpErrorResponse, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

// ── DTO (khớp record C# của service; enum trao đổi dạng chuỗi) ─────────────────────────
export type TenantState = 'Provisioning' | 'Active' | 'Suspended' | 'ProvisioningFailed';
export type LicenseStatus = 'Trial' | 'Active' | 'Suspended' | 'Expired';
export type StepStatus = 'Pending' | 'Succeeded' | 'Failed';

export interface PagedResult<T> { items: T[]; total: number; page: number; pageSize: number; }
/** readOnly = quản trị nền tảng đang xem đơn vị (đóng vai, chỉ đọc) tới expiresAt. */
export interface Me {
  userId: number; tenantId: number | null; userName: string; fullName: string; mustChangePassword: boolean; permissions: string[];
  readOnly?: boolean; expiresAt?: string | null;
}
export interface Features {
  tenantId: number; code: string; name: string; status: TenantState; modules: string[]; logoUrl: string | null; logoText: string | null;
}
export interface ModuleDef { code: string; package: string; name: string; service: string; dependsOn: string[]; }
export interface LicenseInput { moduleCode: string; status?: LicenseStatus; validFrom?: string | null; validTo?: string | null; }
export interface License { moduleCode: string; moduleName: string; status: LicenseStatus; validFrom: string | null; validTo: string | null; effectiveToday: boolean; }
export interface ProvisioningStep { service: string; status: StepStatus; error: string | null; updatedAt: string; }
export interface TenantSummary { publicId: string; id: number; code: string; name: string; subdomain: string; status: TenantState; }
/** Thống kê toàn hệ thống: tenantIds rỗng = mọi đơn vị; from/to "yyyy-MM-dd" (giờ VN) cho số liệu trong kỳ. */
export interface PlatformStatsRequest { tenantIds: number[]; from: string | null; to: string | null; }
export interface ReaderPlatformStats { tenantId: number; total: number; locked: number; expired: number; added: number; }
export interface LoanPlatformStats { tenantId: number; loans: number; returns: number; readersServed: number; lost: number; onLoan: number; overdue: number; }
export interface FinePlatformStats { tenantId: number; tickets: number; total: number; discount: number; paid: number; remaining: number; }
export interface CirculationPlatformStats { loans: LoanPlatformStats[]; fines: FinePlatformStats[]; }
export interface ItemPlatformStats {
  tenantId: number; titles: number; copies: number; added: number; available: number; unshelved: number; onLoan: number; outOfStore: number;
  lost: number; liquidated: number;
}
export interface Tenant extends TenantSummary {
  timeZone: string; parentOrgId: number | null; version: number;
  provisioningStartedAt: string | null; provisioningError: string | null;
  provisioningSteps: ProvisioningStep[]; licenses: License[]; logoUrl: string | null; logoText: string | null;
}

// ── Bạn đọc (service patron) ──
/** Ngày dạng yyyy-MM-dd. status: 2 = hoạt động, 1 = bị khoá. sex: 1 = nam, 0 = nữ. */
export interface Reader {
  id: number; publicId: string; cardNo: string; lastName: string | null; firstName: string; fullName: string;
  citizenId: string | null; cardUid: string | null; email: string | null; phone: string | null; address: string | null;
  birthDate: string | null; sex: number | null; readerTypeId: number | null; classId: number | null; courseId: number | null;
  orgId: number | null; degreeId: number | null; ethnicityId: number | null; academicTitleId: number | null;
  issueDate: string | null; expireDate: string | null; status: number; lockReason: string | null; createdAt: string;
  /** Ảnh thẻ: id file ở media (riêng tư) — xem qua fileUrl(). */
  photoId: string | null;
}
export interface ExportField { code: string; name: string; }
export interface ReaderPhotosResult { matched: number; notFound: string[]; }
/** Bộ lọc như ELIB-LRC: họ/tên/mã thẻ chứa, đơn vị (kèm đơn vị con), ngày cấp và hạn thẻ trong khoảng. Ngày yyyy-MM-dd. */
export interface ReaderSearch extends CrudSearch {
  readerTypeId?: number | null; classId?: number | null; courseId?: number | null; expired?: boolean | null;
  cardNoContains?: string | null; lastName?: string | null; firstName?: string | null; orgIds?: number[] | null;
  issuedFrom?: string | null; issuedTo?: string | null; expiredFrom?: string | null; expiredTo?: string | null;
  /** Đúng các bạn đọc này (in thẻ) — tối đa 500. */
  publicIds?: string[] | null;
  groupId?: number | null;
}
/** Bạn đọc cho thao tác hàng loạt: đã tích (≤ 2.000) hoặc toàn bộ kết quả lọc kèm mã xem trước (15 phút). */
export interface ReaderSelection { publicIds?: string[] | null; search?: ReaderSearch | null; token?: string | null; }
export interface ReaderSelectionPreview { count: number; token: string; expiresAt: string; }
export interface ReaderBatchUpdate {
  selection: ReaderSelection; readerTypeId?: number | null; classId?: number | null; courseId?: number | null; orgId?: number | null;
  issueDate?: string | null; expireDate?: string | null; status?: number | null;
}
/** Một dòng lịch sử thay đổi (audit). changes: JSON {"trường": [cũ, mới], "_reason": "…"}. */
export interface EntityHistory {
  publicId: string; occurredAt: string; actorName: string | null; service: string; action: string; summary: string | null; changes: string | null;
}
export interface ReaderBulkUpdate {
  publicIds: string[]; readerTypeId?: number | null; classId?: number | null; courseId?: number | null;
  issueDate?: string | null; expireDate?: string | null; status?: number | null;
}

/** Tình trạng đăng nhập OPAC của bạn đọc (identity) — không có mật khẩu/chuỗi băm. */
export interface ReaderAccount {
  readerPublicId: string; hasPassword: boolean; cardNoPassword: boolean; mustChangePassword: boolean; lockedOut: boolean; lastLoginAt: string | null;
}

// ── Biên mục (service catalog) ──
export interface MarcSubfield { code: string; value: string; }
/** Trường điều khiển (001–009) chỉ có value; trường dữ liệu có ind1/ind2 + subfields. */
export interface MarcField { tag: string; ind1?: string | null; ind2?: string | null; value?: string | null; subfields?: MarcSubfield[] | null; }
export interface MarcSubfieldDef { code: string; name: string; repeatable: boolean; }
export interface MarcFieldDef { tag: string; name: string; repeatable: boolean; ind1: string | null; ind2: string | null; subfields: MarcSubfieldDef[]; }
export interface BibType { id: number; publicId: string; name: string; code: string; recordType: string; bibLevel: string; }
export interface Worksheet { id: number; publicId: string; name: string; bibTypeId: number | null; fields: MarcField[]; updatedAt: string | null; }
/** status: 2 = hiện trên OPAC, 1 = ẩn. mfn = id. */
export interface Bib {
  id: number; mfn: number; publicId: string; bibTypeId: number | null; worksheetId: number | null; leader: string; fields: MarcField[];
  title: string; author: string | null; publisher: string | null; publishYear: string | null; isbns: string[]; ddc: string | null;
  keywords: string | null; status: number; version: number; createdAt: string; updatedAt: string | null;
  /** Ảnh bìa: ảnh đã upload (/s3/media-public/…) hoặc URL https ngoài. */
  coverUrl: string | null;
}
export interface BibSearch extends CrudSearch { bibTypeId?: number | null; isbn?: string | null; ddc?: string | null; }
export interface BibInput { bibTypeId: number | null; worksheetId?: number | null; leader?: string | null; fields: MarcField[]; status?: number | null; }
export interface IsbnMatch { publicId: string; mfn: number; title: string; }
/** Nhập/xuất file MARC (ISO2709 .mrc, MARCXML .xml, text MARC hệ cũ). record: thứ tự trong file, đếm từ 1. */
export interface MarcImportError { record: number; title: string | null; message: string; }
export interface MarcImportResult {
  total: number; imported: number; skipped: number; failed: number; errors: MarcImportError[]; rejected: boolean; detail: string;
}
export interface MarcImportOptions { bibTypeId: number | null; status: number; skipDuplicates: boolean; skipInvalid: boolean; }
export interface MarcPreviewRecord { record: number; leader: string; fields: MarcField[]; title: string | null; bibTypeId: number | null; error: string | null; }
export interface MarcFilePreview { total: number; records: MarcPreviewRecord[]; }
export type MarcFormat = 'iso2709' | 'marcxml';

// ── Kho (service holdings) ──
export interface Store {
  id: number; publicId: string; code: string; name: string; storeTypeId: number | null; position: string | null; capacity: number | null; itemCount: number;
}
/** barcodes: ĐKCB quét/dán (in đúng thứ tự); hoặc khoảng ĐKCB / biểu ghi / kho / trạng thái. */
/** Ảnh khuôn mặt bổ sung của bạn đọc (id file ở media). */
export interface FaceMatch { readerPublicId: string; cardNo: string; fullName: string; confidence: number; }
export interface ExtraPhoto { fileId: string; createdAt: string; }

export interface ItemLabelRequest {
  barcodes?: string[] | null; mfn?: number | null; storeId?: number | null; itemStatus?: string | null; barcodeFrom?: string | null; barcodeTo?: string | null;
}
/** Một nhãn: ĐKCB, ký hiệu phân loại (DDC) / ký hiệu tác giả (Cutter), nhan đề – tác giả, kho. */
export interface ItemLabel {
  barcode: string; mfn: number; title: string | null; author: string | null; publishYear: string | null; ddc: string | null; cutter: string | null;
  storeCode: string | null; storeName: string | null;
}
/** Trạng thái bản sách (mã monolith): I chưa xếp giá, R sẵn sàng, L mất, S thanh lý, X xuất kho. */
export const ITEM_STATUSES: { code: string; name: string; badge: string }[] = [
  { code: 'I', name: 'ITEM_STATUS.I', badge: 'badge-off' },
  { code: 'R', name: 'ITEM_STATUS.R', badge: 'badge-on' },
  { code: 'L', name: 'ITEM_STATUS.L', badge: 'badge-off' },
  { code: 'S', name: 'ITEM_STATUS.S', badge: 'badge-off' },
  { code: 'X', name: 'ITEM_STATUS.X', badge: 'badge-off' },
];
export interface Item {
  id: number; publicId: string; barcode: string; mfn: number; bibPublicId: string; title: string | null; author: string | null;
  publishYear: string | null; ddc: string | null; storeId: number | null; storeCode: string | null; storeName: string | null;
  status: string; note: string | null; version: number; createdAt: string; updatedAt: string | null;
  onLoan: boolean; loanCardNo: string | null; loanDueAt: string | null;
  /** Giá/ngăn đang nằm (từ sơ đồ tầng); null = chưa xếp vào giá nào. */
  shelfPublicId?: string | null; shelfName?: string | null; shelfRowIndex?: number | null; shelfRowRange?: string | null;
}
export interface ItemSearch extends CrudSearch {
  mfn?: number | null; storeId?: number | null; itemStatus?: string | null; barcodeFrom?: string | null; barcodeTo?: string | null;
}
export interface RegisterItems { mfn: number; quantity: number; prefix: string; digits: number; startNumber?: number | null; storeId?: number | null; }
export interface NextBarcode { prefix: string; number: number; barcode: string; }
export interface ShelveResult { shelved: number; notFound: string[]; skipped: string[]; }

// ── Xếp giá trực quan / xếp giá lại ──
export interface MapShelvingSearch {
  status?: string | null; storeId?: number | null; receipt?: string | null; barcodeFrom?: string | null; barcodeTo?: string | null; keyword?: string | null;
  pageIndex: number; pageSize: number;
}
export interface MapShelvingRow {
  publicId: string; barcode: string; mfn: number; title: string | null; ddc: string | null; status: string; onLoan: boolean; storeId: number | null;
  storeName: string | null; receiptCode: number | null; shelfPublicId: string | null; shelfName: string | null; shelfDeleted: boolean; rowIndex: number | null;
  rowRange: string | null; suggestedShelfPublicId: string | null; suggestedShelfName: string | null; suggestedRowPublicId: string | null; suggestedRowIndex: number | null;
}
export interface MapShelfRowNode { rowPublicId: string; rowIndex: number; ddcStart: string; ddcEnd: string; description: string | null; items: number; }
export interface MapShelfNode {
  shelfPublicId: string; code: string; name: string; positionX: number | null; positionY: number | null; width: number | null; height: number | null;
  colorHex: string | null; items: number; rows: MapShelfRowNode[];
}
export interface MapFloorNode {
  floorPublicId: string; name: string; floorNumber: number | null; buildingName: string | null; canvasWidth: number; canvasHeight: number;
  layoutImageUrl: string | null; shelves: MapShelfNode[];
}
export interface PlaceResult { placed: number; skipped: { barcode: string; reason: string }[]; }
/** Lịch sử di chuyển của một bản sách (xếp giá, xếp giá lại, chuyển kho, đăng ký lại mã vạch). */
export interface ItemMovement {
  id: number; kind: string; kindName: string; at: string; actorId: number | null; actorName: string | null; fromStatus: string | null; toStatus: string | null;
  fromStore: string | null; toStore: string | null; fromShelf: string | null; toShelf: string | null; fromRow: string | null; toRow: string | null; reason: string | null;
}
/** Kho rút gọn cho ô chọn (mọi màn kho xem được, không cần quyền STORES). */
export interface StoreOption { id: number; code: string; name: string; }
export interface SkippedItem { barcode: string; reason: string; }
/** Kết quả thao tác hàng loạt theo ĐKCB (báo mất, thanh lý, thêm dòng phiếu). */
export interface ItemBatchResult { done: number; notFound: string[]; skipped: SkippedItem[]; }
export interface CompleteResult { done: number; skipped: SkippedItem[]; }
/** Lọc sách mất / thanh lý. Ngày yyyy-MM-dd. */
export interface StoreRecordSearch extends CrudSearch { storeId?: number | null; from?: string | null; to?: string | null; }
export interface LostRow {
  id: number; publicId: string; barcode: string; itemPublicId: string | null; mfn: number | null; title: string | null; author: string | null;
  storeId: number | null; storeName: string | null; lostOn: string; reason: string | null; cardNo: string | null; itemStatus: string | null; createdAt: string;
}
export interface LiquidationRow {
  id: number; publicId: string; barcode: string; itemPublicId: string | null; mfn: number | null; title: string | null; author: string | null;
  publisher: string | null; storeId: number | null; storeName: string | null; liquidatedOn: string; reason: string | null; itemStatus: string | null; createdAt: string;
  decisionNo?: string | null; decisionDate?: string | null;
}
/** Báo mất / thanh lý theo lô: đã xử lý (result) hoặc chờ duyệt (pending, chưa đổi gì). */
export interface BatchOutcome { result: ItemBatchResult | null; pending: PendingApproval | null; }
export interface StoreOutSearch extends CrudSearch {
  barcode?: string | null; open?: boolean | null; reasonId?: number | null; unitId?: number | null; exhibitionLocationId?: number | null;
  outFrom?: string | null; outTo?: string | null; inFrom?: string | null; inTo?: string | null;
}
export interface StoreOutRow {
  id: number; publicId: string; barcode: string; itemPublicId: string | null; mfn: number | null; title: string | null; storeId: number | null;
  storeName: string | null; reasonId: number | null; reasonName: string | null; unitId: number | null; unitName: string | null;
  exhibitionLocationId: number | null; exhibitionLocationName: string | null; delivererName: string | null; receiverName: string | null;
  returnAtLibrary: boolean; outAt: string | null; inAt: string | null;
}
export interface ScanOut {
  barcode: string; reasonId: number | null; unitId: number | null; exhibitionLocationId: number | null; delivererName: string | null;
  receiverName: string | null; returnAtLibrary: boolean;
}
export interface IdName { id: number; name: string; }
export interface StoreOutOptions { reasons: IdName[]; units: IdName[]; locations: IdName[]; }
export interface Inventory {
  id: number; publicId: string; name: string; startedOn: string; storeId: number | null; storeName: string | null; note: string | null;
  closedAt: string | null; scanCount: number; createdAt: string;
}
export interface InventoryScanRow {
  id: number; publicId: string; barcode: string; itemPublicId: string | null; mfn: number | null; title: string | null; storeId: number | null;
  storeName: string | null; scanStoreId: number | null; registered: boolean; wrongStore: boolean; onLoan: boolean; abnormal: boolean;
  itemStatus: string | null; createdAt: string;
}
export interface InventorySummary { scanned: number; wrongStore: number; unregistered: number; onLoan: number; abnormal: number; missing: number; }
export interface MissingRow { itemPublicId: string; barcode: string; mfn: number; title: string | null; author: string | null; storeName: string | null; status: string; }
export interface InventoryImportResult { scanned: number; alreadyScanned: number; unregistered: number; errors: ImportError[]; }
/** Dòng của phiếu điều chuyển / giao nhận; skipReason: lý do không chuyển được khi hoàn thành. */
export interface TransferLine {
  publicId: string; itemPublicId: string; barcode: string; mfn: number; title: string | null; author: string | null; storeId: number | null;
  storeName: string | null; status: string; onLoan: boolean; skipReason: string | null;
}
export interface StoreMove {
  id: number; publicId: string; fromStoreId: number; fromStoreName: string | null; toStoreId: number; toStoreName: string | null;
  delivererName: string | null; receiverName: string | null; movedOn: string | null; note: string | null; completedAt: string | null;
  movedCount: number; lineCount: number; createdAt: string;
}
/** status: 1 đang giao, 2 đã nhận đủ (mới ký nhận được). receiptPublicId/receiptCode: đơn nhận (phiếu nhận bổ sung) được phân bổ. */
export interface Delivery {
  id: number; publicId: string; delivererName: string | null; delivererAddress: string | null; receiverName: string | null;
  receiverAddress: string | null; deliveredOn: string | null; receivedOn: string | null; storeId: number | null; storeName: string | null;
  status: number; note: string | null; signedAt: string | null; shelvedCount: number; lineCount: number; createdAt: string;
  receiptPublicId: string | null; receiptCode: number | null;
}
/** Đơn nhận chọn được cho phiếu giao nhận: đã hoàn tất; pending = số bản chưa xếp giá chưa phân bổ. */
export interface DeliveryReceiptOption { publicId: string; code: number; name: string | null; receivedOn: string | null; storeId: number | null; pending: number; }
export type TransferKind = 'store-moves' | 'deliveries';
export interface ReRegisterLine { row: number; oldBarcode: string | null; newBarcode: string | null; storeCode: string | null; error: string | null; }
export interface ReRegisterImportResult { total: number; applied: number; dryRun: boolean; errorCount: number; lines: ReRegisterLine[]; }
/** criterion: status | store | ddc | bibtype. */
export interface DocumentStatsRequest { criterion: string; from: string | null; to: string | null; storeId: number | null; }
export interface DocumentStatRow { key: string; label: string; titles: number; copies: number; }
export interface DocumentStats { criterion: string; rows: DocumentStatRow[]; titles: number; copies: number; }
export interface StoreBookReportRequest { storeId: number | null; from: string | null; to: string | null; pageIndex: number; pageSize: number; }
export interface StoreBookRow {
  barcode: string; mfn: number; title: string | null; author: string | null; ddc: string | null; registeredOn: string; storeName: string | null;
  status: string; onLoan: boolean;
  /** Ngày nhận / số phiếu nhận đã đăng ký bản (null = đăng ký không qua phiếu nhận — báo cáo tính theo ngày đăng ký). */
  receivedOn: string | null; receiptCode: number | null;
}
export interface StoreBookReport {
  rows: StoreBookRow[]; totalCount: number; titleCount: number; storeStats: { storeId: number | null; storeName: string; titles: number; copies: number }[];
  libraryName: string | null;
}

// ── Bổ sung (service acquisition) ──
export interface AcqOption { id: number; name: string; }
export interface AcquisitionOptions { sources: AcqOption[]; suppliers: AcqOption[]; budgets: AcqOption[]; funds: AcqOption[]; }
/** Biểu ghi chọn được cho dòng đơn đặt / phiếu nhận (bản sao biểu ghi của acquisition). */
export interface AcqBib {
  bibPublicId: string; mfn: number; title: string; author: string | null; publisher: string | null; publishYear: string | null; isbns: string | null;
}
/** "Tra trùng" (ELIB-LRC): điều kiện tìm biểu ghi có sẵn — chữ không phân biệt dấu. */
export interface AcqBibSearch {
  mfnFrom: number | null; mfnTo: number | null; publishYear: string | null; publisher: string | null; title: string | null; author: string | null;
  bibTypeId: number | null; pageIndex: number; pageSize: number;
}
/** hasReceipt / hasOrder: biểu ghi đã có trong phiếu nhận / đơn đặt nào chưa. */
export interface AcqBibRow extends AcqBib { bibTypeName: string | null; hasReceipt: boolean; hasOrder: boolean; }
/** status: 1 mới lập, 2 đã gửi đặt, 3 đã huỷ. quantity/received: tổng số bản đặt / đã nhận theo phiếu nhận. */
export interface PurchaseOrder {
  id: number; publicId: string; name: string; orderedOn: string | null; dueOn: string | null; sourceId: number | null; sourceName: string | null;
  supplierId: number | null; supplierName: string | null; budgetId: number | null; fundId: number | null; note: string | null; status: number;
  paymentMethod: number; lineCount: number; quantity: number; received: number; total: number; createdAt: string;
}
/** Dòng đơn đặt: biểu ghi đã có (mfn) hoặc mô tả tài liệu chưa biên mục; remaining = còn chờ nhận. */
export interface OrderLine {
  id: number; publicId: string; orderId: number; bibPublicId: string | null; mfn: number | null; title: string; author: string | null;
  publisher: string | null; publishYear: string | null; isbn: string | null; quantity: number; price: number | null; currency: string | null;
  rate: number; cancelReason: string | null; received: number; remaining: number;
}
export interface OrderLineInput {
  linePublicId?: string | null; mfn?: number | null; title?: string | null; author?: string | null; publisher?: string | null;
  publishYear?: string | null; isbn?: string | null; quantity: number; price?: number | null; currency?: string | null; rate?: number | null;
  cancelReason?: string | null;
}
/** Phiếu nhận (đơn nhận); completedAt khác null = đã hoàn tất (khoá). */
export interface Receipt {
  id: number; publicId: string; name: string | null; receivedOn: string; sourceId: number | null; sourceName: string | null;
  supplierId: number | null; supplierName: string | null; budgetId: number | null; fundId: number | null; storeId: number | null;
  note: string | null; paymentStatus: number; paymentMethod: number; completedAt: string | null; lineCount: number; quantity: number; total: number;
  createdAt: string;
}
/** registered: số ĐKCB đã đăng ký cho dòng (null = phân hệ Kho không trả lời). */
export interface ReceiptLine {
  id: number; publicId: string; bibPublicId: string; mfn: number; title: string; author: string | null; quantity: number; price: number | null;
  currency: string | null; rate: number; orderLinePublicId: string | null; orderId: number | null; registered: number | null;
}
export interface ReceiptLineInput {
  linePublicId?: string | null; mfn?: number | null; quantity: number; price?: number | null; currency?: string | null; orderLinePublicId?: string | null;
  rate?: number | null;
}
/** ĐKCB của phiếu nhận (ở holdings); delivered = đã nằm ở phiếu giao nhận. */
export interface ReceiptItem {
  publicId: string; linePublicId: string | null; barcode: string; mfn: number; storeId: number | null; storeName: string | null; status: string;
  onLoan: boolean; delivered: boolean; createdAt: string;
}
export interface ReceiptLinesView { lines: ReceiptLine[]; items: ReceiptItem[]; itemsAvailable: boolean; }
export interface ReceivableLine {
  orderLinePublicId: string; orderId: number; orderName: string; supplierId: number | null; bibPublicId: string | null; mfn: number | null;
  title: string; author: string | null; isbn: string | null; quantity: number; received: number; remaining: number; price: number | null;
  currency: string | null; rate: number;
}
/** Đăng ký ĐKCB cho dòng phiếu nhận: theo lô (quantity + prefix) hoặc một mã (barcode). */
export interface RegisterReceiptItems {
  quantity?: number; prefix?: string | null; digits?: number; startNumber?: number | null; barcode?: string | null; storeId?: number | null;
}
export interface RegisteredItem { publicId: string; barcode: string; mfn: number; storeId: number | null; storeName: string | null; status: string; }

/** Bộ lọc báo cáo bổ sung / tra cứu đơn nhận. Ngày lọc theo ngày nhận của phiếu (thư mục sách mới: ngày biên mục). */
export interface AcqReportRequest {
  from: string | null; to: string | null; storeId: number | null; supplierId: number | null; sourceId: number | null; fundId: number | null;
  receiptCodeFrom: number | null; receiptCodeTo: number | null; receiptName: string | null; completed: boolean | null;
  createdFrom: string | null; createdTo: string | null; createdBy?: number | null;
  mfnFrom: number | null; mfnTo: number | null; title: string | null; author: string | null; publisher: string | null; publishYear: string | null;
  pageIndex: number; pageSize: number;
}
export type AcqReportName = 'AcquisitionList' | 'LookupLines' | 'StoreAllocation' | 'AccessionRegister' | 'NewBooks' | 'LabelQuantities';
export interface AcqListRow {
  receiptId: number; receiptName: string | null; receivedOn: string; mfn: number; title: string; author: string | null; quantity: number;
  price: number | null; currency: string | null; rate: number; total: number;
}
/** quantity / amount tính trên toàn bộ kết quả lọc. */
export interface AcqListPage { items: AcqListRow[]; total: number; pageIndex: number; pageSize: number; quantity: number; amount: number; }
export interface AcqLookupRow {
  mfn: number; receiptId: number; isbd: string; createdAt: string; receivedOn: string; sourceName: string | null; storeId: number | null;
  storeName: string | null; bibPublicId: string; receiptPublicId: string;
}
export interface AcqStoreCount { storeId: number | null; storeName: string | null; copies: number; }
export interface AcqAllocation { items: AcqStoreCount[]; total: number; }
export interface AcqAccessionRow {
  barcode: string; mfn: number; title: string | null; author: string | null; publisher: string | null; publishYear: string | null; ddc: string | null;
  storeName: string | null; date: string; receiptId: number | null; price: number | null; currency: string | null; rate: number | null;
}
/** catalogedOn null = biểu ghi không rõ ngày biên mục (xếp cuối). */
export interface AcqNewBookRow { mfn: number; title: string; author: string | null; publisher: string | null; publishYear: string | null; catalogedOn: string | null; }
/** Số lượng theo biểu ghi (chưa có ĐKCB cụ thể) — in nhãn gáy/môn loại theo số lượng đã đặt/nhận. */
export interface AcqLabelQuantityRow { mfn: number; title: string; author: string | null; ddc: string | null; cutter: string | null; quantity: number; }

// ── Lưu thông (service circulation) ──
/** Khung giờ làm việc của điểm lưu thông: day 1 = thứ Hai … 7 = Chủ nhật, giờ "HH:mm". */
export interface WorkSession { day: number; from: string; to: string; }
/** kind: 1 = mượn về, 2 = đọc tại chỗ (hạn trả cuối ngày, không gia hạn). readerTypeIds/storeIds rỗng = mọi loại/mọi kho. */
export interface CircPlace {
  id: number; publicId: string; code: string; name: string; storeIds: number[]; readerTypeIds: number[]; kind: number;
  maxLoans: number | null; workSessions: WorkSession[]; note: string | null;
}
export interface BibTypeRule { bibTypeId: number; loanDays: number | null; maxLoans: number | null; maxRenewals: number | null; maxHolds: number | null; }
/** Chính sách lưu thông theo loại bạn đọc × điểm lưu thông (null = mọi loại / mọi điểm). */
export interface LoanPolicy {
  id: number; publicId: string; readerTypeId: number | null; circPlaceId: number | null; loanDays: number; maxLoans: number | null;
  maxRenewals: number | null; renewDays: number; finePerDay: number; renewFromToday: boolean; maxHolds: number | null; holdDays: number;
  techFee: number; shippingFee: number; bibTypeRules: BibTypeRule[] | null;
}
export interface Loan {
  id: number; publicId: string; readerPublicId: string; cardNo: string; readerName: string | null; itemPublicId: string; barcode: string;
  mfn: number; title: string | null; author: string | null; circPlaceId: number | null; storeName: string | null; loanedAt: string; dueAt: string;
  returnedAt: string | null; renewCount: number; note: string | null; version: number;
  /** Kho của bản sách lúc mượn; người cho mượn / nhận trả ("Họ tên (tên đăng nhập)"); trả sau ngày hết hạn. */
  storeId?: number | null; returnCircPlaceId?: number | null; loanedByName?: string | null; returnedByName?: string | null; returnedLate?: boolean;
  /** Returned | Lost | Forced (đóng cưỡng bức); lý do đóng; số lần được mở lại. */
  closeKind?: string | null; closeReason?: string | null; reopenCount?: number;
}
/** Kết quả đóng cưỡng bức / mở lại: đã áp dụng (200) hoặc chờ duyệt (202, chưa đổi gì). */
export type LoanActionResult = { loan: Loan; pending: null } | { loan: null; pending: PendingApproval };
export interface ReaderPanel {
  readerPublicId: string; cardNo: string; fullName: string; readerTypeName: string | null; className: string | null; courseName: string | null;
  photoId: string | null; expireDate: string | null; isLocked: boolean; isExpired: boolean; hasOverdue: boolean; canBorrow: boolean;
  blockReason: string | null; loanDays: number; maxLoans: number | null; maxRenewals: number | null; currentLoans: Loan[];
  unpaidFines: number; holds: Hold[] | null;
}
export interface CheckoutLine { barcode: string; success: boolean; message: string; loan: Loan | null; }
export interface CheckoutResult { succeeded: number; failed: number; lines: CheckoutLine[]; }
/** holdFor: bản vừa trả được giữ cho người đặt mượn kế tiếp — để riêng, không xếp lên giá. */
export interface ReturnResult { loan: Loan; overdueDays: number; holdFor: Hold | null; }
/** status: 1 chờ sách, 2 đang giữ sách, 3 đã mượn, 4 đã huỷ, 5 hết hạn giữ. */
export interface Hold {
  id: number; publicId: string; readerPublicId: string; cardNo: string; readerName: string | null; mfn: number; title: string | null;
  author: string | null; circPlaceId: number | null; status: number; requestedAt: string; itemPublicId: string | null; barcode: string | null;
  readyAt: string | null; expiresAt: string | null; closedAt: string | null; note: string | null; queuePosition: number | null;
}
export interface HoldSearch extends CrudSearch { cardNo?: string | null; holdStatus?: number | null; mfn?: number | null; }
export const HOLD_STATUSES: { code: number; name: string; badge: string }[] = [
  { code: 1, name: 'HOLD_STATUS.1', badge: 'badge-warn' },
  { code: 2, name: 'HOLD_STATUS.2', badge: 'badge-info' },
  { code: 3, name: 'HOLD_STATUS.3', badge: 'badge-on' },
  { code: 4, name: 'HOLD_STATUS.4', badge: 'badge-off' },
  { code: 5, name: 'HOLD_STATUS.5', badge: 'badge-off' },
];
/** state: open | borrowing | overdue | returned | returned_ontime | returned_late; storeId: kho lúc mượn; from/to: yyyy-MM-dd (ngày mượn). */
export interface LoanSearch extends CrudSearch {
  cardNo?: string | null; barcode?: string | null; state?: string | null; circPlaceId?: number | null; storeId?: number | null;
  from?: string | null; to?: string | null;
}
/** Lượt vào/ra thư viện theo thẻ bạn đọc (monolith: CheckIn+CheckOut gộp một — đang ở trong khi checkedOutAt null). */
export interface Visit {
  id: number; publicId: string; readerPublicId: string; cardNo: string; readerName: string | null; readerTypeName: string | null;
  className: string | null; circPlaceId: number; circPlaceName: string | null; checkedInAt: string; checkedOutAt: string | null;
  circPlaceOutId: number | null; circPlaceOutName: string | null;
}
/** Thẻ bạn đọc ở cổng: panel lưu thông (như quầy mượn trả) lồng tình trạng vào/ra hiện tại. */
export interface GateStatus { reader: ReaderPanel; checkedIn: boolean; visitPublicId: string | null; checkedInAt: string | null; visitCount: number; }
/** state: open | closed; circPlaceId khớp cả điểm vào hoặc ra; from/to: yyyy-MM-dd (ngày vào). */
export interface VisitSearch extends CrudSearch { cardNo?: string | null; circPlaceId?: number | null; state?: string | null; from?: string | null; to?: string | null; }

export interface CirculationReportRequest {
  reportType: number; from: string | null; to: string | null; circPlaceId: number | null; readerTypeId: number | null; className: string | null;
  pageIndex: number; pageSize: number;
}
export interface CirculationReport {
  reportType: number; title: string; headers: string[]; rows: string[][]; totalCount: number; totalRow: string[] | null;
  libraryName: string | null; truncated: boolean;
}
export interface SearchIndexStatus {
  status: string; startedAt: string | null; finishedAt: string | null; bibs: number; items: number; loans: number; error: string | null;
  indexedBibs: number; visibleBibs: number; indexedItems: number;
}
export interface Z3950ServerOption { publicId: string; name: string; groupName: string | null; recordSyntax: string; }
export interface Z3950Record { leader: string; fields: MarcField[]; }
export interface Z3950Hit { position: number; title: string | null; author: string | null; publisher: string | null; year: string | null; isbn: string | null; record: Z3950Record; }
export interface Z3950ServerResult {
  serverId: string; serverName: string; recordSyntax: string; connected: boolean; total: number; page: number; pageSize: number; hits: Z3950Hit[]; error: string | null;
}
export interface Z3950SearchRequest {
  title?: string | null; author?: string | null; publisher?: string | null; isbn?: string | null; keyword?: string | null; subject?: string | null;
  serverIds?: string[]; page?: number; pageSize?: number;
}
export interface DailySearchStat { date: string; searches: number; zeroResults: number; withClicks: number; }
export interface QueryStat { text: string; searches: number; avgTotal: number; withClicks: number; }
/** Thống kê chất lượng tìm kiếm OPAC trong khoảng ngày (service search). */
export interface SearchStatsSummary {
  from: string; to: string; searches: number; zeroResults: number; withClicks: number; advancedSearches: number; avgClickPosition: number | null;
  days: DailySearchStat[]; topQueries: QueryStat[]; topZeroResults: QueryStat[];
}
export interface FineReason { id: number; publicId: string; code: string; name: string; amount: number; itemStatus: string | null; isBuiltIn: boolean; }
/** status: 1 đang xử lý, 2 đã hoàn thành (như monolith). */
export interface FineTicket {
  id: number; publicId: string; number: number; code: string; readerPublicId: string; cardNo: string; readerName: string | null; fineDate: string;
  status: number; round: number; manualAmount: number | null; total: number; discount: number; paid: number; remaining: number;
  note: string | null; lineCount: number; createdAt: string; updatedAt: string | null;
}
export interface FineLine {
  id: number; reasonId: number; reasonCode: string; reasonName: string | null; amount: number; loanPublicId: string | null;
  barcode: string | null; mfn: number | null; title: string | null; overdueDays: number; dueAt: string | null; loanOpen: boolean;
}
// ── Thanh toán QR (service payment) ──
export interface PaymentAccount {
  configured: boolean; vietQrEnabled: boolean; bankBin: string | null; accountNo: string | null; accountName: string | null; hasSepayApiKey: boolean;
  vnPayEnabled: boolean; vnPayTmnCode: string | null; hasVnPayHashSecret: boolean; vnPayPaymentUrl: string; expireMinutes: number;
}
/** Bí mật: để trống = giữ nguyên; clear… = xoá. */
export interface PaymentAccountInput {
  vietQrEnabled: boolean; bankBin: string | null; accountNo: string | null; accountName: string | null; sepayApiKey: string | null; clearSepayApiKey: boolean;
  vnPayEnabled: boolean; vnPayTmnCode: string | null; vnPayHashSecret: string | null; clearVnPayHashSecret: boolean; vnPayPaymentUrl: string | null; expireMinutes: number;
}
/** Pending | Paid | Expired | Cancelled | PaidLate */
export interface PaymentTx {
  publicId: string; transactionCode: string; provider: string; amount: number; status: string; expiresAt: string; qrContent: string; label: string; kind: string;
  paidAt: string | null; needsReview: boolean; reviewNote: string | null;
}
export interface PaymentRow {
  publicId: string; transactionCode: string; provider: string; amount: number; status: string; createdAt: string; expiresAt: string; paidAt: string | null;
  providerRef: string | null; needsReview: boolean; reviewNote: string | null; label: string; kind: string; cardNo: string; chargeStatus: string;
}
export interface PaymentSearch extends CrudSearch { transactionStatus?: string | null; provider?: string | null; needsReview?: boolean | null; from?: string | null; to?: string | null; }
export interface PaymentSearchResult { page: CrudPage<PaymentRow>; totals: { paid: number; pending: number; needsReview: number; needsReviewCount: number }; }
export interface WebhookLogRow { id: number; provider: string; transactionCode: string | null; result: string; payload: string; remoteIp: string | null; receivedAt: string; }
export interface CardFee {
  publicId: string; cardNo: string; readerName: string; amount: number; reason: string; status: string; paidAt: string | null; paidVia: string | null;
  createdByName: string | null; createdAt: string;
}
export interface FinePayment { id: number; amount: number; method: string; note: string | null; reason: string | null; byName: string | null; paidAt: string; }
export interface FineTicketDetail {
  ticket: FineTicket; lines: FineLine[]; finePerDay: number; payments?: FinePayment[] | null; pendingRequests?: ChangeRequest[] | null;
}

// ── Phê duyệt theo thẩm quyền (mọi service dùng chung khung; service = circulation | holdings) ──
export type ApprovalService = 'circulation' | 'holdings';
/** Pending | Approved | Rejected | Cancelled | Failed */
export interface ChangeRequest {
  publicId: string; kind: string; kindName: string; module: string; targetPublicId: string; targetLabel: string | null; summary: string; reason: string;
  amount: number | null; count: number | null; status: string; requestedByName: string | null; requestedAt: string; decidedByName: string | null;
  decidedAt: string | null; decisionNote: string | null; before: Record<string, unknown> | null; after: Record<string, unknown> | null; canDecide: boolean;
  service?: ApprovalService;
}
export interface PendingApproval { requestId: string; kind: string; kindName: string; message: string; }
export interface ApprovalRule {
  kind: string; module: string; name: string; description: string; enabled: boolean; amount: number | null; percent: number | null; count: number | null;
  isCustom: boolean; service?: ApprovalService;
}
/** Kết quả lưu có thể là đã áp dụng (200) hoặc chờ duyệt (202, chưa đổi gì). */
export type FineSaveResult = { detail: FineTicketDetail; pending: null } | { detail: null; pending: PendingApproval };
export interface FineTicketTotals { receivable: number; received: number; remaining: number; }
export interface FineTicketSearch extends CrudSearch {
  cardNo?: string | null; ticketStatus?: number | null; unpaid?: boolean | null; from?: string | null; to?: string | null;
}
export interface FineLineChange { id: number | null; reasonCode: string; amount: number; barcode?: string | null; }
export interface SaveFineTicket {
  status: number; discount: number; paid: number; note: string | null; manualAmount?: number | null; fineDate?: string | null;
  lines: FineLineChange[]; deletedLineIds: number[]; reason?: string | null;
}

// ── Tài liệu số (service digital) ──
export interface ReadingSessionRow {
  publicId: string; ebookPublicId: string; ebookTitle: string; readerPublicId: string; cardNo: string | null; fullName: string | null; startedAt: string; lastSeenAt: string;
  lastPage: number; maxPage: number; pageCount: number; activeSeconds: number; device: string | null;
}
export interface ReadingSessionPage { totalCount: number; items: ReadingSessionRow[]; }
export interface PortalCounterSummary { total: number; today: number; last7Days: number; }
export interface PortalCounterDay { day: string; hits: number; }

export interface ReadingStats {
  totals: { sessions: number; readers: number; ebooks: number; activeSeconds: number; views: number; downloads: number };
  topEbooks: { ebookPublicId: string; title: string; sessions: number; readers: number; activeSeconds: number }[];
  topReaders: { readerPublicId: string; cardNo: string | null; fullName: string | null; sessions: number; activeSeconds: number }[];
  daily: { date: string; sessions: number; activeSeconds: number }[];
}

export interface MetadataField {
  id: number; publicId: string; schemaPublicId: string; schemaShortId: string; field: string; subfield: string | null; descriptionVn: string; descriptionEn: string | null;
  sortOrder: number; input: string; exportField: number | null; status: number;
}
export interface EbookMetadataRow { fieldPublicId: string; field: string; subfield: string | null; descriptionVn: string; descriptionEn: string | null; input: string; value: string; language: string | null; }

export interface EbookItem {
  id: number; publicId: string; status: number; collectionId: number | null; collectionName: string | null; subjectId: number | null;
  topicId: number | null; typeId: number | null; title: string; author: string | null; publisher: string | null; publishDate: string | null;
  keyword: string | null; otherTitle: string | null; page: string | null; oldAuthor: string | null; xml: string | null; language: string | null;
  free: boolean; allowDownload: boolean; printCopies: number | null; offlineDays: number | null; totalView: number; totalDownload: number;
  submited: string | null; lastUpdate: string | null; coverUrl: string | null; linkedBibPublicId: string | null;
  createdAt: string; updatedAt: string | null;
  abstract: string | null; description: string | null; journalName: string | null; publishDay: number | null; publishMonth: number | null; publishYear: number | null;
  authors: EbookPerson[]; advisors: EbookPerson[]; identifiers: EbookIdentifier[]; totalFile: number; share: number | null; show: number | null; indexContent: number | null;
}
export interface EbookPerson { last: string | null; first: string | null; title?: string | null; }
export interface EbookIdentifier { type: string; value: string; }
export interface EbookItemSearch extends CrudSearch {
  collectionId?: number | null; subjectId?: number | null; topicId?: number | null; typeId?: number | null;
  title?: string | null; author?: string | null; publisher?: string | null; publishYearFrom?: number | null; publishYearTo?: number | null;
  submittedFrom?: string | null; submittedTo?: string | null; free?: boolean | null;
}
/** Chọn tài liệu để thao tác hàng loạt: các dòng đã tích HOẶC toàn bộ kết quả của bộ lọc. */
export interface ItemSelection { publicIds?: string[] | null; search?: EbookItemSearch | null; }
export interface ItemBulkResult { updated: number; skipped: { publicId: string; title: string; code: string; reason: string }[]; }
export interface ItemImportResult { imported: number; failed: number; errors: string[]; warnings: string[]; message: string; }
export interface ItemImportDefaults { collectionId: number | null; subjectId: number | null; topicId: number | null; published: boolean; free: boolean; allowDownload: boolean; }
export interface CollectionPermission {
  readerTypeId: number; read: boolean; download: boolean; maxDocument: number | null; offlineDays: number | null; reservationExpireHours: number | null;
}
export interface EbookFile {
  id: number; publicId: string; ebookItemPublicId: string; mediaFileId: string; type: string | null; isConvert: boolean; fileType: string | null;
  fileSize: number | null; fileExt: string | null; description: string | null; source: string | null; formatId: number | null;
  checkSumAlgorithm: string | null; sortOrder: number; createdAt: string; updatedAt: string | null;
}
export interface EbookFileSearch extends CrudSearch { ebookItemPublicId?: string | null; }
/** Một biểu ghi sách in tìm được ở catalog — ô chọn liên kết của màn thêm/sửa tài liệu số. */
export interface BibLinkOption { bibPublicId: string; title: string; author: string | null; publisher: string | null; publishYear: string | null; }

/** status: 1 Đang mượn, 2 Đã thu hồi, 3 Đã trả, 4 Tự hết hạn. */
export interface EbookLoan {
  id: number; publicId: string; ebookItemId: number; ebookItemPublicId: string; title: string | null; readerPublicId: string;
  cardNo: string | null; readerName: string | null; checkedOutAt: string; expiresAt: string | null; lastAccessAt: string | null;
  status: number; recalledAt: string | null; recalledBy: number | null; recallReason: string | null;
}
export interface EbookLoanSearch extends CrudSearch { ebookItemPublicId?: string | null; readerPublicId?: string | null; loanStatus?: number | null; }
export const EBOOK_LOAN_STATUSES: { code: number; name: string; badge: string }[] = [
  { code: 1, name: 'DIGITAL_LOAN.STATUS_1', badge: 'badge-on' },
  { code: 2, name: 'DIGITAL_LOAN.STATUS_2', badge: 'badge-warn' },
  { code: 3, name: 'DIGITAL_LOAN.STATUS_3', badge: 'badge-off' },
  { code: 4, name: 'DIGITAL_LOAN.STATUS_4', badge: 'badge-off' },
];
/** status: 1 Chờ, 2 Sẵn sàng, 3 Đã mượn, 4 Đã huỷ, 5 Hết hạn. */
export interface EbookReservation {
  id: number; publicId: string; ebookItemId: number; ebookItemPublicId: string; title: string | null; readerPublicId: string;
  cardNo: string | null; readerName: string | null; requestedAt: string; status: number; readyAt: string | null; readyExpiresAt: string | null;
  fulfilledAt: string | null; cancelledAt: string | null; queuePosition: number | null;
}
export interface EbookReservationSearch extends CrudSearch { ebookItemPublicId?: string | null; readerPublicId?: string | null; reservationStatus?: number | null; }
export const EBOOK_RESERVATION_STATUSES: { code: number; name: string; badge: string }[] = [
  { code: 1, name: 'DIGITAL_RESERVATION.STATUS_1', badge: 'badge-warn' },
  { code: 2, name: 'DIGITAL_RESERVATION.STATUS_2', badge: 'badge-info' },
  { code: 3, name: 'DIGITAL_RESERVATION.STATUS_3', badge: 'badge-on' },
  { code: 4, name: 'DIGITAL_RESERVATION.STATUS_4', badge: 'badge-off' },
  { code: 5, name: 'DIGITAL_RESERVATION.STATUS_5', badge: 'badge-off' },
];

/** status: 1 Chờ duyệt, 2 Đã duyệt, 3 Từ chối. */
export interface DocumentSubmission {
  id: number; publicId: string; readerPublicId: string; cardNo: string | null; readerName: string | null; title: string; author: string | null;
  digTypeId: number | null; digTypeCode: string | null; abstract: string | null; mediaFileId: string; fileName: string; fileExt: string | null;
  fileSize: number; status: number; rejectReason: string | null; reviewedBy: number | null; reviewedAt: string | null;
  createdItemPublicId: string | null; createdAt: string;
}
export interface DocumentSubmissionSearch extends CrudSearch { readerPublicId?: string | null; submissionStatus?: number | null; }
export const DOCUMENT_SUBMISSION_STATUSES: { code: number; name: string; badge: string }[] = [
  { code: 1, name: 'DIGITAL_SUBMISSIONS_PAGE.STATUS_1', badge: 'badge-warn' },
  { code: 2, name: 'DIGITAL_SUBMISSIONS_PAGE.STATUS_2', badge: 'badge-on' },
  { code: 3, name: 'DIGITAL_SUBMISSIONS_PAGE.STATUS_3', badge: 'badge-off' },
];

// ── Không gian & đặt phòng (service space) ──
export interface SpaceBuilding { id: number; publicId: string; code: string; name: string; description: string | null; createdAt: string; updatedAt: string | null; }
export interface SpaceFloor {
  id: number; publicId: string; buildingId: number; buildingName: string | null; name: string; floorNumber: number | null;
  layoutImageUrl: string | null; canvasWidth: number; canvasHeight: number; createdAt: string; updatedAt: string | null;
}
export const SPACE_KIND_ROOM = 1;
export const SPACE_KIND_STUDY = 2;
export const SPACE_CATEGORIES: { value: number; label: string }[] = [
  { value: 1, label: 'SPACE_CAT.CATEGORY_1' },
  { value: 2, label: 'SPACE_CAT.CATEGORY_2' },
  { value: 3, label: 'SPACE_CAT.CATEGORY_3' },
  { value: 4, label: 'SPACE_CAT.CATEGORY_4' },
];
export interface SpaceRoom {
  id: number; publicId: string; floorId: number; floorName: string | null; buildingId: number | null; buildingName: string | null;
  code: string; name: string; kind: number; category: number; capacity: number; minAdvanceMinutes: number; maxAdvanceDays: number;
  maxAdvanceHours: number | null; maxBookingMinutesPerReader: number; checkInGraceMinutes: number; autoApprove: boolean; rules: string | null;
  allowedReaderTypeIds: number[]; minGroupSize: number | null; colorHex: string | null; iconName: string | null;
  positionX: number | null; positionY: number | null; width: number | null; height: number | null; maintenance: boolean;
  maintenanceNote: string | null; status: number; createdAt: string; updatedAt: string | null;
}

/** Vật dụng đặt trên sơ đồ tầng, không đặt được (monolith: map.MapObject, ngoài ROOM/STUDY_SPACE). */
export interface SpaceFixture {
  id: number; publicId: string; floorId: number; floorName: string | null; buildingId: number | null; buildingName: string | null;
  code: string; name: string; objectType: string;
  positionX: number | null; positionY: number | null; width: number | null; height: number | null; colorHex: string | null; iconName: string | null;
  shelfCategoryRange: string | null; shelfSubjectName: string | null; shelfCapacity: number | null; shelfDescription: string | null;
  storePublicId: string | null; storeName: string | null; status: number; createdAt: string; updatedAt: string | null;
}
/** Kho vật lý (bản sao từ Holdings, StoreChanged) — gợi ý khi đặt giá sách, chỉ là thông tin mô tả. */
export interface SpaceStoreOption { storePublicId: string; code: string; name: string; }
/** Thiết bị gắn theo một vật dụng (monolith: map.MapEquipment). */
export interface SpaceFixtureEquipment {
  id: number; publicId: string; fixtureId: number; name: string; description: string | null; quantity: number | null; conditionStatus: number;
  createdAt: string; updatedAt: string | null;
}
/** Tiện ích theo tầng (monolith: map.MapFloorUtility) — bình chữa cháy, camera... */
export interface SpaceFloorUtility {
  id: number; publicId: string; floorId: number; name: string; description: string | null; quantity: number | null; conditionStatus: number;
  iconName: string | null; createdAt: string; updatedAt: string | null;
}
export const SPACE_CONDITION_STATUSES: { value: number; label: string }[] = [
  { value: 1, label: 'SPACE_FLOOR_MAP_PAGE.CONDITION_1' },
  { value: 2, label: 'SPACE_FLOOR_MAP_PAGE.CONDITION_2' },
  { value: 3, label: 'SPACE_FLOOR_MAP_PAGE.CONDITION_3' },
];
export const SPACE_FIXTURE_TYPES: { value: string; label: string; icon: string }[] = [
  { value: 'SHELF', label: 'SPACE_FLOOR_MAP_PAGE.TYPE_SHELF', icon: 'menu_book' },
  { value: 'PC', label: 'SPACE_FLOOR_MAP_PAGE.TYPE_PC', icon: 'computer' },
  { value: 'COUNTER', label: 'SPACE_FLOOR_MAP_PAGE.TYPE_COUNTER', icon: 'storefront' },
  { value: 'SERVER_ROOM', label: 'SPACE_FLOOR_MAP_PAGE.TYPE_SERVER_ROOM', icon: 'dns' },
  { value: 'OFFICE', label: 'SPACE_FLOOR_MAP_PAGE.TYPE_OFFICE', icon: 'business_center' },
  { value: 'DOOR', label: 'SPACE_FLOOR_MAP_PAGE.TYPE_DOOR', icon: 'sensor_door' },
  { value: 'ELEVATOR', label: 'SPACE_FLOOR_MAP_PAGE.TYPE_ELEVATOR', icon: 'elevator' },
  { value: 'RESTROOM', label: 'SPACE_FLOOR_MAP_PAGE.TYPE_RESTROOM', icon: 'wc' },
  { value: 'OTHER', label: 'SPACE_FLOOR_MAP_PAGE.TYPE_OTHER', icon: 'category' },
];

/** Tủ đựng đồ trên sơ đồ tầng (monolith: PrintBook.Cabinet) — lưới Rows×Cols ngăn, đặt theo FloorId như SpaceFixture. */
export interface SpaceLockerCabinet {
  id: number; publicId: string; floorId: number; floorName: string | null; buildingId: number | null; buildingName: string | null;
  code: string; name: string; rows: number; cols: number;
  positionX: number | null; positionY: number | null; width: number | null; height: number | null;
  colorHex: string | null; iconName: string | null; note: string | null; status: number; createdAt: string; updatedAt: string | null;
}
/** Ngăn của giá sách theo khoảng DDC (monolith: map.MapShelfRow). Bản sách có DDC trong khoảng (hoặc bắt đầu bằng DDC đến) thuộc ngăn này. */
export interface SpaceShelfRow { id: number; publicId: string; fixtureId: number; rowIndex: number; ddcStart: string; ddcEnd: string; description: string | null; createdAt: string; updatedAt: string | null; }
/** Ngăn trong tủ (monolith: PrintBook.CabinetCompartment) — sinh tự động theo Rows×Cols khi tạo/sửa tủ. */
export interface SpaceLockerCompartment {
  id: number; publicId: string; cabinetId: number; rowIndex: number; colIndex: number; code: string; name: string | null; note: string | null;
  status: number; createdAt: string; updatedAt: string | null;
}
/** Lượt mượn/trả ngăn tủ (monolith: KeyOut+KeyIn gộp). loanStatus: 1 Đang mượn, 2 Đã trả — suy ra từ returnedAt. */
export interface SpaceLockerLoan {
  id: number; publicId: string; compartmentId: number; compartmentCode: string | null; cabinetId: number | null; cabinetCode: string | null;
  readerPublicId: string; cardNo: string; readerName: string | null; borrowedAt: string; returnedAt: string | null; note: string | null;
  loanStatus: number; createdAt: string;
}
export interface SpaceLockerLoanSearch extends CrudSearch { cardNo?: string | null; compartmentId?: number | null; loanStatus?: number | null; from?: string | null; to?: string | null; }

export interface SpaceMember { readerPublicId: string; cardNo: string; name: string | null; }
/** status: 1 Chờ duyệt, 2 Đã duyệt, 3 Đang sử dụng, 4 Hoàn tất, 5 Đã huỷ, 6 Từ chối, 7 Không tới. */
export interface SpaceBooking {
  id: number; publicId: string; readerPublicId: string; cardNo: string; readerName: string | null; spaceId: number; spaceName: string | null;
  floorName: string | null; buildingName: string | null; startAt: string; endAt: string; partySize: number; status: number;
  approvedBy: number | null; approvedAt: string | null; rejectReason: string | null; cancelledAt: string | null; checkedInAt: string | null;
  checkedOutAt: string | null; note: string | null; createdAt: string; members: SpaceMember[];
}
export interface SpaceBookingSearch extends CrudSearch { cardNo?: string | null; spaceId?: number | null; bookingStatus?: number | null; from?: string | null; to?: string | null; }
export const SPACE_BOOKING_STATUSES: { code: number; name: string; badge: string }[] = [
  { code: 1, name: 'SPACE_BOOKINGS_PAGE.STATUS_1', badge: 'badge-warn' },
  { code: 2, name: 'SPACE_BOOKINGS_PAGE.STATUS_2', badge: 'badge-info' },
  { code: 3, name: 'SPACE_BOOKINGS_PAGE.STATUS_3', badge: 'badge-on' },
  { code: 4, name: 'SPACE_BOOKINGS_PAGE.STATUS_4', badge: 'badge-off' },
  { code: 5, name: 'SPACE_BOOKINGS_PAGE.STATUS_5', badge: 'badge-off' },
  { code: 6, name: 'SPACE_BOOKINGS_PAGE.STATUS_6', badge: 'badge-off' },
  { code: 7, name: 'SPACE_BOOKINGS_PAGE.STATUS_7', badge: 'badge-off' },
];
export interface SpaceBusySlot { startAt: string; endAt: string; }
export interface SpaceAvailability {
  closed: boolean; openTime: string | null; closeTime: string | null; closedReason: string | null; maintenance: boolean;
  maintenanceNote: string | null; busy: SpaceBusySlot[];
}

export interface SpaceBan {
  id: number; publicId: string; readerPublicId: string; cardNo: string; readerName: string | null; reason: string; source: number;
  bannedUntil: string; liftedAt: string | null; liftedBy: number | null; isActive: boolean; createdAt: string;
}
export interface SpaceBanSearch extends CrudSearch { activeOnly?: boolean | null; }

export interface SpaceWeeklyHourRow { weekday: number; openTime: string; closeTime: string; isClosed: boolean; }
export interface SpaceSpecialDay {
  id: number; publicId: string; date: string; category: number | null; isClosed: boolean; openTime: string | null; closeTime: string | null;
  reason: string | null; createdAt: string; updatedAt: string | null;
}
export interface SpaceSettings {
  defaultOpenTime: string; defaultCloseTime: string; maxPerDay: number; maxActive: number; maxSessionMinutes: number;
  noShowLimit: number; noShowWindowDays: number; banDays: number;
}

export interface SpaceReportRow { spaceId: number; spaceName: string; bookings: number; completed: number; noShow: number; cancelled: number; usedHours: number; }
export interface SpaceTopReaderRow { cardNo: string; readerName: string | null; bookings: number; noShow: number; }
export interface SpaceReport {
  totalBookings: number; pending: number; approved: number; checkedIn: number; completed: number; cancelled: number; rejected: number; noShow: number;
  noShowRatePercent: number; cancelRatePercent: number; totalUsedHours: number; totalVisitors: number;
  bySpace: SpaceReportRow[]; topReaders: SpaceTopReaderRow[];
}

export interface SpaceAccessDevice {
  id: number; publicId: string; code: string; name: string; spaceId: number | null; spaceName: string | null; lastSeenAt: string | null;
  status: number; createdAt: string; updatedAt: string | null;
}
export interface SpaceAccessStaffCard { id: number; publicId: string; cardUid: string; label: string | null; status: number; createdAt: string; updatedAt: string | null; }
export interface SpaceAccessScanLog {
  id: number; publicId: string; deviceId: number; deviceCode: string | null; cardUid: string; readerPublicId: string | null;
  readerName: string | null; bookingPublicId: string | null; allowed: boolean; reason: string; scannedAt: string;
}
export interface SpaceAccessScanLogSearch extends CrudSearch { deviceId?: number | null; allowed?: boolean | null; }

// ── File (service media): upload 2 bước qua URL ký sẵn ──
export interface UploadTicket { fileId: string; uploadUrl: string; expiresAt: string; maxBytes: number; }
export interface MediaFilePage { total: number; page: number; pageSize: number; items: MediaFile[]; }

/** Cổng thông tin (portal): tin tức — xem pages/portal/news.ts. */
export interface PortalNews {
  id: number; publicId: string; status: number; title: string; slug: string; brief: string | null; content: string | null; images: string | null; thumb: string | null;
  startTime: string | null; endTime: string | null; publishedAt: string | null; categoryId: number | null; categoryName: string | null; eventId: number | null;
  language: string | null; keyword: string | null; author: string | null; source: string | null; types: string | null; featured: boolean; allowComment: boolean;
  totalView: number; metaTitle: string | null; metaKeyword: string | null; metaDescription: string | null; createdAt: string; updatedAt: string | null;
}
export interface PortalNewsSearch extends CrudSearch { categoryId?: number | null; eventId?: number | null; language?: string | null; }
export interface PortalPageItem {
  id: number; publicId: string; slug: string; title: string; content: string | null; language: string | null; metaDescription: string | null; status: number;
}
export interface PortalAttachment { id: number; publicId: string; newsId: number; name: string; url: string; fileSize: number | null; }

export interface MediaFile { id: string; purpose: string; fileName: string; contentType: string; size: number; status: string; url: string | null; }
export interface CreateTenant { code: string; name: string; subdomain: string; timeZone?: string | null; parentOrgId?: number | null; licenses: LicenseInput[]; }
export interface RoleRef { publicId: string; name: string; }
export interface User {
  publicId: string; id: number; userName: string; fullName: string; email: string | null; phone: string | null;
  isActive: boolean; lockedOut: boolean; lastLoginAt: string | null; roles: RoleRef[];
}
export interface Role { publicId: string; name: string; description: string | null; isBuiltIn: boolean; permissions: string[]; }
export interface RoleInput { name: string; description: string | null; permissions: string[]; }
/** Module quyền (danh mục quyền của identity); group phân cấp bằng " / " theo menu. */
export interface PermissionModule { code: string; name: string; group: string; actions: string[]; license: string | null; }
export interface CreateUser { userName: string; fullName: string; email?: string | null; phone?: string | null; password: string; roleIds?: string[]; }

// ── Danh mục CRUD chuẩn (building block Crud: 8 endpoint như GenericController của monolith) ──
export interface CrudSearch { keyword?: string; status?: number | null; pageIndex?: number; pageSize?: number; [extra: string]: unknown; }
export interface CrudPage<T> { items: T[]; totalCount: number; pageIndex: number; pageSize: number; totalPages: number; }
export interface NamedItem { id: number; publicId: string; name: string; createdAt: string; updatedAt: string | null; }
export interface Currency { id: number; publicId: string; code: string; name: string; exchangeRate: number; status: number; }
export interface SystemParameter {
  id: number; publicId: string; code: string; value: string | null; description: string | null; descriptionEn: string | null;
  type: string | null; service: string | null; isPublic: boolean; isBuiltIn: boolean;
}
export interface Org { id: number; publicId: string; name: string; parentId: number | null; level: number; sortOrder: number; status: number; link: string | null; }
export interface OrgNode extends Org { parentName: string | null; childCount: number; hasChildren: boolean; children: OrgNode[]; }

// ── Gửi tin (service notification) ──
export type SmtpSecurity = 'None' | 'StartTls' | 'SslOnConnect';
export type NotificationStatus = 'Sent' | 'Failed' | 'Skipped';
export interface EmailSettings {
  configured: boolean; host: string | null; port: number | null; security: SmtpSecurity | null; userName: string | null;
  hasPassword: boolean; fromAddress: string | null; fromName: string | null; status: number | null; platformAvailable: boolean;
}
export interface EmailSettingsInput {
  host: string; port: number; security: SmtpSecurity; userName: string | null; password: string | null; clearPassword: boolean;
  fromAddress: string; fromName: string | null; status: number;
}
export interface EmailTemplate { id: number; publicId: string; code: string; name: string; subject: string; body: string; status: number; isBuiltIn: boolean; }
/** Nhật ký giao dịch SIP2 — request/response đã che mật khẩu. */
export interface Sip2Log {
  id: number; stationId: number; stationCode: string | null; command: string; request: string; response: string; ok: boolean;
  elapsedMs: number; remoteIp: string | null; at: string;
}

export interface NotificationLog {
  id: number; publicId: string; createdAt: string; channel: string; templateCode: string; recipient: string | null;
  subject: string | null; status: NotificationStatus; error: string | null; viaPlatform: boolean;
}

// ── Nhật ký (service audit) ──
export interface AuditLog {
  publicId: string; occurredAt: string; actorId: number | null; actorKind: string; actorName: string | null; service: string; action: string;
  entityType: string; entityId: string | null; summary: string | null; ipAddress: string | null; tenantId?: number | null; tenantName?: string | null;
}

/** Trạng thái kiểu monolith: 2 = hoạt động, 1 = không hoạt động. */
export const STATUS_ACTIVE = 2;
export const STATUS_INACTIVE = 1;

/** Client cho một danh mục dưới /api/admin/<service>/<resource>. */
export class CrudClient<T extends { publicId: string }> {
  constructor(private readonly http: HttpClient, readonly base: string) {}

  search(body: CrudSearch) { return firstValueFrom(this.http.post<CrudPage<T>>(`${this.base}/Search`, body)); }
  searchAll(body: CrudSearch = {}) { return firstValueFrom(this.http.post<T[]>(`${this.base}/SearchAll`, body)); }
  get(publicId: string) { return firstValueFrom(this.http.get<T>(`${this.base}/GetById/${publicId}`)); }
  add(body: unknown) { return firstValueFrom(this.http.post<T>(`${this.base}/Add`, body)); }
  /** reason: lý do sửa (ghi vào lịch sử thay đổi) — header chỉ nhận ASCII nên mã hoá URL. */
  update(publicId: string, body: unknown, reason?: string | null) {
    return firstValueFrom(this.http.put<T>(`${this.base}/Update/${publicId}`, body, { headers: reasonHeader(reason) }));
  }
  delete(publicId: string) { return firstValueFrom(this.http.delete<void>(`${this.base}/Delete/${publicId}`)); }
  changeStatus(publicId: string, status: number) { return firstValueFrom(this.http.put<void>(`${this.base}/ChangeStatus`, { publicId, status })); }
}

// ── Nhập Excel (building block Crud: ImportTemplate + Import) ──
export interface ImportError { row: number; message: string; }
export interface ImportResult { imported: number; skipped: number; errors: ImportError[]; detail: string; }

/** Nhập Excel cho danh mục ở <base> (vd /api/admin/tenant/ethnicities). Lỗi theo dòng trả về trong body 400 (ImportResult). */
export class ImportClient {
  constructor(private readonly http: HttpClient, readonly base: string) {}

  template() { return firstValueFrom(this.http.get(`${this.base}/ImportTemplate`, { responseType: 'blob' })); }

  run(file: File, skipDuplicates: boolean) {
    const form = new FormData();
    form.append('file', file, file.name);
    return firstValueFrom(this.http.post<ImportResult>(`${this.base}/Import`, form, { params: { skipDuplicates } }));
  }
}

// ── Nhập bạn đọc nâng cao (patron: Import/Columns → Import/Jobs xem trước → Start chạy nền) ──
export interface ReaderImportHeader { index: number; title: string; }
export interface ReaderImportField { key: string; header: string; required: boolean; note: string | null; }
export interface ReaderImportColumns {
  headerRow: number; headers: ReaderImportHeader[]; suggested: Record<string, number>; fields: ReaderImportField[]; dataRows: number;
}
export interface ReaderImportOptions {
  headerRow: number; columns: Record<string, number>; overwrite: boolean; readerTypeId: number | null; autoCreateRefs: boolean;
  /** Bạn đọc mới đăng nhập OPAC bằng mật khẩu = số thẻ (bắt đổi ở lần đầu). */
  opacCardNoPassword: boolean;
  /** Cột Lớp/Khoá/Đơn vị chứa số id có sẵn thay cho tên (ELIB-LRC: nhập "theo mã"). */
  classByCode: boolean; courseByCode: boolean; orgByCode: boolean;
}
/** failed/errors: bạn đọc bỏ qua vì vi phạm quy tắc (ví dụ hạn thẻ trước ngày cấp). */
export interface ReaderBatchResult { count: number; failed: number; errors: string[] | null; jobId: string | null; }
export type ReaderImportStatus = 'Ready' | 'Queued' | 'Running' | 'Done' | 'Failed';
export interface ReaderImportJob {
  id: string; status: ReaderImportStatus; fileName: string; total: number; toCreate: number; toUpdate: number; errorCount: number;
  processed: number; created: number; updated: number; failed: number; hasErrorFile: boolean; message: string | null;
  errors: ImportError[]; createdAt: string; finishedAt: string | null;
}

/** PUT nội dung file vào URL ký sẵn của MinIO — dùng fetch (không qua HttpClient) để không gắn Authorization: chữ ký nằm trong URL. */
async function putToStorage(uploadUrl: string, file: File): Promise<void> {
  const response = await fetch(uploadUrl, { method: 'PUT', body: file, credentials: 'omit' });
  if (!response.ok) {
    throw new HttpErrorResponse({ status: response.status, error: { code: 'UPLOAD_FAILED', detail: `Upload failed (${response.status}).` } });
  }
}

function fileForm(file: File): FormData {
  const form = new FormData();
  form.append('file', file, file.name);
  return form;
}

/** Header X-Change-Reason (lý do sửa) — rỗng thì không gửi. */
export function reasonHeader(reason?: string | null): Record<string, string> {
  const text = reason?.trim();
  return text ? { 'X-Change-Reason': encodeURIComponent(text.slice(0, 500)) } : {};
}

/** Lưu blob thành file tải về. */
export function saveFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

/** Yêu cầu trả file: lỗi problem+json đến dưới dạng Blob → đọc lại thành JSON để errorMessage() hiện đúng thông báo. */
export async function blobRequest(request: Promise<Blob>): Promise<Blob> {
  try {
    return await request;
  } catch (e) {
    if (e instanceof HttpErrorResponse && e.error instanceof Blob) {
      let body: unknown = null;
      try { body = JSON.parse(await e.error.text()); } catch { /* không phải JSON */ }
      throw new HttpErrorResponse({ status: e.status, statusText: e.statusText, url: e.url ?? undefined, error: body });
    }
    throw e;
  }
}

/**
 * Thông báo lỗi theo ngôn ngữ đang chọn: dịch theo mã lỗi ổn định của service (ERRORS.<code>), chưa có bản dịch thì dùng detail
 * (tiếng Việt) của problem+json. Phần dưới chỉ chạy khi app chưa khởi tạo ngôn ngữ (test).
 */
export function errorMessage(error: unknown): string {
  const translated = translateError(error);
  if (translated !== null) return translated;
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { detail?: string; title?: string; code?: string } | null;
    if (body?.detail) return body.detail;
    if (error.status === 0) return 'Không kết nối được máy chủ.'; // i18n-data: dự phòng khi từ điển chưa nạp
    if (error.status === 403) return 'Bạn không có quyền thực hiện chức năng này.'; // i18n-data: dự phòng
    return body?.title ?? `HTTP ${error.status}`;
  }
  if (error instanceof Error && error.message) return error.message;
  return 'Đã xảy ra lỗi không xác định.'; // i18n-data: dự phòng khi từ điển chưa nạp
}

/**
 * API qua gateway. Tiền tố theo docs 03 §2:
 * /api/me (mọi host), /api/system/<service>/... (host hệ thống), /api/admin/<service>/... (host đơn vị).
 */
@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  private get<T>(url: string, params?: Record<string, string | number | undefined>): Promise<T> {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(params ?? {})) if (v !== undefined && v !== '') p = p.set(k, v);
    return firstValueFrom(this.http.get<T>(url, { params: p }));
  }
  private post<T>(url: string, body: unknown = {}): Promise<T> { return firstValueFrom(this.http.post<T>(url, body)); }
  private put<T>(url: string, body: unknown): Promise<T> { return firstValueFrom(this.http.put<T>(url, body)); }

  // Tài khoản của tôi
  me() { return this.get<Me>('/api/me'); }
  changePassword(currentPassword: string, newPassword: string) { return this.post<void>('/api/me/password', { currentPassword, newPassword }); }
  /** Tuỳ chọn giao diện theo tài khoản (ELIB-LRC: AdminPreference) — giá trị là chuỗi JSON. */
  preferences() { return this.get<Record<string, string>>('/api/me/preferences'); }
  setPreference(key: string, value: string | null) { return this.put<void>(`/api/me/preferences/${encodeURIComponent(key)}`, { value }); }

  // Host đơn vị
  features() { return this.get<Features>('/api/admin/tenant/features'); }
  users(search?: string, page = 1) { return this.get<PagedResult<User>>('/api/admin/identity/users', { search, page, pageSize: 20 }); }
  createUser(body: CreateUser) { return this.post<User>('/api/admin/identity/users', body); }
  roles() { return this.get<Role[]>('/api/admin/identity/roles'); }
  createRole(body: RoleInput) { return this.post<Role>('/api/admin/identity/roles', body); }
  updateRole(publicId: string, body: RoleInput) { return this.put<Role>(`/api/admin/identity/roles/${publicId}`, body); }
  deleteRole(publicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/identity/roles/${publicId}`)); }
  permissionCatalog() { return this.get<PermissionModule[]>('/api/admin/identity/permission-catalog'); }

  // Host hệ thống
  tenants(search?: string, status?: TenantState | '', page = 1) {
    return this.get<PagedResult<TenantSummary>>('/api/system/tenant/tenants', { search, status: status || undefined, page, pageSize: 20 });
  }
  /** Mọi đơn vị (tải từng trang 200) — bộ lọc của thống kê toàn hệ thống. */
  async allTenants(): Promise<TenantSummary[]> {
    const all: TenantSummary[] = [];
    for (let page = 1; ; page++) {
      const r = await this.get<PagedResult<TenantSummary>>('/api/system/tenant/tenants', { page, pageSize: 200 });
      all.push(...r.items);
      if (r.items.length === 0 || all.length >= r.total) return all;
    }
  }
  // Thống kê toàn hệ thống (host hệ thống): mỗi service một endpoint, một dòng cho mỗi đơn vị có số liệu.
  platformReaderStats(body: PlatformStatsRequest) { return this.post<ReaderPlatformStats[]>('/api/system/patron/stats/readers', body); }
  platformCirculationStats(body: PlatformStatsRequest) { return this.post<CirculationPlatformStats>('/api/system/circulation/stats/circulation', body); }
  platformItemStats(body: PlatformStatsRequest) { return this.post<ItemPlatformStats[]>('/api/system/holdings/stats/items', body); }
  async tenantCount(status?: TenantState) {
    return (await this.get<PagedResult<TenantSummary>>('/api/system/tenant/tenants', { status, page: 1, pageSize: 1 })).total;
  }
  tenant(publicId: string) { return this.get<Tenant>(`/api/system/tenant/tenants/${publicId}`); }
  modules() { return this.get<ModuleDef[]>('/api/system/tenant/modules'); }
  createTenant(body: CreateTenant) { return this.post<Tenant>('/api/system/tenant/tenants', body); }
  setLicenses(publicId: string, licenses: LicenseInput[]) { return this.put<Tenant>(`/api/system/tenant/tenants/${publicId}/licenses`, { licenses }); }
  suspendTenant(publicId: string, reason: string) { return this.post<Tenant>(`/api/system/tenant/tenants/${publicId}/suspend`, { reason }); }
  activateTenant(publicId: string) { return this.post<Tenant>(`/api/system/tenant/tenants/${publicId}/activate`); }
  resyncTenant(publicId: string) { return this.post<Tenant>(`/api/system/tenant/tenants/${publicId}/resync`); }
  setTenantBranding(publicId: string, body: { logoText: string | null; logoUrl: string | null }) {
    return this.put<Tenant>(`/api/system/tenant/tenants/${publicId}/branding`, body);
  }

  /**
   * Quản trị nền tảng upload hộ đơn vị (logo): xin URL → PUT thẳng nội dung lên kho (/s3, không kèm token)
   * → media kiểm tra nội dung (magic bytes) và trả URL công khai.
   */
  async uploadForTenant(tenantId: number, purpose: string, file: File): Promise<MediaFile> {
    const ticket = await this.post<UploadTicket>('/api/system/media/files/uploads', {
      tenantId, purpose, fileName: file.name, contentType: file.type, size: file.size,
    });
    await putToStorage(ticket.uploadUrl, file);
    return this.post<MediaFile>(`/api/system/media/files/${tenantId}/${ticket.fileId}/complete`);
  }

  /** Cán bộ đơn vị upload file: xin URL → PUT nội dung lên kho → media kiểm tra nội dung. */
  async upload(purpose: string, file: File): Promise<MediaFile> {
    const ticket = await this.post<UploadTicket>('/api/admin/media/files/uploads', {
      purpose, fileName: file.name, contentType: file.type, size: file.size,
    });
    await putToStorage(ticket.uploadUrl, file);
    return this.post<MediaFile>(`/api/admin/media/files/${ticket.fileId}/complete`);
  }
  /** Media lấy ảnh từ URL ngoài về kho công khai (ảnh bìa) — trang của hệ thống chỉ hiện ảnh cùng origin. */
  importMedia(purpose: string, url: string) { return this.post<MediaFile>('/api/admin/media/files/import', { purpose, url }); }
  /** URL xem file: cố định với file công khai, ký có hạn (vài phút) với file riêng tư. */
  async fileUrl(fileId: string): Promise<string> {
    return (await this.get<{ url: string }>(`/api/admin/media/files/${fileId}/download`)).url;
  }
  retryProvisioning(publicId: string) { return this.post<Tenant>(`/api/system/tenant/tenants/${publicId}/provisioning/retry`); }
  createTenantAdmin(tenantId: number, body: { userName: string; fullName: string; email?: string | null; password: string }) {
    return this.post<User>(`/api/system/identity/tenants/${tenantId}/admins`, body);
  }
  impersonate(tenantId: number, reason: string) {
    return this.post<{ url: string; expiresAt: string }>('/api/system/identity/impersonation', { tenantId, reason });
  }
  seedTenantDefaults(publicId: string) { return this.post<{ added: number }>(`/api/system/tenant/tenants/${publicId}/defaults`); }

  // Danh mục của đơn vị (service tenant)
  crud<T extends { publicId: string }>(resource: string, service = 'tenant') { return new CrudClient<T>(this.http, `/api/admin/${service}/${resource}`); }
  importer(resource: string, service = 'tenant') { return new ImportClient(this.http, `/api/admin/${service}/${resource}`); }

  // Bạn đọc (patron): 8 endpoint chuẩn qua crud('readers', 'patron') + khoá/mở thẻ, sửa hàng loạt
  lockReader(publicId: string, reason: string | null) { return this.post<Reader>(`/api/admin/patron/readers/Lock/${publicId}`, { reason }); }
  unlockReader(publicId: string) { return this.post<Reader>(`/api/admin/patron/readers/Unlock/${publicId}`); }
  /** Link xem nhiều file (ảnh thẻ) một lần — tối đa 100; file không thuộc đơn vị/chưa xong bị bỏ qua. */
  fileUrls(ids: string[]) { return this.post<{ id: string; url: string; expiresAt: string | null }[]>('/api/admin/media/files/urls', { ids }); }
  readerImportColumns(file: File) { return this.post<ReaderImportColumns>('/api/admin/patron/readers/Import/Columns', fileForm(file)); }
  previewReaderImport(file: File, options: ReaderImportOptions) {
    const form = fileForm(file);
    form.append('options', JSON.stringify(options));
    return this.post<ReaderImportJob>('/api/admin/patron/readers/Import/Jobs', form);
  }
  readerImportJob(id: string) { return this.get<ReaderImportJob>(`/api/admin/patron/readers/Import/Jobs/${id}`); }
  startReaderImport(id: string) { return this.post<ReaderImportJob>(`/api/admin/patron/readers/Import/Jobs/${id}/Start`); }
  readerImportErrors(id: string) {
    return blobRequest(firstValueFrom(this.http.get(`/api/admin/patron/readers/Import/Jobs/${id}/Errors`, { responseType: 'blob' })));
  }
  previewReaderSelection(search: ReaderSearch) { return this.post<ReaderSelectionPreview>('/api/admin/patron/readers/Batch/Preview', search); }
  /** Trên 2.000 bạn đọc (theo bộ lọc): server chạy nền và trả jobId — theo dõi bằng readerBatchJob. */
  batchUpdateReaders(body: ReaderBatchUpdate, reason?: string | null) {
    return firstValueFrom(this.http.put<ReaderBatchResult>('/api/admin/patron/readers/BatchUpdate', body, { headers: reasonHeader(reason) }));
  }
  readerBatchJob(id: string) { return this.get<ReaderImportJob>(`/api/admin/patron/readers/Batch/Jobs/${id}`); }
  bulkDeleteReaders(publicIds: string[]) { return this.post<{ count: number }>('/api/admin/patron/readers/BulkDelete', { publicIds }); }
  changeReaderGroups(selection: ReaderSelection, groupId: number, add: boolean) {
    return this.put<{ count: number }>('/api/admin/patron/readers/Groups', { selection, groupId, add });
  }
  readerGroups(publicId: string) { return this.get<number[]>(`/api/admin/patron/readers/Groups/${publicId}`); }
  setReaderGroups(publicId: string, groupIds: number[], reason?: string | null) {
    return firstValueFrom(this.http.put<number[]>(`/api/admin/patron/readers/Groups/${publicId}`, { groupIds }, { headers: reasonHeader(reason) }));
  }
  /** Lịch sử thay đổi của một bản ghi (audit) — vd entityHistory('Reader', publicId). */
  entityHistory(entityType: string, publicId: string) { return this.get<EntityHistory[]>(`/api/admin/audit/audit-logs/entity/${entityType}/${publicId}`); }
  bulkUpdateReaders(body: ReaderBulkUpdate) { return this.put<{ updatedCount: number }>('/api/admin/patron/readers/BulkUpdate', body); }
  /** Mật khẩu OPAC của bạn đọc nằm ở identity; useCardNo = mật khẩu mặc định là số thẻ. */
  readerAccounts(readerIds: string[]) { return this.post<ReaderAccount[]>('/api/admin/identity/reader-accounts/Status', { readerIds }); }
  setReaderPassword(readerId: string, password: string | null, useCardNo: boolean) {
    return this.put<ReaderAccount>(`/api/admin/identity/reader-accounts/${readerId}/password`, { password, useCardNo });
  }
  bulkSetReaderPassword(readerIds: string[], password: string | null, useCardNo: boolean) {
    return this.put<{ updatedCount: number; notFound: string[] }>('/api/admin/identity/reader-accounts/BulkPassword', { readerIds, password, useCardNo });
  }
  unlockReaderAccount(readerId: string) { return this.post<ReaderAccount>(`/api/admin/identity/reader-accounts/${readerId}/unlock`); }
  // Biên mục (catalog): 8 endpoint chuẩn qua crud('bibs' | 'bib-types' | 'worksheets', 'catalog')
  marc21Fields() { return this.get<MarcFieldDef[]>('/api/admin/catalog/marc21/fields'); }
  worksheetsByBibType(bibTypeId: number) { return this.get<Worksheet[]>(`/api/admin/catalog/worksheets/GetByBibType/${bibTypeId}`); }
  bibByMfn(mfn: number) { return this.get<Bib>(`/api/admin/catalog/bibs/GetByMfn/${mfn}`); }
  setBibCover(publicId: string, coverUrl: string | null) { return this.put<Bib>('/api/admin/catalog/bibs/Cover', { publicId, coverUrl }); }
  lookupCover(isbn: string) { return this.get<{ url: string | null }>('/api/admin/catalog/bibs/LookupCover', { isbn }); }
  checkIsbn(isbn: string, excludePublicId?: string) { return this.get<IsbnMatch[]>('/api/admin/catalog/bibs/CheckIsbn', { isbn, excludePublicId }); }
  previewMarc(file: File) { return this.post<MarcFilePreview>('/api/admin/catalog/bibs/PreviewMarc', fileForm(file)); }
  /** Nhập file MARC. Bị từ chối (có biểu ghi lỗi) → body 400 vẫn là MarcImportResult, trả về như kết quả thường. */
  async importMarc(file: File, o: MarcImportOptions): Promise<MarcImportResult> {
    const params: Record<string, string | number | boolean> = { status: o.status, skipDuplicates: o.skipDuplicates, skipInvalid: o.skipInvalid };
    if (o.bibTypeId != null) params['bibTypeId'] = o.bibTypeId;
    try {
      return await firstValueFrom(this.http.post<MarcImportResult>('/api/admin/catalog/bibs/ImportMarc', fileForm(file), { params }));
    } catch (e) {
      if (e instanceof HttpErrorResponse && (e.error as MarcImportResult | null)?.rejected) return e.error as MarcImportResult;
      throw e;
    }
  }
  exportMarc(search: BibSearch, format: MarcFormat) {
    return blobRequest(firstValueFrom(this.http.post('/api/admin/catalog/bibs/ExportMarc', { search, format }, { responseType: 'blob' })));
  }
  // Kho (holdings): crud('stores' | 'store-types' | 'items', 'holdings') + đăng ký theo lô, tìm, xếp giá
  lookupItems(search: ItemSearch) { return this.post<CrudPage<Item>>('/api/admin/holdings/items/Lookup', search); }
  /** Dữ liệu in nhãn mã vạch / nhãn gáy (quyền PRINT_LABEL hoặc CATALOG_BIBS). */
  itemLabels(body: ItemLabelRequest) { return this.post<ItemLabel[]>('/api/admin/holdings/items/Labels', body); }
  registerItems(body: RegisterItems) { return this.post<Item[]>('/api/admin/holdings/items/Register', body); }
  nextBarcode(prefix: string, digits: number) { return this.get<NextBarcode>('/api/admin/holdings/items/NextBarcode', { prefix, digits }); }
  shelveItems(body: { ids?: string[]; barcodes?: string[]; storeId?: number | null }) { return this.post<ShelveResult>('/api/admin/holdings/items/Shelve', body); }
  storeOptions() { return this.get<StoreOption[]>('/api/admin/holdings/stores/Options'); }
  // Thanh toán QR
  paymentAccount() { return this.get<PaymentAccount>('/api/admin/payment/payment-account'); }
  savePaymentAccount(body: PaymentAccountInput) { return this.put<PaymentAccount>('/api/admin/payment/payment-account', body); }
  paymentProviders() { return this.get<string[]>('/api/admin/payment/payments/Providers'); }
  createPayment(sourceService: string, sourceId: string, provider: string) {
    return this.post<PaymentTx>('/api/admin/payment/payments/Create', { sourceService, sourceId, provider });
  }
  paymentStatus(publicId: string) { return this.get<PaymentTx>(`/api/admin/payment/payments/${publicId}`); }
  searchPayments(search: PaymentSearch) { return this.post<PaymentSearchResult>('/api/admin/payment/payments/Search', search); }
  resolvePaymentReview(publicId: string, note: string) { return this.post<void>(`/api/admin/payment/payments/${publicId}/ResolveReview`, { note }); }
  searchWebhookLogs(search: CrudSearch & { provider?: string | null }) { return this.post<CrudPage<WebhookLogRow>>('/api/admin/payment/webhook-logs/Search', search); }
  resyncCharges() { return this.post<{ synced: number }>('/api/admin/payment/charges/Resync', {}); }
  // Phí cấp lại thẻ (patron)
  searchCardFees(search: CrudSearch & { cardNo?: string | null; feeStatus?: string | null }) { return this.post<CrudPage<CardFee>>('/api/admin/patron/card-fees/Search', search); }
  addCardFee(body: { cardNo: string; amount: number; reason: string }) { return this.post<CardFee>('/api/admin/patron/card-fees/Add', body); }
  settleCardFeeCash(publicId: string) { return this.post<CardFee>(`/api/admin/patron/card-fees/${publicId}/SettleCash`, {}); }
  cancelCardFee(publicId: string, reason: string) { return this.post<CardFee>(`/api/admin/patron/card-fees/${publicId}/Cancel`, { reason }); }
  searchMapShelving(body: MapShelvingSearch) { return this.post<CrudPage<MapShelvingRow>>('/api/admin/holdings/map-shelving/Search', body); }
  mapShelves(storeId: number) { return this.get<MapFloorNode[]>('/api/admin/holdings/map-shelving/Shelves', { storeId }); }
  placeItems(body: { ids: string[]; shelfPublicId: string; rowPublicId?: string | null; reason?: string | null }) {
    return this.post<PlaceResult>('/api/admin/holdings/map-shelving/Place', body);
  }
  itemMovements(publicId: string) { return this.get<ItemMovement[]>(`/api/admin/holdings/items/Movements/${publicId}`); }
  // Sách mất (LOST_BOOKS) / thanh lý (LIQUIDATES)
  searchLost(search: StoreRecordSearch) { return this.post<CrudPage<LostRow>>('/api/admin/holdings/lost-books/Search', search); }
  exportLost(search: StoreRecordSearch) { return this.holdingsFile('lost-books/Export', search); }
  markLost(body: { barcodes: string[]; lostOn: string | null; reason: string }) { return this.batch('/api/admin/holdings/lost-books/MarkLost', body); }
  /** Hoàn tác báo mất: lý do bắt buộc; trả pending khi cần duyệt (HTTP 202). */
  restoreLost(barcode: string, reason: string) { return this.undo('/api/admin/holdings/lost-books/Restore', { barcode, reason }); }
  searchLiquidations(search: StoreRecordSearch) { return this.post<CrudPage<LiquidationRow>>('/api/admin/holdings/liquidations/Search', search); }
  exportLiquidations(search: StoreRecordSearch) { return this.holdingsFile('liquidations/Export', search); }
  liquidate(body: { barcodes: string[]; liquidatedOn: string | null; reason: string; decisionNo?: string | null; decisionDate?: string | null }) {
    return this.batch('/api/admin/holdings/liquidations/Liquidate', body);
  }
  cancelLiquidation(barcode: string, reason: string) { return this.undo('/api/admin/holdings/liquidations/Cancel', { barcode, reason }); }
  private async batch(url: string, body: unknown): Promise<BatchOutcome> {
    const r = await firstValueFrom(this.http.post<ItemBatchResult | PendingApproval>(url, body, { observe: 'response' }));
    return r.status === 202 ? { result: null, pending: r.body as PendingApproval } : { result: r.body as ItemBatchResult, pending: null };
  }
  private async undo(url: string, body: unknown): Promise<PendingApproval | null> {
    const r = await firstValueFrom(this.http.post<PendingApproval | null>(url, body, { observe: 'response' }));
    return r.status === 202 ? (r.body as PendingApproval) : null;
  }
  // Sách ra vào kho (BOOK_OUT_STORE)
  storeOutOptions() { return this.get<StoreOutOptions>('/api/admin/holdings/store-outs/Options'); }
  searchStoreOuts(search: StoreOutSearch) { return this.post<CrudPage<StoreOutRow>>('/api/admin/holdings/store-outs/Search', search); }
  exportStoreOuts(search: StoreOutSearch) { return this.holdingsFile('store-outs/Export', search); }
  scanOut(body: ScanOut) { return this.post<StoreOutRow>('/api/admin/holdings/store-outs/ScanOut', body); }
  scanIn(barcode: string) { return this.post<StoreOutRow>('/api/admin/holdings/store-outs/ScanIn', { barcode }); }
  deleteStoreOut(publicId: string, reason: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/holdings/store-outs/Delete/${publicId}`, { headers: reasonHeader(reason) })); }
  // Kiểm kê (INVENTORY): crud('inventories', 'holdings') + quét, nhập Excel, đối chiếu
  closeInventory(publicId: string) { return this.post<Inventory>(`/api/admin/holdings/inventories/Close/${publicId}`); }
  reopenInventory(publicId: string) { return this.post<Inventory>(`/api/admin/holdings/inventories/Reopen/${publicId}`); }
  scanInventory(inventoryId: string, barcode: string, storeId: number | null) {
    return this.post<{ scan: InventoryScanRow; alreadyScanned: boolean }>('/api/admin/holdings/inventories/Scan', { inventoryId, barcode, storeId });
  }
  inventoryScanTemplate() { return blobRequest(firstValueFrom(this.http.get('/api/admin/holdings/inventories/ScanTemplate', { responseType: 'blob' }))); }
  /** Có dòng lỗi → body 400 vẫn là InventoryImportResult, trả về như kết quả thường. */
  async importInventoryScans(publicId: string, file: File): Promise<InventoryImportResult> {
    try {
      return await this.post<InventoryImportResult>(`/api/admin/holdings/inventories/ImportScans/${publicId}`, fileForm(file));
    } catch (e) {
      if (e instanceof HttpErrorResponse && (e.error as InventoryImportResult | null)?.errors?.length) return e.error as InventoryImportResult;
      throw e;
    }
  }
  inventoryScans(body: CrudSearch & { inventoryId: string; filter: string | null }) {
    return this.post<CrudPage<InventoryScanRow>>('/api/admin/holdings/inventories/Scans', body);
  }
  inventorySummary(publicId: string) { return this.get<InventorySummary>(`/api/admin/holdings/inventories/Summary/${publicId}`); }
  inventoryMissing(publicId: string, pageIndex: number, pageSize: number) {
    return this.get<CrudPage<MissingRow>>(`/api/admin/holdings/inventories/Missing/${publicId}`, { pageIndex, pageSize });
  }
  inventoryReport(publicId: string) {
    return blobRequest(firstValueFrom(this.http.get(`/api/admin/holdings/inventories/Report/${publicId}`, { responseType: 'blob' })));
  }
  // Điều chuyển kho (AB_MOVES) / giao nhận (AB_DELIVERERS): crud(kind, 'holdings') + dòng phiếu
  transferLines(kind: TransferKind, publicId: string) { return this.get<TransferLine[]>(`/api/admin/holdings/${kind}/Lines/${publicId}`); }
  transferCandidates(kind: TransferKind, body: CrudSearch & {
    transferId: string; mfn?: number | null; barcodeFrom?: string | null; barcodeTo?: string | null; receiptPublicId?: string | null;
  }) {
    return this.post<CrudPage<Item>>(`/api/admin/holdings/${kind}/Candidates`, body);
  }
  addTransferLines(kind: TransferKind, publicId: string, body: { itemIds?: string[]; barcodes?: string[] }) {
    return this.post<ItemBatchResult>(`/api/admin/holdings/${kind}/AddLines/${publicId}`, body);
  }
  removeTransferLine(kind: TransferKind, linePublicId: string) {
    return firstValueFrom(this.http.delete<void>(`/api/admin/holdings/${kind}/Lines/${linePublicId}`));
  }
  completeMove(publicId: string) { return this.post<CompleteResult>(`/api/admin/holdings/store-moves/Complete/${publicId}`); }
  signDelivery(publicId: string) { return this.post<CompleteResult>(`/api/admin/holdings/deliveries/Sign/${publicId}`); }
  /** Đơn nhận đã hoàn tất còn sách chưa phân bổ (+ đơn đang chọn của phiếu). */
  deliveryReceipts(include?: string | null) {
    return this.get<DeliveryReceiptOption[]>('/api/admin/holdings/deliveries/Receipts', include ? { include } : undefined);
  }
  // Bổ sung (acquisition): crud('orders' | 'receipts' | 'sources' | 'suppliers' | 'budgets' | 'funds', 'acquisition') + dòng, ĐKCB theo phiếu
  acquisitionOptions() { return this.get<AcquisitionOptions>('/api/admin/acquisition/options'); }
  lookupAcqBibs(keyword: string) { return this.post<AcqBib[]>('/api/admin/acquisition/bibs/Lookup', { keyword }); }
  searchAcqBibs(body: AcqBibSearch) { return this.post<CrudPage<AcqBibRow>>('/api/admin/acquisition/bibs/Search', body); }
  acqBibTypes() { return this.get<{ id: number; name: string }[]>('/api/admin/acquisition/bibs/Types'); }
  orderLines(publicId: string) { return this.get<OrderLine[]>(`/api/admin/acquisition/orders/Lines/${publicId}`); }
  saveOrderLine(publicId: string, body: OrderLineInput) { return this.post<OrderLine>(`/api/admin/acquisition/orders/SaveLine/${publicId}`, body); }
  removeOrderLine(linePublicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/acquisition/orders/Lines/${linePublicId}`)); }
  receiptLines(publicId: string) { return this.get<ReceiptLinesView>(`/api/admin/acquisition/receipts/Lines/${publicId}`); }
  saveReceiptLine(publicId: string, body: ReceiptLineInput) { return this.post<ReceiptLine>(`/api/admin/acquisition/receipts/SaveLine/${publicId}`, body); }
  removeReceiptLine(linePublicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/acquisition/receipts/Lines/${linePublicId}`)); }
  receivableLines(body: CrudSearch & { orderId?: number | null; supplierId?: number | null }) {
    return this.post<CrudPage<ReceivableLine>>('/api/admin/acquisition/receipts/Receivable', body);
  }
  registerReceiptItems(linePublicId: string, body: RegisterReceiptItems) {
    return this.post<RegisteredItem[]>(`/api/admin/acquisition/receipts/RegisterItems/${linePublicId}`, body);
  }
  removeReceiptItem(publicId: string, itemPublicId: string) {
    return firstValueFrom(this.http.delete<void>(`/api/admin/acquisition/receipts/Items/${publicId}/${itemPublicId}`));
  }
  completeReceipt(publicId: string) { return this.post<Receipt>(`/api/admin/acquisition/receipts/Complete/${publicId}`); }
  reopenReceipt(publicId: string) { return this.post<Receipt>(`/api/admin/acquisition/receipts/Reopen/${publicId}`); }
  /** Chốt thanh toán — làm được cả khi phiếu đã hoàn tất (quyền sửa phiếu nhận). */
  setReceiptPayment(publicId: string, body: { paymentStatus: number; paymentMethod: number }) {
    return this.post<Receipt>(`/api/admin/acquisition/receipts/Payment/${publicId}`, body);
  }
  /** Tiền tệ đang dùng + tỷ giá gợi ý (người có quyền bổ sung xem được, không cần quyền quản lý tiền tệ). */
  currencyOptions() { return this.get<Currency[]>('/api/admin/tenant/currencies/Options'); }
  /** Báo cáo bổ sung / tra cứu đơn nhận (quyền AB_RECEIPTS xem): danh sách, tra cứu theo dòng, phân bổ kho, sổ ĐKCB, thư mục sách mới. */
  acqReport<T>(name: AcqReportName, body: AcqReportRequest) { return this.post<T>(`/api/admin/acquisition/reports/${name}`, body); }
  exportAcqReport(name: AcqReportName, body: AcqReportRequest) {
    return blobRequest(firstValueFrom(this.http.post(`/api/admin/acquisition/reports/${name}Export`, body, { responseType: 'blob' })));
  }
  // Đăng ký lại mã vạch (RE_REGISTER)
  reRegister(body: { oldBarcode: string; newBarcode: string; storeId: number | null }) { return this.post<Item>('/api/admin/holdings/items/ReRegister', body); }
  reRegisterTemplate() { return blobRequest(firstValueFrom(this.http.get('/api/admin/holdings/items/ReRegister/Template', { responseType: 'blob' }))); }
  /** dryRun: chỉ kiểm tra. Có dòng lỗi → body 400 vẫn là kết quả, trả về như thường. */
  async reRegisterImport(file: File, dryRun: boolean): Promise<ReRegisterImportResult> {
    try {
      return await firstValueFrom(this.http.post<ReRegisterImportResult>('/api/admin/holdings/items/ReRegister/Import', fileForm(file), { params: { dryRun } }));
    } catch (e) {
      if (e instanceof HttpErrorResponse && (e.error as ReRegisterImportResult | null)?.lines) return e.error as ReRegisterImportResult;
      throw e;
    }
  }
  // Thống kê tài liệu (STAT_DOC), báo cáo sách trong kho (STORE_BOOK_REPORT)
  documentStats(body: DocumentStatsRequest) { return this.post<DocumentStats>('/api/admin/holdings/statistics/Document', body); }
  exportDocumentStats(body: DocumentStatsRequest) { return this.holdingsFile('statistics/DocumentExport', body); }
  storeBookReport(body: StoreBookReportRequest) { return this.post<StoreBookReport>('/api/admin/holdings/store-book-report/Search', body); }
  exportStoreBookReport(body: StoreBookReportRequest) { return this.holdingsFile('store-book-report/Export', body); }
  private holdingsFile(path: string, body: unknown) {
    return blobRequest(firstValueFrom(this.http.post(`/api/admin/holdings/${path}`, body, { responseType: 'blob' })));
  }
  // Lưu thông (circulation): quầy mượn trả + lịch sử; điểm lưu thông/chính sách qua crud('circ-places' | 'loan-policies', 'circulation')
  loanReader(cardNo: string, circPlaceId: number | null) { return this.post<ReaderPanel>('/api/admin/circulation/loans/Reader', { cardNo, circPlaceId }); }
  checkout(cardNo: string, barcodes: string[], circPlaceId: number) { return this.post<CheckoutResult>('/api/admin/circulation/loans/Checkout', { cardNo, barcodes, circPlaceId }); }
  returnLoan(body: { barcode?: string; loanId?: string; circPlaceId?: number | null }) { return this.post<ReturnResult>('/api/admin/circulation/loans/Return', body); }
  renewLoan(loanId: string, reason: string) { return this.post<Loan>('/api/admin/circulation/loans/Renew', { loanId, reason }); }
  noteLoan(loanId: string, note: string, reason: string) { return this.post<Loan>('/api/admin/circulation/loans/Note', { loanId, note, reason }); }
  searchLoans(search: LoanSearch) { return this.post<CrudPage<Loan>>('/api/admin/circulation/loans/Search', search); }
  exportLoans(search: LoanSearch) {
    return blobRequest(firstValueFrom(this.http.post('/api/admin/circulation/loans/Export', search, { responseType: 'blob' })));
  }
  // Vào/ra thư viện (quyền CHECK_IN_OUT)
  gateStatus(cardNo: string, circPlaceId: number | null) { return this.post<GateStatus>('/api/admin/circulation/visits/Status', { cardNo, circPlaceId }); }
  checkInGate(cardNo: string, circPlaceId: number) { return this.post<Visit>('/api/admin/circulation/visits/CheckIn', { cardNo, circPlaceId }); }
  checkOutGate(visitPublicId: string, circPlaceId: number | null) { return this.post<Visit>('/api/admin/circulation/visits/CheckOut', { visitPublicId, circPlaceId }); }
  searchVisits(search: VisitSearch) { return this.post<CrudPage<Visit>>('/api/admin/circulation/visits/Search', search); }
  exportVisits(search: VisitSearch) {
    return blobRequest(firstValueFrom(this.http.post('/api/admin/circulation/visits/Export', search, { responseType: 'blob' })));
  }
  deleteVisit(publicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/circulation/visits/Delete/${publicId}`)); }
  // Báo cáo lưu thông (quyền CIRC_REPORT)
  circulationReport(body: CirculationReportRequest) { return this.post<CirculationReport>('/api/admin/circulation/reports/Search', body); }
  exportCirculationReport(body: CirculationReportRequest) {
    return blobRequest(firstValueFrom(this.http.post('/api/admin/circulation/reports/Export', body, { responseType: 'blob' })));
  }
  // Chỉ mục tra cứu (search, quyền SEARCH_INDEX)
  searchIndexStatus() { return this.get<SearchIndexStatus>('/api/admin/search/index/Status'); }
  rebuildSearchIndex() { return this.post<{ status: string }>('/api/admin/search/index/Rebuild'); }
  // Tra cứu liên thư viện (search, Z39.50/SRU)
  testZ3950Server(publicId: string) { return this.post<{ ok: boolean; error: string | null; elapsedMs: number }>(`/api/admin/search/z3950-servers/${publicId}/Test`); }
  z3950Servers() { return this.get<Z3950ServerOption[]>('/api/admin/search/z3950/servers'); }
  z3950Search(body: Z3950SearchRequest) { return this.post<Z3950ServerResult[]>('/api/admin/search/z3950/Search', body); }
  searchStats(from: string | null, to: string | null) {
    const q: Record<string, string> = {};
    if (from) q['from'] = from;
    if (to) q['to'] = to;
    return this.get<SearchStatsSummary>('/api/admin/search/stats/Summary', q);
  }
  // Đặt mượn (circulation, quyền REQUEST_BOOKS)
  searchHolds(search: HoldSearch) { return this.post<CrudPage<Hold>>('/api/admin/circulation/holds/Search', search); }
  placeHold(body: { cardNo: string; mfn?: number | null; barcode?: string | null; note?: string | null }) { return this.post<Hold>('/api/admin/circulation/holds/Place', body); }
  cancelHold(holdId: string, reason: string | null) { return this.post<Hold>('/api/admin/circulation/holds/Cancel', { holdId, reason }); }
  // Phiếu phạt (circulation, quyền FINES); lý do phạt qua crud('fine-reasons', 'circulation')
  searchFineTickets(search: FineTicketSearch) { return this.post<CrudPage<FineTicket>>('/api/admin/circulation/fine-tickets/Search', search); }
  fineTicketTotals(search: FineTicketSearch) { return this.post<FineTicketTotals>('/api/admin/circulation/fine-tickets/Totals', search); }
  fineTicket(publicId: string) { return this.get<FineTicketDetail>(`/api/admin/circulation/fine-tickets/Detail/${publicId}`); }
  addFineTicket(body: { cardNo: string; amount: number; note: string | null }) { return this.post<FineTicket>('/api/admin/circulation/fine-tickets/Add', body); }
  buildFineTicket(cardNo: string, loanIds: string[] = [], circPlaceId: number | null = null) {
    return this.post<FineTicketDetail>('/api/admin/circulation/fine-tickets/BuildForReader', { cardNo, loanIds, circPlaceId });
  }
  async saveFineTicket(publicId: string, body: SaveFineTicket): Promise<FineSaveResult> {
    return this.fineResult(await firstValueFrom(this.http.put<FineTicketDetail | PendingApproval>(`/api/admin/circulation/fine-tickets/Save/${publicId}`, body, { observe: 'response' })));
  }
  /** Hoàn tiền / điều chỉnh giảm số đã thu (amount âm) — luôn có lý do. */
  async forceCloseLoan(loanId: string, reason: string): Promise<LoanActionResult> {
    return this.loanResult(await firstValueFrom(this.http.post<Loan | PendingApproval>('/api/admin/circulation/loans/ForceClose', { loanId, reason }, { observe: 'response' })));
  }
  async reopenLoan(loanId: string, reason: string): Promise<LoanActionResult> {
    return this.loanResult(await firstValueFrom(this.http.post<Loan | PendingApproval>('/api/admin/circulation/loans/Reopen', { loanId, reason }, { observe: 'response' })));
  }
  private loanResult(response: HttpResponse<Loan | PendingApproval>): LoanActionResult {
    return response.status === 202 ? { loan: null, pending: response.body as PendingApproval } : { loan: response.body as Loan, pending: null };
  }
  async adjustFineTicket(publicId: string, body: { amount: number; reason: string; note?: string | null }): Promise<FineSaveResult> {
    return this.fineResult(await firstValueFrom(this.http.post<FineTicketDetail | PendingApproval>(`/api/admin/circulation/fine-tickets/Adjust/${publicId}`, body, { observe: 'response' })));
  }
  deleteFineTicket(publicId: string, reason: string) {
    return firstValueFrom(this.http.delete<void>(`/api/admin/circulation/fine-tickets/Delete/${publicId}`, { headers: reasonHeader(reason) }));
  }
  private fineResult(response: HttpResponse<FineTicketDetail | PendingApproval>): FineSaveResult {
    return response.status === 202
      ? { detail: null, pending: response.body as PendingApproval }
      : { detail: response.body as FineTicketDetail, pending: null };
  }

  // Phê duyệt theo thẩm quyền — mỗi service có hộp riêng; màn "Chờ phê duyệt" gộp các service có quyền.
  searchApprovals(service: ApprovalService, scope: 'inbox' | 'mine', status: number | null, pageIndex = 1, pageSize = 50) {
    return this.post<CrudPage<ChangeRequest>>(`/api/admin/${service}/approvals/Search`, { scope, status, pageIndex, pageSize });
  }
  approvalCount(service: ApprovalService) { return this.get<{ pending: number }>(`/api/admin/${service}/approvals/Count`); }
  approve(service: ApprovalService, publicId: string, note: string | null) { return this.post<ChangeRequest>(`/api/admin/${service}/approvals/${publicId}/Approve`, { note }); }
  rejectApproval(service: ApprovalService, publicId: string, note: string) { return this.post<ChangeRequest>(`/api/admin/${service}/approvals/${publicId}/Reject`, { note }); }
  cancelApproval(service: ApprovalService, publicId: string) { return this.post<ChangeRequest>(`/api/admin/${service}/approvals/${publicId}/Cancel`, {}); }
  approvalRules(service: ApprovalService) { return this.get<ApprovalRule[]>(`/api/admin/${service}/approval-rules`); }
  saveApprovalRule(service: ApprovalService, kind: string, body: { enabled: boolean; amount: number | null; percent: number | null; count: number | null }, reason?: string | null) {
    return firstValueFrom(this.http.put<ApprovalRule>(`/api/admin/${service}/approval-rules/${kind}`, body, { headers: reasonHeader(reason) }));
  }
  addDefaultFineReasons() { return this.post<{ added: number }>('/api/admin/circulation/fine-reasons/AddDefaults'); }
  restoreCatalogDefaults() { return this.post<{ added: number; worksheets: number }>('/api/admin/catalog/bib-types/RestoreDefaults'); }
  faceStatus() { return this.get<{ enabled: boolean }>('/api/admin/patron/readers/FaceStatus'); }
  /** Nhận diện bạn đọc từ ảnh camera (base64 JPEG, có hoặc không tiền tố data URL); không ai khớp → null. */
  faceIdentify(imageBase64: string) { return this.post<FaceMatch | null>('/api/admin/patron/readers/FaceIdentify', { imageBase64 }); }
  readerExtraPhotos(publicId: string) { return this.get<ExtraPhoto[]>(`/api/admin/patron/readers/ExtraPhotos/${publicId}`); }
  addReaderExtraPhoto(publicId: string, fileId: string) { return this.post<ExtraPhoto>(`/api/admin/patron/readers/ExtraPhotos/${publicId}`, { fileId }); }
  removeReaderExtraPhoto(publicId: string, fileId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/patron/readers/ExtraPhotos/${publicId}/${fileId}`)); }
  setReaderPhoto(publicId: string, fileId: string | null) { return this.put<Reader>(`/api/admin/patron/readers/Photo/${publicId}`, { fileId }); }
  /** Gán ảnh theo số thẻ (sau khi đã upload từng ảnh lên media). */
  assignReaderPhotos(items: { cardNo: string; fileId: string }[]) { return this.post<ReaderPhotosResult>('/api/admin/patron/readers/Photos', { items }); }
  readerExportFields() { return this.get<ExportField[]>('/api/admin/patron/readers/GetExportFields'); }
  exportReaders(search: ReaderSearch, fields: string[]) {
    return blobRequest(firstValueFrom(this.http.post('/api/admin/patron/readers/Export', { search, fields }, { responseType: 'blob' })));
  }
  cardNoExists(cardNo: string, excludePublicId?: string) {
    return this.get<{ exists: boolean }>('/api/admin/patron/readers/CheckExist', { cardNo, excludePublicId });
  }
  orgTree(keyword?: string) { return this.post<OrgNode[]>('/api/admin/tenant/orgs/GetTree', { keyword }); }
  moveOrg(publicId: string, newParentId: number | null, newOrder: number) {
    return this.put<Org>(`/api/admin/tenant/orgs/Move/${publicId}`, { newParentId, newOrder });
  }
  // Gửi tin của đơn vị (service notification)
  emailSettings() { return this.get<EmailSettings>('/api/admin/notification/email-settings'); }
  saveEmailSettings(body: EmailSettingsInput) { return this.put<EmailSettings>('/api/admin/notification/email-settings', body); }
  deleteEmailSettings() { return firstValueFrom(this.http.delete<void>('/api/admin/notification/email-settings')); }
  sendTestEmail(to: string) { return this.post<NotificationLog>('/api/admin/notification/email-settings/test', { to }); }
  restoreEmailTemplates() { return this.post<{ added: number }>('/api/admin/notification/email-templates/RestoreDefaults'); }
  /** Kiểm tra xác thực bạn đọc qua LDAP/API (READER_AUTH_CONFIG) với một tài khoản thử. */
  testReaderAuth(login: string, password: string) {
    return this.post<{ provider: string; outcome: string; cardNo: string | null; email: string | null; profileFound: boolean; message: string | null }>(
      '/api/admin/identity/reader-auth/Test', { login, password });
  }
  sip2Logs(body: CrudSearch & { command?: string | null; failed?: boolean | null; from?: string | null; to?: string | null }) {
    return this.post<CrudPage<Sip2Log>>('/api/admin/circulation/sip2-stations/Logs', body);
  }
  notificationLogs(body: CrudSearch & { templateCode?: string | null }) {
    return this.post<CrudPage<NotificationLog>>('/api/admin/notification/notification-logs/Search', body);
  }

  // Nhật ký: đơn vị (/api/admin/audit, quyền SYSTEM_LOG) hoặc nền tảng (/api/system/audit, host hệ thống)
  auditLogs(platform: boolean, body: CrudSearch & { action?: string | null; from?: string | null; to?: string | null }) {
    return this.post<CrudPage<AuditLog>>(platform ? '/api/system/audit/audit-logs/Search' : '/api/admin/audit/audit-logs/Search', body);
  }
  /** Nhật ký biên mục: thao tác trên biểu ghi (quyền CATALOG_BIBS:view hoặc SYSTEM_LOG:view). */
  catalogLogs(body: CrudSearch & { action?: string | null; from?: string | null; to?: string | null }) {
    return this.post<CrudPage<AuditLog>>('/api/admin/audit/audit-logs/catalog/Search', body);
  }
  /** Gợi ý từ điển biên mục khi gõ (kind: author | publisher | keyword | class). */
  dictionarySuggest(kind: string, q: string) {
    return this.get<{ value: string; description: string | null }[]>(`/api/admin/catalog/dictionary/Suggest?kind=${kind}&q=${encodeURIComponent(q)}&take=10`);
  }
  bibIsbd(publicId: string) { return this.get<string[]>(`/api/admin/catalog/bibs/Isbd/${publicId}`); }
  isbdTemplate() { return this.get<Record<string, unknown>[]>('/api/admin/catalog/isbd-configs/Template'); }
  auditActions(platform: boolean) {
    return this.get<string[]>(platform ? '/api/system/audit/audit-logs/actions' : '/api/admin/audit/audit-logs/actions');
  }

  deleteOrgWithChildren(publicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/tenant/orgs/DeleteWithChildren/${publicId}`)); }

  // Tài liệu số (digital): danh mục cây/phẳng + tài liệu/file qua crud('collections'|'subjects'|'topics'|'dig-types'|'policies'|
  // 'policies-by-collection'|'items'|'files', 'digital'); lượt mượn/đặt trước (quyền DIGITAL_LOANS) tra cứu qua crud('loans'|'reservations',
  // 'digital').search() — chỉ thu hồi là thao tác nghiệp vụ riêng, không có Add/Update/Delete.
  ebookBibOptions(q: string) { return this.get<BibLinkOption[]>('/api/admin/digital/items/bib-options', { q }); }
  recallEbookLoan(loanPublicId: string, reason: string) { return this.post<EbookLoan>('/api/admin/digital/loans/Recall', { loanPublicId, reason }); }
  recallAllEbookLoans(ebookItemPublicId: string, reason: string) { return this.post<number>('/api/admin/digital/loans/RecallAll', { ebookItemPublicId, reason }); }
  approveDocumentSubmission(publicId: string) { return this.post<DocumentSubmission>(`/api/admin/digital/document-submissions/${publicId}/Approve`); }
  rejectDocumentSubmission(submissionPublicId: string, reason: string) {
    return this.post<DocumentSubmission>('/api/admin/digital/document-submissions/Reject', { submissionPublicId, reason });
  }

  ebookMetadata(itemPublicId: string) { return this.get<EbookMetadataRow[]>(`/api/admin/digital/items/${itemPublicId}/metadata`); }
  saveEbookMetadata(itemPublicId: string, values: { fieldPublicId: string; value: string; language: string | null }[]) {
    return this.put<EbookMetadataRow[]>(`/api/admin/digital/items/${itemPublicId}/metadata`, { values });
  }
  portalCounterDays(days: number) { return this.get<PortalCounterDay[]>('/api/admin/portal/counter/days', { days }); }
  readingStatistics(range: { from: string | null; to: string | null }) { return this.post<ReadingStats>('/api/admin/digital/reading-tracking/Statistics', range); }
  readingSessions(search: { ebookPublicId: string | null; keyword: string | null; from: string | null; to: string | null; pageIndex: number; pageSize: number }) {
    return this.post<ReadingSessionPage>('/api/admin/digital/reading-tracking/Search', search);
  }
  mediaLibrary(purpose: string, page: number, pageSize: number) { return this.get<MediaFilePage>('/api/admin/media/files', { purpose, page, pageSize }); }
  handlePortalContact(publicId: string, handleNote: string) { return this.post<unknown>(`/api/admin/portal/contacts/${publicId}/Handle`, { handleNote }); }
  portalCounter() { return this.get<PortalCounterSummary>('/api/admin/portal/counter'); }
  approveEbookReview(publicId: string) { return this.post<unknown>(`/api/admin/digital/reviews/${publicId}/Approve`); }
  rejectEbookReview(publicId: string, reason: string) { return this.post<unknown>(`/api/admin/digital/reviews/${publicId}/Reject`, { reason }); }

  // Không gian & đặt phòng (space): danh mục qua crud('buildings'|'floors'|'spaces'|'special-days', 'space'); phần còn lại
  // (vị trí/bảo trì trên sơ đồ, đặt/duyệt/check-in, cấm, giờ mở cửa, tham số, báo cáo, thiết bị RFID) là thao tác nghiệp vụ riêng.
  placeSpaceRoom(publicId: string, body: { positionX: number | null; positionY: number | null; width: number | null; height: number | null }) {
    return this.post<SpaceRoom>(`/api/admin/space/spaces/${publicId}/Place`, body);
  }
  setSpaceRoomMaintenance(publicId: string, on: boolean, note: string | null) {
    return this.post<SpaceRoom>(`/api/admin/space/spaces/${publicId}/SetMaintenance`, { on, note });
  }
  setSpaceFloorLayout(publicId: string, layoutImageUrl: string | null, canvasWidth: number, canvasHeight: number) {
    return this.post<SpaceFloor>(`/api/admin/space/floors/${publicId}/SetLayout`, { layoutImageUrl, canvasWidth, canvasHeight });
  }
  placeSpaceFixture(publicId: string, body: { positionX: number | null; positionY: number | null; width: number | null; height: number | null }) {
    return this.post<SpaceFixture>(`/api/admin/space/fixtures/${publicId}/Place`, body);
  }
  spaceStores() { return this.get<SpaceStoreOption[]>('/api/admin/space/stores'); }
  placeLockerCabinet(publicId: string, body: { positionX: number | null; positionY: number | null; width: number | null; height: number | null }) {
    return this.post<SpaceLockerCabinet>(`/api/admin/space/locker-cabinets/${publicId}/Place`, body);
  }
  lockerLoanSnapshot(cardNo: string) { return this.post<SpaceLockerLoan[]>('/api/admin/space/locker-loans/Snapshot', { cardNo }); }
  borrowLocker(compartmentCode: string, cardNo: string, note: string | null = null) {
    return this.post<SpaceLockerLoan>('/api/admin/space/locker-loans/Borrow', { compartmentCode, cardNo, note });
  }
  returnLocker(compartmentCode: string | null, loanPublicId: string | null = null) {
    return this.post<SpaceLockerLoan>('/api/admin/space/locker-loans/Return', { compartmentCode, loanPublicId });
  }
  searchLockerLoans(body: SpaceLockerLoanSearch) { return this.post<CrudPage<SpaceLockerLoan>>('/api/admin/space/locker-loans/Search', body); }

  searchSpaceBookings(body: SpaceBookingSearch) { return this.post<CrudPage<SpaceBooking>>('/api/admin/space/bookings/Search', body); }
  spaceBooking(publicId: string) { return this.get<SpaceBooking>(`/api/admin/space/bookings/GetById/${publicId}`); }
  approveSpaceBooking(publicId: string) { return this.post<SpaceBooking>(`/api/admin/space/bookings/${publicId}/Approve`); }
  rejectSpaceBooking(publicId: string, reason: string) { return this.post<SpaceBooking>('/api/admin/space/bookings/Reject', { publicId, reason }); }
  checkInSpaceBooking(publicId: string, kioskSpaceId?: number | null) {
    return this.post<SpaceBooking>('/api/admin/space/bookings/CheckIn', { publicId, kioskSpaceId: kioskSpaceId ?? null });
  }
  checkInSpaceBookingByCard(cardNo: string, kioskSpaceId?: number | null) {
    return this.post<SpaceBooking>('/api/admin/space/bookings/CheckInByCard', { cardNo, kioskSpaceId: kioskSpaceId ?? null });
  }
  checkOutSpaceBooking(publicId: string) { return this.post<SpaceBooking>(`/api/admin/space/bookings/${publicId}/CheckOut`); }

  spaceWeeklyHours(category?: number | null) { return this.get<SpaceWeeklyHourRow[]>('/api/admin/space/hours/weekly', { category: category ?? undefined }); }
  saveSpaceWeeklyHours(category: number | null, rows: SpaceWeeklyHourRow[]) {
    return this.put<SpaceWeeklyHourRow[]>('/api/admin/space/hours/weekly', { category, rows });
  }
  spaceSettings() { return this.get<SpaceSettings>('/api/admin/space/settings'); }
  updateSpaceSettings(body: SpaceSettings) { return this.put<SpaceSettings>('/api/admin/space/settings', body); }

  searchSpaceBans(body: SpaceBanSearch) { return this.post<CrudPage<SpaceBan>>('/api/admin/space/bans/Search', body); }
  addSpaceBan(cardNo: string, days: number, reason: string) { return this.post<SpaceBan>('/api/admin/space/bans/Add', { cardNo, days, reason }); }
  liftSpaceBan(publicId: string) { return this.post<SpaceBan>(`/api/admin/space/bans/${publicId}/Lift`); }

  spaceReport(body: { from: string; to: string; spaceId?: number | null; category?: number | null }) {
    return this.post<SpaceReport>('/api/admin/space/reports/Search', body);
  }
  exportSpaceReport(body: { from: string; to: string; spaceId?: number | null; category?: number | null }) {
    return blobRequest(firstValueFrom(this.http.post('/api/admin/space/reports/Export', body, { responseType: 'blob' })));
  }

  searchSpaceAccessDevices(body: CrudSearch) { return this.post<CrudPage<SpaceAccessDevice>>('/api/admin/space/access/devices/Search', body); }
  addSpaceAccessDevice(code: string, name: string, spaceId: number | null) {
    return this.post<{ device: SpaceAccessDevice; key: { rawKey: string } }>('/api/admin/space/access/devices/Add', { code, name, spaceId });
  }
  updateSpaceAccessDevice(publicId: string, code: string, name: string, spaceId: number | null) {
    return this.put<SpaceAccessDevice>(`/api/admin/space/access/devices/Update/${publicId}`, { code, name, spaceId });
  }
  regenerateSpaceAccessDeviceKey(publicId: string) { return this.post<{ rawKey: string }>(`/api/admin/space/access/devices/${publicId}/RegenerateKey`); }
  changeSpaceAccessDeviceStatus(publicId: string, status: number) {
    return this.put<void>('/api/admin/space/access/devices/ChangeStatus', { publicId, status });
  }
  deleteSpaceAccessDevice(publicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/space/access/devices/Delete/${publicId}`)); }
  unlockSpaceAccessDevice(body: { deviceId?: number | null; bookingPublicId?: string | null; note?: string | null }) {
    return this.post<void>('/api/admin/space/access/unlock', body);
  }
  searchSpaceAccessScanLogs(body: SpaceAccessScanLogSearch) { return this.post<CrudPage<SpaceAccessScanLog>>('/api/admin/space/access/scan-logs/Search', body); }
}
