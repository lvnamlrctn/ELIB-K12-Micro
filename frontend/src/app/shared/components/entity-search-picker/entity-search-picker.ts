import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { Observable } from 'rxjs';

export interface EntityPage<T> { items: T[]; total: number; }

export interface EntitySearchFilters {
  keyword?: string;
  title?: string;
  author?: string;
  publisher?: string;
  publishDate?: string;
  mfnFrom?: number | null;
  mfnTo?: number | null;
}

export type AdvancedFilterField = 'title' | 'author' | 'publisher' | 'publishDate' | 'mfnFrom' | 'mfnTo';

/** Modal tìm-và-chọn 1 bản ghi bất kỳ (Bib, EbookDocument, ...) — dùng chung cho các tính năng liên kết. */
@Component({
  selector: 'app-entity-search-picker',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, TranslateModule],
  templateUrl: './entity-search-picker.html'
})
export class EntitySearchPickerComponent<T = any> {
  @Input() title = '';
  @Input() searchPlaceholder = '';
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  @Input() fetchPage!: (filters: EntitySearchFilters, page: number, pageSize: number) => Observable<EntityPage<T>>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  @Input() displayFn: (item: T) => string = (item: any) => String(item?.title ?? item?.name ?? item?.id ?? '');
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  @Input() subDisplayFn: (item: T) => string = () => '';
  /** Dòng phụ thứ 3 trong mỗi kết quả (vd năm xuất bản) — chỉ hiện nếu trả về chuỗi khác rỗng. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  @Input() extraDisplayFn?: (item: T) => string;
  /** Rỗng (mặc định) = 1 ô tìm kiếm gộp như cũ. Có giá trị = ẩn ô gộp, hiện đúng các ô lọc riêng này. */
  @Input() advancedFields: AdvancedFilterField[] = [];

  @Output() picked = new EventEmitter<T>();

  isOpen = signal(false);
  isLoading = signal(false);
  items = signal<T[]>([]);
  filters = signal<EntitySearchFilters>({});
  page = signal(1);
  total = signal(0);
  pageSize = 10;

  open(): void {
    this.filters.set({});
    this.page.set(1);
    this.isOpen.set(true);
    this.load();
  }

  close(): void {
    this.isOpen.set(false);
  }

  updateFilter(field: keyof EntitySearchFilters, value: string | number | null): void {
    this.filters.update(f => ({ ...f, [field]: value }));
  }

  search(): void {
    this.page.set(1);
    this.load();
  }

  prevPage(): void {
    if (this.page() <= 1) return;
    this.page.set(this.page() - 1);
    this.load();
  }

  nextPage(): void {
    if (this.page() * this.pageSize >= this.total()) return;
    this.page.set(this.page() + 1);
    this.load();
  }

  private load(): void {
    if (!this.fetchPage) return;
    this.isLoading.set(true);
    this.fetchPage(this.filters(), this.page(), this.pageSize).subscribe(res => {
      this.items.set(res.items);
      this.total.set(res.total);
      this.isLoading.set(false);
    });
  }

  choose(item: T): void {
    this.picked.emit(item);
    this.close();
  }
}
