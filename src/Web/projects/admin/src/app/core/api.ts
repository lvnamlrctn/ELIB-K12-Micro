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
export interface ReaderSearch extends CrudSearch {
  readerTypeId?: number | null; classId?: number | null; courseId?: number | null; expired?: boolean | null;
}
export interface ReaderBulkUpdate {
  publicIds: string[]; readerTypeId?: number | null; classId?: number | null; courseId?: number | null;
  issueDate?: string | null; expireDate?: string | null; status?: number | null;
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
  { code: 'I', name: 'Chưa xếp giá', badge: 'badge-off' },
  { code: 'R', name: 'Sẵn sàng', badge: 'badge-on' },
  { code: 'L', name: 'Mất', badge: 'badge-off' },
  { code: 'S', name: 'Đã thanh lý', badge: 'badge-off' },
  { code: 'X', name: 'Xuất kho', badge: 'badge-off' },
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
export interface CircPlace { id: number; publicId: string; code: string; name: string; storeIds: number[]; }
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
  { code: 1, name: 'Chờ sách', badge: 'badge-warn' },
  { code: 2, name: 'Đang giữ sách', badge: 'badge-info' },
  { code: 3, name: 'Đã mượn', badge: 'badge-on' },
  { code: 4, name: 'Đã huỷ', badge: 'badge-off' },
  { code: 5, name: 'Hết hạn giữ', badge: 'badge-off' },
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
  update(publicId: string, body: unknown) { return firstValueFrom(this.http.put<T>(`${this.base}/Update/${publicId}`, body)); }
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

/** PUT nội dung file vào URL ký sẵn của MinIO — dùng fetch (không qua HttpClient) để không gắn Authorization: chữ ký nằm trong URL. */
async function putToStorage(uploadUrl: string, file: File): Promise<void> {
  const response = await fetch(uploadUrl, { method: 'PUT', body: file, credentials: 'omit' });
  if (!response.ok) {
    throw new HttpErrorResponse({ status: response.status, error: { detail: `Không tải được file lên kho (lỗi ${response.status}).` } });
  }
}

function fileForm(file: File): FormData {
  const form = new FormData();
  form.append('file', file, file.name);
  return form;
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

/** Thông báo lỗi tiếng Việt từ problem+json của service ({ code, detail, title }). */
export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { detail?: string; title?: string; code?: string } | null;
    if (body?.detail) return body.detail;
    if (error.status === 0) return 'Không kết nối được máy chủ.';
    if (error.status === 403) return 'Bạn không có quyền thực hiện chức năng này.';
    return body?.title ?? `Lỗi ${error.status}`;
  }
  return 'Đã xảy ra lỗi không xác định.';
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
  bulkUpdateReaders(body: ReaderBulkUpdate) { return this.put<{ updatedCount: number }>('/api/admin/patron/readers/BulkUpdate', body); }
  // Biên mục (catalog): 8 endpoint chuẩn qua crud('bibs' | 'bib-types' | 'worksheets', 'catalog')
  marc21Fields() { return this.get<MarcFieldDef[]>('/api/admin/catalog/marc21/fields'); }
  worksheetsByBibType(bibTypeId: number) { return this.get<Worksheet[]>(`/api/admin/catalog/worksheets/GetByBibType/${bibTypeId}`); }
  bibByMfn(mfn: number) { return this.get<Bib>(`/api/admin/catalog/bibs/GetByMfn/${mfn}`); }
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
  notificationLogs(body: CrudSearch & { templateCode?: string | null }) {
    return this.post<CrudPage<NotificationLog>>('/api/admin/notification/notification-logs/Search', body);
  }

  // Nhật ký: đơn vị (/api/admin/audit, quyền SYSTEM_LOG) hoặc nền tảng (/api/system/audit, host hệ thống)
  auditLogs(platform: boolean, body: CrudSearch & { action?: string | null; from?: string | null; to?: string | null }) {
    return this.post<CrudPage<AuditLog>>(platform ? '/api/system/audit/audit-logs/Search' : '/api/admin/audit/audit-logs/Search', body);
  }
  auditActions(platform: boolean) {
    return this.get<string[]>(platform ? '/api/system/audit/audit-logs/actions' : '/api/admin/audit/audit-logs/actions');
  }

  deleteOrgWithChildren(publicId: string) { return firstValueFrom(this.http.delete<void>(`/api/admin/tenant/orgs/DeleteWithChildren/${publicId}`)); }
}
