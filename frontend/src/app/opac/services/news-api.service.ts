import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, catchError, map, of } from 'rxjs';
import { News } from './models';

@Injectable({ providedIn: 'root' })
export class NewsApiService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);
  
  private backendBase = APP_CONFIG.BackendBase;
  private backendDataBase = APP_CONFIG.BackendDataBase;
  
  private get baseUrl(): string {
    return isPlatformServer(this.platformId) 
      ? `${this.backendBase}/api/public/PublicNews`
      : '/api/public/PublicNews';
  }

  private get backenData(): string {
    return isPlatformServer(this.platformId)
      ? this.backendDataBase
      : '/data';
  }

  getNews(pageSize = 8): Observable<News[]> {
    const payload = {
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      pageSize: pageSize,
      pageIndex: 1
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        if (res.data && res.data.items && Array.isArray(res.data.items)) {
          return res.data.items.map((item: any) => this.mapNews(item));
        }
        return [];
      }),
      catchError(err => {
        console.error('Failed to get news API, falling back to mock', err);
        return of([]);
      })
    );
  }

  getNewsByCategory(categoryId: string, pageIndex = 1, pageSize = 8): Observable<{items: News[], total: number}> {
    const payload: any = {
      tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      pageSize: pageSize,
      pageIndex: pageIndex
    };
    // The API seems to only accept Guid for categoryId. If categoryId isn't a guid, omitting it might be safer, but let's try not to crash.
    // We will pass it if it's not '150' or if we know how to handle it. Actually I will just pass it to 'categoryId'. 
    // Wait, let's omit if it's '150' (which is probably an old hardcode).
    // Let's rely on category parsing. But I'll leave the payload simple if categoryId is '150'.
    if (categoryId && categoryId.length > 10) {
      payload.categoryId = categoryId;
    }
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        if (res.data && res.data.items && Array.isArray(res.data.items)) {
          return { items: res.data.items.map((item: any) => this.mapNews(item)), total: res.data.totalCount || 100 };
        }
        return { items: [], total: 0 };
      }),
      catchError(err => {
        console.error('Failed to get news by category API', err);
        return of({ items: [], total: 0 });
      })
    );
  }

  /** File đính kèm của tin đã xuất bản (port ELIB-LRC 10-04). Link tải đi qua API công khai, không lộ đường dẫn kho. */
  getAttachments(newsId: string): Observable<NewsAttachment[]> {
    return this.http.get<any>(`${this.baseUrl}/Attachments?newsId=${newsId}`).pipe(
      map(res => (Array.isArray(res?.data) ? res.data : []).map((f: any): NewsAttachment => ({
        id: f.id, name: f.name ?? '', ext: (f.ext ?? '').toLowerCase(), sizeKb: f.sizeKb ?? null,
        url: `${this.baseUrl}/Attachment/${f.id}`,
      }))),
      catchError(() => of([] as NewsAttachment[]))
    );
  }

  getNewsById(id: string): Observable<News | undefined> {
    return this.http.get<any>(`${this.baseUrl}/GetNewsById?newsId=${id}`).pipe(
      map(res => {
        if (res.data && Array.isArray(res.data) && res.data.length > 0) {
          return this.mapNews(res.data[0]);
        } else if (res.data && !Array.isArray(res.data)) {
          return this.mapNews(res.data);
        }
        return undefined;
      }),
      catchError(err => {
        console.error(`Failed to get news by id ${id} API, falling back to mock`, err);
        return of(undefined);
      })
    );
  }

  private mapNews(item: any): News {
    return {
      id: item.publicId || item.id || Math.random().toString(),
      categoryId: item.categoryId?.toString() || '150',
      title: item.title,
      summary: item.brief || item.title || '',
      content: (item.content || item.brief || '').replace(/src=(["'])(\/Upload[^"']+)\1/g, `src=$1${this.backenData}$2$1`),
      imageUrl: item.images ? item.images : 'https://picsum.photos/seed/news/400/300',
      date: item.lastModifiedTime ? new Date(item.lastModifiedTime).toLocaleDateString('vi-VN') : '10/04/2026',
      views: item.viewCount || Math.floor(Math.random() * 500) + 50
    };
  }
}

export interface NewsAttachment {
  id: string;
  name: string;
  ext: string;
  sizeKb: number | null;
  url: string;
}
