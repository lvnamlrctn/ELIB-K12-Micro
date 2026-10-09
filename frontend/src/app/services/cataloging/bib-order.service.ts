import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Bib } from '../../models/cataloging/bib';

/**
 * Biểu ghi MARC "nháp" của Đơn đặt bổ sung — lưu vào BibOrder/BibXmlOrder/BibDataOrder/
 * fixed_field_value_order (không phải Bib/BibXml/BibData thật), nên không có Bộ sưu tập/Liên kết tài
 * liệu số (BibOrder không có CollectionId/EbookId). Xem CatalogueBookOrderController.
 */
@Injectable({ providedIn: 'root' })
export class BibOrderService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/BookOrder`; }

  /** Lấy thông tin biểu ghi nháp + các trường MARC theo Mfn nháp. */
  getByMfn(mfn: number): Observable<Bib | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/GetByMfn/${mfn}`).pipe(
      map(r => r?.data ?? r ?? null), catchError(() => of(null))
    );
  }

  /** Lưu biểu ghi MARC nháp (thêm/sửa). Trả về { mfn, bibId, ... }. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  save(bib: Partial<Bib>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Save`, bib).pipe(map(r => r.data ?? r), catchError(() => of(null))); }

  /** Chuyển 1 biểu ghi nháp (BibOrder) thành biểu ghi Bib thật — dùng khi thêm dòng đơn đặt vào Đơn nhận. */
  promoteToBib(bibOrderId: number): Observable<{ bibId: number; mfn: number } | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/PromoteToBib/${bibOrderId}`, {}).pipe(
      map(r => r?.data ?? null), catchError(() => of(null))
    );
  }
}
