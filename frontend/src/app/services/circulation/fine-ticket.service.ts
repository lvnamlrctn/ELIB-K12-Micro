import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { FineTicket, FineTicketListRow, FineTicketTotals } from '../../models/circulation/fine-ticket';

export interface SaveFineTicketLineParams {
  /** <= 0 = dòng phạt mới thêm tay. */
  id:          number;
  fineTypeId?: string | null;
  value?:      number | null;
  /** Chỉ cho dòng mới: ĐKCB (không bắt buộc). */
  barcode?:    string | null;
}

export interface SaveFineTicketParams {
  fineDate?:       string | null;
  status?:         number | null;
  discountAmount?: number | null;
  paidAmount?:     number | null;
  totalAmount?:    number | null;
  owesDocument?:   number | null;
  fineTypeId?:     number | null;
  fineMethodId?:   number | null;
  note?:           string | null;
  circPlaceId?:    number | null;
  lines?:          SaveFineTicketLineParams[];
  /** Dòng phạt cần xoá (phiếu chưa hoàn thành). */
  deletedLineIds?: number[];
}

export interface CreateFineTicketParams {
  readerId?:       number | null;
  fineDate?:       string | null;
  status?:         number | null;
  fineTypeId?:     number | null;
  fineMethodId?:   number | null;
  totalAmount?:    number | null;
  discountAmount?: number | null;
  paidAmount?:     number | null;
  owesDocument?:   number | null;
  note?:           string | null;
}

export interface FineTicketSearchParams {
  code?:         string | null;
  cardNo?:       string | null;
  readerName?:   string | null;
  statusFilter?: number | null;
  debtStatus?:   number | null;
  fineMethodId?: number | null;
  createdRowBy?: number | null;
  fineDateFrom?: string | null;
  fineDateTo?:   string | null;
  tenantId?:     string | null;
  pageIndex?:    number;
  pageSize?:     number;
}

export interface FineTicketSearchResult {
  data:         FineTicketListRow[];
  recordsTotal: number;
}

@Injectable({ providedIn: 'root' })
export class FineTicketService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Circulation/FineTicket`; }

  /** Gom tài liệu quá hạn + các phiếu mượn được tích chọn (`loanIds`, vd mất tài liệu) vào phiếu phạt đang mở. */
  buildForReader(readerId: number, circPlaceId?: number | null, loanIds: number[] = []): Observable<FineTicket | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/BuildForReader`, { readerId, circPlaceId: circPlaceId ?? null, loanIds }).pipe(
      map(r => (r?.data ?? null) as FineTicket | null),
      catchError(() => of(null))
    );
  }

  getByPublicId(publicId: string): Observable<FineTicket | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/Detail/${publicId}`).pipe(
      map(r => (r?.data ?? null) as FineTicket | null),
      catchError(() => of(null))
    );
  }

  save(publicId: string, params: SaveFineTicketParams): Observable<FineTicket | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.baseUrl}/Save/${publicId}`, params).pipe(
      map(r => (r?.data ?? null) as FineTicket | null),
      catchError(() => of(null))
    );
  }

  /** Tạo phiếu phạt thủ công (không sinh dòng tài liệu) — dùng cho nút "Thêm mới". */
  create(params: CreateFineTicketParams): Observable<FineTicket | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Create`, params).pipe(
      map(r => (r?.data ?? null) as FineTicket | null),
      catchError(() => of(null))
    );
  }

  search(params: FineTicketSearchParams): Observable<FineTicketSearchResult> {
    const payload = {
      code:         params.code         || null,
      cardNo:       params.cardNo       || null,
      readerName:   params.readerName   || null,
      statusFilter: params.statusFilter ?? null,
      debtStatus:   params.debtStatus   ?? null,
      fineMethodId: params.fineMethodId ?? null,
      createdRowBy: params.createdRowBy ?? null,
      fineDateFrom: params.fineDateFrom || null,
      fineDateTo:   params.fineDateTo   || null,
      tenantId:     params.tenantId     ?? null,
      pageIndex:    params.pageIndex    ?? 1,
      pageSize:     params.pageSize     ?? 10,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        const items = res?.data?.items ?? [];
        const total = res?.data?.totalCount ?? items.length;
        return { data: items as FineTicketListRow[], recordsTotal: total };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getTotals(params: Omit<FineTicketSearchParams, 'pageIndex' | 'pageSize'>): Observable<FineTicketTotals> {
    const payload = {
      code:         params.code         || null,
      cardNo:       params.cardNo       || null,
      readerName:   params.readerName   || null,
      statusFilter: params.statusFilter ?? null,
      debtStatus:   params.debtStatus   ?? null,
      fineMethodId: params.fineMethodId ?? null,
      createdRowBy: params.createdRowBy ?? null,
      fineDateFrom: params.fineDateFrom || null,
      fineDateTo:   params.fineDateTo   || null,
      tenantId:     params.tenantId     ?? null,
      pageIndex: 1, pageSize: 1,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Totals`, payload).pipe(
      map(r => (r?.data ?? { totalReceivable: 0, totalReceived: 0, remaining: 0 }) as FineTicketTotals),
      catchError(() => of({ totalReceivable: 0, totalReceived: 0, remaining: 0 }))
    );
  }
}
