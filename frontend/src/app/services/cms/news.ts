import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { resolveMediaUrl } from '../../shared/utils/media-url';
import { NewsArticle, NewsSearchParams } from '../../models/cms/news';
export type { NewsArticle, NewsSearchParams } from '../../models/cms/news';

@Injectable({ providedIn: 'root' })
export class NewsService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Cms/News`;
  }

  search(params: NewsSearchParams): Observable<{ items: NewsArticle[]; total: number }> {
    return this.http.post<any>(`${this.baseUrl}/Search`, params).pipe(
      map(res => {
        let items: NewsArticle[] = [];
        let total = 0;
        if (Array.isArray(res)) { items = res; total = res.length; }
        else if (res?.data?.items) { items = res.data.items; total = res.data.totalCount ?? items.length; }
        else if (res?.items) { items = res.items; total = res.total ?? res.totalCount ?? items.length; }
        else if (res?.data && Array.isArray(res.data)) { items = res.data; total = items.length; }
        return { items, total };
      }),
      catchError(() => of({ items: [], total: 0 }))
    );
  }

  getById(id: string | number): Observable<NewsArticle> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(
      map(res => res?.data ?? res)
    );
  }

  create(item: Partial<NewsArticle>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res?.data ?? res));
  }

  update(id: string | number, item: Partial<NewsArticle>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, item).pipe(map(res => res?.data ?? res));
  }

  delete(id: string | number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res?.data ?? res));
  }

  changeStatus(publicId: string, status: number): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/ChangeStatus`, { publicId, status }).pipe(
      map(res => res?.data ?? res)
    );
  }

  uploadImage(file: File): Observable<string> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<any>(`${this.baseUrl}/UploadImage`, formData).pipe(
      map(res => resolveMediaUrl(res?.data?.url ?? res?.data?.path ?? res?.url ?? res?.path ?? '')),
      catchError(() => of(''))
    );
  }
}
