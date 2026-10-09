import { Component, EventEmitter, Input, Output, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { TranslateModule } from '@ngx-translate/core';
import { Observable, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs/operators';

/** Combobox dùng chung kiểu select2 (Đợt 25): bọc ng-select với 2 chế độ dữ liệu —
 * `items` tĩnh (danh sách đã tải sẵn, giống các trang admin đang dùng ng-select trực tiếp) hoặc
 * `loadFn` bất đồng bộ (gõ để tìm kiếm phía server, debounce 300ms) — đây là phần "select2 thật"
 * (async, có debounce) mà cả ELIB lẫn ELIB-LRC chưa có sẵn ở dạng component tái dùng được.
 * Theo đúng bài học từ tenant-filter-select.ts: `host: display:contents` để không thêm thẻ bao
 * ngoài, tránh phá layout grid/flex của trang cha. */
@Component({
  selector: 'app-searchable-select',
  standalone: true,
  imports: [CommonModule, FormsModule, NgSelectModule, TranslateModule],
  templateUrl: './searchable-select.html',
  host: { style: 'display: contents' }
})
export class SearchableSelectComponent<T = any> implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  @Input() items: T[] | null = null;
  @Input() loadFn?: (query: string) => Observable<T[]>;
  @Input() bindLabel = 'name';
  @Input() bindValue = 'id';
  @Input() placeholder = '';
  @Input() clearable = true;
  @Input() multiple = false;
  @Input() disabled = false;

  @Input() value: any = null;
  @Output() valueChange = new EventEmitter<any>();

  loading = false;
  searchInput$ = new Subject<string>();

  ngOnInit(): void {
    if (!this.loadFn) return;
    this.searchInput$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(query => {
        this.loading = true;
        return this.loadFn!(query);
      }),
      takeUntil(this.destroy$)
    ).subscribe(results => {
      this.items = results;
      this.loading = false;
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onChange(v: any): void {
    this.valueChange.emit(v);
  }
}
