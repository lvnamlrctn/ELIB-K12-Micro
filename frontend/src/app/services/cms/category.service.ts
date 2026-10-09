import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';
import { environment } from '../../../environments/environment';
import { Category } from '../../models/cms/category';
export type { Category, CategoryFlatNode } from '../../models/cms/category';

@Injectable({
  providedIn: 'root'
})
export class CategoryService {
  private http = inject(HttpClient);
  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Cms/Category`;
  }

  getAll(params: DataTableParams): Observable<DataTableResponse<Category>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      pageIndex: pageIndex,
      pageSize: params.length || 10,
      tenantId: params['tenantId'] ?? null
    };

    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: Category[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        else if (res?.data?.items && Array.isArray(res.data.items)) data = res.data.items;
        else if (res && Array.isArray(res.items)) data = res.items;

        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { draw: params.draw, data: data, recordsTotal: total, recordsFiltered: total };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }

  getAllCategories(tenantId: string | null = null): Observable<Category[]> {
    const payload = { keyword: '', pageIndex: 1, pageSize: 1000, tenantId };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: Category[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        else if (res?.data?.items && Array.isArray(res.data.items)) data = res.data.items;
        else if (res && Array.isArray(res.items)) data = res.items;
        return data;
      }),
      catchError(() => of([]))
    );
  }

  getById(publicId: string): Observable<Category> {
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

  getTree(language = '', keyword = '', tenantId: string | null = null): Observable<Category[]> {
    const payload = { keyword, language, status: 0, pageIndex: 0, pageSize: 0, parentId: 0, level: 0, tenantId };
    return this.http.post<any>(`${this.baseUrl}/GetTree`, payload).pipe(
      map(res => {
        const data: Category[] = res?.data ?? res;
        return Array.isArray(data) ? this.markDepth(data, 0) : [];
      }),
      catchError(() => of([]))
    );
  }

  private markDepth(nodes: Category[], depth: number): Category[] {
    return nodes.map(node => ({
      ...node,
      depth,
      expanded: depth === 0,
      children: node.children ? this.markDepth(node.children, depth + 1) : []
    }));
  }

  flattenForSelect(nodes: Category[], depth = 0): { id: number; label: string }[] {
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
