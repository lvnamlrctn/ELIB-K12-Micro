import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { EbookLoan } from '../../models/ebook/ebook-loan';

export interface LoanSearchResult {
  data:       EbookLoan[];
  totalCount: number;
}

export interface LoanSearchParams {
  ebookItemId?: number | null;
  loanStatus?:  number | null;
  keyword?:     string | null;
}

@Injectable({ providedIn: 'root' })
export class EbookLoanService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook/EbookItemLoan`;
  }

  search(params: LoanSearchParams): Observable<LoanSearchResult> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, {
      ebookItemId: params.ebookItemId ?? null,
      loanStatus:  params.loanStatus  ?? null,
      keyword:     params.keyword     || '',
    }).pipe(
      map(res => {
        const items: EbookLoan[] = res?.data ?? [];
        return { data: items, totalCount: items.length };
      }),
      catchError(() => of({ data: [], totalCount: 0 }))
    );
  }

  recall(publicId: string, reason?: string | null): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Recall/${publicId}`, { reason: reason || null }).pipe(
      map(res => res?.data ?? res)
    );
  }

  recallAll(ebookItemPublicId: string, reason?: string | null): Observable<{ count: number }> {
    return this.http.put<any>(`${this.baseUrl}/RecallAll/${ebookItemPublicId}`, { reason: reason || null }).pipe(
      map(res => res?.data ?? { count: 0 })
    );
  }
}
