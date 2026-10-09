import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

export interface BorrowingItem {
  id: string;
  bookPublicId?: string;
  bibId?: number;
  title: string;
  author: string;
  barcode: string;
  borrowDate: string;
  dueDate?: string;
  renewCount: number;
  overdue: boolean;
}

export interface HistoryItem {
  id: string;
  bookPublicId?: string;
  bibId?: number;
  title: string;
  author: string;
  barcode: string;
  borrowDate: string;
  returnDate?: string;
}

export interface ReservedItem {
  id: string;
  bookPublicId?: string;
  bibId?: number;
  title: string;
  author: string;
  status: string;
  reserveDate: string;
  expireDate?: string;
  queuePosition?: number;
}

export interface DigitalBorrowingItem {
  id: string;
  loanPublicId: string;
  ebookPublicId?: string;
  title: string;
  author: string;
  borrowDate: string;
  dueDate?: string;
  overdue: boolean;
}

export interface DigitalHistoryItem {
  id: string;
  ebookPublicId?: string;
  title: string;
  author: string;
  borrowDate: string;
  returnDate?: string;
  selfReturned: boolean;
}

export interface DigitalReservedItem {
  id: string;
  reservationPublicId: string;
  ebookPublicId?: string;
  title: string;
  author: string;
  reserveDate: string;
  status: 'pending' | 'ready';
  readyExpiresAt?: string;
  queuePosition?: number;
}

export interface MyLibraryStatsMonth {
  year: number;
  month: number;
  label: string;
  reads: number;
  pages: number;
}

export interface MyLibraryRecentRead {
  id: string;
  title: string;
  author: string;
  date?: string;
  page: number;
}

export interface MyLibraryStats {
  totalReads: number;
  distinctDocs: number;
  totalPages: number;
  thisMonthDocs: number;
  monthly: MyLibraryStatsMonth[];
  recentReads: MyLibraryRecentRead[];
}

const EMPTY_STATS: MyLibraryStats = {
  totalReads: 0, distinctDocs: 0, totalPages: 0, thisMonthDocs: 0, monthly: [], recentReads: [],
};

export interface EarnedBadge {
  badgeId: number;
  code?: string | null;
  name: string;
  description: string;
  iconName: string;
  earnedAt: string;
}

export interface InProgressBadge {
  badgeId: number;
  code?: string | null;
  name: string;
  description: string;
  iconName: string;
  current: number;
  threshold: number;
}

export interface ReaderBadges {
  enabled: boolean;
  earned: EarnedBadge[];
  inProgress: InProgressBadge[];
}

const EMPTY_BADGES: ReaderBadges = { enabled: false, earned: [], inProgress: [] };

/** Sổ mượn/giữ chỗ của bạn đọc hiện tại (MyLibraryController), gọi kèm Bearer token (xem opac/auth.interceptor.ts). */
@Injectable({ providedIn: 'root' })
export class MyLibraryApiService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private get backendRoot(): string {
    return isPlatformServer(this.platformId) ? APP_CONFIG.BackendBase : '';
  }

  private get baseUrl(): string {
    return `${this.backendRoot}/api/public/MyLibrary`;
  }

  private items<T>(url: string): Observable<T[]> {
    return this.http.get<any>(url).pipe(
      map(res => (res?.success && res.data?.items ? (res.data.items as T[]) : [])),
      catchError(() => of([]))
    );
  }

  getBorrowing(): Observable<BorrowingItem[]> { return this.items(`${this.baseUrl}/Borrowing`); }
  getHistory(): Observable<HistoryItem[]> { return this.items(`${this.baseUrl}/History`); }
  getReserved(): Observable<ReservedItem[]> { return this.items(`${this.baseUrl}/Reserved`); }

  hold(bibId: number): Observable<{ ok: boolean; message?: string }> {
    return this.http.post<any>(`${this.baseUrl}/Hold`, { bibId }).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  getStats(): Observable<MyLibraryStats> {
    return this.http.get<any>(`${this.baseUrl}/Stats`).pipe(
      map(res => (res?.success && res.data ? (res.data as MyLibraryStats) : EMPTY_STATS)),
      catchError(() => of(EMPTY_STATS))
    );
  }

  getBadges(): Observable<ReaderBadges> {
    return this.http.get<any>(`${this.baseUrl}/Badges`).pipe(
      map(res => (res?.success && res.data ? (res.data as ReaderBadges) : EMPTY_BADGES)),
      catchError(() => of(EMPTY_BADGES))
    );
  }

  getDigitalBorrowing(): Observable<DigitalBorrowingItem[]> { return this.items(`${this.baseUrl}/DigitalBorrowing`); }
  getDigitalHistory(): Observable<DigitalHistoryItem[]> { return this.items(`${this.baseUrl}/DigitalHistory`); }
  getDigitalReserved(): Observable<DigitalReservedItem[]> { return this.items(`${this.baseUrl}/DigitalReserved`); }

  borrowDigital(ebookItemPublicId: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.post<any>(`${this.baseUrl}/BorrowDigital`, { ebookItemPublicId }).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  returnDigital(loanPublicId: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.post<any>(`${this.baseUrl}/ReturnDigital`, { loanPublicId }).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  reserveDigital(ebookItemPublicId: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.post<any>(`${this.baseUrl}/ReserveDigital`, { ebookItemPublicId }).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  cancelDigitalReservation(publicId: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.delete<any>(`${this.baseUrl}/CancelDigitalReservation/${publicId}`).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }
}
