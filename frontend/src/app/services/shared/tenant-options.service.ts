import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { map } from 'rxjs/operators';
import { DepartmentService } from '../system/department.service';

export interface TenantOption { id: string; name: string; }

/** Đợt 24 — danh sách đơn vị dùng cho ô lọc "Đơn vị" ở các trang admin (chỉ tài khoản đặc quyền mới
 * gọi tới). Trước đây mỗi trang tự gọi DepartmentService.getAll() riêng (thực chất là /api/Dbo/Tenant) —
 * gộp lại đây, cache bằng shareReplay(1) để cả phiên làm việc chỉ gọi API 1 lần. */
@Injectable({ providedIn: 'root' })
export class TenantOptionsService {
  private departmentService = inject(DepartmentService);
  private cached$?: Observable<TenantOption[]>;

  getAll(): Observable<TenantOption[]> {
    if (!this.cached$) {
      this.cached$ = this.departmentService
        .getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          map(res => (res.data as any[]).map(t => ({ id: t.publicId ?? t.id, name: t.name }) as TenantOption)),
          shareReplay(1)
        );
    }
    return this.cached$;
  }
}
