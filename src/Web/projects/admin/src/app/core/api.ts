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

/** PUT nội dung file vào URL ký sẵn của MinIO — dùng fetch (không qua HttpClient) để không gắn Authorization: chữ ký nằm trong URL. */
async function putToStorage(uploadUrl: string, file: File): Promise<void> {
  const response = await fetch(uploadUrl, { method: 'PUT', body: file, credentials: 'omit' });
  if (!response.ok) {
    throw new HttpErrorResponse({ status: response.status, error: { detail: `Không tải được file lên kho (lỗi ${response.status}).` } });
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
