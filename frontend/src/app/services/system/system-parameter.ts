import { Injectable } from '@angular/core';
import { BaseEntityService } from '../shared/base-entity.service';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { SystemParameter } from '../../models/system/system-parameter';
export type { SystemParameter } from '../../models/system/system-parameter';

@Injectable({
  providedIn: 'root'
})
export class SystemParameterService extends BaseEntityService<SystemParameter> {
  protected override get baseUrl(): string {
    return '/api/Dbo/SystemParameter';
  }

  override getAll(params: DataTableParams): Observable<DataTableResponse<SystemParameter>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      portalId: '',
      language: '',
      tenantId: params['tenantId'] ?? null,
      pageIndex: pageIndex,
      pageSize: params.length || 10
    };

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: SystemParameter[] = [];
        if (Array.isArray(res)) {
          data = res;
        } else if (res?.data && Array.isArray(res.data)) {
          data = res.data;
        } else if (res?.data?.items && Array.isArray(res.data.items)) {
          data = res.data.items;
        } else if (res && Array.isArray(res.items)) {
           data = res.items;
        }

        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;

        return {
          draw: params.draw,
          data: data,
          recordsTotal: total,
          recordsFiltered: total
        };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }

  override update(item: SystemParameter): Observable<SystemParameter> {
    const itemRecord = item as unknown as Record<string, unknown>;
    const id = itemRecord['publicId'] || item.id || itemRecord['Id'];
    
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const payload: any = {
      code: item.code || item.Code,
      descriptionVn: item.descriptionVn || item.DescriptionVn,
      descriptionEn: item.descriptionEn || item.DescriptionEn,
      value: item.value || item.Value,
      isRemoveHtml: item.isRemoveHtml
    };
    
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, payload).pipe(
      map(res => res.data || res)
    );
  }
}
