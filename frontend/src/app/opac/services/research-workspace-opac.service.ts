import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

export interface ResearchProject {
  id: string;
  title: string;
  description: string;
  category: string;
  createdAt: string;
  color: string;
  documentIds?: string[];
}

/** Đợt 9 — chỉ đồng bộ Đề tài nghiên cứu (projects); highlights giữ nguyên dạng mảng rỗng/qua tay vì
 * ELIB chưa có UI chọn-đoạn-văn-bản trong trang đọc ebook (opac/pages/reader) để tạo highlight — phần đó
 * để lại làm việc tương lai riêng, không giả vờ có tính năng chưa tồn tại. */
export interface WorkspaceSnapshot {
  version: string;
  updatedAt: string | null;
  projects: ResearchProject[];
  highlights: unknown[];
}

/** Không gian nghiên cứu đồng bộ đa thiết bị (Đợt 9) — ReaderWorkspaceController, api/public/ReaderWorkspace.
 * Ghi bằng compare-and-swap trên version — 409 nghĩa là thiết bị khác vừa sửa trước, phải tải lại. */
@Injectable({ providedIn: 'root' })
export class ResearchWorkspaceOpacService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private get backendRoot(): string {
    return isPlatformServer(this.platformId) ? APP_CONFIG.BackendBase : '';
  }
  private get baseUrl(): string {
    return `${this.backendRoot}/api/public/ReaderWorkspace`;
  }

  get(): Observable<WorkspaceSnapshot> {
    return this.http.get<any>(this.baseUrl).pipe(
      map(res => {
        const d = res?.data ?? res ?? {};
        const snap = d.snapshot ?? {};
        return {
          version: d.version ?? '00000000-0000-0000-0000-000000000000',
          updatedAt: d.updatedAt ?? null,
          projects: Array.isArray(snap.projects) ? snap.projects : [],
          highlights: Array.isArray(snap.highlights) ? snap.highlights : [],
        } as WorkspaceSnapshot;
      }),
      catchError(() => of({ version: '00000000-0000-0000-0000-000000000000', updatedAt: null, projects: [], highlights: [] }))
    );
  }

  save(version: string, projects: ResearchProject[], highlights: unknown[]): Observable<{ ok: boolean; conflict: boolean; version?: string }> {
    return this.http.put<any>(this.baseUrl, { version, projects, highlights }).pipe(
      map(res => ({ ok: !!res?.success, conflict: false, version: res?.data?.version })),
      catchError(err => of({ ok: false, conflict: err?.status === 409 }))
    );
  }
}
