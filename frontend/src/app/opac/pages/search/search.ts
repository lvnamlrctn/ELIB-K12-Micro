import { TranslateModule } from '@ngx-translate/core';
import { Component, inject, signal, computed, OnInit, AfterViewInit, OnDestroy, PLATFORM_ID } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { BookApiService } from '../../services/book-api.service';
import { Book } from '../../services/models';
import { FormsModule } from '@angular/forms';
import { isPlatformBrowser } from '@angular/common';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
declare const $: any;

@Component({
  selector: 'app-search',
  imports: [TranslateModule, RouterLink, FormsModule],
  templateUrl: './search.html'
})
export class SearchComponent implements OnInit, AfterViewInit, OnDestroy {
  private bookApi = inject(BookApiService);
  private route = inject(ActivatedRoute);
  private platformId = inject(PLATFORM_ID);
  private isBrowser = isPlatformBrowser(this.platformId);
  
  searchQuery = '';
  searchAuthor = '';
  searchYear = '';
  searchCollection = '';
  searchPublisher = '';
  searchKeyword = '';
  sortBy = 'publishDate';
  sortOrder = 'DESC';
  
  results = signal<Book[]>([]);
  totalCount = signal(0);
  filterType = signal<string | null>(null);

  currentPage = signal(1);
  itemsPerPage = 8;

  // Bộ sưu tập theo cây (Đợt 20): depth dùng để thụt lề trong ô chọn.
  collections = signal<{id: string, title: string, depth: number}[]>([]);

  totalPages = computed(() => Math.ceil(this.totalCount() / this.itemsPerPage) || 1);
  paginatedResults = computed(() => this.results());
  pages = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    
    let start = Math.max(1, current - 2);
    const end = Math.min(total, start + 4);
    
    if (end - start < 4) {
      start = Math.max(1, end - 4);
    }
    
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  });

  private _allMultimediaCache: Book[] = [];

  ngOnInit() {
    this.bookApi.getCollectionTree().subscribe(cols => {
      this.collections.set(cols.map(c => ({ id: c.id, title: c.title, depth: c.depth })));
      // Re-init select2 data if already initialized
      if (typeof $ !== 'undefined' && $.fn.select2) {
        setTimeout(() => this.initSelect2(), 50);
      }
    });

    this.route.queryParamMap.subscribe(params => {
      const type = params.get('type');
      const collection = params.get('collection');
      const query = params.get('query');
      const author = params.get('author');
      
      if (collection) {
        this.searchCollection = collection;
      }
      if (query) {
        this.searchQuery = query;
      }
      if (author) {
        this.searchAuthor = author;
      }
      
      if (type === 'multimedia') {
        this.filterType.set('multimedia');
        this.fetchMultimedia();
      } else {
        this.filterType.set(null);
        this.onSearch();
      }

      // If already view initialized, sync Select2
      if (this.isBrowser && typeof $ !== 'undefined' && $.fn.select2) {
        if (collection) {
          $('#searchCollection').val(collection).trigger('change.select2');
        }
      }
    });
  }

  ngAfterViewInit() {
    if (this.isBrowser) {
      this.initSelect2();
    }
  }

  initSelect2() {
    if (typeof $ !== 'undefined' && $.fn.select2) {
      $('#searchCollection').select2({
        placeholder: '-- Tất cả bộ sưu tập --',
        allowClear: true,
        width: '100%',
        theme: 'classic'
      }).on('change', (e: Event) => {
        const target = e.target as HTMLSelectElement;
        this.searchCollection = target.value;
        this.onSearch();
      });

      // Set initial values if they come from query params
      if (this.searchCollection) {
        $('#searchCollection').val(this.searchCollection).trigger('change.select2');
      }
      
      // Fix for select2 inside containers where it might get cut off
      $('.select2-container').addClass('text-sm');
    } else {
      setTimeout(() => this.initSelect2(), 200);
    }
  }

  fetchMultimedia() {
    this.bookApi.getBooks().subscribe(books => {
      this._allMultimediaCache = books.filter(b => 
        b.files.some(f => f.type === 'audio' || f.type === 'video')
      );
      this.totalCount.set(this._allMultimediaCache.length);
      this.currentPage.set(1);
      this._updateMultimediaPage();
    });
  }

  private _updateMultimediaPage() {
    const start = (this.currentPage() - 1) * this.itemsPerPage;
    this.results.set(this._allMultimediaCache.slice(start, start + this.itemsPerPage));
  }

  onSearch(resetPage = true) {
    if (resetPage) this.currentPage.set(1);
    const orderStr = `${this.sortBy} ${this.sortOrder}`;
    this.bookApi.searchBooks(
        this.searchQuery, 
        this.searchAuthor, 
        this.searchYear, 
        this.searchCollection, 
        this.currentPage(), 
        this.itemsPerPage,
        this.searchPublisher,
        this.searchKeyword,
        orderStr
    ).subscribe(res => {
      this.results.set(res.items);
      this.totalCount.set(res.totalCount);
    });
  }

  clearFilters() {
    this.searchQuery = '';
    this.searchAuthor = '';
    this.searchYear = '';
    this.searchCollection = '';
    this.searchPublisher = '';
    this.searchKeyword = '';
    this.sortBy = 'publishDate';
    this.sortOrder = 'DESC';
    
    if (this.isBrowser && typeof $ !== 'undefined' && $.fn.select2) {
      $('#searchCollection').val('').trigger('change.select2');
    }
    
    if (this.filterType() === 'multimedia') {
      this.fetchMultimedia();
    } else {
      this.onSearch();
    }
  }

  goToPage(page: number) {
    if (page >= 1 && page <= this.totalPages() && page !== this.currentPage()) {
      this.currentPage.set(page);
      if (this.filterType() === 'multimedia') {
        this._updateMultimediaPage();
      } else {
        this.onSearch(false);
      }
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  ngOnDestroy() {
    if (this.isBrowser && typeof $ !== 'undefined' && $.fn.select2) {
      $('#searchCollection').off('change').select2('destroy');
    }
  }
}
