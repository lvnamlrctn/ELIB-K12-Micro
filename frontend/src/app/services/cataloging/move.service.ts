import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AbMove, AbMoveCompleteResult } from '../../models/cataloging/move';
import { AbMoveDetailLine, MoveDocumentCandidate, MoveDocumentSearchParams } from '../../models/cataloging/move-detail';

export interface AbMoveSearchResult { data: AbMove[]; recordsTotal: number; }
export interface MoveDocumentSearchResult { data: MoveDocumentCandidate[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class AbMoveService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/Move`; }

  search(params: { keyword?: string | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<AbMoveSearchResult> {
    const payload = { keyword: params.keyword || '', pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: AbMove[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getById(id: number): Observable<AbMove | null> { return this.http.get<any>(`${this.baseUrl}/${id}`).pipe(map(r => r?.data ?? r ?? null), catchError(() => of(null))); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  add(item: Partial<AbMove>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<AbMove>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<any> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }

  searchDocuments(params: MoveDocumentSearchParams): Observable<MoveDocumentSearchResult> {
    const payload = {
      moveId: params.moveId, mfnFrom: params.mfnFrom ?? null, mfnTo: params.mfnTo ?? null,
      title: params.title || null, author: params.author || null, publisher: params.publisher || null,
      publishYear: params.publishYear || null, barcodeFrom: params.barcodeFrom || null, barcodeTo: params.barcodeTo || null,
      pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchDocuments`, payload).pipe(
      map(res => ({ data: (res?.data?.items ?? []) as MoveDocumentCandidate[], recordsTotal: res?.data?.recordsTotal ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getLines(moveId: number): Observable<AbMoveDetailLine[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/Lines/${moveId}`).pipe(
      map(res => (res?.data ?? []) as AbMoveDetailLine[]),
      catchError(() => of([]))
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  addDetails(moveId: number, barcodeIds: number[]): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/AddDetails`, { moveId, barcodeIds }).pipe(map(r => r.data ?? r));
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  deleteLine(publicId: string): Observable<any> {
    return this.http.delete<any>(`${environment.baseApiUrl}/api/PrintBook/AbMoveDetail/Delete/${publicId}`).pipe(map(r => r.data ?? r));
  }

  // Hoàn thành điều chuyển: ĐKCB trong phiếu chuyển sang kho nhận, phiếu bị khoá.
  complete(moveId: number): Observable<AbMoveCompleteResult> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Complete/${moveId}`, {}).pipe(map(r => (r?.data ?? r) as AbMoveCompleteResult));
  }
}
