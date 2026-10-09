import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface NganhMonHoc {
  id?: string;
  publicId?: string;
  majorId?: number | null;
  monHocId?: number | null;
}

/** Bảng nối Ngành học ↔ Môn học — không có trang danh sách riêng, chỉ dùng trong trang "Thiết lập môn học" của 1 ngành. */
@Injectable({ providedIn: 'root' })
export class NganhMonHocService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Evaluate/NganhMonHoc`;
  }

  /** Toàn bộ môn học đã gán cho 1 ngành. */
  listByMajor(majorId: string | number): Observable<NganhMonHoc[]> {
    return this.http.post<any>(`${this.baseUrl}/Search`, { majorId, pageIndex: 1, pageSize: 500 }).pipe(
      map(res => {
        let data: NganhMonHoc[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        return data;
      }),
      catchError(() => of([]))
    );
  }

  /** Gán 1 môn học vào ngành. */
  link(majorId: string | number, monHocId: string | number): Observable<NganhMonHoc | null> {
    return this.http.post<any>(`${this.baseUrl}/Add`, { majorId, monHocId }).pipe(
      map(res => res?.data ?? res ?? null),
      catchError(() => of(null))
    );
  }

  /** Gỡ 1 môn học khỏi ngành (publicId của dòng NganhMonHoc, không phải của môn học). */
  unlink(publicId: string): Observable<boolean> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(
      map(() => true),
      catchError(() => of(false))
    );
  }
}
