import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface DigitalItemSummary {
  publicId: string; title: string; author: string | null; publisher: string | null; publishDate: string | null; coverUrl: string | null;
  free: boolean; allowDownload: boolean; collectionId: number | null; collectionName: string | null;
}
export interface DigitalFileSummary { publicId: string; fileExt: string | null; description: string | null; }

export interface DigitalItemDetail {
  publicId: string; title: string; author: string | null; publisher: string | null; publishDate: string | null; keyword: string | null;
  otherTitle: string | null; page: string | null; oldAuthor: string | null; language: string | null; coverUrl: string | null; free: boolean;
  allowDownload: boolean; printCopies: number | null; collectionId: number | null; collectionName: string | null; subjectId: number | null;
  subjectName: string | null; topicId: number | null; topicName: string | null; topicRequiresLogin: boolean; typeId: number | null;
  typeName: string | null; linkedBibPublicId: string | null; files: DigitalFileSummary[];
}
export interface DigitalCatalogNode { id: number; name: string; parentId: number | null; level: number; sortOrder: number; childCount: number; hasChildren: boolean; }
export interface DigitalSearchRequest {
  keyword?: string | null; collectionId?: number | null; subjectId?: number | null; topicId?: number | null; typeId?: number | null;
  pageIndex?: number; pageSize?: number;
}
export interface DigitalPage<T> { items: T[]; totalCount: number; pageIndex: number; pageSize: number; totalPages: number; }
export interface DigitalFileUrl { url: string; expiresAt: string | null; }

/** status: 1 Đang mượn, 2 Đã thu hồi, 3 Đã trả, 4 Tự hết hạn. */
export interface DigitalLoan {
  publicId: string; ebookItemPublicId: string; title: string | null; checkedOutAt: string; expiresAt: string | null; lastAccessAt: string | null; status: number;
}
/** status: 1 Đang chờ, 2 Sẵn sàng, 3 Đã mượn, 4 Đã huỷ, 5 Hết hạn. */
export interface DigitalReservation {
  publicId: string; ebookItemPublicId: string; title: string | null; requestedAt: string; status: number; readyAt: string | null;
  readyExpiresAt: string | null; queuePosition: number | null;
}

export const DigitalLoanStatus = { Active: 1, Recalled: 2, Returned: 3, AutoExpired: 4 } as const;
export const DigitalReservationStatus = { Pending: 1, Ready: 2, Fulfilled: 3, Cancelled: 4, Expired: 5 } as const;

/**
 * API tài liệu số của OPAC — ẩn danh dưới /api/opac/digital/browse/** (gateway → digital's /api/opac/**), cần đăng nhập bạn đọc
 * dưới /api/opac/digital/** còn lại (gateway → digital's /api/reader/**), đúng quyết định #7 của plan.
 */
@Injectable({ providedIn: 'root' })
export class DigitalApi {
  private readonly http = inject(HttpClient);

  search(request: DigitalSearchRequest) {
    return firstValueFrom(this.http.post<DigitalPage<DigitalItemSummary>>('/api/opac/digital/browse/items/Search', request));
  }
  detail(publicId: string) {
    return firstValueFrom(this.http.get<DigitalItemDetail>(`/api/opac/digital/browse/items/${publicId}`));
  }
  fileUrl(itemPublicId: string, filePublicId: string, download: boolean) {
    return firstValueFrom(this.http.get<DigitalFileUrl>(`/api/opac/digital/browse/items/${itemPublicId}/files/${filePublicId}/url`, { params: { download } }));
  }
  collections() { return firstValueFrom(this.http.get<DigitalCatalogNode[]>('/api/opac/digital/browse/collections')); }
  subjects() { return firstValueFrom(this.http.get<DigitalCatalogNode[]>('/api/opac/digital/browse/subjects')); }
  topics() { return firstValueFrom(this.http.get<DigitalCatalogNode[]>('/api/opac/digital/browse/topics')); }

  myLoans() { return firstValueFrom(this.http.get<DigitalLoan[]>('/api/opac/digital/loans')); }
  borrow(ebookItemPublicId: string) { return firstValueFrom(this.http.post<DigitalLoan>('/api/opac/digital/loans/Borrow', { ebookItemPublicId })); }
  returnLoan(loanPublicId: string) { return firstValueFrom(this.http.post<DigitalLoan>(`/api/opac/digital/loans/${loanPublicId}/Return`, {})); }
  myReservations() { return firstValueFrom(this.http.get<DigitalReservation[]>('/api/opac/digital/reservations')); }
  reserve(ebookItemPublicId: string) { return firstValueFrom(this.http.post<DigitalReservation>('/api/opac/digital/reservations', { ebookItemPublicId })); }
  cancelReservation(reservationPublicId: string) {
    return firstValueFrom(this.http.post<DigitalReservation>(`/api/opac/digital/reservations/${reservationPublicId}/Cancel`, {}));
  }
}
