import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

/**
 * Tìm kiếm GỘP tài liệu in + tài liệu số (OPAC, công khai).
 * Endpoint: POST /api/public/search/unified — index `library_docs`.
 *
 * Mọi tiêu chí đều thuộc bộ trường CHUNG nên áp dụng được cho cả 2 loại tài liệu.
 * Riêng `content` chỉ tài liệu số có nội dung nên tự khoanh vùng về tài liệu số.
 */

export type DocType = 'print' | 'digital';

export interface UnifiedSearchRequest {
  q?: string;
  // tìm nâng cao theo từng tiêu chí
  title?: string;
  author?: string;
  publisher?: string;
  keyword?: string;
  summary?: string;
  isbn?: string;
  ddc?: string;
  content?: string;
  callNumber?: string;
  // lọc
  docType?: DocType | null;
  publishYearFrom?: number | null;
  publishYearTo?: number | null;
  language?: string;
  materialType?: string;
  collectionId?: string;
  storeId?: number | null;
  /** Đợt 22.2 — chọn từ facet "Kho" (khớp đúng tên, khác storeId dùng nơi khác). */
  storeName?: string;
  /** Đợt 25 — chọn NHIỀU Kho (multi-select, ưu tiên hơn storeName nếu cả 2 cùng có giá trị). */
  storeNames?: string[];
  /** Đợt 22.2 — chọn từ facet "Tác giả" (khớp đúng tên, khác `author` là ô tìm nâng cao khớp gần đúng). */
  authorExact?: string;
  /** Đợt 25 — chọn NHIỀU tác giả (multi-select, ưu tiên hơn authorExact nếu cả 2 cùng có giá trị). */
  authorsExact?: string[];
  /** Đợt 25 — chọn NHIỀU năm xuất bản cụ thể (multi-select, khác publishYearFrom/To là 1 khoảng liên tục). */
  publishYears?: number[];
  free?: boolean | null;
  availableOnly?: boolean | null;
  page?: number;
  pageSize?: number;
  sortBy?: 'relevance' | 'newest' | 'oldest' | 'title';
}

export interface UnifiedHolding {
  barcode?: string;
  storeId?: number;
  storeName?: string;
  status?: string;
  statusName?: string;
}

export interface UnifiedItem {
  groupId: string;
  docType: DocType;
  publicId?: string;
  title: string;
  author: string;
  publisher: string;
  publishDate: string;
  publishYear?: number;
  ddc?: string;
  isbn?: string;
  keyword?: string;
  materialType?: string;
  collectionName?: string;
  imageUrl: string;
  // tài liệu số
  ebookId?: number;
  ebookFileId?: number;
  bestPageNumber?: number;
  free?: boolean;
  // tài liệu in
  bibId?: number;
  mfn?: number;
  copyCount?: number;
  availableCount?: number;
  holdings?: UnifiedHolding[];
  highlight?: string;
  score?: number;
}

export interface FacetItem { key: string; count: number; }

export interface UnifiedFacets {
  docTypes: FacetItem[];
  years: FacetItem[];
  collections: FacetItem[];
  materialTypes: FacetItem[];
  ddc: FacetItem[];
  languages: FacetItem[];
  /** Đợt 22.2 — số đầu tài liệu (không phải số bản) hiện có tại từng Kho. */
  stores: FacetItem[];
  authors: FacetItem[];
}

export interface UnifiedResult {
  total: number;
  page: number;
  pageSize: number;
  items: UnifiedItem[];
  facets: UnifiedFacets;
  /** true = kết quả từ DB dự phòng (ES lỗi/chậm) — không xếp hạng liên quan/facet/highlight đầy đủ. */
  usedFallback: boolean;
  /** Đợt 25 — thời gian Elasticsearch xử lý truy vấn (ms). null khi dùng DB fallback. */
  searchExecutionTimeMs?: number | null;
}

const EMPTY_FACETS: UnifiedFacets = {
  docTypes: [], years: [], collections: [], materialTypes: [], ddc: [], languages: [], stores: [], authors: [],
};

@Injectable({ providedIn: 'root' })
export class UnifiedSearchService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private get backendRoot(): string {
    return isPlatformServer(this.platformId) ? APP_CONFIG.BackendBase : '';
  }
  private get mediaBase(): string {
    return isPlatformServer(this.platformId) ? APP_CONFIG.BackendDataBase : '/data';
  }

  private buildImg(p?: string): string {
    if (!p) return '';
    if (/^https?:\/\//i.test(p)) return p;
    return `${this.mediaBase}/${p.replace(/^\/+/, '')}`;
  }

  search(req: UnifiedSearchRequest): Observable<UnifiedResult> {
    const body: any = {
      ...req,
      page: req.page ?? 1,
      pageSize: Math.min(req.pageSize ?? 12, 50),
      tenantId: APP_CONFIG.TenantId,
    };
    Object.keys(body).forEach(k => {
      const v = body[k];
      if (v === '' || v === null || v === undefined || (Array.isArray(v) && v.length === 0)) delete body[k];
    });

    return this.http.post<any>(`${this.backendRoot}/api/public/search/unified`, body).pipe(
      map(res => {
        const d = res?.data ?? res ?? {};
        const raw = Array.isArray(d.items) ? d.items : [];
        return {
          total: d.total ?? raw.length,
          page: d.page ?? body.page,
          pageSize: d.pageSize ?? body.pageSize,
          items: raw.map((x: any) => this.mapItem(x)),
          facets: this.mapFacets(d.facets),
          usedFallback: !!d.usedFallback,
          searchExecutionTimeMs: d.searchExecutionTimeMs ?? null,
        } as UnifiedResult;
      }),
      catchError(() => of({
        total: 0, page: body.page, pageSize: body.pageSize, items: [], facets: { ...EMPTY_FACETS }, usedFallback: false,
      }))
    );
  }

  /** Đợt 22.2 — "Có thể bạn quan tâm": more_like_this theo title/author/keyword của chính tài liệu
   *  `publicId` (docType "print" | "digital"), lọc tenant, loại chính nó ra khỏi kết quả. */
  similar(docType: 'print' | 'digital', publicId: string, size = 8): Observable<UnifiedItem[]> {
    const params: any = { docType, publicId, size };
    if (APP_CONFIG.TenantId) params.tenantId = APP_CONFIG.TenantId;
    return this.http.get<any>(`${this.backendRoot}/api/public/search/unified/similar`, { params }).pipe(
      map(res => (Array.isArray(res?.data) ? res.data : []).map((x: any) => this.mapItem(x))),
      catchError(() => of([]))
    );
  }

  /** Gợi ý theo tiền tố cho 1 trong 4 ô lọc nâng cao (title/author/publisher/keyword). */
  suggestField(field: 'title' | 'author' | 'publisher' | 'keyword', q: string): Observable<string[]> {
    if (!q || !q.trim()) return of([]);
    const params: any = { field, q: q.trim() };
    if (APP_CONFIG.TenantId) params.tenantId = APP_CONFIG.TenantId;
    return this.http.get<any>(`${this.backendRoot}/api/public/search/unified/suggest-field`, { params }).pipe(
      map(res => Array.isArray(res?.data) ? res.data : []),
      catchError(() => of([]))
    );
  }

  private mapItem(x: any): UnifiedItem {
    return {
      groupId: x.groupId ?? '',
      docType: (x.docType === 'print' ? 'print' : 'digital') as DocType,
      publicId: x.publicId ?? undefined,
      title: x.title ?? '',
      author: x.author ?? '',
      publisher: x.publisher ?? '',
      publishDate: x.publishDate ?? '',
      publishYear: x.publishYear ?? undefined,
      ddc: x.ddc ?? undefined,
      isbn: x.isbn ?? undefined,
      keyword: x.keyword ?? undefined,
      materialType: x.materialType ?? undefined,
      collectionName: x.collectionName ?? undefined,
      imageUrl: this.buildImg(x.images ?? ''),
      ebookId: x.ebookId ?? undefined,
      ebookFileId: x.ebookFileId ?? undefined,
      bestPageNumber: x.bestPageNumber ?? undefined,
      free: x.free ?? undefined,
      bibId: x.bibId ?? undefined,
      mfn: x.mfn ?? undefined,
      copyCount: x.copyCount ?? undefined,
      availableCount: x.availableCount ?? undefined,
      holdings: Array.isArray(x.holdings) ? x.holdings : undefined,
      highlight: x.highlight ?? '',
      score: x.score ?? undefined,
    };
  }

  private mapFacets(f: any): UnifiedFacets {
    if (!f) return { ...EMPTY_FACETS };
    const arr = (a: any): FacetItem[] => Array.isArray(a)
      ? a.map((x: any) => ({ key: String(x.key ?? ''), count: x.count ?? 0 }))
      : [];
    return {
      docTypes: arr(f.docTypes),
      years: arr(f.years),
      collections: arr(f.collections),
      materialTypes: arr(f.materialTypes),
      ddc: arr(f.ddc),
      languages: arr(f.languages),
      stores: arr(f.stores),
      authors: arr(f.authors),
    };
  }
}
