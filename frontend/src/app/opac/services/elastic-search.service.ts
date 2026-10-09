import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

/**
 * Tìm kiếm Elasticsearch tài liệu số (OPAC, công khai).
 * Endpoint: POST /api/public/ebook/SearchElastic — xem docs FRONTEND-ELASTIC-SEARCH.md.
 */

export interface ElasticSearchRequest {
  q?: string;
  page?: number;
  pageSize?: number;
  // tìm theo trường (match)
  title?: string;
  author?: string;
  publisher?: string;
  keyword?: string;
  dcSubject?: string;
  // lọc chính xác (term)
  collectionId?: string;
  topicId?: string;
  subjectId?: string;
  language?: string;
  dcType?: string;
  free?: boolean | null;
  share?: boolean | null;
  // khoảng năm
  publishDateFrom?: string;
  publishDateTo?: string;
}

export interface ElasticItem {
  ebookId: string;          // PublicId (GUID) → link chi tiết
  title: string;
  author: string;
  publisher: string;
  publishDate: string;
  imageUrl: string;
  collectionId?: string;
  collectionName?: string;
  topicId?: string;
  topicName?: string;
  subjectId?: string;
  subjectName?: string;
  free: boolean;
  share: boolean;
  dcType?: string;
  dcLanguage?: string;
  highlight?: string;       // HTML có <mark>
  score?: number;
}

export interface FacetItem { key: string; count: number; }

export interface EbookSearchFacets {
  languages: FacetItem[];
  topics: FacetItem[];
  collections: FacetItem[];
  years: FacetItem[];
  freeCount: number;
}

export interface ElasticResult {
  total: number;
  page: number;
  pageSize: number;
  items: ElasticItem[];
  facets: EbookSearchFacets;
}

const EMPTY_FACETS: EbookSearchFacets = { languages: [], topics: [], collections: [], years: [], freeCount: 0 };

@Injectable({ providedIn: 'root' })
export class ElasticSearchService {
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

  search(req: ElasticSearchRequest): Observable<ElasticResult> {
    const body: any = {
      ...req,
      page: req.page ?? 1,
      pageSize: Math.min(req.pageSize ?? 12, 50),
      tenantId: APP_CONFIG.TenantId,   // OPAC chỉ có GUID (resolve theo host)
    };
    // bỏ các trường rỗng/null cho gọn body
    Object.keys(body).forEach(k => {
      const v = body[k];
      if (v === '' || v === null || v === undefined) delete body[k];
    });

    return this.http.post<any>(`${this.backendRoot}/api/public/ebook/SearchElastic`, body).pipe(
      map(res => {
        const d = res?.data ?? res ?? {};
        const itemsRaw = Array.isArray(d.items) ? d.items : [];
        return {
          total: d.total ?? itemsRaw.length,
          page: d.page ?? body.page,
          pageSize: d.pageSize ?? body.pageSize,
          items: itemsRaw.map((x: any) => this.mapItem(x)),
          facets: this.mapFacets(d.facets),
        } as ElasticResult;
      }),
      catchError(() => of({ total: 0, page: body.page, pageSize: body.pageSize, items: [], facets: EMPTY_FACETS }))
    );
  }

  private mapItem(x: any): ElasticItem {
    return {
      ebookId: x.ebookId ?? x.EbookId ?? x.publicId ?? '',
      title: x.title ?? '',
      author: x.author ?? '',
      publisher: x.publisher ?? '',
      publishDate: x.publishDate ?? '',
      imageUrl: this.buildImg(x.images ?? x.imageUrl ?? ''),
      collectionId: x.collectionId != null ? String(x.collectionId) : undefined,
      collectionName: x.collectionName ?? undefined,
      topicId: x.topicId != null ? String(x.topicId) : undefined,
      topicName: x.topicName ?? undefined,
      subjectId: x.subjectId != null ? String(x.subjectId) : undefined,
      subjectName: x.subjectName ?? undefined,
      free: !!x.free,
      share: !!x.share,
      dcType: x.dcType ?? undefined,
      dcLanguage: x.dcLanguage ?? undefined,
      highlight: x.highlight ?? '',
      score: x.score ?? undefined,
    };
  }

  private mapFacets(f: any): EbookSearchFacets {
    if (!f) return { ...EMPTY_FACETS };
    const arr = (a: any): FacetItem[] => Array.isArray(a)
      ? a.map((x: any) => ({ key: String(x.key ?? x.Key ?? ''), count: x.count ?? x.Count ?? 0 }))
      : [];
    return {
      languages: arr(f.languages),
      topics: arr(f.topics),
      collections: arr(f.collections),
      years: arr(f.years),
      freeCount: f.freeCount ?? 0,
    };
  }
}
