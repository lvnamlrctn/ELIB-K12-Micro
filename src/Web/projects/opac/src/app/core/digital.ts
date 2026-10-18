import { HttpClient, HttpErrorResponse } from '@angular/common/http';
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

export interface DigitalReview { publicId: string; rating: number | null; displayName: string | null; content: string | null; createdAt: string; }
export interface DigitalReviewSubmission { rating?: number | null; displayName?: string | null; content?: string | null; email?: string | null; }
export interface DigitalFavorite { ebookItemPublicId: string; title: string; author: string | null; coverUrl: string | null; free: boolean; createdAt: string; }
export interface DigitalByBibSummary { publicId: string; title: string; free: boolean; }

export interface DigitalDigType { id: number; code: string; descriptionVn: string | null; }

/** status: 1 Chờ duyệt, 2 Đã duyệt, 3 Từ chối. */
export interface DigitalSubmission {
  publicId: string; title: string; author: string | null; digTypeId: number | null; digTypeCode: string | null; abstract: string | null;
  fileName: string; status: number; rejectReason: string | null; createdItemPublicId: string | null; createdAt: string;
}
export const DigitalSubmissionStatus = { Pending: 1, Approved: 2, Rejected: 3 } as const;
export interface DigitalSubmissionFields { title: string; author?: string | null; digTypeId?: number | null; abstract?: string | null; }

async function putToStorage(uploadUrl: string, file: File): Promise<void> {
  const response = await fetch(uploadUrl, { method: 'PUT', body: file, credentials: 'omit' });
  if (!response.ok) throw new HttpErrorResponse({ status: response.status, error: { code: 'UPLOAD_FAILED', detail: `Upload failed (${response.status}).` } });
}

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

  publishedReviews(itemPublicId: string) {
    return firstValueFrom(this.http.get<DigitalReview[]>(`/api/opac/digital/browse/items/${itemPublicId}/reviews`));
  }
  submitReview(itemPublicId: string, submission: DigitalReviewSubmission) {
    return firstValueFrom(this.http.post<DigitalReview>(`/api/opac/digital/browse/items/${itemPublicId}/reviews`, submission));
  }

  /** Tài liệu số liên kết với một biểu ghi sách in (null nếu không có — trang sách in chỉ hiện banner khi có). */
  findByLinkedBib(bibPublicId: string) {
    return firstValueFrom(this.http.get<DigitalByBibSummary | null>(`/api/opac/digital/browse/items/by-bib/${bibPublicId}`));
  }

  myFavorites() { return firstValueFrom(this.http.get<DigitalFavorite[]>('/api/opac/digital/favorites')); }
  addFavorite(itemPublicId: string) { return firstValueFrom(this.http.post(`/api/opac/digital/favorites/${itemPublicId}`, {})); }
  removeFavorite(itemPublicId: string) { return firstValueFrom(this.http.delete(`/api/opac/digital/favorites/${itemPublicId}`)); }

  digTypes() { return firstValueFrom(this.http.get<DigitalDigType[]>('/api/opac/digital/browse/dig-types')); }
  myDocumentSubmissions() { return firstValueFrom(this.http.get<DigitalSubmission[]>('/api/opac/digital/document-submissions')); }

  /** Nộp tài liệu — xin vé → PUT thẳng vào kho (qua gateway's /s3/**, không đổi) → hoàn tất (digital tự gọi media complete). */
  async submitDocument(file: File, fields: DigitalSubmissionFields): Promise<DigitalSubmission> {
    const ticket = await firstValueFrom(this.http.post<{ fileId: string; uploadUrl: string }>(
      '/api/opac/digital/document-submissions/upload-ticket', { fileName: file.name, contentType: file.type, size: file.size }));
    await putToStorage(ticket.uploadUrl, file);
    const ext = file.name.includes('.') ? file.name.slice(file.name.lastIndexOf('.') + 1) : null;
    return firstValueFrom(this.http.post<DigitalSubmission>('/api/opac/digital/document-submissions',
      { ...fields, mediaFileId: ticket.fileId, fileName: file.name, fileExt: ext, fileSize: file.size }));
  }
}
