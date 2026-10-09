import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { ElasticSearchService, ElasticItem, EbookSearchFacets } from '../../services/elastic-search.service';

const EMPTY_FACETS: EbookSearchFacets = { languages: [], topics: [], collections: [], years: [], freeCount: 0 };

@Component({
  selector: 'app-elastic-search',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './elastic-search.html'
})
export class ElasticSearchComponent implements OnInit {
  private api = inject(ElasticSearchService);
  private route = inject(ActivatedRoute);

  // Full-text + tìm theo trường
  q = '';
  title = '';
  author = '';
  publisher = '';
  keyword = '';
  dcSubject = '';

  // Lọc
  language = '';
  collectionId = '';
  free: boolean | null = null;
  yearFrom = '';
  yearTo = '';

  items = signal<ElasticItem[]>([]);
  facets = signal<EbookSearchFacets>(EMPTY_FACETS);
  total = signal(0);
  loading = signal(false);

  page = signal(1);
  pageSize = 12;

  totalPages = computed(() => Math.ceil(this.total() / this.pageSize) || 1);
  pages = computed(() => {
    const total = this.totalPages();
    const current = this.page();
    let start = Math.max(1, current - 2);
    const end = Math.min(total, start + 4);
    if (end - start < 4) start = Math.max(1, end - 4);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  });

  // Map id -> tên (collection/topic) suy từ kết quả để hiển thị facet thân thiện hơn.
  private nameMap = new Map<string, string>();

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap.get('q');
    if (q) this.q = q;
    this.runSearch();
  }

  runSearch(resetPage = true): void {
    if (resetPage) this.page.set(1);
    this.loading.set(true);
    this.api.search({
      q: this.q, title: this.title, author: this.author, publisher: this.publisher,
      keyword: this.keyword, dcSubject: this.dcSubject,
      language: this.language, collectionId: this.collectionId,
      free: this.free, publishDateFrom: this.yearFrom, publishDateTo: this.yearTo,
      page: this.page(), pageSize: this.pageSize,
    }).subscribe(res => {
      this.items.set(res.items);
      this.total.set(res.total);
      this.facets.set(res.facets);
      // cập nhật map tên từ item
      res.items.forEach(it => {
        if (it.collectionId && it.collectionName) this.nameMap.set('c:' + it.collectionId, it.collectionName);
      });
      this.loading.set(false);
    });
  }

  clearFilters(): void {
    this.q = this.title = this.author = this.publisher = this.keyword = this.dcSubject = '';
    this.language = this.collectionId = this.yearFrom = this.yearTo = '';
    this.free = null;
    this.runSearch();
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages() && p !== this.page()) {
      this.page.set(p);
      this.runSearch(false);
      if (typeof window !== 'undefined') window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  // ── Facet helpers ─────────────────────────────────────────────
  toggleLanguage(key: string): void { this.language = this.language === key ? '' : key; this.runSearch(); }
  toggleYear(key: string): void {
    const on = this.yearFrom === key && this.yearTo === key;
    this.yearFrom = this.yearTo = on ? '' : key;
    this.runSearch();
  }
  toggleCollection(key: string): void { this.collectionId = this.collectionId === key ? '' : key; this.runSearch(); }
  toggleFree(): void { this.free = this.free ? null : true; this.runSearch(); }

  collectionName(key: string): string { return this.nameMap.get('c:' + key) ?? `Bộ sưu tập ${key}`; }
  languageLabel(key: string): string {
    const m: Record<string, string> = { vi: 'Tiếng Việt', en: 'Tiếng Anh', fr: 'Tiếng Pháp', zh: 'Tiếng Trung' };
    return m[key] ?? (key || 'Khác');
  }
}
