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
/** state: open | overdue | returned; from/to: yyyy-MM-dd (ngày mượn). */
export interface LoanSearch extends CrudSearch {
  cardNo?: string | null; barcode?: string | null; state?: string | null; circPlaceId?: number | null; from?: string | null; to?: string | null;
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
  registerItems(body: RegisterItems) { return this.post<Item[]>('/api/admin/holdings/items/Register', body); }
  nextBarcode(prefix: string, digits: number) { return this.get<NextBarcode>('/api/admin/holdings/items/NextBarcode', { prefix, digits }); }
  shelveItems(body: { ids?: string[]; barcodes?: string[]; storeId?: number | null }) { return this.post<ShelveResult>('/api/admin/holdings/items/Shelve', body); }
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
  buildFineTicket(cardNo: string, loanIds: string[] = []) { return this.post<FineTicketDetail>('/api/admin/circulation/fine-tickets/BuildForReader', { cardNo, loanIds }); }
  saveFineTicket(publicId: string, body: SaveFineTicket) { return this.put<FineTicketDetail>(`/api/admin/circulation/fine-tickets/Save/${publicId}`, body); }
  deleteFineTicket(publicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/circulation/fine-tickets/Delete/${publicId}`)); }
  addDefaultFineReasons() { return this.post<{ added: number }>('/api/admin/circulation/fine-reasons/AddDefaults'); }
  restoreCatalogDefaults() { return this.post<{ added: number; worksheets: number }>('/api/admin/catalog/bib-types/RestoreDefaults'); }
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
