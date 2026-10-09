import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { FaceMatchResult } from '../../models/circulation/face-match';

@Injectable({ providedIn: 'root' })
export class FaceRecognitionService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/FaceRecognition`; }

  identify(imageBase64: string): Observable<FaceMatchResult | null> {
    return this.http.post<any>(`${this.baseUrl}/Identify`, { imageBase64 }).pipe(
      map(res => (res?.data as FaceMatchResult) ?? null),
      catchError(() => of(null))
    );
  }
}
