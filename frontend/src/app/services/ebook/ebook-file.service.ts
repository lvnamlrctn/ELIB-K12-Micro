import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { EbookFile } from '../../models/ebook/ebook-file';

export interface FileSearchResult {
  data:       EbookFile[];
  totalCount: number;
}

export interface FileSearchParams {
  ebookPublicId: string;
}

export interface FileToken {
  token:     string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class EbookFileService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook/EbookFile`;
  }

  search(params: FileSearchParams): Observable<FileSearchResult> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, {
      ebookPublicId: params.ebookPublicId,
    }).pipe(
      map(res => {
        const items: EbookFile[] = res?.data ?? [];
        return { data: items, totalCount: items.length };
      }),
      catchError(() => of({ data: [], totalCount: 0 }))
    );
  }

  getToken(publicId: string): Observable<FileToken> {
    return this.http.post<any>(`${this.baseUrl}/GetToken`, { publicId }).pipe(
      map(res => res?.data as FileToken)
    );
  }

  getViewUrl(token: string): string {
    return `${environment.baseApiUrl}/api/Ebook/EbookFile/View/${token}`;
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(
      map(res => res?.data || res)
    );
  }

  upload(payload: FormData): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Upload`, payload).pipe(
      map(res => res?.data || res)
    );
  }
}
