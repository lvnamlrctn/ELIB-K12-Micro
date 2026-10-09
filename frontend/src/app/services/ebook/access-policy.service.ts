import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AccessPolicy } from '../../models/ebook/access-policy';

export interface AccessPolicySearchResult {
  data: AccessPolicy[];
  recordsTotal: number;
}

@Injectable({ providedIn: 'root' })
export class AccessPolicyService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook/PolicyDigital`;
  }

  search(keyword = '', pageIndex = 1, pageSize = 10, tenantId: string | null = null): Observable<AccessPolicySearchResult> {
    const payload = { keyword, pageIndex, pageSize, tenantId };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: AccessPolicy[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        else if (res?.data?.items) data = res.data.items;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  create(item: Partial<AccessPolicy>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data || res));
  }

  update(id: number | string, item: Partial<AccessPolicy>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, item).pipe(map(res => res.data || res));
  }

  delete(id: number | string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data || res));
  }
}
