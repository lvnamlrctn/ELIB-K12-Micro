import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MarcBibLevel } from '../../models/cataloging/marc-bib-level';

@Injectable({ providedIn: 'root' })
export class MarcBibLevelService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/MarcBibLevel`; }

  searchAll(): Observable<MarcBibLevel[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, {}).pipe(
      map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])),
      catchError(() => of([]))
    );
  }
}
