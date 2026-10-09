import { Component, inject, signal, computed, OnInit, PLATFORM_ID } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { isPlatformBrowser } from '@angular/common';
import { of } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { PrintBookApiService, PrintBookDetail, PrintBookHolding } from '../../services/print-book-api.service';
import { UnifiedSearchService, UnifiedItem } from '../../services/unified-search.service';
import { LibraryMapApiService, BookLocation } from '../../services/library-map-api.service';
import { ShelfLocationPinComponent } from '../../components/shelf-location-pin/shelf-location-pin';

type ExportTab = 'fields' | 'text' | 'card';

@Component({
  selector: 'app-print-book-detail',
  imports: [RouterLink, ShelfLocationPinComponent],
  templateUrl: './print-book-detail.html'
})
export class PrintBookDetailComponent implements OnInit {
  private printBookApi = inject(PrintBookApiService);
  private unifiedSearch = inject(UnifiedSearchService);
  private libraryMapApi = inject(LibraryMapApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);

  book    = signal<PrintBookDetail | null>(null);
  loading = signal(true);
  related = signal<UnifiedItem[]>([]);

  qrOpen       = signal(false);
  citationOpen = signal(false);
  exportOpen   = signal(false);
  exportTab    = signal<ExportTab>('fields');
  copied       = signal<string | null>(null);

  locationOpen    = signal(false);
  locationLoading = signal(false);
  location        = signal<BookLocation | null>(null);

  holdingsPage    = signal(1);
  holdingsPerPage = 10;
  holdingsTotalPages = computed(() => Math.ceil((this.book()?.holdings.length ?? 0) / this.holdingsPerPage) || 1);
  paginatedHoldings = computed(() => {
    const b = this.book();
    if (!b) return [];
    const start = (this.holdingsPage() - 1) * this.holdingsPerPage;
    return b.holdings.slice(start, start + this.holdingsPerPage);
  });
  holdingsPages = computed(() => {
    const total = this.holdingsTotalPages();
    const current = this.holdingsPage();

    let start = Math.max(1, current - 2);
    const end = Math.min(total, start + 4);

    if (end - start < 4) {
      start = Math.max(1, end - 4);
    }

    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  });

  currentUrl = computed(() => isPlatformBrowser(this.platformId) ? window.location.href : '');
  qrImageUrl = computed(() =>
    `https://api.qrserver.com/v1/create-qr-code/?size=220x220&data=${encodeURIComponent(this.currentUrl())}`);

  citationApa = computed(() => {
    const b = this.book();
    if (!b) return '';
    const author = b.author || 'Không rõ tác giả';
    const year   = b.publishYear ? `(${b.publishYear})` : '(k.n.)';
    return `${author}. ${year}. ${b.title}. ${b.publisher || 'Không rõ nhà xuất bản'}.`;
  });

  citationMla = computed(() => {
    const b = this.book();
    if (!b) return '';
    const author = b.author || 'Không rõ tác giả';
    return `${author}. ${b.title}. ${b.publisher || 'Không rõ nhà xuất bản'}, ${b.publishYear ?? 'k.n.'}.`;
  });

  citationBibtex = computed(() => {
    const b = this.book();
    if (!b) return '';
    const author = b.author || 'Không rõ tác giả';
    const key = `bib${b.bibId}_${b.publishYear ?? 'kn'}`;
    return [
      `@book{${key},`,
      `  title={${b.title}},`,
      `  author={${author}},`,
      `  year={${b.publishYear ?? ''}},`,
      `  publisher={${b.publisher || 'Không rõ nhà xuất bản'}},`,
      ...(b.isbn ? [`  isbn={${b.isbn}}`] : []),
      '}',
    ].join('\n');
  });

  citationRis = computed(() => {
    const b = this.book();
    if (!b) return '';
    const author = b.author || 'Không rõ tác giả';
    const lines = [
      'TY  - BOOK',
      `TI  - ${b.title}`,
      `AU  - ${author}`,
      `PY  - ${b.publishYear ?? ''}`,
      `PB  - ${b.publisher || 'Không rõ nhà xuất bản'}`,
    ];
    if (b.isbn) lines.push(`SN  - ${b.isbn}`);
    if (b.language) lines.push(`LA  - ${b.language}`);
    lines.push('ER  - ');
    return lines.join('\n');
  });

  private indDisplay(ind1?: string, ind2?: string): string {
    return `${ind1 || '\\'}${ind2 || '\\'}`;
  }

  marcTableRows = computed(() => {
    const b = this.book();
    if (!b) return [];
    const rows: { tag: string; ind: string; data: string; desc?: string }[] = [];
    for (const cf of b.controlFields) {
      rows.push({ tag: cf.field, ind: '', data: cf.value, desc: cf.description });
    }
    for (const m of b.marcFields) {
      rows.push({
        tag: m.field,
        ind: this.indDisplay(m.indicator1, m.indicator2),
        data: `$${m.subField} ${m.data}`,
        desc: m.subFieldDescription || m.fieldDescription,
      });
    }
    return rows;
  });

  marcText = computed(() => {
    const b = this.book();
    if (!b) return '';
    const lines: string[] = [];
    for (const cf of b.controlFields) lines.push(`${cf.field.padEnd(3)} ${cf.value}`);
    for (const m of b.marcFields) lines.push(`${m.field.padEnd(3)} ${this.indDisplay(m.indicator1, m.indicator2)} $${m.subField} ${m.data}`);
    return lines.join('\n');
  });

  ngOnInit(): void {
    this.route.paramMap.pipe(
      switchMap(params => {
        const id = params.get('id');
        this.book.set(null);
        this.related.set([]);
        this.loading.set(true);
        return id ? this.printBookApi.getDetail(id) : of(null);
      })
    ).subscribe(b => {
      this.book.set(b);
      this.loading.set(false);
      this.holdingsPage.set(1);
      if (b) this.loadRelated(b);
    });
  }

  goToHoldingsPage(page: number): void {
    if (page >= 1 && page <= this.holdingsTotalPages() && page !== this.holdingsPage()) {
      this.holdingsPage.set(page);
    }
  }

  // Đợt 22.2 — "Tài liệu liên quan" dùng more_like_this (title/author/keyword) thay vì chỉ lọc cùng DDC/
  // bộ sưu tập (kém liên quan hơn); giữ cách cũ làm dự phòng khi more_like_this không trả kết quả nào
  // (thường vì thiếu cả title/author/keyword — hiếm).
  private loadRelated(b: PrintBookDetail): void {
    this.unifiedSearch.similar('print', b.publicId, 6).subscribe(items => {
      if (items.length) this.related.set(items);
      else this.loadRelatedFallback(b);
    });
  }

  private loadRelatedFallback(b: PrintBookDetail): void {
    const filter = b.ddc
      ? { ddc: b.ddc }
      : b.collectionId ? { collectionId: b.collectionId } : null;
    if (!filter) return;

    this.unifiedSearch.search({ ...filter, pageSize: 7 }).subscribe(res => {
      this.related.set(res.items.filter(it => it.publicId !== b.publicId).slice(0, 6));
    });
  }

  isAvailable(h: PrintBookHolding): boolean {
    return (h.statusName || '').toLowerCase().includes('sẵn sàng');
  }

  viewLocation(h: PrintBookHolding): void {
    this.locationOpen.set(true);
    this.locationLoading.set(true);
    this.location.set(null);
    this.libraryMapApi.locateBarcode(h.barcodeId).subscribe(loc => {
      this.location.set(loc);
      this.locationLoading.set(false);
    });
  }

  closeLocation(): void {
    this.locationOpen.set(false);
    this.location.set(null);
  }

  goToMap(): void {
    const loc = this.location();
    if (!loc?.found || !loc.floorId) return;
    const params: Record<string, string> = { floorId: String(loc.floorId) };
    if (loc.shelfObjectId) params['highlightObjectId'] = String(loc.shelfObjectId);
    if (loc.shelfRowId) params['highlightRowId'] = String(loc.shelfRowId);
    if (loc.entrancePositionX != null && loc.entrancePositionY != null) {
      params['entranceX'] = String(loc.entrancePositionX);
      params['entranceY'] = String(loc.entrancePositionY);
    }
    this.router.navigate(['/so-do-thu-vien'], { queryParams: params });
  }

  copyText(text: string, key: string): void {
    if (!isPlatformBrowser(this.platformId)) return;
    navigator.clipboard.writeText(text).then(() => {
      this.copied.set(key);
      setTimeout(() => this.copied.set(null), 1500);
    });
  }

  downloadText(content: string, filename: string, mime = 'text/plain'): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const blob = new Blob([content], { type: mime });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href = url; a.download = filename; a.click();
    URL.revokeObjectURL(url);
  }

  downloadMarcBinary(): void {
    const b = this.book();
    if (!b || !isPlatformBrowser(this.platformId)) return;
    this.printBookApi.getMarcBinary(b.publicId).subscribe(buf => {
      const blob = new Blob([buf], { type: 'application/octet-stream' });
      const url  = URL.createObjectURL(blob);
      const a    = document.createElement('a');
      a.href = url; a.download = `mfn-${b.mfn ?? b.bibId}.mrc`; a.click();
      URL.revokeObjectURL(url);
    });
  }
}
