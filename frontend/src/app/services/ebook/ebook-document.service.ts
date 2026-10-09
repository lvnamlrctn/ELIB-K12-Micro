import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { EbookDocument, EbookDocumentAuthor } from '../../models/ebook/ebook-document';

export interface ImportParams {
  collectionId?:  number | null;
  subjectId?:     number | null;
  topicId?:       number | null;
  status?:        number | null;
  isFree?:        number | null;
  allowDownload?: number | null;
}

export interface ImportResult {
  imported: number;
  failed:   number;
  errors:   string[];
  message?: string;
}

export interface DocSearchResult {
  data: EbookDocument[];
  recordsTotal: number;
}

/** Kết quả AI phân tích ảnh bìa (POST /AnalyzeBookImage). Trường không đọc được = null. */
export interface BookMetadataResult {
  title:       string | null;
  author:      string | null;
  publisher:   string | null;
  publishYear: string | null;
  isbn:        string | null;
  language:    string | null;
  description: string | null;
}

export interface DocSearchParams {
  keyword?:         string;
  collectionId?:    number | null;
  subjectId?:       number | null;
  typeId?:          number | null;
  topicId?:         number | null;
  title?:           string | null;
  author?:          string | null;
  publisher?:       string | null;
  publishDateFrom?: string | null;
  publishDateTo?:   string | null;
  submitedFrom?:    string | null;
  submitedTo?:      string | null;
  status?:          number | null;
  tenantId?:        string | null;
  pageIndex?:       number;
  pageSize?:        number;
}

@Injectable({ providedIn: 'root' })
export class EbookDocumentService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook/EbookItem`;
  }

  /** Gửi 1..n ảnh bìa cho AI phân tích, trả metadata sách để tự điền form. */
  analyzeBookImage(files: File[]): Observable<BookMetadataResult | null> {
    const form = new FormData();
    files.forEach(f => form.append('images', f));
    return this.http.post<any>(`${this.baseUrl}/AnalyzeBookImage`, form).pipe(
      map(res => (res?.data ?? null) as BookMetadataResult | null)
    );
  }

  search(params: DocSearchParams): Observable<DocSearchResult> {
    const payload = {
      keyword:         params.keyword         || '',
      collectionId:    params.collectionId    || null,
      subjectId:       params.subjectId       || null,
      typeId:          params.typeId          || null,
      topicId:         params.topicId         || null,
      title:           params.title           || null,
      author:          params.author          || null,
      publisher:       params.publisher       || null,
      publishDateFrom: params.publishDateFrom || null,
      publishDateTo:   params.publishDateTo   || null,
      submitedFrom:    params.submitedFrom     || null,
      submitedTo:      params.submitedTo       || null,
      status:          params.status          ?? null,
      tenantId:        params.tenantId         ?? null,
      pageIndex:       params.pageIndex       ?? 1,
      pageSize:        params.pageSize        ?? 10,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: EbookDocument[] = [];
        let recordsTotal = 0;
        const pick = (...keys: string[]) => (obj: any) =>
          keys.reduce<number | undefined>((v, k) => v ?? obj?.[k], undefined);

        if (Array.isArray(res)) {
          data = res;
          recordsTotal = res.length;
        } else if (res?.data?.items && Array.isArray(res.data.items)) {
          data = res.data.items;
          recordsTotal = pick('totalCount','recordsTotal','recordsFiltered','total','totalRecords')(res.data) ?? data.length;
        } else if (res?.items && Array.isArray(res.items)) {
          data = res.items;
          recordsTotal = pick('totalCount','recordsTotal','recordsFiltered','total','totalRecords')(res) ?? data.length;
        } else if (res?.data && Array.isArray(res.data)) {
          data = res.data;
          recordsTotal = pick('totalCount','recordsTotal','recordsFiltered','total','totalRecords')(res) ?? data.length;
        }
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getById(id: string | number): Observable<EbookDocument> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(
      map(res => {
        // Response: { success, data: { item: {..., itemXml: {...}}, metaData: [...] } }
        const item = res?.data?.item ?? res?.data ?? res;
        const xml  = item?.itemXml  ?? {};

        const split = (s: string | null | undefined): string[] =>
          s ? s.split(';').map((x: string) => x.trim()).filter(Boolean) : [];

        const authors: EbookDocumentAuthor[] = split(xml.author).map((name: string) => {
          const spaceIdx = name.indexOf(' ');
          return spaceIdx === -1
            ? { lastName: name, firstName: '' }
            : { lastName: name.slice(0, spaceIdx), firstName: name.slice(spaceIdx + 1) };
        });

        const metaData: { id?: number; metaDataFieldId: number; value: string; sortOrder?: number | null }[] =
          (res?.data?.metaData ?? []).map((m: any) => ({
            id:              m.id ?? 0,
            metaDataFieldId: m.metaDataFieldId,
            value:           m.value ?? '',
            sortOrder:       m.sortOrder ?? null,
          }));

        return {
          id:           item.id,
          publicId:     item.publicId,
          collectionId: item.collectionId  ?? null,
          subjectId:    item.subjectId     ?? null,
          topicId:      item.topicId       ?? null,
          typeId:       item.typeId        ?? null,
          status:       item.status        ?? null,
          images:       item.images        ?? null,
          totalFile:    item.totalFile     ?? null,
          printCopies:  item.printCopies   ?? null,
          offlineDays:  item.offlineDays   ?? null,
          itemXml:      xml,

          // From itemXml
          title:        xml.title       ?? null,
          publisher:    xml.publisher   ?? null,
          publishYear:  xml.publishDate ? (parseInt(xml.publishDate, 10) || null) : null,
          publishMonth: item.publishMonth ?? null,
          publishDay:   item.publishDay  ?? null,
          pages:        xml.page        ? (parseInt(xml.page, 10)        || null) : null,
          keywords:     split(xml.keyword),
          otherTitles:  split(xml.otherTitle),
          authors:      authors,

          // Direct item fields — API uses numeric: 2 = true, 1/null = false
          isPublished:   item.status        === 2,
          allowDownload: item.allowDownload === 2,
          isFree:        item.free          === 2,
          shareType:     item.shareType     ?? null,
          docTypeId:     item.docTypeId     ?? item.typeId   ?? null,
          languageId:    item.languageId    ?? null,
          coverImage:    item.coverImage    ?? item.images   ?? null,
          abstract:      item.abstract      ?? null,
          description:   item.description   ?? null,
          journalName:   item.journalName   ?? null,
          volumes:       item.volumes       ?? [],
          reportPages:   item.reportPages   ?? [],
          advisors:      item.advisors      ?? [],
          metaData,
        } as EbookDocument;
      }),
      catchError(() => of({} as EbookDocument))
    );
  }

  create(item: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data || res));
  }

  // Đợt 17 — lý do sửa tuỳ chọn, gửi riêng qua header X-Change-Reason (mã hoá URI) cho Lịch sử thay đổi
  // theo hồ sơ, không đụng payload/DTO hiện có.
  update(id: string | number, item: any, reason?: string): Observable<any> {
    let headers: HttpHeaders | undefined;
    if (reason && reason.trim()) headers = new HttpHeaders({ 'X-Change-Reason': encodeURIComponent(reason.trim()) });
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, item, { headers }).pipe(map(res => res.data || res));
  }

  delete(id: string | number): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data || res));
  }

  toggleStatus(id: string | number): Observable<any> {
    return this.http.patch<any>(`${this.baseUrl}/ChangeStatus/${id}`, {}).pipe(map(res => res.data || res));
  }

  /** Di chuyển hàng loạt nhiều tài liệu sang bộ sưu tập khác. */
  bulkMoveCollection(publicIds: string[], collectionId: number): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/BulkMoveCollection`, { publicIds, collectionId }).pipe(map(r => r.data ?? r));
  }

  /** Render 1 trang PDF của tài liệu thành ảnh PNG để xem trước khi chọn làm ảnh bìa. */
  getCoverPreview(publicId: string, page: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${publicId}/CoverPreview/${page}`, { responseType: 'blob' });
  }

  /** Chốt dùng trang PDF này làm ảnh bìa: server render, upload MinIO, xóa ảnh cũ, đồng bộ Elasticsearch. */
  setCoverFromPage(publicId: string, page: number): Observable<{ images: string }> {
    return this.http.post<any>(`${this.baseUrl}/${publicId}/SetCoverFromPage`, { page }).pipe(
      map(res => res?.data ?? res)
    );
  }

  getExportFields(): Observable<any[]> {
    return this.http.get<any>(`${this.baseUrl}/GetExportFields`).pipe(
      map(res => Array.isArray(res?.data) ? res.data : Array.isArray(res) ? res : []),
      catchError(() => of([]))
    );
  }

  exportExcel(params: DocSearchParams & { fields?: string[] }): Observable<Blob> {
    const payload = { ...params, pageIndex: 1, pageSize: 99999 };
    return this.http.post(`${this.baseUrl}/Export`, payload, { responseType: 'blob' });
  }

  exportMetadataFile(params: DocSearchParams): Observable<Blob> {
    const payload = { ...params, pageIndex: 1, pageSize: 99999 };
    return this.http.post(`${this.baseUrl}/ExportMetaDataFile`, payload, { responseType: 'blob' });
  }

  exportMetadata(params: DocSearchParams): Observable<Blob> {
    const payload = { ...params, pageIndex: 1, pageSize: 99999 };
    return this.http.post(`${this.baseUrl}/ExportMetaData`, payload, { responseType: 'blob' });
  }

  importExcel(file: File, params: ImportParams = {}): Observable<ImportResult> {
    const fd = this.buildFormData(params);
    fd.append('file', file);
    return this.http.post<any>(`${this.baseUrl}/ImportExcel`, fd).pipe(
      map(res => this.toImportResult(res)),
      catchError(() => of({ imported: 0, failed: 1, errors: ['Lỗi kết nối'] }))
    );
  }

  importXml(files: File[], params: ImportParams = {}): Observable<ImportResult> {
    const fd = this.buildFormData(params);
    for (const f of files) fd.append('file', f);
    return this.http.post<any>(`${this.baseUrl}/ImportXml`, fd).pipe(
      map(res => this.toImportResult(res)),
      catchError(() => of({ imported: 0, failed: 1, errors: ['Lỗi kết nối'] }))
    );
  }

  importDSpace(file: File, params: ImportParams = {}): Observable<ImportResult> {
    const fd = this.buildFormData(params);
    fd.append('file', file);
    return this.http.post<any>(`${this.baseUrl}/ImportDSpace`, fd).pipe(
      map(res => this.toImportResult(res)),
      catchError(() => of({ imported: 0, failed: 1, errors: ['Lỗi kết nối'] }))
    );
  }

  private buildFormData(params: ImportParams): FormData {
    const fd = new FormData();
    if (params.collectionId  != null) fd.append('collectionId',  String(params.collectionId));
    if (params.subjectId     != null) fd.append('subjectId',     String(params.subjectId));
    if (params.topicId       != null) fd.append('topicId',       String(params.topicId));
    if (params.status        != null) fd.append('status',        String(params.status));
    if (params.isFree        != null) fd.append('isFree',        String(params.isFree));
    if (params.allowDownload != null) fd.append('allowDownload', String(params.allowDownload));
    return fd;
  }

  private toImportResult(res: any): ImportResult {
    const d = res?.data ?? res;
    return {
      imported: d?.imported ?? d?.success ?? 0,
      failed:   d?.failed   ?? d?.error   ?? 0,
      errors:   Array.isArray(d?.errors)  ? d.errors : [],
      message:  res?.message ?? d?.message ?? '',
    };
  }

}
