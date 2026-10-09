import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { DicClass } from '../../models/cataloging/dic-class';

export interface DicClassSearchResult { data: DicClass[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class DicClassService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/Dic/DicClass`; }

  search(params: { keyword?: string | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<DicClassSearchResult> {
    const payload = { keyword: params.keyword || '', pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: DicClass[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getTree(): Observable<DicClass[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/GetTree`, {}).pipe(
      map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])),
      catchError(() => of([]))
    );
  }

  /** Làm phẳng cây thành danh sách option có thụt lề cho dropdown chọn cha. */
  flattenForSelect(nodes: DicClass[], depth = 0): { id: number; label: string }[] {
    const out: { id: number; label: string }[] = [];
    for (const n of nodes) {
      out.push({ id: n.id, label: `${'— '.repeat(depth)}${n.code ? n.code + ' ' : ''}${n.vnDescription || n.description || ''}` });
      if (n.children?.length) out.push(...this.flattenForSelect(n.children, depth + 1));
    }
    return out;
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getById(publicId: string): Observable<DicClass> { return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Partial<DicClass>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<DicClass>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<void> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }
}
