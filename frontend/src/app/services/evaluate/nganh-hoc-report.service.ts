import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface DocumentReportRow {
  title: string;
  loaiTaiLieu: number;
  hasLink: boolean;
  hasDigital: boolean;
  printCopyCount: number;
}

export interface CourseReportRow {
  maMon: string;
  tenMon: string;
  documents: DocumentReportRow[];
}

export interface ReportBucket {
  total: number;
  available: number;
}

export interface NganhHocReportData {
  stats: {
    main: ReportBucket;
    reference: ReportBucket;
    overall: ReportBucket;
  };
  courses: CourseReportRow[];
}

@Injectable({ providedIn: 'root' })
export class NganhHocReportService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Evaluate/NganhHocReport`;
  }

  getSummary(majorId: number): Observable<NganhHocReportData | null> {
    return this.http.post<any>(`${this.baseUrl}/Summary`, { majorId }).pipe(
      map(res => res?.data ?? null),
      catchError(() => of(null))
    );
  }

  exportByCourse(majorId: number): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/ExportByCourse`, { majorId }, { responseType: 'blob' });
  }

  exportAvailable(majorId: number): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/ExportAvailable`, { majorId }, { responseType: 'blob' });
  }

  exportNotAvailable(majorId: number): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/ExportNotAvailable`, { majorId }, { responseType: 'blob' });
  }

  exportCourseList(majorId: number): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/ExportCourseList`, { majorId }, { responseType: 'blob' });
  }
}
