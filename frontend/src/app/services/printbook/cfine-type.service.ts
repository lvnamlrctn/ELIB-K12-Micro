import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { CFineType } from '../../models/printbook/cfine-type';

export interface CFineTypeSearchResult {
  data:         CFineType[];
  recordsTotal: number;
}

// API trả/nhận cột Status_Reg_Id dưới tên `status_Reg_Id`; model dùng `statusRegId` (port ELIB-LRC 10-03 — trước đây
// cột "Trạng thái" luôn trống và sửa một lý do làm mất trạng thái đã gán).
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const fromApi = (x: any): CFineType => x ? { ...x, statusRegId: x.status_Reg_Id ?? x.statusRegId ?? undefined } : x;
const toApi = (x: Partial<CFineType>) => ({ code: x.code, name: x.name, status_Reg_Id: x.statusRegId ?? null });

@Injectable({ providedIn: 'root' })
export class CFineTypeService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/PrintBook/CFineType`;
  }

  search(params: { keyword?: string | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<CFineTypeSearchResult> {
    const payload = {
      keyword:   params.keyword   || '',
      pageIndex: params.pageIndex ?? 1,
      pageSize:  params.pageSize  ?? 10,
      tenantId:  params.tenantId  ?? null,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: CFineType[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data: data.map(fromApi), recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getById(publicId: string): Observable<CFineType> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(res => fromApi(res.data ?? res)));
  }

  create(item: Partial<CFineType>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, toApi(item)).pipe(map(res => res.data ?? res));
  }

  update(publicId: string, item: Partial<CFineType>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, toApi(item)).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }

  searchAll(): Observable<CFineType[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, {}).pipe(
      map(res => {
        const list: CFineType[] = Array.isArray(res) ? res : res?.data?.items ?? (Array.isArray(res?.data) ? res.data : []);
        return list.map(fromApi);
      }),
      catchError(() => of([]))
    );
  }
}
