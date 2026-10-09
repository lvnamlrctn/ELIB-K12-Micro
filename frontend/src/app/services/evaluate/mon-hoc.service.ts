import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';
import { resolveMediaUrl } from '../../shared/utils/media-url';

export interface MonHoc {
  id?: string;
  publicId?: string;
  maMon?: string;
  tenMon?: string;
  soTinChi?: number | null;
  degreeId?: number | null;
  knowledgeId?: number | null;
  optionId?: number | null;
  nguoiBienSoan?: string;
  active?: number | null;
  attachment?: string;
  note?: string;
  portalId?: string;
  language?: string;
  tenantName?: string;
}

@Injectable({ providedIn: 'root' })
export class MonHocService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Evaluate/MonHoc`;
  }

  getAll(params: DataTableParams): Observable<DataTableResponse<MonHoc>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      portalId: '',
      language: '',
      tenantId: params['tenantId'] ?? null,
      pageIndex,
      pageSize: params.length || 10
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: MonHoc[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { draw: params.draw, data, recordsTotal: total, recordsFiltered: total };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }

  getById(id: string): Observable<MonHoc> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(map(res => res.data ?? res));
  }

  private toPayload(item: Partial<MonHoc>) {
    return {
      maMon: item.maMon,
      tenMon: item.tenMon,
      soTinChi: item.soTinChi ?? null,
      degreeId: item.degreeId ?? null,
      knowledgeId: item.knowledgeId ?? null,
      optionId: item.optionId ?? null,
      nguoiBienSoan: item.nguoiBienSoan,
      active: item.active ?? 2,
      attachment: item.attachment,
      note: item.note
    };
  }

  create(item: Partial<MonHoc>): Observable<MonHoc> {
    return this.http.post<any>(`${this.baseUrl}/Add`, this.toPayload(item)).pipe(map(res => res.data ?? res));
  }

  update(id: string, item: Partial<MonHoc>): Observable<MonHoc> {
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, this.toPayload(item)).pipe(map(res => res.data ?? res));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data ?? res));
  }

  searchAll(): Observable<MonHoc[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, {}).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res?.data?.items) return res.data.items;
        if (res?.data && Array.isArray(res.data)) return res.data;
        return [];
      }),
      catchError(() => of([]))
    );
  }

  /** Upload 1 file đính kèm, trả về URL đầy đủ để ghi vào field `attachment`. */
  uploadAttachment(file: File): Observable<string> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<any>(`${this.baseUrl}/UploadAttachment`, formData).pipe(
      map(res => resolveMediaUrl(res?.data?.path ?? res?.data?.url ?? '')),
      catchError(() => of(''))
    );
  }
}
