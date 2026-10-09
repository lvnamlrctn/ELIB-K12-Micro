import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { CmsMenu } from '../../models/cms/cms-menu';

@Injectable({ providedIn: 'root' })
export class CmsMenuService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Cms/Menu`;
  }

  getTree(menuTypePublicId = '', tenantId: string | null = null): Observable<CmsMenu[]> {
    const payload = { menuType: menuTypePublicId, status: 0, keyword: '', pageIndex: 0, pageSize: 0, tenantId };
    return this.http.post<any>(`${this.baseUrl}/GetTree`, payload).pipe(
      map(res => {
        const data: CmsMenu[] = res?.data ?? res;
        return Array.isArray(data) ? this.markDepth(data, 0) : [];
      }),
      catchError(() => of([]))
    );
  }

  private markDepth(nodes: CmsMenu[], depth: number): CmsMenu[] {
    return nodes.map(node => ({
      ...node,
      depth,
      expanded: depth === 0,
      children: node.children ? this.markDepth(node.children, depth + 1) : []
    }));
  }

  flattenForSelect(nodes: CmsMenu[], depth = 0): { id: number; label: string }[] {
    const result: { id: number; label: string }[] = [];
    for (const node of nodes) {
      const prefix = '——'.repeat(depth);
      result.push({ id: node.id, label: prefix + (node.name || '') });
      if (node.children?.length) {
        result.push(...this.flattenForSelect(node.children, depth + 1));
      }
    }
    return result;
  }

  getById(publicId: string): Observable<CmsMenu> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(res => res.data || res));
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

  getMenuTypes(): Observable<{ id: number; publicId: string; name: string; code: string }[]> {
    const payload = { draw: 1, start: 0, length: 200, search: { value: '' }, order: [] };
    return this.http.post<any>(`${environment.baseApiUrl}/api/Cms/MenuType/Search`, payload).pipe(
      map(res => {
        const data = res?.data ?? [];
        return Array.isArray(data) ? data.map((item: any) => ({
          id: item.id,
          publicId: item.publicId || '',
          name: item.name || item.Name || '',
          code: item.code || item.Code || ''
        })) : [];
      }),
      catchError(() => of([]))
    );
  }

  getPages(keyword = ''): Observable<{ id: string; name: string }[]> {
    const payload = { keyword, pageIndex: 1, pageSize: 200 };
    return this.http.post<any>(`${environment.baseApiUrl}/api/Cms/Pages/SearchAll`, payload).pipe(
      map(res => {
        const raw = res?.data ?? res;
        const arr = Array.isArray(raw) ? raw
          : Array.isArray(raw?.items) ? raw.items
          : [];
        return arr.map((item: any) => ({
          id: String(item.publicId || item.id),
          name: item.name || item.Name || item.title || item.Title || ''
        }));
      }),
      catchError(() => of([]))
    );
  }
}
