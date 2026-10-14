import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Lưu thông tự phục vụ của bạn đọc (service circulation) — /api/opac/circulation/**, chỉ bạn đọc đã đăng nhập. */
export interface LoanDto {
  id: number; publicId: string; readerPublicId: string; cardNo: string; readerName: string | null; itemPublicId: string; barcode: string; mfn: number;
  title: string | null; author: string | null; circPlaceId: number | null; storeName: string | null; loanedAt: string; dueAt: string;
  returnedAt: string | null; renewCount: number; note: string | null; version: number;
}

/** status: 1 chờ sách, 2 đang giữ sách, 3 đã mượn, 4 huỷ, 5 hết hạn (xem HoldStatus ở backend). */
export interface HoldDto {
  id: number; publicId: string; readerPublicId: string; cardNo: string; readerName: string | null; mfn: number; title: string | null;
  author: string | null; circPlaceId: number | null; status: number; requestedAt: string; itemPublicId: string | null; barcode: string | null;
  readyAt: string | null; expiresAt: string | null; closedAt: string | null; note: string | null; queuePosition: number | null;
}

export const HoldStatus = { Waiting: 1, Ready: 2, Fulfilled: 3, Cancelled: 4, Expired: 5 } as const;

export interface ReaderPanel {
  readerPublicId: string; cardNo: string; fullName: string; readerTypeName: string | null; className: string | null; courseName: string | null;
  photoId: string | null; expireDate: string | null; isLocked: boolean; isExpired: boolean; hasOverdue: boolean; canBorrow: boolean;
  blockReason: string | null; loanDays: number; maxLoans: number | null; maxRenewals: number | null; currentLoans: LoanDto[]; unpaidFines: number;
  holds: HoldDto[] | null;
}

export interface CrudPage<T> { items: T[]; totalCount: number; pageIndex: number; pageSize: number; totalPages: number; }

export interface LoanHistoryRequest { state?: string | null; from?: string | null; to?: string | null; pageIndex?: number; pageSize?: number; }

@Injectable({ providedIn: 'root' })
export class CirculationApi {
  private readonly http = inject(HttpClient);

  /** Sách đang mượn + hạn trả + chính sách gia hạn của chính bạn đọc đang đăng nhập. */
  myLoans() { return firstValueFrom(this.http.get<ReaderPanel>('/api/opac/circulation/loans')); }
  renew(loanPublicId: string) { return firstValueFrom(this.http.post<LoanDto>(`/api/opac/circulation/loans/${loanPublicId}/Renew`, {})); }
  history(request: LoanHistoryRequest) { return firstValueFrom(this.http.post<CrudPage<LoanDto>>('/api/opac/circulation/loans/History', request)); }

  /** Đặt mượn còn hiệu lực (chờ sách hoặc đang giữ) của chính bạn đọc. */
  myHolds() { return firstValueFrom(this.http.get<HoldDto[]>('/api/opac/circulation/holds')); }
  placeHold(mfn: number) { return firstValueFrom(this.http.post<HoldDto>('/api/opac/circulation/holds', { mfn })); }
  cancelHold(holdPublicId: string) { return firstValueFrom(this.http.post<HoldDto>(`/api/opac/circulation/holds/${holdPublicId}/Cancel`, {})); }
}
