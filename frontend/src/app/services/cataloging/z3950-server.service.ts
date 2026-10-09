import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/**
 * Trạng thái server Z39.50 (Zebra). Backend chỉ xuất file MARC ra thư mục dùng chung; tiến trình Zebra
 * chạy ở container riêng và tự đánh chỉ mục thư mục đó — nên số liệu ở đây là "đã xuất bao nhiêu biểu
 * ghi", không phải "Zebra đã đánh chỉ mục xong bao nhiêu".
 */
export interface ZebraDatabaseInfo {
  /** Mã đơn vị = tên thư mục xuất. "_system" = biểu ghi không thuộc đơn vị nào. */
  code: string;
  /** Tên database Z39.50 của đơn vị; null với biểu ghi hệ thống (chỉ nằm trong database gộp). */
  database: string | null;
  tenantName: string;
  recordCount: number;
  lastExportAt: string | null;
}

export interface ZebraExportStatus {
  host: string;
  port: string;
  /** Database gộp toàn bộ đơn vị; null khi người dùng chỉ thuộc 1 đơn vị. */
  combinedDatabase: string | null;
  directory: string;
  directoryExists: boolean;
  totalCount: number;
  lastExportAt: string | null;
  databases: ZebraDatabaseInfo[];
}

@Injectable({ providedIn: 'root' })
export class Z3950ServerService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/admin/job`; }

  getStatus(): Observable<ZebraExportStatus | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/zebra-export-status`).pipe(
      map(r => (r?.data ?? null) as ZebraExportStatus | null), catchError(() => of(null))
    );
  }

  /**
   * Xuất lại chạy NỀN (Hangfire) — trả jobId, null nếu lỗi.
   * Không dùng bản `-sync` ở giao diện: đơn vị nhiều biểu ghi (hàng nghìn) mất vài phút, request sẽ
   * timeout trước khi xong. Endpoint `-sync` vẫn còn để gọi bằng script cho đơn vị nhỏ.
   */
  rebuild(): Observable<string | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/rebuild-zebra-export`, {}).pipe(
      map(r => (r?.data?.jobId ?? '') as string), catchError(() => of(null))
    );
  }
}
