import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { SearchQualityReport } from '../../models/system/search-quality';

@Injectable({ providedIn: 'root' })
export class SearchQualityService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Cms/SearchQuality`; }

  getReport(days: number): Observable<SearchQualityReport | null> {
    return this.http.get<any>(`${this.baseUrl}?days=${days}`).pipe(
      map(res => (res?.data ?? res) as SearchQualityReport),
      catchError(() => of(null))
    );
  }
}
