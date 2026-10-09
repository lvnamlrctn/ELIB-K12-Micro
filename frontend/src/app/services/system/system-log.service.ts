import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { SystemLog, SystemUser, SystemLogSearchParams } from '../../models/system/system-log';
export type { SystemLog, SystemUser, SystemLogSearchParams } from '../../models/system/system-log';

@Injectable({ providedIn: 'root' })
export class SystemLogService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.baseApiUrl + '/api/Dbo/UserLog';
  private readonly userUrl = environment.baseApiUrl + '/api/Dbo/User';

  search(params: SystemLogSearchParams): Observable<{ data: SystemLog[]; total: number }> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, params).pipe(
      map(res => {
        let data: SystemLog[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        else if (res?.data?.items && Array.isArray(res.data.items)) data = res.data.items;
        else if (res?.items && Array.isArray(res.items)) data = res.items;
        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, total };
      }),
      catchError(() => of({ data: [], total: 0 }))
    );
  }

  getUsers(): Observable<SystemUser[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.userUrl}/SearchAll`).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res?.data && Array.isArray(res.data)) return res.data;
        if (res?.data?.items && Array.isArray(res.data.items)) return res.data.items;
        return [];
      }),
      catchError(() => of([]))
    );
  }
}
