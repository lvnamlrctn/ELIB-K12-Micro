import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Tra cứu (service search) — /api/opac/search/**, đơn vị theo host, không cần đăng nhập. */
export interface SearchRequest {
  q?: string | null; title?: string | null; author?: string | null; publisher?: string | null; keyword?: string | null;
  isbn?: string | null; ddc?: string | null; barcode?: string | null; yearFrom?: number | null; yearTo?: number | null;
  years?: number[]; authors?: string[]; materialTypes?: string[]; languages?: string[]; stores?: string[];
  availableOnly?: boolean; sort?: string | null; page: number; pageSize: number;
  /** Chỉ đổi bộ lọc/sắp xếp của câu tìm đang xem — không tính lượt tìm mới trong thống kê. */
  refine?: boolean;
}
export interface Bib {
  publicId: string; mfn: number; title: string; author: string | null; publisher: string | null; publishYear: string | null;
  materialType: string | null; language: string | null; ddc: string | null; isbns: string | null; summary: string | null; copies: number; available: number;
  coverUrl: string | null;
}
export interface Facet { key: string; count: number; }
export interface Facets { years: Facet[]; authors: Facet[]; materialTypes: Facet[]; languages: Facet[]; stores: Facet[]; availableCount: number; }
/** queryId: mã lượt tìm (thống kê) — gửi lại khi bạn đọc mở một kết quả. */
export interface SearchResult { total: number; page: number; pageSize: number; items: Bib[]; facets: Facets; queryId: string | null; }
/** status: available | on-loan | processing */
export interface Copy { barcode: string; storeName: string | null; status: string; statusName: string; dueAt: string | null; }
export interface BibDetail {
  publicId: string; mfn: number; title: string; author: string | null; otherAuthors: string | null; publisher: string | null; publishPlace: string | null;
  publishYear: string | null; edition: string | null; physicalDescription: string | null; series: string | null; materialType: string | null;
  language: string | null; ddc: string | null; cutter: string | null; isbns: string | null; keywords: string | null; summary: string | null;
  copies: number; available: number; holdings: Copy[]; coverUrl: string | null;
}
export interface MarcSubfield { code: string; value: string; }
export interface MarcField { tag: string; ind1: string | null; ind2: string | null; value: string | null; subfields: MarcSubfield[] | null; }
/** Tra cứu liên thư viện (Z39.50/SRU) — máy chủ thư viện bật cho OPAC. */
export interface Z3950Server { publicId: string; name: string; groupName: string | null; recordSyntax: string; }
export interface Z3950Hit {
  position: number; title: string | null; author: string | null; publisher: string | null; year: string | null; isbn: string | null;
  record: { leader: string; fields: MarcField[] };
}
export interface Z3950Result {
  serverId: string; serverName: string; recordSyntax: string; connected: boolean; total: number; page: number; pageSize: number; hits: Z3950Hit[]; error: string | null;
}
export interface Z3950Request {
  title?: string | null; author?: string | null; isbn?: string | null; keyword?: string | null; serverIds?: string[]; page: number; pageSize: number;
}
/** MARC + ISBD của biểu ghi (service catalog, /api/opac/catalog/**). */
export interface OpacMarc { publicId: string; mfn: number; leader: string; fields: MarcField[]; isbd: string[]; }

/** Mã ngôn ngữ MARC (041/008) thường gặp → tên hiển thị. */
const LANGUAGES: Record<string, string> = {
  vie: 'Tiếng Việt', eng: 'Tiếng Anh', fre: 'Tiếng Pháp', fra: 'Tiếng Pháp', chi: 'Tiếng Trung', zho: 'Tiếng Trung', rus: 'Tiếng Nga',
  jpn: 'Tiếng Nhật', kor: 'Tiếng Hàn', ger: 'Tiếng Đức', deu: 'Tiếng Đức',
};

/** Thông báo lỗi tra cứu cho bạn đọc — 429: gateway giới hạn số lượt tìm từ một địa chỉ (chống quét dữ liệu). */
export function searchErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse && error.status === 429) {
    const wait = Number(error.headers.get('Retry-After'));
    return `Bạn tra cứu quá nhanh. Vui lòng chờ ${wait > 0 ? `${wait} giây` : 'ít phút'} rồi thử lại.`;
  }
  return 'Không tra cứu được lúc này, vui lòng thử lại.';
}

export function languageName(code: string | null | undefined): string {
  return code ? LANGUAGES[code.toLowerCase()] ?? code : '';
}

@Injectable({ providedIn: 'root' })
export class SearchApi {
  private readonly http = inject(HttpClient);

  search(body: SearchRequest) { return firstValueFrom(this.http.post<SearchResult>('/api/opac/search/bibs/Search', body)); }
  detail(publicId: string) { return firstValueFrom(this.http.get<BibDetail>(`/api/opac/search/bibs/${publicId}`)); }
  similar(publicId: string) { return firstValueFrom(this.http.get<Bib[]>(`/api/opac/search/bibs/${publicId}/similar`)); }
  suggest(q: string) { return firstValueFrom(this.http.get<string[]>('/api/opac/search/suggest', { params: { q } })); }
  z3950Servers() { return firstValueFrom(this.http.get<Z3950Server[]>('/api/opac/search/z3950/servers')); }
  z3950Search(body: Z3950Request) { return firstValueFrom(this.http.post<Z3950Result[]>('/api/opac/search/z3950/Search', body)); }
  marc(publicId: string) { return firstValueFrom(this.http.get<OpacMarc>(`/api/opac/catalog/bibs/${publicId}/marc`)); }
  exportUrl(publicId: string, format: 'iso2709' | 'marcxml') { return `/api/opac/catalog/bibs/${publicId}/export?format=${format}`; }

  /** Bạn đọc mở kết quả thứ position của lượt tìm — chỉ để thống kê, lỗi bỏ qua. */
  click(queryId: string, bibPublicId: string, position: number): void {
    this.http.post('/api/opac/search/stats/click', { queryId, bibPublicId, position }).subscribe({ error: () => undefined });
  }
}
