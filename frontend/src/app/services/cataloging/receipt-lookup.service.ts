import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

// 1 dòng kết quả tra cứu đơn nhận = 1 dòng chi tiết (1 biểu ghi trong 1 đơn nhận cụ thể)
export interface ReceiptLookupRow {
  mfn?:         number;
  receiptCode?: number;
  isbd?:        string;
  createdDate?: string;
  sourceName?:  string;
  storeName?:   string;
  bibPublicId?: string;
}

export interface ReceiptLookupParams {
  receiptCodeFrom?: number | null;
  receiptCodeTo?:   number | null;
  mfnFrom?:         number | null;
  mfnTo?:           number | null;
  receiptDateFrom?: string | null;
  receiptDateTo?:   string | null;
  createdDateFrom?: string | null;
  createdDateTo?:   string | null;
  receiptName?:     string | null;
  status?:          number | null;
  sourceId?:        number | null;
  fundId?:          number | null;
  supplierId?:      number | null;
  storeId?:         number | null;
  createdBy?:       number | null;
  title?:           string | null;
  author?:          string | null;
  publisher?:       string | null;
  publishYear?:     string | null;
  pageIndex?:       number;
  pageSize?:        number;
}

export interface ReceiptLookupResult {
  data:         ReceiptLookupRow[];
  recordsTotal: number;
}

export interface ReceiptLabelAmount {
  bibPublicId: string;
  amount:      number;
}

@Injectable({ providedIn: 'root' })
export class ReceiptLookupService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/Receipt`; }

  search(params: ReceiptLookupParams): Observable<ReceiptLookupResult> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/LookupLines`, params).pipe(
      map(res => ({
        data:         res?.data?.items ?? [],
        recordsTotal: res?.data?.totalCount ?? 0,
      })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  export(params: ReceiptLookupParams): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/LookupLinesExport`, params, { responseType: 'blob' }).pipe(
      catchError(() => of(new Blob()))
    );
  }

  searchForLabel(params: ReceiptLookupParams): Observable<ReceiptLabelAmount[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/LookupLinesForLabel`, params).pipe(
      map(res => Array.isArray(res) ? res : (res?.data ?? [])),
      catchError(() => of([]))
    );
  }
}
