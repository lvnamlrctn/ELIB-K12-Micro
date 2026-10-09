import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { LinkedBibInfo, LinkedEbookInfo } from '../../models/cataloging/print-book-and-digital';

@Injectable({ providedIn: 'root' })
export class PrintBookAndDigitalService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/PrintBookAndDigital`; }

  /** Tài liệu số đang liên kết với 1 tài liệu in (theo bibId), hoặc null nếu chưa liên kết. */
  getByBibId(bibId: number): Observable<LinkedEbookInfo | null> {
    return this.http.get<any>(`${this.baseUrl}/GetByBibId/${bibId}`).pipe(
      map(r => r?.data ?? null), catchError(() => of(null))
    );
  }

  /** Tài liệu in đang liên kết với 1 tài liệu số (theo ebookId), hoặc null nếu chưa liên kết. */
  getByEbookId(ebookId: number): Observable<LinkedBibInfo | null> {
    return this.http.get<any>(`${this.baseUrl}/GetByEbookId/${ebookId}`).pipe(
      map(r => r?.data ?? null), catchError(() => of(null))
    );
  }

  /** Liên kết 1-1: tự động gỡ liên kết cũ (nếu có) của cả 2 phía trước khi tạo liên kết mới. */
  link(bibId: number, ebookId: number): Observable<boolean> {
    return this.http.post<any>(`${this.baseUrl}/Link`, { bibId, ebookId }).pipe(
      map(() => true), catchError(() => of(false))
    );
  }

  /** Gỡ liên kết hiện tại của 1 tài liệu in hoặc 1 tài liệu số. */
  unlink(params: { bibId?: number; ebookId?: number }): Observable<boolean> {
    return this.http.post<any>(`${this.baseUrl}/Unlink`, params).pipe(
      map(() => true), catchError(() => of(false))
    );
  }
}
