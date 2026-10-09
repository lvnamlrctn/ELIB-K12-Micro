import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { SerialIssue, SerialStatistics } from '../../models/serial/serial-issue';

export interface SerialIssueSearchResult { data: SerialIssue[]; recordsTotal: number; }

// ELIB: /api/PrintBook/Magazine/Serial — nguồn DataAccess.PrintBook.Magazine.Serial
//   SearchSerialSubscriptionReceipt (danh sách kỳ), SaveSerialItem (nhận kỳ), GetLasPublishDate, ThongKe...
@Injectable({ providedIn: 'root' })
export class SerialIssueService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Magazine/Serial`; }

  // SearchSerialSubscriptionReceipt(SubscriptionId, page)
  search(params: { subscriptionId: number; pageIndex?: number; pageSize?: number }): Observable<SerialIssueSearchResult> {
    const payload = { subscriptionId: params.subscriptionId, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 20 };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchReceipt`, payload).pipe(
      map(res => {
        let data: SerialIssue[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  // SaveSerialItem — nhận/cập nhật một kỳ
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  save(item: Partial<SerialIssue>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/SaveItem`, item).pipe(map(r => r.data ?? r)); }

  // Khiếu nại kỳ (claim) — tăng claim_count, đặt claim_date
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  claim(item: { id: number; note?: string }): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Claim`, item).pipe(map(r => r.data ?? r), catchError(() => of(null))); }

  // GetLasPublishDate — ngày phát hành kỳ gần nhất của đăng ký
  getLastPublishDate(subscriptionId: number): Observable<string | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/LastPublishDate/${subscriptionId}`).pipe(
      map(r => r?.data?.publishedDate ?? r?.publishedDate ?? r?.data ?? null),
      catchError(() => of(null))
    );
  }

  // Sinh kỳ dự kiến (issue prediction) — chuẩn ILS: từ pattern + frequency + startX/Y/Z + firstTime
  // backend tạo trước `count` kỳ status=0 (dự kiến) cho đăng ký
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  predict(subscriptionId: number, count: number): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/PredictIssues`, { subscriptionId, count }).pipe(map(r => r.data ?? r));
  }

  // ThongKe — thống kê nhận kỳ của một đăng ký
  getStatistics(subscriptionId: number): Observable<SerialStatistics> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/Statistics/${subscriptionId}`).pipe(
      map(r => { const d = r?.data ?? r ?? {}; return { expected: d.expected ?? 0, received: d.received ?? 0, claimed: d.claimed ?? 0, missing: d.missing ?? 0, late: d.late ?? 0 }; }),
      catchError(() => of({ expected: 0, received: 0, claimed: 0, missing: 0, late: 0 }))
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<void> { return this.http.delete<any>(`${this.baseUrl}/DeleteItem/${publicId}`).pipe(map(r => r.data ?? r)); }
}
