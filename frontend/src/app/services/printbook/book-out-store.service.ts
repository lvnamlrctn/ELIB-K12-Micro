import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { BookOutStoreLine, ScanOutParams, BookOutStoreSearchParams } from '../../models/printbook/book-out-store';

export interface BookOutStoreHistoryResult { data: BookOutStoreLine[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class BookOutStoreService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Store/BookOutStore`; }

  private buildHistoryPayload(params: BookOutStoreSearchParams) {
    return {
      keyword: params.keyword || '', typeStatus: params.typeStatus || null, unitId: params.unitId ?? null,
      reasonId: params.reasonId ?? null, exhibitionLocationId: params.exhibitionLocationId ?? null,
      delivererName: params.delivererName || null, receiverName: params.receiverName || null, barcode: params.barcode || null,
      returnBarcodeAtLibrary: params.returnBarcodeAtLibrary ?? null,
      exportDateFrom: params.exportDateFrom || null, exportDateTo: params.exportDateTo || null,
      importDateFrom: params.importDateFrom || null, importDateTo: params.importDateTo || null,
      pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10,
      tenantId: params.tenantId ?? null,
    };
  }

  history(params: BookOutStoreSearchParams): Observable<BookOutStoreHistoryResult> {
    const payload = this.buildHistoryPayload(params);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/History`, payload).pipe(
      map(res => ({ data: (res?.data?.items ?? []) as BookOutStoreLine[], recordsTotal: res?.data?.recordsTotal ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  exportExcel(params: BookOutStoreSearchParams): Observable<Blob> {
    const payload = this.buildHistoryPayload(params);
    return this.http.post(`${this.baseUrl}/Export`, payload, { responseType: 'blob' }).pipe(
      catchError(() => of(new Blob()))
    );
  }

  scanOut(p: ScanOutParams): Observable<BookOutStoreLine> {
    const payload = {
      barcode: p.barcode, delivererName: p.delivererName || null, receiverName: p.receiverName || null,
      reasonId: p.reasonId ?? null, unitId: p.unitId ?? null, exhibitionLocationId: p.exhibitionLocationId ?? null,
      returnBarcodeAtLibrary: p.returnBarcodeAtLibrary,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/ScanOut`, payload).pipe(map(r => r.data ?? r));
  }

  scanIn(barcode: string): Observable<BookOutStoreLine> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/ScanIn`, { barcode }).pipe(map(r => r.data ?? r));
  }
}
