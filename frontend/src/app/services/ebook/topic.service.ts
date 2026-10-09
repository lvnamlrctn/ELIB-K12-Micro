import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { EbookTopic } from '../../models/ebook/ebook-topic';
export type { EbookTopic, EbookTopicFlatNode } from '../../models/ebook/ebook-topic';

@Injectable({ providedIn: 'root' })
export class EbookTopicService {
  private http = inject(HttpClient);
  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook/EBookTopic`;
  }

  getTree(language = '', keyword = '', tenantId: string | null = null): Observable<EbookTopic[]> {
    const payload = { keyword, language, status: 0, pageIndex: 0, pageSize: 0, parentId: 0, level: 0, tenantId };
    return this.http.post<any>(`${this.baseUrl}/GetTree`, payload).pipe(
      map(res => {
        const data: EbookTopic[] = res?.data ?? res;
        return Array.isArray(data) ? this.markDepth(data, 0) : [];
      }),
      catchError(() => of([]))
    );
  }

  private markDepth(nodes: EbookTopic[], depth: number): EbookTopic[] {
    return nodes.map(node => ({
      ...node,
      depth,
      expanded: depth === 0,
      children: node.children ? this.markDepth(node.children, depth + 1) : []
    }));
  }

  flattenForSelect(nodes: EbookTopic[], depth = 0): { id: number; label: string }[] {
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

  getById(publicId: string): Observable<EbookTopic> {
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
}
