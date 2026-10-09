import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Reader } from '../../models/reader/reader';
import { AdminTaskView } from '../../models/system/admin-task';
import { ReaderImportMapping } from '../../models/reader/reader-import-mapping';

/** Tuỳ chọn nhập bổ sung (Đợt 20): ghép cột Excel + tự tạo Lớp/Khóa học/Đơn vị chưa có. */
export interface ReaderImportOptions { mapping?: ReaderImportMapping; autoCreateRefs?: boolean; }

export interface ReaderSearchResult {
  data: Reader[];
  recordsTotal: number;
}

export interface ReaderSearchParams {
  keyword?:      string | null;
  lastName?:     string | null;
  firstName?:    string | null;
  cardno?:       string | null;
  classId?:      number | null;
  courseId?:     number | null;
  readerTypeId?: number | null;
  status?:       number | null;
  issuedFrom?:   string | null;
  issuedTo?:     string | null;
  expiredFrom?:  string | null;
  expiredTo?:    string | null;
  tenantId?:     string | null;
  pageIndex?:    number;
  pageSize?:     number;
  fields?:       string[];
}

@Injectable({ providedIn: 'root' })
export class ReaderService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Circulation/Reader`;
  }

  search(params: ReaderSearchParams): Observable<ReaderSearchResult> {
    const payload = {
      keyword:      params.keyword      || '',
      lastName:     params.lastName     || null,
      firstName:    params.firstName    || null,
      cardno:       params.cardno       || null,
      classId:      params.classId      || null,
      courseId:     params.courseId     || null,
      readerTypeId: params.readerTypeId || null,
      status:       params.status       ?? null,
      issuedFrom:   params.issuedFrom   || null,
      issuedTo:     params.issuedTo     || null,
      expiredFrom:  params.expiredFrom  || null,
      expiredTo:    params.expiredTo    || null,
      tenantId:     params.tenantId     ?? null,
      pageIndex:    params.pageIndex    ?? 1,
      pageSize:     params.pageSize     ?? 10,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: Reader[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        else if (res?.data?.items) data = res.data.items;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getById(id: string | number): Observable<Reader> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(map(res => res.data || res));
  }

  create(item: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data || res));
  }

  // Đợt 16 — lý do sửa tuỳ chọn, gửi riêng qua header X-Change-Reason (mã hoá URI) để không đụng payload/
  // DTO hiện có; backend đọc header này cho Lịch sử thay đổi theo hồ sơ, không bắt buộc.
  update(id: string | number, item: any, reason?: string): Observable<any> {
    let headers: HttpHeaders | undefined;
    if (reason && reason.trim()) headers = new HttpHeaders({ 'X-Change-Reason': encodeURIComponent(reason.trim()) });
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, item, { headers }).pipe(map(res => res.data || res));
  }

  delete(id: string | number): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data || res));
  }

  changeStatus(publicId: string | number, status: number): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/ChangeStatus`, { publicId, status }).pipe(map(res => res.data || res));
  }

  exportExcel(params: ReaderSearchParams): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/Export`, params, { responseType: 'blob' });
  }

  getExportFields(): Observable<any[]> {
    return this.http.get<any>(`${this.baseUrl}/GetExportFields`).pipe(
      map(res => { const d = res?.data ?? res; return Array.isArray(d) ? d : []; }),
      catchError(() => of([]))
    );
  }

  importExcel(
    file: File,
    overwrite = false,
    readerTypeId?: number | null,
    classByCode = false,
    courseByCode = false,
    orgByCode = false,
    options: ReaderImportOptions = {}
  ): Observable<{ imported: number; failed: number; errors: string[]; message: string; createdRefs: string[] }> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('overwrite', String(overwrite));
    if (readerTypeId != null) formData.append('readerTypeId', String(readerTypeId));
    formData.append('classByCode', String(classByCode));
    formData.append('courseByCode', String(courseByCode));
    formData.append('orgByCode', String(orgByCode));
    this.appendImportOptions(formData, options);
    return this.http.post<any>(`${this.baseUrl}/Import`, formData).pipe(
      map(res => {
        const d = res?.data ?? res;
        return {
          imported: d?.successCount ?? d?.imported ?? 0,
          failed:   d?.failedCount  ?? d?.failed   ?? 0,
          errors:   Array.isArray(d?.errors) ? d.errors : [],
          message:  res?.message ?? d?.message ?? '',
          createdRefs: Array.isArray(d?.createdRefs) ? d.createdRefs : [],
        };
      }),
      // Lỗi 400 (ánh xạ cột sai, file hỏng...) → hiện đúng thông báo backend thay vì "Lỗi kết nối" chung chung.
      catchError(err => of({ imported: 0, failed: 1, errors: [err?.error?.message || 'Lỗi kết nối'], message: '', createdRefs: [] }))
    );
  }

  // Đợt 10 — xử lý nền (xem trước → xác nhận), không chặn request HTTP, trần 10.000 dòng (so với 1.000
  // dòng đường đồng bộ importExcel ở trên). Trả về tác vụ vừa tạo (Preview=true) thay vì kết quả nhập.
  importExcelBackground(
    file: File,
    overwrite = false,
    readerTypeId?: number | null,
    classByCode = false,
    courseByCode = false,
    orgByCode = false,
    options: ReaderImportOptions = {}
  ): Observable<AdminTaskView | null> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('overwrite', String(overwrite));
    if (readerTypeId != null) formData.append('readerTypeId', String(readerTypeId));
    formData.append('classByCode', String(classByCode));
    formData.append('courseByCode', String(courseByCode));
    formData.append('orgByCode', String(orgByCode));
    formData.append('background', 'true');
    this.appendImportOptions(formData, options);
    return this.http.post<any>(`${this.baseUrl}/Import`, formData).pipe(
      map(res => (res?.data ?? res) as AdminTaskView),
      catchError(() => of(null))
    );
  }

  private appendImportOptions(formData: FormData, options: ReaderImportOptions): void {
    formData.append('autoCreateRefs', String(!!options.autoCreateRefs));
    if (options.mapping) formData.append('mapping', JSON.stringify(options.mapping));
  }

  // Upload ảnh hàng loạt theo ZIP: tên file (bỏ phần mở rộng) = Số thẻ. Cập nhật ảnh đại diện
  // chính (Photo) của từng bạn đọc khớp mã thẻ — khác với reader-photo.service.ts (ảnh bổ sung).
  uploadPhotosZip(file: File): Observable<{
    matched: number; notFound: number; failed: number;
    results: { fileName: string; cardNo: string; matched: boolean; error?: string }[];
    message: string;
  }> {
    const formData = new FormData();
    formData.append('zipFile', file);
    return this.http.post<any>(`${this.baseUrl}/UploadPhotosZip`, formData).pipe(
      map(res => {
        const d = res?.data ?? res;
        return {
          matched:  d?.matched  ?? 0,
          notFound: d?.notFound ?? 0,
          failed:   d?.failed   ?? 0,
          results:  Array.isArray(d?.results) ? d.results : [],
          message:  res?.message ?? d?.message ?? '',
        };
      }),
      catchError(() => of({ matched: 0, notFound: 0, failed: 1, results: [], message: 'Lỗi kết nối' }))
    );
  }

  uploadPhoto(id: string | number, file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<any>(`${this.baseUrl}/UploadAvatar/${id}`, formData).pipe(map(res => res.data || res));
  }

  uploadIfBase64(id: string | number, photo: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/UploadIfBase64Async`, { publicId: id, photo }).pipe(
      map(res => res.data || res)
    );
  }

  lockCard(id: string | number, reason: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Lock/${id}`, { reason }).pipe(map(res => res.data || res));
  }

  // Đặt lại mật khẩu bạn đọc (quyền READERS/edit). Trả nguyên response để đọc message backend.
  resetPassword(id: string | number, password: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/ResetPassword/${id}`, { password });
  }

  checkCardNoExists(cardno: string, excludePublicId?: string | number | null): Observable<boolean> {
    let url = `${this.baseUrl}/CheckExist?cardno=${encodeURIComponent(cardno)}`;
    if (excludePublicId != null) url += `&excludePublicId=${excludePublicId}`;
    return this.http.get<any>(url).pipe(
      map(res => res?.data === true),
      catchError(() => of(false))
    );
  }

  batchUpdate(searchParams: ReaderSearchParams, action: string, value: number | string | null): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/BatchUpdate`, { ...searchParams, action, value })
      .pipe(map(res => res.data || res));
  }

  bulkResetPassword(publicIds: (string | number)[], password: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/BulkResetPassword`, { publicIds, password })
      .pipe(map(res => res.data || res));
  }
}
