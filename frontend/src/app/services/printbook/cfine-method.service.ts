import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { CFineMethod } from '../../models/printbook/cfine-method';

@Injectable({ providedIn: 'root' })
export class CFineMethodService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/CFineMethod`; }

  searchAll(): Observable<CFineMethod[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, {}).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res?.data?.items) return res.data.items;
        if (res?.data && Array.isArray(res.data)) return res.data;
        return [];
      }),
      catchError(() => of([]))
    );
  }
}
