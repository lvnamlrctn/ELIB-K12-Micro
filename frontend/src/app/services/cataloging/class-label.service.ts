import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

// Một nhãn môn loại: ký hiệu phân loại + tác giả viết tắt + mã KCB — nguồn: Book/DicClass join qua bibId
export interface ClassLabelItem {
  id:               number;
  barcode?:         string;
  classSymbol?:     string;  // ký hiệu phân loại (DDC)
  authorMark?:      string;  // ký hiệu tác giả (Cutter)
  title?:           string;
}

export interface ClassLabelSearchParams {
  receiptCode?:  string | null;
  barcodeFrom?:  string | null;
  barcodeTo?:    string | null;
}

// Kết quả in nhãn theo danh sách Bib.PublicId — nguồn: Book(Bib/BibXml) + MARC 082 $a/$b
export interface ClassLabelByBibItem {
  id:           number;
  bibPublicId:  string;
  title?:       string;
  author?:      string;
  classSymbol?: string;  // DDC (082 $a)
  authorMark?:  string;  // Cutter (082 $b)
}

export interface ClassLabelByBibResult {
  parentLibrary: string;
  libraryName:   string;
  items:         ClassLabelByBibItem[];
}

@Injectable({ providedIn: 'root' })
export class ClassLabelService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/ClassLabel`; }

  search(params: ClassLabelSearchParams): Observable<ClassLabelItem[]> {
    const payload = { receiptCode: params.receiptCode || null, barcodeFrom: params.barcodeFrom || null, barcodeTo: params.barcodeTo || null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])),
      catchError(() => of([]))
    );
  }

  // In nhãn môn loại theo danh sách Bib.PublicId được chọn (vd từ trang Tìm theo đầu sách)
  searchByBib(publicIds: string[]): Observable<ClassLabelByBibResult> {
    const empty: ClassLabelByBibResult = { parentLibrary: '', libraryName: '', items: [] };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchByBib`, { bibPublicIds: publicIds }).pipe(
      map(res => res?.data ?? empty),
      catchError(() => of(empty))
    );
  }
}
