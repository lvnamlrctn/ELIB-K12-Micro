import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Deliverer, DelivererBarcodeSearchPage, DelivererBarcodeSearchParams, DelivererLine } from '../../models/cataloging/deliverer';

export interface DelivererSearchResult { data: Deliverer[]; recordsTotal: number; }

export interface DelivererSearchParams {
  keyword?:           string | null;
  codeFrom?:          number | null;
  codeTo?:            number | null;
  delivererName?:     string | null;
  receiptName?:       string | null;
  status?:            number | null;
  createdBy?:         number | null;
  delivererDateFrom?: string | null;
  delivererDateTo?:   string | null;
  sign?:              number | null;
  tenantId?:          string | null;
  pageIndex?:         number;
  pageSize?:          number;
}

@Injectable({ providedIn: 'root' })
export class DelivererService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/Deliverer`; }

  search(params: DelivererSearchParams): Observable<DelivererSearchResult> {
    const payload = {
      keyword:           params.keyword           || '',
      codeFrom:          params.codeFrom           ?? null,
      codeTo:            params.codeTo             ?? null,
      delivererName:     params.delivererName      || null,
      receiptName:       params.receiptName        || null,
      status:            params.status             ?? null,
      createdBy:         params.createdBy          ?? null,
      delivererDateFrom: params.delivererDateFrom  || null,
      delivererDateTo:   params.delivererDateTo    || null,
      sign:              params.sign               ?? null,
      tenantId:          params.tenantId           ?? null,
      pageIndex:         params.pageIndex ?? 1,
      pageSize:          params.pageSize ?? 10,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => ({ data: res?.data?.items ?? [], recordsTotal: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  /** Lấy đơn phân bổ theo publicId (dùng cho trang chi tiết). */
  getByPublicId(publicId: string): Observable<Deliverer | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(r => r?.data ?? null), catchError(() => of(null)));
  }

  /** Tạo mới đơn phân bổ. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Partial<Deliverer>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  /** Cập nhật đơn phân bổ đã có. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<Deliverer>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<any> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }

  /** Danh sách sách trong đơn phân bổ. */
  getLines(delivererId: number): Observable<DelivererLine[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/Lines/${delivererId}`).pipe(map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])), catchError(() => of([])));
  }

  /** Liệt kê sách thuộc đúng đơn nhận của đơn phân bổ, chưa được phân bổ cho đơn nào — dùng cho khung "Thêm sách". */
  searchBarcode(params: DelivererBarcodeSearchParams): Observable<DelivererBarcodeSearchPage> {
    const payload = {
      receiptId:   params.receiptId,
      searchBy:    params.searchBy ?? 'mfn',
      keyword:     params.keyword || '',
      barcode:     params.barcode || '',
      title:       params.title || '',
      author:      params.author || '',
      publisher:   params.publisher || '',
      publishDate: params.publishDate || '',
      mfnFrom:     params.mfnFrom ?? null,
      mfnTo:       params.mfnTo ?? null,
      pageIndex:   params.pageIndex ?? 1,
      pageSize:    params.pageSize ?? 20,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchBarcode`, payload).pipe(
      map(res => ({ items: res?.data?.items ?? [], totalCount: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ items: [], totalCount: 0 }))
    );
  }

  /** Thêm 1 dòng sách (theo BarcodeId) vào đơn phân bổ. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  addLine(payload: { deliverer_Id: number; barcodeId: number; store_Id?: number | null }): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/AddLine`, payload).pipe(map(r => r.data ?? r));
  }

  /** Thêm nhiều sách vào đơn 1 lần (Đợt 20) — backend bỏ qua sách đã có trong đơn / không thuộc đơn vị của đơn. */
  addLines(delivererId: number, barcodeIds: number[], storeId?: number | null): Observable<{ added: number; skipped: number }> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/AddLines`, { delivererId, barcodeIds, store_Id: storeId ?? null }).pipe(map(r => r.data ?? r));
  }

  /** Xóa 1 dòng sách khỏi đơn phân bổ (không ảnh hưởng Bib/Barcode gốc). */
  deleteLine(lineId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/DeleteLine/${lineId}`);
  }

  /** Ký nhận đơn phân bổ (chuyển Sign 1 → 2). */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  signConfirm(publicId: string): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.baseUrl}/SignConfirm/${publicId}`, {}).pipe(map(r => r.data ?? r));
  }

  /** Đơn đã ký nhận (Sign=2) thì bị khoá — chỉ role đặc quyền mới canEdit=true. */
  canEdit(publicId: string): Observable<{ locked: boolean; canEdit: boolean }> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/CanEdit/${publicId}`).pipe(
      map(r => ({ locked: !!r?.data?.locked, canEdit: r?.data?.canEdit !== false })),
      catchError(() => of({ locked: false, canEdit: true }))
    );
  }
}
