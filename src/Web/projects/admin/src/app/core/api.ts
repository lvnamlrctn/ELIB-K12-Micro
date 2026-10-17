import { translateError } from './i18n';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
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
}
export interface ItemSearch extends CrudSearch {
  mfn?: number | null; storeId?: number | null; itemStatus?: string | null; barcodeFrom?: string | null; barcodeTo?: string | null;
}
export interface RegisterItems { mfn: number; quantity: number; prefix: string; digits: number; startNumber?: number | null; storeId?: number | null; }
export interface NextBarcode { prefix: string; number: number; barcode: string; }
export interface ShelveResult { shelved: number; notFound: string[]; skipped: string[]; }
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
}
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
  createdFrom: string | null; createdTo: string | null;
  mfnFrom: number | null; mfnTo: number | null; title: string | null; author: string | null; publisher: string | null; publishYear: string | null;
  pageIndex: number; pageSize: number;
}
export type AcqReportName = 'AcquisitionList' | 'LookupLines' | 'StoreAllocation' | 'AccessionRegister' | 'NewBooks';
export interface AcqListRow {
  receiptId: number; receiptName: string | null; receivedOn: string; mfn: number; title: string; author: string | null; quantity: number;
  price: number | null; currency: string | null; rate: number; total: number;
}
/** quantity / amount tính trên toàn bộ kết quả lọc. */
export interface AcqListPage { items: AcqListRow[]; total: number; pageIndex: number; pageSize: number; quantity: number; amount: number; }
export interface AcqLookupRow {
  mfn: number; receiptId: number; isbd: string; createdAt: string; receivedOn: string; sourceName: string | null; storeId: number | null;
  storeName: string | null; bibPublicId: string;
}
export interface AcqStoreCount { storeId: number | null; storeName: string | null; copies: number; }
export interface AcqAllocation { items: AcqStoreCount[]; total: number; }
export interface AcqAccessionRow {
  barcode: string; mfn: number; title: string | null; author: string | null; publisher: string | null; publishYear: string | null; ddc: string | null;
  storeName: string | null; date: string; receiptId: number | null; price: number | null; currency: string | null; rate: number | null;
}
/** catalogedOn null = biểu ghi không rõ ngày biên mục (xếp cuối). */
export interface AcqNewBookRow { mfn: number; title: string; author: string | null; publisher: string | null; publishYear: string | null; catalogedOn: string | null; }

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
}
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
export interface FineTicketDetail { ticket: FineTicket; lines: FineLine[]; finePerDay: number; }
export interface FineTicketTotals { receivable: number; received: number; remaining: number; }
export interface FineTicketSearch extends CrudSearch {
  cardNo?: string | null; ticketStatus?: number | null; unpaid?: boolean | null; from?: string | null; to?: string | null;
}
export interface FineLineChange { id: number | null; reasonCode: string; amount: number; barcode?: string | null; }
export interface SaveFineTicket {
  status: number; discount: number; paid: number; note: string | null; manualAmount?: number | null; fineDate?: string | null;
  lines: FineLineChange[]; deletedLineIds: number[];
}

// ── File (service media): upload 2 bước qua URL ký sẵn ──
export interface UploadTicket { fileId: string; uploadUrl: string; expiresAt: string; maxBytes: number; }
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
async function blobRequest(request: Promise<Blob>): Promise<Blob> {
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
  // Sách mất (LOST_BOOKS) / thanh lý (LIQUIDATES)
  searchLost(search: StoreRecordSearch) { return this.post<CrudPage<LostRow>>('/api/admin/holdings/lost-books/Search', search); }
  exportLost(search: StoreRecordSearch) { return this.holdingsFile('lost-books/Export', search); }
  markLost(body: { barcodes: string[]; lostOn: string | null; reason: string | null }) { return this.post<ItemBatchResult>('/api/admin/holdings/lost-books/MarkLost', body); }
  restoreLost(barcode: string) { return this.post<void>('/api/admin/holdings/lost-books/Restore', { barcode }); }
  searchLiquidations(search: StoreRecordSearch) { return this.post<CrudPage<LiquidationRow>>('/api/admin/holdings/liquidations/Search', search); }
  exportLiquidations(search: StoreRecordSearch) { return this.holdingsFile('liquidations/Export', search); }
  liquidate(body: { barcodes: string[]; liquidatedOn: string | null; reason: string | null }) {
    return this.post<ItemBatchResult>('/api/admin/holdings/liquidations/Liquidate', body);
  }
  cancelLiquidation(barcode: string) { return this.post<void>('/api/admin/holdings/liquidations/Cancel', { barcode }); }
  // Sách ra vào kho (BOOK_OUT_STORE)
  storeOutOptions() { return this.get<StoreOutOptions>('/api/admin/holdings/store-outs/Options'); }
  searchStoreOuts(search: StoreOutSearch) { return this.post<CrudPage<StoreOutRow>>('/api/admin/holdings/store-outs/Search', search); }
  exportStoreOuts(search: StoreOutSearch) { return this.holdingsFile('store-outs/Export', search); }
  scanOut(body: ScanOut) { return this.post<StoreOutRow>('/api/admin/holdings/store-outs/ScanOut', body); }
  scanIn(barcode: string) { return this.post<StoreOutRow>('/api/admin/holdings/store-outs/ScanIn', { barcode }); }
  deleteStoreOut(publicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/holdings/store-outs/Delete/${publicId}`)); }
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
  saveFineTicket(publicId: string, body: SaveFineTicket) { return this.put<FineTicketDetail>(`/api/admin/circulation/fine-tickets/Save/${publicId}`, body); }
  deleteFineTicket(publicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/circulation/fine-tickets/Delete/${publicId}`)); }
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
}
