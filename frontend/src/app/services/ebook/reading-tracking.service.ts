import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface ReadingStats {
  labels: string[];
  counts: number[];
  totalReads: number;
  totalDocuments: number;
  totalReaders: number;
}

export interface ReadingLog {
  readerName: string;
  itemTitle: string;
  readAt: string;       // ISO datetime
  duration: number;     // số phút đọc
  tenantName?: string;  // tên đơn vị (hiển thị khi super-admin lọc chéo đơn vị)
}

export interface ReadingDetailResult {
  items: ReadingLog[];
  recordsTotal: number;
}

export interface ReadingStatsParams { fromDate?: string | null; toDate?: string | null; groupBy?: 'day' | 'month'; tenantId?: string | null; }
export interface ReadingSearchParams {
  keyword?: string; fromDate?: string | null; toDate?: string | null; tenantId?: string | null; pageIndex?: number; pageSize?: number;
}

/**
 * Theo dõi đọc tài liệu số. Endpoint backend chưa có → trả dữ liệu mẫu khi lỗi/khuyết
 * (xem docs/BACKEND-READING-TRACKING.md cho hợp đồng API).
 */
@Injectable({ providedIn: 'root' })
export class ReadingTrackingService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook/ReadingTracking`;
  }

  getStatistics(params: ReadingStatsParams): Observable<ReadingStats> {
    const body = {
      fromDate: params.fromDate || null,
      toDate:   params.toDate   || null,
      groupBy:  params.groupBy  || 'month',
      tenantId: params.tenantId ?? null,
    };
    return this.http.post<any>(`${this.baseUrl}/Statistics`, body).pipe(
      map(res => {
        const d = res?.data ?? res ?? {};
        return {
          labels:         Array.isArray(d.labels) ? d.labels : [],
          counts:         Array.isArray(d.counts) ? d.counts : [],
          totalReads:     d.totalReads     ?? 0,
          totalDocuments: d.totalDocuments ?? 0,
          totalReaders:   d.totalReaders   ?? 0,
        } as ReadingStats;
      }),
      catchError(() => of(this.mockStats(body.groupBy as 'day' | 'month')))
    );
  }

  searchDetail(params: ReadingSearchParams): Observable<ReadingDetailResult> {
    const body = {
      keyword:   params.keyword   || '',
      fromDate:  params.fromDate  || null,
      toDate:    params.toDate    || null,
      tenantId:  params.tenantId  ?? null,
      pageIndex: params.pageIndex ?? 1,
      pageSize:  params.pageSize  ?? 10,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, body).pipe(
      map(res => {
        const data = res?.data ?? res ?? {};
        const items = Array.isArray(data.items) ? data.items : (Array.isArray(data) ? data : []);
        const recordsTotal = data.recordsTotal ?? data.totalCount ?? items.length;
        return { items, recordsTotal } as ReadingDetailResult;
      }),
      catchError(() => of(this.mockDetail(body.pageSize)))
    );
  }

  // ===== Dữ liệu mẫu (khi backend chưa sẵn) =====
  private mockStats(groupBy: 'day' | 'month'): ReadingStats {
    const labels = groupBy === 'day'
      ? ['01', '02', '03', '04', '05', '06', '07']
      : ['T1', 'T2', 'T3', 'T4', 'T5', 'T6'];
    const counts = groupBy === 'day'
      ? [12, 18, 9, 25, 30, 22, 28]
      : [120, 210, 180, 260, 320, 410];
    return {
      labels, counts,
      totalReads: counts.reduce((a, b) => a + b, 0),
      totalDocuments: 84,
      totalReaders: 53,
    };
  }

  private mockDetail(pageSize: number): ReadingDetailResult {
    const sample: ReadingLog[] = [
      { readerName: 'Nguyễn Văn A', itemTitle: 'Lập trình C# nâng cao',        readAt: '2026-06-20T09:15:00', duration: 35 },
      { readerName: 'Trần Thị B',   itemTitle: 'Cẩm nang pháp luật giao thông', readAt: '2026-06-20T10:02:00', duration: 18 },
      { readerName: 'Lê Văn C',     itemTitle: 'Kỹ năng sống cho học sinh',      readAt: '2026-06-19T14:40:00', duration: 52 },
      { readerName: 'Phạm Thị D',   itemTitle: 'Lịch sử Việt Nam',               readAt: '2026-06-19T08:05:00', duration: 27 },
      { readerName: 'Hoàng Văn E',  itemTitle: 'Nông nghiệp bền vững',           readAt: '2026-06-18T16:20:00', duration: 41 },
    ];
    return { items: sample.slice(0, pageSize), recordsTotal: sample.length };
  }
}
