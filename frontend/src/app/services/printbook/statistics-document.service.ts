import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

// Một dòng thống kê (nhóm + số lượng)
export interface StatisticRow {
  key:   string;   // mã/định danh nhóm (bibTypeId, statusId, languageId, ddc, storeId)
  label: string;   // tên hiển thị
  count: number;   // số biểu ghi/bản
  detail?: number; // số bản cá biệt (nếu có)
}

// Tiêu chí thống kê (ELIB StatisticsDocument: ByBibType/ByStatus/ByLanguage/ByDDC/BookInStore)
export type StatCriterion = 'bibType' | 'status' | 'language' | 'ddc' | 'store';

// ELIB: /api/PrintBook/Store/Statistics — nguồn Bussiness.PrintBook.Store.StatisticsDocument
@Injectable({ providedIn: 'root' })
export class StatisticsDocumentService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Store/Statistics`; }

  // StatisticsBy{BibType|Status|Language|DDC} / StatisticBookInStore(ReceiptDateFrom, ReceiptDateTo, StoreId)
  statistics(params: { criterion: StatCriterion; receiptDateFrom?: string | null; receiptDateTo?: string | null; storeId?: number | null }): Observable<StatisticRow[]> {
    const payload = { criterion: params.criterion, receiptDateFrom: params.receiptDateFrom || null, receiptDateTo: params.receiptDateTo || null, storeId: params.storeId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Document`, payload).pipe(
      map(res => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        let rows: any[] = [];
        if (Array.isArray(res)) rows = res;
        else if (res?.data?.items) rows = res.data.items;
        else if (res?.data && Array.isArray(res.data)) rows = res.data;
        return rows.map(r => ({ key: String(r.key ?? r.id ?? ''), label: r.label ?? r.name ?? '', count: r.count ?? r.total ?? 0, detail: r.detail ?? r.itemCount }));
      }),
      catchError(() => of([]))
    );
  }

  // Xuất Excel báo cáo thống kê
  export(params: { criterion: StatCriterion; receiptDateFrom?: string | null; receiptDateTo?: string | null; storeId?: number | null }): Observable<Blob> {
    const payload = { criterion: params.criterion, receiptDateFrom: params.receiptDateFrom || null, receiptDateTo: params.receiptDateTo || null, storeId: params.storeId ?? null };
    return this.http.post(`${this.baseUrl}/DocumentExport`, payload, { responseType: 'blob' }).pipe(catchError(() => of(new Blob())));
  }
}
