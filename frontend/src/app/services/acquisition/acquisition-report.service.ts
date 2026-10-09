import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export type AcquisitionReportType = 'ACQUISITION_LIST' | 'STORE_ALLOCATION' | 'ACCESSION_REGISTER' | 'NEW_BOOK_CATALOG';

const REPORT_ACTIONS: Record<AcquisitionReportType, string> = {
  ACQUISITION_LIST:   'AcquisitionList',
  STORE_ALLOCATION:   'StoreAllocation',
  ACCESSION_REGISTER: 'AccessionRegister',
  NEW_BOOK_CATALOG:   'NewBookCatalog',
};

// Một dòng báo cáo bổ sung — các trường tuỳ loại báo cáo, xem docs/BACKEND-PRINT-DOCUMENT-MGMT.md
export interface AcquisitionReportRow {
  [key: string]: unknown;
  title?:       string;
  author?:      string;
  isbn?:        string;
  storeName?:   string;
  barcode?:     string;
  amount?:      number;
  receiptCode?: string;
}

export interface AcquisitionReportParams {
  fromDate?:    string | null;
  toDate?:      string | null;
  storeId?:     number | null;
  supplierId?:  number | null;
  pageIndex?:   number;
  pageSize?:    number;
  receiptCodeFrom?: number | null;
  receiptCodeTo?:   number | null;
  receiptName?:     string | null;
  title?:           string | null;
  mfnFrom?:         number | null;
  mfnTo?:           number | null;
  status?:          number | null;
  author?:          string | null;
  sourceId?:        number | null;
  publishYear?:     string | null;
  createdDateFrom?: string | null;
  createdDateTo?:   string | null;
  fundId?:          number | null;
  publisher?:       string | null;
  createdBy?:       number | null;
}

export interface AcquisitionPrintItem {
  stt:     number;
  title?:  string;
  author?: string;
  price:   number;
  amount:  number;
  total:   number;
}

export interface AcquisitionPrintResult {
  parentLibrary: string;
  libraryName:   string;
  deptName:      string;
  receiptCode?:  number | null;
  receiptDate?:  string | null;
  items:         AcquisitionPrintItem[];
  totalCount:    number;
  totalMoney:    number;
}

export interface LibraryHeader {
  parentLibrary: string;
  libraryName:   string;
  deptName:      string;
}

export interface StoreAllocationPrintItem {
  stt:        number;
  title?:     string;
  author?:    string;
  publisher?: string;
  mlCutter?:  string;
  sl:         number;
  barcodes:   string[];
}

export interface StoreAllocationGroup {
  storeId?:   number | null;
  storeName?: string;
  items:      StoreAllocationPrintItem[];
}

export interface StoreAllocationPrintResult {
  parentLibrary: string;
  deptName:      string;
  receiptCode?:  number | null;
  receiptDate?:  string | null;
  groups:        StoreAllocationGroup[];
  totalCount:    number;
}

export interface AccessionRegisterPrintItem {
  date?:        string;
  barcode?:     string;
  bibId?:       number | null;
  title?:       string;
  author?:      string;
  publisher?:   string;
  publishDate?: string;
  price?:       number;
  mlDigit?:     string;
}

export interface AccessionRegisterPrintResult {
  year:       number;
  items:      AccessionRegisterPrintItem[];
  totalCount: number;
}

export interface ClassLabelAccessionItem {
  barcode?:     string;
  classSymbol?: string;
  authorMark?:  string;
}

export interface ClassLabelAccessionPrintResult {
  items:      ClassLabelAccessionItem[];
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class AcquisitionReportService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Acquisition/Report`; }

  report(type: AcquisitionReportType, params: AcquisitionReportParams): Observable<{ data: AcquisitionReportRow[]; recordsTotal: number }> {
    const payload = {
      fromDate: params.fromDate || null, toDate: params.toDate || null, storeId: params.storeId ?? null,
      supplierId: params.supplierId ?? null, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 20,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/${REPORT_ACTIONS[type]}`, payload).pipe(
      map(res => {
        let data: AcquisitionReportRow[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  export(type: AcquisitionReportType, params: AcquisitionReportParams): Observable<Blob> {
    const payload = { fromDate: params.fromDate || null, toDate: params.toDate || null, storeId: params.storeId ?? null, supplierId: params.supplierId ?? null };
    return this.http.post(`${this.baseUrl}/${REPORT_ACTIONS[type]}Export`, payload, { responseType: 'blob' }).pipe(catchError(() => of(new Blob())));
  }

  getLibraryHeader(): Observable<LibraryHeader> {
    const empty: LibraryHeader = { parentLibrary: '', libraryName: '', deptName: '' };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/LibraryHeader`).pipe(
      map(res => res?.data ?? empty),
      catchError(() => of(empty))
    );
  }

  private fullReportPayload(params: AcquisitionReportParams) {
    return {
      fromDate: params.fromDate || null, toDate: params.toDate || null,
      storeId: params.storeId ?? null, supplierId: params.supplierId ?? null,
      receiptCodeFrom: params.receiptCodeFrom ?? null, receiptCodeTo: params.receiptCodeTo ?? null,
      receiptName: params.receiptName || null, title: params.title || null,
      mfnFrom: params.mfnFrom ?? null, mfnTo: params.mfnTo ?? null,
      status: params.status ?? null, author: params.author || null,
      sourceId: params.sourceId ?? null, publishYear: params.publishYear || null,
      createdDateFrom: params.createdDateFrom || null, createdDateTo: params.createdDateTo || null,
      fundId: params.fundId ?? null, publisher: params.publisher || null,
      createdBy: params.createdBy ?? null,
    };
  }

  printAcquisitionList(params: AcquisitionReportParams): Observable<AcquisitionPrintResult> {
    const empty: AcquisitionPrintResult = { parentLibrary: '', libraryName: '', deptName: '', items: [], totalCount: 0, totalMoney: 0 };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/AcquisitionListPrint`, this.fullReportPayload(params)).pipe(
      map(res => res?.data ?? empty),
      catchError(() => of(empty))
    );
  }

  printStoreAllocation(params: AcquisitionReportParams): Observable<StoreAllocationPrintResult> {
    const empty: StoreAllocationPrintResult = { parentLibrary: '', deptName: '', groups: [], totalCount: 0 };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/StoreAllocationPrint`, this.fullReportPayload(params)).pipe(
      map(res => res?.data ?? empty),
      catchError(() => of(empty))
    );
  }

  printAccessionRegister(params: AcquisitionReportParams): Observable<AccessionRegisterPrintResult> {
    const empty: AccessionRegisterPrintResult = { year: new Date().getFullYear(), items: [], totalCount: 0 };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/AccessionRegisterPrint`, this.fullReportPayload(params)).pipe(
      map(res => res?.data ?? empty),
      catchError(() => of(empty))
    );
  }

  printClassLabelAccession(params: AcquisitionReportParams): Observable<ClassLabelAccessionPrintResult> {
    const empty: ClassLabelAccessionPrintResult = { items: [], totalCount: 0 };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/ClassLabelAccessionPrint`, this.fullReportPayload(params)).pipe(
      map(res => res?.data ?? empty),
      catchError(() => of(empty))
    );
  }
}
