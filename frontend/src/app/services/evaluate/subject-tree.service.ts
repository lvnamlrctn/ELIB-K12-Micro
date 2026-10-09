import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { SubjectTreeNode } from '../../models/evaluate/subject-tree';

@Injectable({ providedIn: 'root' })
export class SubjectTreeService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Evaluate/SubjectTree`; }

  getFullTree(tenantId?: string | null): Observable<SubjectTreeNode[]> {
    return this.http.post<any>(`${this.baseUrl}/GetFullTree`, { tenantId: tenantId ?? null }).pipe(
      map(res => (res?.data ?? res ?? []) as SubjectTreeNode[]),
      catchError(() => of([]))
    );
  }
}
