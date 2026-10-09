import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface ReaderPhoto {
  id: number;
  photoUrl: string;
}

/** Ảnh khuôn mặt bổ sung của 1 bạn đọc (khác với Reader.photo — ảnh đại diện chính).
 *  Dùng cùng với ảnh chính khi nhận diện khuôn mặt để tăng độ ổn định. */
@Injectable({ providedIn: 'root' })
export class ReaderPhotoService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Circulation/Reader`;
  }

  getPhotos(publicId: string): Observable<ReaderPhoto[]> {
    return this.http.get<any>(`${this.baseUrl}/Photos/${publicId}`).pipe(
      map(res => {
        const d = res?.data ?? res;
        return Array.isArray(d) ? d : [];
      }),
      catchError(() => of([]))
    );
  }

  addPhoto(publicId: string, photoBase64: string): Observable<ReaderPhoto> {
    return this.http.post<any>(`${this.baseUrl}/Photos/${publicId}`, { photo: photoBase64 }).pipe(
      map(res => res.data || res)
    );
  }

  deletePhoto(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/Photos/${id}`).pipe(map(res => res.data || res));
  }
}
