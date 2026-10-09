import { Injectable } from '@angular/core';
import { BaseEntityService } from '../shared/base-entity.service';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { Role } from '../../models/system/role';
export type { Role } from '../../models/system/role';

@Injectable({ providedIn: 'root' })
export class RoleService extends BaseEntityService<Role> {
  protected override get baseUrl(): string {
    return '/api/Cms/Roles';
  }

  override getAll(params: DataTableParams): Observable<DataTableResponse<Role>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      tenantId: params['tenantId'] ?? null,
      pageIndex,
      pageSize: params.length || 10
    };

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.apiBase}/Search`, payload).pipe(
      map(res => {
        let data: Role[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        else if (res?.data?.items && Array.isArray(res.data.items)) data = res.data.items;
        else if (res?.items && Array.isArray(res.items)) data = res.items;

        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { draw: params.draw, data, recordsTotal: total, recordsFiltered: total };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }

  override create(item: Partial<Role>): Observable<Role> {
    const payload = { name: item.name, code: item.code, app: item.app };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.apiBase}/Add`, payload).pipe(map(res => res.data || res));
  }

  override update(item: Role): Observable<Role> {
    const rec = item as unknown as Record<string, unknown>;
    const id = rec['publicId'] || item.id || rec['Id'];
    const payload = {
      name: item.name,
      code: item.code || item.Code,
      app: item.app || item.App
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.apiBase}/Update/${id}`, payload).pipe(map(res => res.data || res));
  }
}
