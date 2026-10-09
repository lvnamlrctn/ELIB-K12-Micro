import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Tenant } from '../../models/system/tenant';

export interface TenantSearchResult { data: Tenant[]; recordsTotal: number; }

// Service riêng (không qua BaseEntityService) vì payload Add/Update cần đủ field host/logoText/logoUrl
// mà BaseEntityService.create/update chỉ gửi cố định name/portalId/language.
@Injectable({ providedIn: 'root' })
export class TenantService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Dbo/Tenant`; }

  search(keyword: string, pageIndex: number, pageSize: number): Observable<TenantSearchResult> {
    const payload = { keyword: keyword || '', pageIndex, pageSize };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => ({ data: res?.data?.items ?? [], recordsTotal: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getById(publicId: string): Observable<Tenant | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(
      map(r => r?.data ?? null), catchError(() => of(null))
    );
  }

  create(item: Tenant): Observable<Tenant> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r));
  }

  update(publicId: string, item: Tenant): Observable<Tenant> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r));
  }

  delete(publicId: string): Observable<void> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r));
  }
}
