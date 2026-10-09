import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MediaFile } from '../../models/cms/media-file';

@Injectable({ providedIn: 'root' })
export class MediaLibraryService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Cms/Media`;
  }

  list(params: { keyword?: string; page: number; pageSize: number }): Observable<{ items: MediaFile[]; total: number }> {
    return this.http.get<any>(`${this.baseUrl}/List`, {
      params: {
        keyword: params.keyword ?? '',
        page: params.page,
        pageSize: params.pageSize
      }
    }).pipe(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      map((res: any) => {
        let items: MediaFile[] = [];
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
}
