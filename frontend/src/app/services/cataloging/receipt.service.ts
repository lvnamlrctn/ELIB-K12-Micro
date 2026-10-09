import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AbReceipt, AbReceiptLine, BarcodeRegisterRequest, RegisteredBarcode, SingleBarcodeRequest } from '../../models/cataloging/receipt';

export interface AbReceiptSearchResult { data: AbReceipt[]; recordsTotal: number; }

export interface AbReceiptListSearchParams {
  keyword?:         string | null;
  codeFrom?:        number | null;
  codeTo?:          number | null;
  receiptName?:     string | null;
  createdBy?:       number | null;
  supplierId?:      number | null;
  status?:          number | null;
  receiptDateFrom?: string | null;
  receiptDateTo?:   string | null;
  createdDateFrom?: string | null;
  createdDateTo?:   string | null;
  sourceId?:        number | null;
  fundId?:          number | null;
  tenantId?:        string | null;
  pageIndex?:       number;
  pageSize?:        number;
}

@Injectable({ providedIn: 'root' })
export class AbReceiptService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/Receipt`; }

  search(params: AbReceiptListSearchParams): Observable<AbReceiptSearchResult> {
    const payload = {
      keyword:         params.keyword         || '',
      codeFrom:        params.codeFrom        ?? null,
      codeTo:          params.codeTo          ?? null,
      receiptName:     params.receiptName     || null,
      createdBy:       params.createdBy       ?? null,
      supplierId:      params.supplierId      ?? null,
      status:          params.status          ?? null,
      receiptDateFrom: params.receiptDateFrom || null,
      receiptDateTo:   params.receiptDateTo   || null,
      createdDateFrom: params.createdDateFrom || null,
      createdDateTo:   params.createdDateTo   || null,
      sourceId:        params.sourceId        ?? null,
      fundId:          params.fundId          ?? null,
      tenantId:        params.tenantId        ?? null,
      pageIndex:       params.pageIndex ?? 1,
      pageSize:        params.pageSize ?? 10,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => ({ data: res?.data?.items ?? [], recordsTotal: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  /** Lấy đơn nhận theo publicId (dùng cho trang chi tiết). */
  getByPublicId(publicId: string): Observable<AbReceipt | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(r => r?.data ?? null), catchError(() => of(null)));
  }

  /** Lấy đơn nhận theo id số (dùng để bổ sung đơn nhận hiện tại vào dropdown nếu nó không còn đủ điều kiện). */
  getById(id: number): Observable<AbReceipt | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/${id}`).pipe(map(r => r?.data ?? null), catchError(() => of(null)));
  }

  /** Đơn nhận đã hoàn tất (Status=2) và còn sách chưa phân bổ — dùng cho dropdown "Đơn nhận" ở Đơn phân bổ. */
  searchAvailableForDeliverer(): Observable<AbReceipt[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/SearchAvailableForDeliverer`).pipe(
      map(res => res?.data?.items ?? []),
      catchError(() => of([]))
    );
  }

  /** Tạo mới đơn nhận. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Partial<AbReceipt>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  /** Cập nhật đơn nhận đã có. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<AbReceipt>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<any> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }

  getLines(receiptId: number): Observable<AbReceiptLine[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/Lines/${receiptId}`).pipe(map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])), catchError(() => of([])));
  }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  saveLine(line: Record<string, unknown>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/SaveDetail`, line).pipe(map(r => r.data ?? r)); }
  /** Xóa 1 dòng phiếu nhập. Backend từ chối (400) nếu dòng còn số KCB — FE hiển thị lỗi backend trả về. */
  deleteLine(lineId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/DeleteLine/${lineId}`);
  }
  /** Lấy các số KCB đã đăng ký cho 1 dòng phiếu nhập (route đúng: BarcodesByLine, không phải Barcodes). */
  getBarcodes(receiptLineId: number): Observable<RegisteredBarcode[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/BarcodesByLine/${receiptLineId}`).pipe(map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])), catchError(() => of([])));
  }
  /** Đăng ký số KCB theo lô (tiền tố + độ dài số + số lượng + kho) cho 1 dòng phiếu nhập. */
  registerBarcodes(payload: BarcodeRegisterRequest): Observable<RegisteredBarcode[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/RegisterBarcodes`, payload).pipe(map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])));
  }
  /** Đăng ký 1 số KCB nhập tay (tab "Riêng lẻ"). */
  registerSingleBarcode(payload: SingleBarcodeRequest): Observable<RegisteredBarcode | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/RegisterSingleBarcode`, payload).pipe(map(r => r?.data ?? null), catchError(() => of(null)));
  }
  /** Xóa 1 số KCB đã đăng ký. */
  deleteBarcode(barcodeId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/DeleteBarcode/${barcodeId}`);
  }

  /** Đơn đã "Hoàn tất" (Status=2) thì bị khoá — chỉ role đặc quyền mới canEdit=true. */
  canEdit(publicId: string): Observable<{ locked: boolean; canEdit: boolean }> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/CanEdit/${publicId}`).pipe(
      map(r => ({ locked: !!r?.data?.locked, canEdit: r?.data?.canEdit !== false })),
      catchError(() => of({ locked: false, canEdit: true }))
    );
  }
}
