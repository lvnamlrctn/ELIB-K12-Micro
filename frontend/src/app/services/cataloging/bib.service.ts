import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Bib, MarcField } from '../../models/cataloging/bib';
import { RegisteredBarcode } from '../../models/cataloging/receipt';
import { BookMetadataResult } from '../ebook/ebook-document.service';

export interface BibSearchParams {
  keyword?:     string | null;
  title?:       string | null;
  author?:      string | null;
  publisher?:   string | null;
  publishYear?: string | null;
  mfnFrom?:     number | null;
  mfnTo?:       number | null;
  bibTypeId?:   number | null;
  tenantId?:    string | null;
  pageIndex?:   number;
  pageSize?:    number;
}
export interface BibSearchResult { data: Bib[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class BibService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/Book`; }

  search(params: BibSearchParams): Observable<BibSearchResult> {
    const payload = {
      keyword: params.keyword || '', title: params.title || '', author: params.author || '',
      publisher: params.publisher || '', publishYear: params.publishYear || '',
      mfnFrom: params.mfnFrom ?? null, mfnTo: params.mfnTo ?? null,
      bibTypeId: params.bibTypeId ?? null, tenantId: params.tenantId ?? null, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: Bib[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  /** Lấy thông tin biểu ghi + các trường MARC theo MFN. */
  getByMfn(mfn: number): Observable<Bib | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/GetByMfn/${mfn}`).pipe(
      map(r => r?.data ?? r ?? null), catchError(() => of(null))
    );
  }

  /** Lưu biểu ghi MARC (thêm/sửa). Trả về MFN. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  // Đợt 17 — lý do sửa tuỳ chọn, gửi riêng qua header X-Change-Reason (mã hoá URI) cho Lịch sử thay đổi
  // theo hồ sơ, không đụng payload hiện có.
  save(bib: Partial<Bib>, reason?: string): Observable<any> {
    let headers: HttpHeaders | undefined;
    if (reason && reason.trim()) headers = new HttpHeaders({ 'X-Change-Reason': encodeURIComponent(reason.trim()) });
    return this.http.post<any>(`${this.baseUrl}/Save`, bib, { headers }).pipe(map(r => r.data ?? r), catchError(() => of(null)));
  }

  /** Kiểm tra ISBN đã tồn tại trong kho chưa — dùng để cảnh báo trùng khi thêm biểu ghi mới. */
  checkIsbn(isbn: string): Observable<{ bibId: number; mfn: number | null; title: string | null }[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/CheckIsbn`, { params: { isbn } }).pipe(
      map(r => r?.data ?? []), catchError(() => of([]))
    );
  }

  /** Tra cứu ảnh bìa theo ISBN (Google Books, dự phòng OpenLibrary) — dùng khi thêm nhan đề mới. */
  fetchCoverByIsbn(isbn: string): Observable<string | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/FetchCoverByIsbn`, { params: { isbn } }).pipe(
      map(r => r?.data ?? null), catchError(() => of(null))
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  deleteByMfn(mfn: number): Observable<any> { return this.http.delete<any>(`${this.baseUrl}/Delete/${mfn}`).pipe(map(r => r.data ?? r)); }

  /** Tạo lại chỉ mục tìm kiếm cho toàn bộ biểu ghi in. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  rebuildIndex(): Observable<any> { return this.http.post<any>(`${this.baseUrl}/RebuildIndex`, {}).pipe(map(r => r?.data ?? r), catchError(() => of(null))); }

  /** Tạo lại chỉ mục GỘP (in + số) — upsert, không xóa dữ liệu cũ. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  rebuildUnifiedIndex(): Observable<any> {
    return this.http.post<any>(`${environment.baseApiUrl}/api/admin/job/rebuild-unified-index`, {}).pipe(map(r => r?.data ?? r), catchError(() => of(null)));
  }

  /** Xóa toàn bộ index gộp rồi build lại từ đầu — chỉ dùng khi index bị hỏng/không đồng bộ. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  rebuildUnifiedIndexFromScratch(): Observable<any> {
    return this.http.post<any>(`${environment.baseApiUrl}/api/admin/job/rebuild-unified-index-from-scratch`, {}).pipe(map(r => r?.data ?? r), catchError(() => of(null)));
  }

  /** Gửi 1..n ảnh bìa cho AI phân tích, trả metadata sách để tự điền MARC — tái dùng thẳng endpoint
   * chung đã có cho tài liệu số (endpoint này không phụ thuộc domain Ebook/PrintBook). */
  analyzeBookImage(files: File[]): Observable<BookMetadataResult | null> {
    const form = new FormData();
    files.forEach(f => form.append('images', f));
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${environment.baseApiUrl}/api/Ebook/EbookItem/AnalyzeBookImage`, form).pipe(
      map(res => (res?.data ?? null) as BookMetadataResult | null), catchError(() => of(null))
    );
  }

  /** Phân tích file MARC nhị phân (.mrc/.marc) → danh sách bản ghi, mỗi bản ghi là 1 mảng MarcField. */
  parseMarcFile(file: File): Observable<MarcField[][]> {
    const form = new FormData();
    form.append('file', file);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${environment.baseApiUrl}/api/PrintBook/MarcConvert/MarcFileToFields`, form).pipe(
      map(r => (r?.data ?? []) as MarcField[][]),
      catchError(() => of([]))
    );
  }

  /** Trạng thái các bản (item/holdings) của biểu ghi. */
  getItems(bibId: number): Observable<RegisteredBarcode[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/Items/${bibId}`).pipe(
      map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])),
      catchError(() => of([]))
    );
  }

  /** Đăng ký số ĐKCB theo lô trực tiếp cho 1 biểu ghi (không qua phiếu nhập). */
  registerBarcodes(payload: { bibId: number; prefix?: string; digitLength: number; quantity: number; storeId: number; startNumber?: number }): Observable<RegisteredBarcode[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/RegisterBarcodes`, payload).pipe(map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])));
  }
  /** Đăng ký 1 số ĐKCB nhập tay trực tiếp cho 1 biểu ghi. */
  registerSingleBarcode(payload: { bibId: number; barcodeValue: string; storeId: number }): Observable<RegisteredBarcode | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/RegisterSingleBarcode`, payload).pipe(map(r => r?.data ?? null), catchError(() => of(null)));
  }
  /** Xóa 1 số ĐKCB đã đăng ký trực tiếp cho biểu ghi. */
  deleteBarcode(barcodeId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/DeleteBarcode/${barcodeId}`);
  }

  /** Di chuyển hàng loạt nhiều biểu ghi sang bộ sưu tập khác. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  bulkMoveCollection(bibIds: number[], collectionId: number): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/BulkMoveCollection`, { bibIds, collectionId }).pipe(map(r => r.data ?? r));
  }
}
