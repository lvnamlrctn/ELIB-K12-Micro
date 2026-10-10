import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Tra cứu (service search) — /api/opac/search/**, đơn vị theo host, không cần đăng nhập. */
export interface SearchRequest {
  q?: string | null; title?: string | null; author?: string | null; publisher?: string | null; keyword?: string | null;
  isbn?: string | null; ddc?: string | null; barcode?: string | null; yearFrom?: number | null; yearTo?: number | null;
  years?: number[]; authors?: string[]; materialTypes?: string[]; languages?: string[]; stores?: string[];
  availableOnly?: boolean; sort?: string | null; page: number; pageSize: number;
}
export interface Bib {
  publicId: string; mfn: number; title: string; author: string | null; publisher: string | null; publishYear: string | null;
  materialType: string | null; language: string | null; ddc: string | null; isbns: string | null; summary: string | null; copies: number; available: number;
}
export interface Facet { key: string; count: number; }
export interface Facets { years: Facet[]; authors: Facet[]; materialTypes: Facet[]; languages: Facet[]; stores: Facet[]; availableCount: number; }
export interface SearchResult { total: number; page: number; pageSize: number; items: Bib[]; facets: Facets; }
/** status: available | on-loan | processing */
export interface Copy { barcode: string; storeName: string | null; status: string; statusName: string; dueAt: string | null; }
export interface BibDetail {
  publicId: string; mfn: number; title: string; author: string | null; otherAuthors: string | null; publisher: string | null; publishPlace: string | null;
  publishYear: string | null; edition: string | null; physicalDescription: string | null; series: string | null; materialType: string | null;
  language: string | null; ddc: string | null; cutter: string | null; isbns: string | null; keywords: string | null; summary: string | null;
  copies: number; available: number; holdings: Copy[];
}

/** Mã ngôn ngữ MARC (041/008) thường gặp → tên hiển thị. */
const LANGUAGES: Record<string, string> = {
  vie: 'Tiếng Việt', eng: 'Tiếng Anh', fre: 'Tiếng Pháp', fra: 'Tiếng Pháp', chi: 'Tiếng Trung', zho: 'Tiếng Trung', rus: 'Tiếng Nga',
  jpn: 'Tiếng Nhật', kor: 'Tiếng Hàn', ger: 'Tiếng Đức', deu: 'Tiếng Đức',
};

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
}
