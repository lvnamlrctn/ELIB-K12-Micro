import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface EntityHistoryFieldChange {
  field: string;
  oldValue?: string | null;
  newValue?: string | null;
}

export interface EntityHistoryEvent {
  id: number;
  timestamp?: string | null;
  actorId: number;
  actorName?: string | null;
  action: string;
  reason?: string | null;
  changes: EntityHistoryFieldChange[];
}

export interface EntityHistoryActor { id: number; name?: string | null; }

export interface EntityHistoryPage {
  items: EntityHistoryEvent[];
  next: number | null;
  /** Người đã thao tác trên chính hồ sơ này — chỉ có ở trang đầu (Đợt 21). */
  actors?: EntityHistoryActor[] | null;
}

/** Bộ lọc Đợt 21: ngày dạng yyyy-MM-dd (gồm trọn ngày "đến"), người thao tác, loại thao tác. */
export interface EntityHistoryFilters { from?: string; to?: string; actorId?: number | null; action?: string; }

@Injectable({ providedIn: 'root' })
export class EntityHistoryService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Dbo/EntityHistory`; }

  getHistory(type: string, id: string, before: number | null, pageSize = 25, filters: EntityHistoryFilters = {}): Observable<EntityHistoryPage | null> {
    let params: Record<string, string> = { pageSize: String(pageSize) };
    if (before != null) params = { ...params, before: String(before) };
    if (filters.from) params = { ...params, from: filters.from };
    if (filters.to) params = { ...params, to: filters.to };
    if (filters.actorId != null) params = { ...params, actorId: String(filters.actorId) };
    if (filters.action) params = { ...params, action: filters.action };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/${type}/${id}`, { params }).pipe(
      map(res => (res?.data ?? null) as EntityHistoryPage | null),
      catchError(() => of(null))
    );
  }
}
