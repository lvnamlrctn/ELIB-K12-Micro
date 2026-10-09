import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AdminTaskView } from '../../models/system/admin-task';

export interface BarcodeImportResult { successCount: number; failedCount: number; errors: string[]; message: string }

// ELIB: /api/PrintBook/Store/ReRegisterBarcode — nguồn form Elib/Store/ReRegisterBarcode
// Đổi/đăng ký lại mã vạch (ĐKCB) của một bản cá biệt
@Injectable({ providedIn: 'root' })
export class ReRegisterBarcodeService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Store/ReRegisterBarcode`; }

  // Tra cứu bản cá biệt theo mã vạch hiện tại
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  lookup(barcode: string): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/Lookup/${encodeURIComponent(barcode)}`).pipe(
      map(r => {
        const d = r?.data ?? r;
        if (!d) return null;
        return { barcode: d.barcodeValue, bibTitle: d.title, author: d.author, storeId: d.store ?? null, status: d.status };
      }),
      catchError(() => of(null))
    );
  }

  // Gán mã vạch mới
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  reRegister(payload: { oldBarcode: string; newBarcode: string; storeId?: number }): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/ReRegister`, payload).pipe(map(r => r.data ?? r));
  }

  // Đợt 22.5 — đánh lại mã hàng loạt từ Excel (cột: Mã vạch cũ, Mã vạch mới, Mã kho [tuỳ chọn]).
  importExcel(file: File): Observable<BarcodeImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<any>(`${this.baseUrl}/Import`, formData).pipe(
      map(res => {
        const d = res?.data ?? res;
        return { successCount: d?.successCount ?? 0, failedCount: d?.failedCount ?? 0,
          errors: Array.isArray(d?.errors) ? d.errors : [], message: res?.message ?? d?.message ?? '' };
      }),
      catchError(err => of({ successCount: 0, failedCount: 1, errors: [err?.error?.message || 'Lỗi kết nối'], message: '' }))
    );
  }

  importExcelBackground(file: File): Observable<AdminTaskView | null> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('background', 'true');
    return this.http.post<any>(`${this.baseUrl}/Import`, formData).pipe(
      map(res => (res?.data ?? res) as AdminTaskView),
      catchError(() => of(null))
    );
  }
}
