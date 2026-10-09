import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { PolicyCircDocGroup } from '../../models/circulation/policy-circ-doc-group';

@Injectable({ providedIn: 'root' })
export class PolicyCircDocGroupService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Circulation/CircPolicy/DocGroup`; }

  searchByPolicy(policyCircId: number): Observable<PolicyCircDocGroup[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, { policyCircId }).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res?.data?.items) return res.data.items;
        if (res?.data && Array.isArray(res.data)) return res.data;
        return [];
      }),
      catchError(() => of([]))
    );
  }

  create(item: Partial<PolicyCircDocGroup>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }
}
