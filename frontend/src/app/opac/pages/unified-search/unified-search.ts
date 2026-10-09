import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import {
  UnifiedSearchService, UnifiedItem, UnifiedFacets, DocType,
} from '../../services/unified-search.service';
import { SavedSearchOpacService } from '../../services/saved-search-opac.service';
import { AuthService } from '../../services/auth.service';
import { BookApiService } from '../../services/book-api.service';
import { CollectionOption } from '../../../shared/collection-tree.util';
import { SearchableSelectComponent } from '../../../shared/components/searchable-select/searchable-select';

const EMPTY_FACETS: UnifiedFacets = {
  docTypes: [], years: [], collections: [], materialTypes: [], ddc: [], languages: [], stores: [], authors: [],
};

/**
 * Tra cứu GỘP tài liệu in + tài liệu số trên một kết quả duy nhất.
 *
 * Lưu ý về dữ liệu hiện tại (đã đo trên index thật): `ddc` chỉ tài liệu IN có,
 * `keyword` chỉ tài liệu SỐ có, còn `summary`/`language` rỗng ở cả hai loại.
 * Vì vậy UI chỉ mở các tiêu chí thực sự dùng được cho cả 2 loại, và cảnh báo
 * rõ khi người dùng chọn tiêu chí vốn chỉ tồn tại ở một loại.
 */
@Component({
  selector: 'app-unified-search',
  standalone: true,
  imports: [CommonModule, FormsModule, SearchableSelectComponent],
  templateUrl: './unified-search.html',
})
export class UnifiedSearchComponent implements OnInit {
  private api = inject(UnifiedSearchService);
  private savedSearchApi = inject(SavedSearchOpacService);
  private bookApi = inject(BookApiService);
  public  authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  // ── Lưu tìm kiếm này (Đợt 9) ─────────────────────────────────────────────────
  usedFallback = signal(false);
  showSaveSearchModal = signal(false);
  savingSearch = signal(false);
  saveSearchName = '';
  saveSearchMessage = signal<{ ok: boolean; text: string } | null>(null);

  // Tìm nhanh
  q = '';
  // Tìm nâng cao — chỉ những tiêu chí có dữ liệu ở cả 2 loại
  title = '';
  author = '';
  publisher = '';
  content = '';       // chỉ tài liệu số
  ddc = '';           // chỉ tài liệu in
  callNumber = '';    // chỉ tài liệu in
  showAdvanced = false;

  // Lọc
  docType: DocType | null = null;
  yearFrom: number | null = null;
  yearTo: number | null = null;
  materialType = '';
  collectionId: string | null = null;
  // Đợt 22.2 — chọn từ facet "Kho"/"Tác giả" (khớp đúng, khác ô tìm nâng cao `author` khớp gần đúng).
  // Đợt 25 — nâng lên multi-select (mảng) thay vì chỉ chọn được 1 giá trị; facet phía server đã "sticky"
  // (đếm không tự triệt tiêu khi đã chọn giá trị của chính facet đó).
  selectedStores: string[] = [];
  selectedAuthors: string[] = [];
  selectedYears: number[] = [];
  availableOnly = false;
  sortBy: 'relevance' | 'newest' | 'oldest' | 'title' = 'relevance';

  collectionOptions = signal<CollectionOption[]>([]);
  // Thụt lề theo cấp bậc, giống ô chọn bộ sưu tập ở trang search.html (Đợt 20).
  collectionSelectOptions = computed(() => this.collectionOptions().map(c => ({
    id: c.id, label: '   '.repeat(c.depth) + (c.depth ? '└ ' : '') + c.title,
  })));

  items = signal<UnifiedItem[]>([]);
  facets = signal<UnifiedFacets>(EMPTY_FACETS);
  total = signal(0);
  loading = signal(false);
  searched = signal(false);
  searchExecutionTimeMs = signal<number | null>(null);

  // Đợt 25 — chế độ hiển thị kết quả (grid/list) + số kết quả/trang tuỳ chọn.
  viewMode = signal<'list' | 'grid'>('list');
  pageSizeOptions = [6, 9, 12, 24];

  page = signal(1);
  pageSize = signal(12);

  totalPages = computed(() => Math.ceil(this.total() / this.pageSize()) || 1);
  pages = computed(() => {
    const total = this.totalPages();
    const current = this.page();
    let start = Math.max(1, current - 2);
    const end = Math.min(total, start + 4);
    if (end - start < 4) start = Math.max(1, end - 4);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  });

  countOf = computed(() => {
    const m: Record<string, number> = {};
    this.facets().docTypes.forEach(f => (m[f.key] = f.count));
    return m;
  });

  // Gợi ý theo tiền tố cho 3 ô lọc nâng cao chung cho cả 2 loại tài liệu (title/author/publisher) —
  // chỉ 1 ô mở dropdown tại 1 thời điểm, đủ dùng vì người dùng gõ tuần tự từng ô.
  suggestions = signal<string[]>([]);
  activeSuggestField = signal<'title' | 'author' | 'publisher' | null>(null);
  private suggestTimer?: ReturnType<typeof setTimeout>;

  onFilterInput(field: 'title' | 'author' | 'publisher'): void {
    this.activeSuggestField.set(field);
    clearTimeout(this.suggestTimer);
    const value = field === 'title' ? this.title : field === 'author' ? this.author : this.publisher;
    if (!value || value.trim().length < 2) { this.suggestions.set([]); return; }
    this.suggestTimer = setTimeout(() => {
      this.api.suggestField(field, value).subscribe(list => this.suggestions.set(list));
    }, 300);
  }

  selectSuggestion(field: 'title' | 'author' | 'publisher', value: string): void {
    if (field === 'title') this.title = value;
    else if (field === 'author') this.author = value;
    else this.publisher = value;
    this.suggestions.set([]);
    this.activeSuggestField.set(null);
  }

  // Trễ nhẹ để (mousedown) trên gợi ý kịp xử lý trước khi input mất focus ẩn dropdown.
  closeSuggestions(): void {
    setTimeout(() => this.activeSuggestField.set(null), 150);
  }

  /** Cảnh báo khi tiêu chí đang dùng vốn chỉ tồn tại ở một loại tài liệu. */
  scopeNotice = computed(() => {
    if (this.ddc || this.callNumber) return 'print';
    if (this.content) return 'digital';
    return null;
  });

  ngOnInit(): void {
    const p = this.route.snapshot.queryParamMap;
    this.q         = p.get('q') ?? '';
    this.title     = p.get('title') ?? '';
    this.author    = p.get('author') ?? '';
    this.publisher = p.get('publisher') ?? '';
    const dt = p.get('docType');
    if (dt === 'print' || dt === 'digital') this.docType = dt;

    // Vào thẳng từ ô tìm kiếm ngoài trang chủ với title/author → mở sẵn phần nâng cao
    // để người dùng thấy tiêu chí nào đang áp dụng.
    if (this.title || this.author || this.publisher) this.showAdvanced = true;
    if (this.q || this.title || this.author || this.publisher) this.runSearch();

    this.bookApi.getCollectionTree().subscribe(opts => this.collectionOptions.set(opts));
  }

  runSearch(resetPage = true): void {
    if (resetPage) this.page.set(1);
    this.loading.set(true);
    this.searched.set(true);
    this.api.search({
      q: this.q,
      title: this.title, author: this.author, publisher: this.publisher,
      content: this.content, ddc: this.ddc, callNumber: this.callNumber,
      docType: this.docType,
      publishYearFrom: this.yearFrom, publishYearTo: this.yearTo,
      materialType: this.materialType,
      collectionId: this.collectionId ?? undefined,
      storeNames: this.selectedStores,
      authorsExact: this.selectedAuthors,
      publishYears: this.selectedYears,
      availableOnly: this.availableOnly ? true : null,
      sortBy: this.sortBy,
      page: this.page(), pageSize: this.pageSize(),
    }).subscribe(res => {
      this.items.set(res.items);
      this.total.set(res.total);
      this.facets.set(res.facets);
      this.usedFallback.set(res.usedFallback);
      this.searchExecutionTimeMs.set(res.searchExecutionTimeMs ?? null);
      this.loading.set(false);
    });
  }

  openSaveSearchModal(): void {
    this.saveSearchName = this.q || this.title || '';
    this.saveSearchMessage.set(null);
    this.showSaveSearchModal.set(true);
  }

  closeSaveSearchModal(): void { this.showSaveSearchModal.set(false); }

  confirmSaveSearch(): void {
    if (!this.saveSearchName.trim()) return;
    this.savingSearch.set(true);
    this.savedSearchApi.save(this.saveSearchName.trim(), {
      q: this.q, title: this.title, author: this.author, publisher: this.publisher,
      content: this.content, ddc: this.ddc, callNumber: this.callNumber,
      docType: this.docType, publishYearFrom: this.yearFrom, publishYearTo: this.yearTo,
      materialType: this.materialType, sortBy: this.sortBy,
    }).subscribe(res => {
      this.savingSearch.set(false);
      if (res.ok) { this.showSaveSearchModal.set(false); this.saveSearchMessage.set({ ok: true, text: 'Đã lưu tìm kiếm. Bạn sẽ nhận thông báo khi có kết quả mới.' }); }
      else this.saveSearchMessage.set({ ok: false, text: res.message || 'Không thể lưu tìm kiếm.' });
    });
  }

  clearAll(): void {
    this.q = this.title = this.author = this.publisher = '';
    this.content = this.ddc = this.callNumber = this.materialType = '';
    this.selectedStores = []; this.selectedAuthors = []; this.selectedYears = [];
    this.collectionId = null;
    this.docType = null;
    this.yearFrom = this.yearTo = null;
    this.availableOnly = false;
    this.sortBy = 'relevance';
    this.runSearch();
  }

  setDocType(t: DocType | null): void {
    this.docType = this.docType === t ? null : t;
    this.runSearch();
  }

  private toggleInArray<T>(arr: T[], value: T): T[] {
    return arr.includes(value) ? arr.filter(v => v !== value) : [...arr, value];
  }

  toggleYear(key: string): void {
    this.selectedYears = this.toggleInArray(this.selectedYears, Number(key));
    this.runSearch();
  }

  toggleMaterial(key: string): void {
    this.materialType = this.materialType === key ? '' : key;
    this.runSearch();
  }

  toggleStore(key: string): void {
    this.selectedStores = this.toggleInArray(this.selectedStores, key);
    this.runSearch();
  }

  toggleAuthorFacet(key: string): void {
    this.selectedAuthors = this.toggleInArray(this.selectedAuthors, key);
    this.runSearch();
  }

  onCollectionChange(): void {
    this.runSearch();
  }

  changePageSize(size: number): void {
    this.pageSize.set(size);
    this.runSearch();
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages() && p !== this.page()) {
      this.page.set(p);
      this.runSearch(false);
      if (typeof window !== 'undefined') window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  /** Tài liệu số → trang đọc đúng trang khớp; tài liệu in → trang chi tiết biểu ghi. */
  openItem(it: UnifiedItem): void {
    if (it.docType === 'print') {
      if (it.publicId) this.router.navigate(['/tai-lieu-in', it.publicId]);
      return;
    }
    if (it.docType === 'digital' && it.publicId) {
      if (it.ebookFileId && it.bestPageNumber) {
        this.router.navigate(['/page-view', it.ebookFileId, it.bestPageNumber]);
      } else {
        this.router.navigate(['/book', it.publicId]);
      }
      return;
    }
    if (it.publicId) this.router.navigate(['/book', it.publicId]);
  }

  trackByGroup = (_: number, it: UnifiedItem) => it.groupId;
}
