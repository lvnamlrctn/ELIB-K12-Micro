import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { User } from '../../models/system/user';

export interface PermRow {
  moduleId:  number;
  /** Mã quyền (cms.Module.ModuleCode) — khoá tra quyền phía frontend (admin-perm-codes.ts). */
  moduleCode?: string;
  /** cms.Module.Link — dự phòng cho route chưa khai mã quyền. */
  link?:     string | null;
  name:      string;
  depth:     number;
  parentId:  number | null;
  canView:   boolean;
  canAdd:    boolean;
  canEdit:   boolean;
  canDelete: boolean;
}

export interface UserSearchParams {
  keyword?:  string;
  roleId?:   number | null;
  tenantId?: string | null;
  pageIndex?: number;
  pageSize?:  number;
}

export interface UserSearchResult {
  data:         User[];
  recordsTotal: number;
}

@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Dbo/Users`;
  }

  search(params: UserSearchParams): Observable<UserSearchResult> {
    const payload = {
      keyword:   params.keyword   || '',
      roleId:    params.roleId    ?? null,
      tenantId:  params.tenantId  ?? null,
      pageIndex: params.pageIndex ?? 1,
      pageSize:  params.pageSize  ?? 10,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: User[] = [];
        let recordsTotal = 0;
        if (Array.isArray(res)) {
          data = res; recordsTotal = res.length;
        } else if (res?.data?.items && Array.isArray(res.data.items)) {
          data = res.data.items;
          recordsTotal = res.data.totalCount ?? res.data.recordsTotal ?? data.length;
        } else if (res?.data && Array.isArray(res.data)) {
          data = res.data;
          recordsTotal = res.totalCount ?? res.recordsTotal ?? data.length;
        }
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  searchAll(keyword = '', roleId: number | null = null): Observable<User[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, { keyword, roleId }).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (Array.isArray(res?.data)) return res.data;
        return [];
      }),
      catchError(() => of([]))
    );
  }

  getById(id: string | number): Observable<User> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(
      map(res => res.data || res),
      catchError(() => of({} as User))
    );
  }

  uploadPhoto(file: File): Observable<string> {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<any>(`${this.baseUrl}/UploadPhoto`, fd).pipe(
      map(res => res?.data?.path ?? res?.path ?? ''),
      catchError(() => of(''))
    );
  }

  create(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, payload).pipe(map(res => res.data || res));
  }

  update(id: string | number, payload: any): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, payload).pipe(map(res => res.data || res));
  }

  delete(id: string | number): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data || res));
  }

  changeStatus(publicId: string | number, status: number): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/ChangeStatus`, { publicId, status }).pipe(map(res => res.data || res));
  }

  resetPassword(id: string | number, password: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/ResetPassword/${id}`, { password }).pipe(map(res => res.data || res));
  }

  // Đổi mật khẩu của user đang đăng nhập (token gắn tự động qua AuthInterceptor).
  // Trả về nguyên response để component đọc success/message từ backend.
  changePassword(oldPassword: string, newPassword: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/ChangePassword`, { oldPassword, newPassword });
  }

  exportExcel(keyword = '', roleId: number | null = null): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/ExportExcel`, { keyword, roleId }, { responseType: 'blob' });
  }

  importData(file: File): Observable<any> {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<any>(`${this.baseUrl}/Import`, fd).pipe(map(res => res.data || res));
  }

  checkLoginNameExists(loginName: string, excludePublicId?: string | number | null): Observable<boolean> {
    let url = `${this.baseUrl}/CheckExist?loginName=${encodeURIComponent(loginName)}`;
    if (excludePublicId != null) url += `&excludePublicId=${excludePublicId}`;
    return this.http.get<any>(url).pipe(
      map(res => res?.data === true),
      catchError(() => of(false))
    );
  }

  getUserPermissions(userId: string | number): Observable<PermRow[]> {
    return this.http.get<any>(`${this.baseUrl}/GetPermission/${userId}`).pipe(
      map(res => this.mapPermissionResponse(res)),
      catchError(() => of([]))
    );
  }

  // Quyền của chính user đang đăng nhập (theo token) — dựng menu/nút lúc tải app. Chỉ cần đã đăng nhập, khác
  // GetPermission/{id} (đòi quyền module USERS, dành cho admin xem/sửa quyền người khác).
  getMyPermissions(): Observable<PermRow[]> {
    return this.http.get<any>(`${this.baseUrl}/MyPermission`).pipe(
      map(res => this.mapPermissionResponse(res)),
      catchError(() => of([]))
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private mapPermissionResponse(res: any): PermRow[] {
        const data = res?.data ?? res;
        if (!Array.isArray(data)) return [];

        const flatten = (nodes: any[], depth: number): PermRow[] => {
          const result: PermRow[] = [];
          for (const d of nodes) {
            result.push({
              moduleId:  d.moduleId ?? 0,
              moduleCode: d.moduleCode ?? '',
              link:      d.link ?? null,
              name:      d.moduleName ?? d.name ?? '',
              depth,
              parentId:  d.parentId || null,
              canView:   d.can_View   === 2,
              canAdd:    d.can_Add    === 2,
              canEdit:   d.can_Edit   === 2,
              canDelete: d.can_Delete === 2,
            });
            if (Array.isArray(d.children) && d.children.length > 0) {
              result.push(...flatten(d.children, depth + 1));
            }
          }
          return result;
        };
        return flatten(data, 0);
  }

  saveUserPermissions(userId: string | number, rows: PermRow[]): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/SavePermission`, {
      UserId: userId,
      Permissions: rows.map(r => ({
        ModuleId:  r.moduleId,
        CanView:   r.canView,
        CanAdd:    r.canAdd,
        CanEdit:   r.canEdit,
        CanDelete: r.canDelete,
      }))
    }).pipe(map(res => res?.data ?? res), catchError(e => { throw e; }));
  }
}
