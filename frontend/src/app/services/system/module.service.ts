import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AppModule } from '../../models/system/module';
export type { AppModule, AppModuleFlatNode } from '../../models/system/module';

export interface ModuleSyncMissingEntry {
  code: string;
  name: string;
  parentCode: string | null;
}

export interface ModuleSyncDiffResult {
  missing: ModuleSyncMissingEntry[];
  orphaned: string[];
}

export interface ModuleSyncApplyResult {
  created: number;
}

@Injectable({ providedIn: 'root' })
export class ModuleService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Cms/Module`;
  }

  getTree(keyword = ''): Observable<AppModule[]> {
    const payload = { keyword, status: 0, pageIndex: 0, pageSize: 0, parentId: 0 };
    return this.http.post<any>(`${this.baseUrl}/GetTree`, payload).pipe(
      map(res => {
        const data: AppModule[] = res?.data ?? res;
        return Array.isArray(data) ? this.markDepth(data, 0) : [];
      }),
      catchError(() => of([]))
    );
  }

  getById(publicId: string): Observable<AppModule> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(
      map(res => res.data || res),
      catchError(() => of({} as AppModule))
    );
  }

  create(item: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data || res));
  }

  update(publicId: string, item: any): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data || res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data || res));
  }

  /** So sánh cms.Module hiện có với menu.ts (ModuleCatalog phía backend) — chỉ báo thiếu/thừa, không ghi gì. */
  syncDiff(): Observable<ModuleSyncDiffResult> {
    return this.http.get<any>(`${this.baseUrl}/SyncDiff`).pipe(
      map(res => (res?.data ?? res) as ModuleSyncDiffResult),
      catchError(() => of({ missing: [], orphaned: [] } as ModuleSyncDiffResult))
    );
  }

  /** Tạo các module còn thiếu (chưa xuất bản). Không xoá/sửa module đã có. */
  syncApply(): Observable<ModuleSyncApplyResult> {
    return this.http.post<any>(`${this.baseUrl}/SyncApply`, {}).pipe(
      map(res => (res?.data ?? res) as ModuleSyncApplyResult)
    );
  }

  private markDepth(nodes: AppModule[], depth: number): AppModule[] {
    return nodes.map(node => ({
      ...node,
      depth,
      expanded: depth === 0,
      children: node.children ? this.markDepth(node.children, depth + 1) : []
    }));
  }

  flattenForSelect(nodes: AppModule[], depth = 0): { id: number; label: string }[] {
    const result: { id: number; label: string }[] = [];
    for (const node of nodes) {
      const prefix = '——'.repeat(depth);
      result.push({ id: node.id, label: prefix + node.name });
      if (node.children?.length) {
        result.push(...this.flattenForSelect(node.children, depth + 1));
      }
    }
    return result;
  }
}
