import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AdminTaskView, AdminTaskMonitorItem, AdminTaskMonitorDetail, AdminTaskMonitorSummary,
  RetentionPreview, RetentionRun } from '../../models/system/admin-task';

// Đợt 10 — "tác vụ của tôi" (AdminTaskController). Đợt 13 — giám sát tác vụ người khác
// (AdminTaskMonitorController) + dọn payload/result cũ (AdminTaskRetentionController).
@Injectable({ providedIn: 'root' })
export class AdminTaskService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Dbo/AdminTask`; }
  private get monitorUrl(): string { return `${this.baseUrl}/monitor`; }
  private get retentionUrl(): string { return `${this.baseUrl}/retention`; }

  private unwrap = (res: any): AdminTaskView => (res?.data ?? res) as AdminTaskView;

  health(): Observable<{ enabled: boolean }> {
    return this.http.get<any>(`${this.baseUrl}/Health`).pipe(
      map(res => (res?.data ?? res) as { enabled: boolean }),
      catchError(() => of({ enabled: false }))
    );
  }

  list(page = 1, pageSize = 20): Observable<{ total: number; items: AdminTaskView[] }> {
    return this.http.post<any>(`${this.baseUrl}/Search?page=${page}&pageSize=${pageSize}`, {}).pipe(
      map(res => {
        const d = res?.data ?? res;
        return { total: d?.total ?? 0, items: (d?.items ?? []) as AdminTaskView[] };
      }),
      catchError(() => of({ total: 0, items: [] }))
    );
  }

  get(id: string): Observable<AdminTaskView | null> {
    return this.http.get<any>(`${this.baseUrl}/${id}`).pipe(map(this.unwrap), catchError(() => of(null)));
  }

  pause(id: string): Observable<AdminTaskView | null> {
    return this.http.post<any>(`${this.baseUrl}/${id}/pause`, {}).pipe(map(this.unwrap), catchError(() => of(null)));
  }

  resume(id: string): Observable<AdminTaskView | null> {
    return this.http.post<any>(`${this.baseUrl}/${id}/resume`, {}).pipe(map(this.unwrap), catchError(() => of(null)));
  }

  cancel(id: string): Observable<AdminTaskView | null> {
    return this.http.post<any>(`${this.baseUrl}/${id}/cancel`, {}).pipe(map(this.unwrap), catchError(() => of(null)));
  }

  confirm(id: string): Observable<AdminTaskView | null> {
    return this.http.post<any>(`${this.baseUrl}/${id}/confirm`, {}).pipe(map(this.unwrap), catchError(() => of(null)));
  }

  /** File Excel "Dòng cần sửa" của tác vụ nhập bạn đọc (Đợt 20). */
  importErrors(id: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${id}/import-errors`, { responseType: 'blob' });
  }

  previewRemaining(id: string): Observable<AdminTaskView | null> {
    return this.http.post<any>(`${this.baseUrl}/${id}/preview-remaining`, {}).pipe(map(this.unwrap), catchError(() => of(null)));
  }

  // ── Đợt 13 — Giám sát ────────────────────────────────────────────────────
  monitorList(filters: { actorId?: number; kind?: string; state?: string } = {}): Observable<{ items: AdminTaskMonitorItem[]; nextCursor: string | null }> {
    const params = new URLSearchParams();
    if (filters.actorId) params.set('actorId', String(filters.actorId));
    if (filters.kind) params.set('kind', filters.kind);
    if (filters.state) params.set('state', filters.state);
    return this.http.get<any>(`${this.monitorUrl}?${params.toString()}`).pipe(
      map(res => {
        const d = res?.data ?? res;
        return { items: (d?.items ?? []) as AdminTaskMonitorItem[], nextCursor: d?.nextCursor ?? null };
      }),
      catchError(() => of({ items: [], nextCursor: null }))
    );
  }

  monitorSummary(): Observable<AdminTaskMonitorSummary> {
    return this.http.get<any>(`${this.monitorUrl}/summary`).pipe(
      map(res => (res?.data ?? res) as AdminTaskMonitorSummary),
      catchError(() => of({ counts: {} }))
    );
  }

  monitorDetail(id: string): Observable<AdminTaskMonitorDetail | null> {
    return this.http.get<any>(`${this.monitorUrl}/${id}`).pipe(
      map(res => (res?.data ?? res) as AdminTaskMonitorDetail),
      catchError(() => of(null))
    );
  }

  private controlRequestId(): string {
    return (crypto as any)?.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }

  monitorPause(id: string, reason: string): Observable<any> {
    return this.http.post<any>(`${this.monitorUrl}/${id}/pause`, { reason, requestId: this.controlRequestId() }).pipe(
      map(res => res?.data ?? res), catchError(() => of(null))
    );
  }

  monitorResume(id: string, reason: string): Observable<any> {
    return this.http.post<any>(`${this.monitorUrl}/${id}/resume`, { reason, requestId: this.controlRequestId() }).pipe(
      map(res => res?.data ?? res), catchError(() => of(null))
    );
  }

  // ── Đợt 13 — Dọn dữ liệu ─────────────────────────────────────────────────
  retentionPreview(): Observable<RetentionPreview | null> {
    return this.http.get<any>(`${this.retentionUrl}/preview`).pipe(
      map(res => (res?.data ?? res) as RetentionPreview), catchError(() => of(null))
    );
  }

  retentionRuns(): Observable<RetentionRun[]> {
    return this.http.get<any>(`${this.retentionUrl}/runs`).pipe(
      map(res => (res?.data ?? res ?? []) as RetentionRun[]), catchError(() => of([]))
    );
  }

  retentionRun(): Observable<any> {
    return this.http.post<any>(`${this.retentionUrl}/run`, {}).pipe(
      map(res => res?.data ?? res), catchError(() => of(null))
    );
  }

  retentionHold(id: string, hold: boolean): Observable<any> {
    return this.http.post<any>(`${this.retentionUrl}/hold/${id}`, { hold }).pipe(
      map(res => res?.data ?? res), catchError(() => of(null))
    );
  }
}
