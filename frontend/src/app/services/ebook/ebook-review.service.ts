import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface AdminReview {
  id: number;
  publicId: string;
  itemId: string;
  itemTitle: string;
  displayName: string;
  rating: number;
  content: string;
  status: number;        // 1 = chờ duyệt, 2 = đã duyệt
  createdRowDate: string;
  tenantName?: string;
}

export interface ReviewSearchResult {
  items: AdminReview[];
  recordsTotal: number;
}

export interface ReviewSearchParams {
  keyword?: string;
  status?: number | null;   // null = tất cả
  itemId?: string | null;
  tenantId?: string | null;
  pageIndex?: number;
  pageSize?: number;
}

/**
 * Quản lý bình luận tài liệu số (admin). Endpoint backend có thể chưa sẵn → fallback mock
 * (xem docs/BACKEND-EBOOK-REVIEW.md). Trạng thái: 1 = chờ duyệt, 2 = đã duyệt.
 */
@Injectable({ providedIn: 'root' })
export class EbookReviewService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook/EbookReview`;
  }

  search(params: ReviewSearchParams): Observable<ReviewSearchResult> {
    const body = {
      keyword:   params.keyword   || '',
      status:    params.status    ?? null,
      itemId:    params.itemId    || null,
      tenantId:  params.tenantId  ?? null,
      pageIndex: params.pageIndex ?? 1,
      pageSize:  params.pageSize  ?? 10,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, body).pipe(
      map(res => {
        const data = res?.data ?? res ?? {};
        const items = Array.isArray(data.items) ? data.items : (Array.isArray(data) ? data : []);
        const recordsTotal = data.recordsTotal ?? data.totalCount ?? items.length;
        return { items: items.map((x: any) => this.mapReview(x)), recordsTotal } as ReviewSearchResult;
      }),
      catchError(() => of(this.mockResult(body.status, body.pageSize)))
    );
  }

  approve(publicId: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/ChangeStatus`, { publicId, status: 2 }).pipe(
      map(r => r?.data ?? r), catchError(() => of(null))
    );
  }

  unapprove(publicId: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/ChangeStatus`, { publicId, status: 1 }).pipe(
      map(r => r?.data ?? r), catchError(() => of(null))
    );
  }

  remove(publicId: string): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(
      map(r => r?.data ?? r), catchError(() => of(null))
    );
  }

  private mapReview(x: any): AdminReview {
    return {
      id:             x.id ?? x.Id ?? 0,
      publicId:       x.publicId ?? x.PublicId ?? '',
      itemId:         x.itemId ?? x.ItemId ?? '',
      itemTitle:      x.itemTitle ?? x.ItemTitle ?? x.bookTitle ?? '',
      displayName:    x.displayName ?? x.DisplayName ?? '',
      rating:         x.rating ?? x.Rating ?? 0,
      content:        x.content ?? x.Content ?? '',
      status:         x.status ?? x.Status ?? 1,
      createdRowDate: x.createdRowDate ?? x.CreatedRowDate ?? x.createdDate ?? '',
      tenantName:     x.tenantName ?? x.TenantName ?? undefined,
    };
  }

  // ===== Dữ liệu mẫu (khi backend chưa sẵn) =====
  private mockResult(status: number | null, pageSize: number): ReviewSearchResult {
    const all: AdminReview[] = [
      { id: 1, publicId: 'mock-1', itemId: 'doc-1', itemTitle: 'Lập trình C# nâng cao',         displayName: 'Nguyễn Văn A', rating: 5, content: 'Tài liệu rất hữu ích, trình bày dễ hiểu.', status: 1, createdRowDate: '2026-06-20T09:15:00' },
      { id: 2, publicId: 'mock-2', itemId: 'doc-2', itemTitle: 'Cẩm nang pháp luật giao thông', displayName: 'Trần Thị B',   rating: 4, content: 'Nội dung đầy đủ, cập nhật mới.',          status: 2, createdRowDate: '2026-06-19T10:02:00' },
      { id: 3, publicId: 'mock-3', itemId: 'doc-1', itemTitle: 'Lập trình C# nâng cao',         displayName: 'Lê Văn C',     rating: 3, content: 'Cần thêm ví dụ thực tế.',                status: 1, createdRowDate: '2026-06-18T14:40:00' },
      { id: 4, publicId: 'mock-4', itemId: 'doc-3', itemTitle: 'Lịch sử Việt Nam',              displayName: 'Phạm Thị D',   rating: 5, content: 'Hay và bổ ích.',                        status: 2, createdRowDate: '2026-06-17T08:05:00' },
    ];
    const filtered = status == null ? all : all.filter(r => r.status === status);
    return { items: filtered.slice(0, pageSize), recordsTotal: filtered.length };
  }
}
