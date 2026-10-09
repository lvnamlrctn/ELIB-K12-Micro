import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { DonViNode } from '../../models/evaluate/don-vi';

export interface DonViOption { id: number; name?: string | null; }

// Đơn vị đào tạo (Khoa/Viện/Trường) — Đợt 8, KHÁC OrgService (services/system/org.service.ts = phòng ban
// nội bộ hệ thống). Mirror đúng khuôn OrgService cho cây phân cấp.
@Injectable({ providedIn: 'root' })
export class DonViService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Evaluate/DonVi`; }

  getTree(keyword = '', tenantId: string | null = null): Observable<DonViNode[]> {
    const payload = { keyword, pageIndex: 0, pageSize: 0, parentId: 0, tenantId };
    return this.http.post<any>(`${this.baseUrl}/GetTree`, payload).pipe(
      map(res => {
        const data: DonViNode[] = res?.data ?? res;
        return Array.isArray(data) ? this.markDepth(data, 0) : [];
      }),
      catchError(() =>
        this.http.post<any>(`${this.baseUrl}/Search`, { keyword, pageIndex: 1, pageSize: 999 }).pipe(
          map(res => {
            let data: DonViNode[] = [];
            if (Array.isArray(res))                   data = res;
            else if (Array.isArray(res?.data?.items)) data = res.data.items;
            else if (Array.isArray(res?.data))        data = res.data;
            return this.buildTree(data);
          }),
          catchError(() => of([]))
        )
      )
    );
  }

  getAllForCombobox(): Observable<DonViOption[]> {
    return this.getTree('').pipe(map(tree => this.flattenToList(tree)));
  }

  flattenForSelect(nodes: DonViNode[], depth = 0): { id: number; label: string }[] {
    const result: { id: number; label: string }[] = [];
    for (const node of nodes) {
      result.push({ id: node.id, label: '——'.repeat(depth) + node.name });
      if (node.children?.length) result.push(...this.flattenForSelect(node.children, depth + 1));
    }
    return result;
  }

  getById(id: string | number): Observable<DonViNode> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(
      map(res => res.data || res),
      catchError(() => of({} as DonViNode))
    );
  }

  create(item: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data || res));
  }

  update(id: string | number, item: any): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, item).pipe(map(res => res.data || res));
  }

  delete(id: string | number): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data || res));
  }

  private flattenToList(nodes: DonViNode[]): DonViOption[] {
    const result: DonViOption[] = [];
    for (const node of nodes) {
      result.push({ id: node.id, name: node.name });
      if (node.children?.length) result.push(...this.flattenToList(node.children));
    }
    return result;
  }

  private markDepth(nodes: DonViNode[], depth: number): DonViNode[] {
    return nodes.map(node => ({
      ...node,
      depth,
      expanded: depth === 0,
      children: node.children ? this.markDepth(node.children, depth + 1) : []
    }));
  }

  private buildTree(flat: DonViNode[]): DonViNode[] {
    const map_ = new Map<number, DonViNode>();
    flat.forEach(n => map_.set(n.id, { ...n, children: [], depth: 0, expanded: true }));
    const roots: DonViNode[] = [];
    flat.forEach(n => {
      const node = map_.get(n.id)!;
      if (n.parentId && map_.has(n.parentId)) {
        const parent = map_.get(n.parentId)!;
        parent.children = parent.children ?? [];
        parent.children.push(node);
        node.depth = (parent.depth ?? 0) + 1;
      } else {
        roots.push(node);
      }
    });
    return roots;
  }
}
