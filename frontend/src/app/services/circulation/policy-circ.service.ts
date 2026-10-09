import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { PolicyCirc, PolicyCircTreeItem } from '../../models/circulation/policy-circ';

@Injectable({ providedIn: 'root' })
export class PolicyCircService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Circulation/CircPolicy`; }

  byCircPlace(circPlacePublicId: string): Observable<PolicyCircTreeItem[]> {
    return this.http.get<any>(`${this.baseUrl}/ByCircPlace/${circPlacePublicId}`).pipe(
      map(res => (Array.isArray(res?.data) ? res.data : []) as PolicyCircTreeItem[]),
      catchError(() => of([]))
    );
  }

  getById(publicId: string): Observable<PolicyCirc> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(res => res.data ?? res));
  }

  create(item: Partial<PolicyCirc>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  update(publicId: string, item: Partial<PolicyCirc>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }
}
