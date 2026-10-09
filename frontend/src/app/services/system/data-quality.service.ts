import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Quy tắc cảnh báo chất lượng dữ liệu (Đợt 21 — port từ ELIB-LRC); khoá trùng AdminDataQualityService phía server. */
export const QUALITY_RULES = [
  { key: 'missing-title',     label: 'DATA_QUALITY.RULE_MISSING_TITLE',     description: 'DATA_QUALITY.DESC_MISSING_TITLE' },
  { key: 'missing-author',    label: 'DATA_QUALITY.RULE_MISSING_AUTHOR',    description: 'DATA_QUALITY.DESC_MISSING_AUTHOR' },
  { key: 'missing-publisher', label: 'DATA_QUALITY.RULE_MISSING_PUBLISHER', description: 'DATA_QUALITY.DESC_MISSING_PUBLISHER' },
  { key: 'missing-ddc',       label: 'DATA_QUALITY.RULE_MISSING_DDC',       description: 'DATA_QUALITY.DESC_MISSING_DDC' },
  { key: 'missing-isbn',      label: 'DATA_QUALITY.RULE_MISSING_ISBN',      description: 'DATA_QUALITY.DESC_MISSING_ISBN' },
  { key: 'duplicate-barcode', label: 'DATA_QUALITY.RULE_DUPLICATE_BARCODE', description: 'DATA_QUALITY.DESC_DUPLICATE_BARCODE' },
  { key: 'unshelved',         label: 'DATA_QUALITY.RULE_UNSHELVED',         description: 'DATA_QUALITY.DESC_UNSHELVED' },
] as const;

export type QualityRuleKey = typeof QUALITY_RULES[number]['key'];

export interface QualitySummary { counts: Partial<Record<QualityRuleKey, number>>; checkedAt: string; }
export interface QualityIssue {
  id: string;
  mfn: number | null;
  bibId: number | null;
  barcode: string | null;
  title: string | null;
  tenantId: number | null;
}
export interface QualityList { rule: string; total: number; page: number; pageSize: number; items: QualityIssue[]; checkedAt: string; }

@Injectable({ providedIn: 'root' })
export class DataQualityService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Dbo/DataQuality`; }

  summary(): Observable<QualitySummary> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/summary`).pipe(map(res => res?.data as QualitySummary));
  }

  list(rule: string, page: number, pageSize = 25): Observable<QualityList> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/issues`, { params: { rule, page, pageSize } }).pipe(map(res => res?.data as QualityList));
  }
}
