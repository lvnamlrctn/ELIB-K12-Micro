import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AttachFile, AttachFileSyncResult } from '../../models/cms/attach-file';

/** File đính kèm tin tức — api/Cms/AttachFile. */
@Injectable({ providedIn: 'root' })
export class AttachFileService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Cms/AttachFile`; }

  list(newsPublicId: string): Observable<AttachFile[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, { newsPublicId }).pipe(map(res => (res?.data ?? []) as AttachFile[]));
  }

  upload(newsPublicId: string, file: File, name?: string | null): Observable<AttachFile> {
    const form = new FormData();
    form.append('file', file);
    form.append('newsPublicId', newsPublicId);
    if (name?.trim()) form.append('name', name.trim());
    return this.http.post<any>(`${this.baseUrl}/Upload`, form).pipe(map(res => res?.data as AttachFile));
  }

  /** Nội dung file (có JWT) — trang tự mở tab mới hoặc lưu với tên file. */
  download(publicId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/Download/${publicId}`, { responseType: 'blob' });
  }

  rename(publicId: string, name: string): Observable<void> {
    return this.http.put<any>(`${this.baseUrl}/Rename/${publicId}`, { name }).pipe(map(() => undefined));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(() => undefined));
  }

  syncLegacy(): Observable<AttachFileSyncResult> {
    return this.http.post<any>(`${this.baseUrl}/SyncFiles`, {}).pipe(map(res => res?.data as AttachFileSyncResult));
  }
}
