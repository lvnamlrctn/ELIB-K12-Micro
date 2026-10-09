import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Z3950ResultItem } from '../../models/cataloging/z3950-result';
import { MarcField } from '../../models/cataloging/bib';

export interface Z3950SearchResult { data: Z3950ResultItem[]; recordsTotal: number; errors: { configId: number; configName: string; error: string }[]; }

// ELIB: /api/PrintBook/Opac/Z3950 — nguồn form Z3950SearchImport (BuilQuery + Connection.Search + Import vào Bib).
// Việc kết nối tới server Z39.50 (YAZ/ZOOM) chạy ở BACKEND; UI chỉ gửi điều kiện và nhận MARC trả về.
@Injectable({ providedIn: 'root' })
export class Z3950SearchService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Opac/Z3950`; }

  // Tra cứu brief trên (các) server Z3950. configIds rỗng/null ⇒ tìm trên toàn bộ server cấu hình.
  search(params: {
    title?: string | null; author?: string | null; isbn?: string | null; issn?: string | null; publisher?: string | null; keyword?: string | null;
    configIds?: number[] | null; groupId?: number | null; pageIndex?: number; pageSize?: number;
  }): Observable<Z3950SearchResult> {
    const payload = {
      title: params.title || '', author: params.author || '', isbn: params.isbn || '', issn: params.issn || '', publisher: params.publisher || '', keyword: params.keyword || '',
      configIds: params.configIds && params.configIds.length ? params.configIds : null, groupId: params.groupId ?? null, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: Z3950ResultItem[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        const errors = res?.data?.errors ?? res?.errors ?? [];
        return { data, recordsTotal, errors };
      }),
      catchError(err => {
        // eslint-disable-next-line no-console
        console.error('Z3950 search HTTP error', err);
        return of({ data: [], recordsTotal: 0, errors: [{ configId: 0, configName: '', error: err?.message || 'Lỗi kết nối máy chủ' }] });
      })
    );
  }

  // Xem bản MARC đầy đủ của 1 kết quả
  getMarc(resultId: string, configId?: number | null): Observable<string> {
    const payload = { resultId, configId: configId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Marc`, payload).pipe(
      map(r => r?.data?.marc ?? r?.marc ?? r?.data ?? ''),
      catchError(() => of(''))
    );
  }

  // Lấy MARC đầy đủ (dạng cấu trúc MarcField[]) của 1 kết quả để hiển thị/sửa trên app-marc-fields-editor
  getMarcFields(resultId: string, configId?: number | null): Observable<MarcField[]> {
    const payload = { resultId, configId: configId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Marc`, payload).pipe(
      map(r => (r?.data?.fields ?? r?.fields ?? []) as MarcField[]),
      catchError(() => of([]))
    );
  }

  // Nhập 1 kết quả vào CSDL biên mục → trả về { mfn, bibId } của biểu ghi mới
  import(payload: { resultId: string; configId?: number | null; bibTypeId: number; worksheetId?: number | null }): Observable<{ mfn: number | null; bibId: number | null }> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Import`, payload).pipe(
      map(r => { const d = r?.data ?? r ?? {}; return { mfn: d.mfn ?? d.Mfn ?? null, bibId: d.bibId ?? d.BibId ?? null }; })
    );
  }
}
