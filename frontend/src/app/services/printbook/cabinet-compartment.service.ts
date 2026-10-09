import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { CabinetCompartment, CabinetCompartmentGrid } from '../../models/printbook/cabinet-compartment';

@Injectable({ providedIn: 'root' })
export class CabinetCompartmentService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/PrintBook/CabinetCompartment`;
  }

  getByCabinet(cabinetPublicId: string): Observable<CabinetCompartmentGrid> {
    return this.http.get<any>(`${this.baseUrl}/ByCabinet/${cabinetPublicId}`).pipe(
      map(res => res.data ?? res),
      catchError(() => of({ cabinetId: 0, rows: 0, cols: 0, items: [] }))
    );
  }

  create(item: Partial<CabinetCompartment> & { cabinetId: number }): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  update(publicId: string, item: Partial<CabinetCompartment>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }
}
