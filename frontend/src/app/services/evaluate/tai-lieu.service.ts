import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';

export interface TaiLieu {
  id?: string;
  publicId?: string;
  bibId?: number | null;
  title?: string;
  author?: string;
  publisher?: string;
  url?: string;
  publishDate?: string;
  loaiTaiLieu?: number | null;
  lanXuatBan?: string;
  note?: string;
  ebookId?: number | null;
  monHocId?: number | null;
}

@Injectable({ providedIn: 'root' })
export class TaiLieuService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Evaluate/TaiLieu`;
  }

  getAll(params: DataTableParams, monHocId: string | number): Observable<DataTableResponse<TaiLieu>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      monHocId,
      pageIndex,
      pageSize: params.length || 10
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: TaiLieu[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { draw: params.draw, data, recordsTotal: total, recordsFiltered: total };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }

  getById(id: string): Observable<TaiLieu> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(map(res => res.data ?? res));
  }

  private toPayload(item: Partial<TaiLieu>) {
    return {
      bibId: item.bibId ?? null,
      title: item.title,
      author: item.author,
      publisher: item.publisher,
      url: item.url,
      publishDate: item.publishDate,
      loaiTaiLieu: item.loaiTaiLieu ?? null,
      lanXuatBan: item.lanXuatBan,
      note: item.note,
      ebookId: item.ebookId ?? null,
      monHocId: item.monHocId ?? null
    };
  }

  create(item: Partial<TaiLieu>): Observable<TaiLieu> {
    return this.http.post<any>(`${this.baseUrl}/Add`, this.toPayload(item)).pipe(map(res => res.data ?? res));
  }

  update(id: string, item: Partial<TaiLieu>): Observable<TaiLieu> {
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, this.toPayload(item)).pipe(map(res => res.data ?? res));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data ?? res));
  }
}
