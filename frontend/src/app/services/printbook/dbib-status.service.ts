import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { DBibStatus } from '../../models/printbook/dbib-status';

@Injectable({ providedIn: 'root' })
export class DBibStatusService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/PrintBook/DBibStatus`;
  }

  searchAll(): Observable<DBibStatus[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, {}).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res?.data && Array.isArray(res.data)) return res.data;
        if (res?.data?.items) return res.data.items;
        return [];
      }),
      catchError(() => of([]))
    );
  }
}
