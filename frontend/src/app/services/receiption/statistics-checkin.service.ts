import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface CheckInStatRow { key: string; label: string; count: number; }
// Tiêu chí (ELIB StatisticsCheckn: ByReaderType/ByClass/ByCourse/ByDepartment)
export type CheckInStatCriterion = 'readerType' | 'class' | 'course' | 'department';

// ELIB: /api/PrintBook/Receiption/Statistics — nguồn Bussiness.PrintBook.Receiption.StatisticsCheckn
@Injectable({ providedIn: 'root' })
export class StatisticsCheckInService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Receiption/Statistics`; }

  statistics(params: { criterion: CheckInStatCriterion; receiptDateFrom?: string | null; receiptDateTo?: string | null; circPlaceId?: number | null }): Observable<CheckInStatRow[]> {
    const payload = { criterion: params.criterion, receiptDateFrom: params.receiptDateFrom || '', receiptDateTo: params.receiptDateTo || '', circPlaceId: params.circPlaceId ?? 0 };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/CheckIn`, payload).pipe(
      map(res => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        let rows: any[] = [];
        if (Array.isArray(res)) rows = res;
        else if (res?.data?.items) rows = res.data.items;
        else if (res?.data && Array.isArray(res.data)) rows = res.data;
        return rows.map(r => ({ key: String(r.key ?? r.id ?? ''), label: r.label ?? r.name ?? '', count: r.count ?? r.total ?? 0 }));
      }),
      catchError(() => of([]))
    );
  }

  export(params: { criterion: CheckInStatCriterion; receiptDateFrom?: string | null; receiptDateTo?: string | null; circPlaceId?: number | null }): Observable<Blob> {
    const payload = { criterion: params.criterion, receiptDateFrom: params.receiptDateFrom || '', receiptDateTo: params.receiptDateTo || '', circPlaceId: params.circPlaceId ?? 0 };
    return this.http.post(`${this.baseUrl}/CheckInExport`, payload, { responseType: 'blob' }).pipe(catchError(() => of(new Blob())));
  }
}
