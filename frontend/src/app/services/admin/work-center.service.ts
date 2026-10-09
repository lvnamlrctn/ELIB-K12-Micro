import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface WorkSourceInfo { type: string; label: string; canView: boolean; canEdit: boolean; }

export interface WorkItemRow {
  type: string; publicId: string; title: string; detail?: string | null;
  submittedByName?: string | null; submittedByCardNo?: string | null; submittedAt?: string | null;
  statusLabel: string;
  assigneeId?: number | null; assigneeName?: string | null; assigneePermissionLost: boolean;
  dueAtUtc?: string | null; overdue: boolean; version: number;
  roomStartAt?: string | null; roomEndAt?: string | null;
  rating?: number | null;
  docType?: string | null; fileName?: string | null;
}

export interface WorkItemsResponse {
  items: WorkItemRow[]; totalCount: number; mineCount: number; unassignedCount: number; overdueCount: number;
  checkedAtUtc: string;
}

export interface WorkItemAssigneeOption { id: number; name: string; }

@Injectable({ providedIn: 'root' })
export class WorkCenterService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Dbo/WorkCenter`; }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getSources(): Observable<WorkSourceInfo[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/sources`).pipe(
      map(res => (res?.data ?? []) as WorkSourceInfo[]),
      catchError(() => of([]))
    );
  }

  getItems(type: string, view: string, page: number, pageSize: number): Observable<WorkItemsResponse | null> {
    const params = { type, view, page: String(page), pageSize: String(pageSize) };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/items`, { params }).pipe(
      map(res => (res?.data ?? null) as WorkItemsResponse | null),
      catchError(() => of(null))
    );
  }

  getAssignees(type: string, keyword: string, after: number): Observable<{ items: WorkItemAssigneeOption[]; next: number | null }> {
    const params: Record<string, string> = { after: String(after) };
    if (keyword) params['keyword'] = keyword;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/${type}/assignees`, { params }).pipe(
      map(res => res?.data ?? { items: [], next: null }),
      catchError(() => of({ items: [], next: null }))
    );
  }

  getDetail(type: string, publicId: string): Observable<WorkItemRow | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/${type}/${publicId}`).pipe(
      map(res => (res?.data ?? null) as WorkItemRow | null),
      catchError(() => of(null))
    );
  }

  submissionFileUrl(publicId: string): string {
    return `${this.baseUrl}/submission/${publicId}/file`;
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  claim(type: string, publicId: string, version: number): Observable<any> {
    return this.http.post(`${this.baseUrl}/${type}/${publicId}/claim`, { version }).pipe(
      map(() => ({ ok: true })),
      catchError(e => of({ ok: false, status: e?.status, message: e?.error?.message }))
    );
  }

  setAssignment(type: string, publicId: string, body: { version: number; assigneeId: number | null; dueAtUtc: string | null; reason: string })
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  : Observable<any> {
    return this.http.put(`${this.baseUrl}/${type}/${publicId}/assignment`, body).pipe(
      map(() => ({ ok: true })),
      catchError(e => of({ ok: false, status: e?.status, message: e?.error?.message }))
    );
  }

  decide(type: string, publicId: string, approve: boolean, reason: string)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  : Observable<any> {
    return this.http.post(`${this.baseUrl}/${type}/${publicId}/decision`, { approve, reason }).pipe(
      map(() => ({ ok: true })),
      catchError(e => of({ ok: false, status: e?.status, message: e?.error?.message }))
    );
  }

  // Đặt phòng — gọi thẳng nghiệp vụ RoomBookingAdmin sẵn có (không có endpoint decision riêng ở Work Center).
  approveRoomBooking(publicId: string)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  : Observable<any> {
    return this.http.put(`${environment.baseApiUrl}/api/Map/RoomBookingAdmin/Approve/${publicId}`, {}).pipe(
      map(() => ({ ok: true })),
      catchError(e => of({ ok: false, status: e?.status, message: e?.error?.message }))
    );
  }

  rejectRoomBooking(publicId: string, reason: string)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  : Observable<any> {
    return this.http.put(`${environment.baseApiUrl}/api/Map/RoomBookingAdmin/Reject/${publicId}`, { reason }).pipe(
      map(() => ({ ok: true })),
      catchError(e => of({ ok: false, status: e?.status, message: e?.error?.message }))
    );
  }
}
