import { Injectable, inject, signal } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map, tap, shareReplay, catchError } from 'rxjs/operators';
import { UserService, PermRow } from './user.service';
import { Auth } from '../auth';
import { ADMIN_ROUTE_PERM } from './admin-perm-codes';

export interface Perm {
  view: boolean;
  add: boolean;
  edit: boolean;
  delete: boolean;
}

// Các route luôn hiển thị dù không có bản ghi module (tránh khóa trắng trang chủ)
const ALWAYS_VISIBLE = ['admin/dashboard', 'admin/reading-tracking', 'admin/ebook-review'];

/**
 * Quyền của user đang đăng nhập (port ELIB-LRC 09-26, giai đoạn 1). Khoá tra là MÃ QUYỀN (ModuleCode, đúng mã backend
 * kiểm tra qua [Permission]) theo bảng route -> mã ở admin-perm-codes.ts; route chưa khai trong bảng thì tra theo
 * cms.Module.Link như trước. Quyền lấy từ Users/MyPermission (chỉ cần đăng nhập) — trước đây gọi GetPermission/{id}
 * vốn đòi quyền module USERS, nên cán bộ không có quyền đó không tải được quyền của chính mình.
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  private userService = inject(UserService);
  private auth = inject(Auth);

  /** Map mã quyền (ModuleCode) -> quyền. Signal để menu/directive tự cập nhật khi tải xong. */
  readonly perms = signal<Map<string, Perm>>(new Map());
  /** Map link module (đã chuẩn hoá) -> quyền — dự phòng cho route chưa khai trong ADMIN_ROUTE_PERM. */
  private readonly byLink = signal<Map<string, Perm>>(new Map());
  readonly ready = signal(false);

  private load$?: Observable<Map<string, Perm>>;
  private loadedUserId: string | number | null = null;

  /** Tải quyền của user đang đăng nhập. Cache theo userId. */
  load(): Observable<Map<string, Perm>> {
    const uid = this.auth.getUserId();
    if (this.load$ && this.loadedUserId === uid) return this.load$;
    this.loadedUserId = uid;
    this.ready.set(false);

    const perms$ = uid != null ? this.userService.getMyPermissions() : of([] as PermRow[]);

    this.load$ = perms$.pipe(
      map(rows => this.build(rows)),
      tap(m => { this.perms.set(m); this.ready.set(true); }),
      catchError(() => { this.perms.set(new Map()); this.byLink.set(new Map()); this.ready.set(true); return of(new Map<string, Perm>()); }),
      shareReplay(1)
    );
    return this.load$;
  }

  clear(): void {
    this.perms.set(new Map());
    this.byLink.set(new Map());
    this.ready.set(false);
    this.load$ = undefined;
    this.loadedUserId = null;
  }

  /** Tra thẳng theo mã quyền (vd 'READERS'). */
  can(code: string, action: keyof Perm): boolean {
    if (!this.hasData) return true;                       // fail-open
    return this.perms().get(code)?.[action] ?? false;
  }

  // ---- Menu: khớp theo link chính xác ----
  canViewLink(link: string): boolean {
    if (!this.hasData) return true;                       // fail-open khi chưa có dữ liệu
    const key = this.norm(link);
    if (ALWAYS_VISIBLE.includes(key)) return true;
    const code = ADMIN_ROUTE_PERM[key];
    return code ? this.perms().get(code)?.view ?? false : this.byLink().get(key)?.view ?? false;
  }

  // ---- Nút trên trang: khớp theo url hiện tại (tiền tố dài nhất) ----
  canView(url: string): boolean { return this.canUrl(url, 'view'); }
  canAdd(url: string): boolean { return this.canUrl(url, 'add'); }
  canEdit(url: string): boolean { return this.canUrl(url, 'edit'); }
  canDelete(url: string): boolean { return this.canUrl(url, 'delete'); }

  /** Mã quyền của route là tiền tố dài nhất của url (cho route con, vd admin/ab-orders/123). */
  codeForUrl(url: string): string | null {
    const u = this.norm(url);
    let best: string | null = null;
    let bestLen = -1;
    for (const link of Object.keys(ADMIN_ROUTE_PERM)) {
      if ((u === link || u.startsWith(link + '/')) && link.length > bestLen) {
        best = ADMIN_ROUTE_PERM[link];
        bestLen = link.length;
      }
    }
    return best;
  }

  /** Quyền của route là tiền tố dài nhất của url: theo bảng mã quyền, không có thì theo cms.Module.Link. */
  permForUrl(url: string): Perm | null {
    const u = this.norm(url);
    let best: Perm | null = null;
    let bestLen = -1;
    for (const link of Object.keys(ADMIN_ROUTE_PERM)) {
      if ((u === link || u.startsWith(link + '/')) && link.length > bestLen) {
        best = this.perms().get(ADMIN_ROUTE_PERM[link]) ?? { view: false, add: false, edit: false, delete: false };
        bestLen = link.length;
      }
    }
    for (const [link, p] of this.byLink()) {
      if (link in ADMIN_ROUTE_PERM) continue;
      if ((u === link || u.startsWith(link + '/')) && link.length > bestLen) {
        best = p;
        bestLen = link.length;
      }
    }
    return best;
  }

  private canUrl(url: string, action: keyof Perm): boolean {
    if (!this.hasData) return true;                       // fail-open
    const p = this.permForUrl(url);
    return p ? p[action] : false;
  }

  private get hasData(): boolean {
    return this.perms().size > 0 || this.byLink().size > 0;
  }

  private build(rows: PermRow[]): Map<string, Perm> {
    const byCode = new Map<string, Perm>();
    const byLink = new Map<string, Perm>();
    for (const r of rows) {
      const p: Perm = { view: r.canView, add: r.canAdd, edit: r.canEdit, delete: r.canDelete };
      if (r.moduleCode) byCode.set(r.moduleCode, p);
      if (r.link) byLink.set(this.norm(r.link), p);
    }
    this.byLink.set(byLink);
    return byCode;
  }

  private norm(link: string): string {
    return (link || '').trim().toLowerCase().replace(/[?#].*$/, '').replace(/^\/+|\/+$/g, '');
  }
}
