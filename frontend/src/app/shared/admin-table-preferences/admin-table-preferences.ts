import { Component, DestroyRef, ElementRef, EventEmitter, HostListener, Input, OnInit, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AdminPreferenceService, AdminTableSettings } from '../../services/system/admin-preference.service';

export interface TablePrefColumn {
  /** Khoá cột — trùng matColumnDef của bảng. */
  key: string;
  /** Khoá i18n tiêu đề cột. */
  label: string;
  /** false = mặc định ẩn (cột tuỳ chọn). */
  defaultVisible?: boolean;
}

const PAGE_SIZES = [5, 10, 20, 25, 50, 100];

/** `automatic` = áp lúc vào trang (mặc định/bản đã lưu) — trang cha có thể giữ trang & số dòng vừa khôi phục qua
 *  ListPageStateService khi quay lại từ trang chi tiết; false = người dùng vừa thao tác (bật cột, tải lại, mặc định). */
export interface TablePrefsApply { settings: AdminTableSettings; automatic: boolean; }

/** Cột hiển thị ban đầu của trang (trước khi cấu hình tài khoản tải về). */
export function defaultVisibleColumns(columns: TablePrefColumn[]): string[] {
  return columns.filter(c => c.defaultVisible !== false).map(c => c.key);
}

/** Trang cha dùng để biết cấu hình mới có đổi bộ lọc/số dòng (cần tải lại dữ liệu) hay chỉ đổi cột. */
export function sameFilters(a: Record<string, unknown>, b: Record<string, unknown>): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/** Chuẩn hoá cấu hình tải về: bỏ cột/bộ lọc lạ (vd cột đã bị bỏ khỏi trang), số dòng ngoài danh sách → mặc định. */
export function normalizeTableSettings(raw: AdminTableSettings, columns: string[], defaults: AdminTableSettings): AdminTableSettings {
  const filters = { ...defaults.filters };
  for (const key of Object.keys(filters)) {
    const value = raw.filters?.[key];
    if (value === null || typeof value === 'string' || typeof value === 'number') filters[key] = value;
  }
  return {
    version: 1,
    pageSize: PAGE_SIZES.includes(raw.pageSize) ? raw.pageSize : defaults.pageSize,
    columns: Array.isArray(raw.columns) ? [...new Set(raw.columns.filter(c => columns.includes(c)))] : defaults.columns,
    filters,
  };
}

/**
 * Cấu hình bảng theo tài khoản (Đợt 21 — port từ ELIB-LRC `AdminTablePreferences`, thay `app-column-toggle` nhớ theo
 * trình duyệt): chọn cột hiển thị, đổi thứ tự cột, lưu cùng bộ lọc + số dòng hiện tại lên máy chủ. Lưu là thao tác
 * chủ động; API lỗi thì bảng vẫn dùng bình thường với cấu hình đang có. Trang cha cung cấp `read()` (trạng thái hiện
 * tại) và nhận `apply` (cấu hình cần áp dụng).
 */
@Component({
  selector: 'app-admin-table-preferences',
  standalone: true,
  imports: [MatIconModule, TranslateModule],
  template: `
    <div class="relative inline-block print:hidden">
      <button type="button" (click)="open.set(!open())" [attr.aria-expanded]="open()"
              class="border border-gray-300 text-gray-600 hover:bg-gray-50 text-sm font-medium py-1.5 px-3 rounded-lg flex items-center gap-1 transition-colors"
              [class.bg-blue-50]="open()" [class.text-blue-700]="open()">
        <mat-icon class="text-[16px]">tune</mat-icon> {{ 'TABLE_PREFS.BUTTON' | translate }}
      </button>
      @if (open()) {
        <div class="absolute right-0 mt-1 w-80 max-w-[90vw] bg-white rounded-xl shadow-lg border border-gray-200 z-50 p-3 text-sm">
          <p class="text-xs text-gray-400 mb-2">{{ 'TABLE_PREFS.HINT' | translate }}</p>
          <div class="flex flex-col gap-1 max-h-72 overflow-y-auto">
            @for (key of orderedKeys(); track key; let i = $index) {
              <div class="flex items-center gap-2 pl-2 pr-1 py-1 rounded-lg border border-gray-100 bg-gray-50/60">
                <input type="checkbox" class="w-4 h-4 rounded border-gray-300 text-blue-600 cursor-pointer"
                       [checked]="read().columns.includes(key)" (change)="toggle(key)" [attr.aria-label]="label(key) | translate" />
                <span class="flex-1 text-gray-700 truncate">{{ label(key) | translate }}</span>
                <button type="button" [disabled]="i === 0 || !read().columns.includes(key)" (click)="move(key, -1)"
                        [attr.aria-label]="'TABLE_PREFS.MOVE_UP' | translate"
                        class="w-6 h-6 flex items-center justify-center rounded text-gray-400 hover:bg-gray-200 hover:text-gray-600 disabled:opacity-30 disabled:hover:bg-transparent">
                  <mat-icon class="text-[16px]">arrow_upward</mat-icon>
                </button>
                <button type="button" [disabled]="!read().columns.includes(key) || i >= read().columns.length - 1" (click)="move(key, 1)"
                        [attr.aria-label]="'TABLE_PREFS.MOVE_DOWN' | translate"
                        class="w-6 h-6 flex items-center justify-center rounded text-gray-400 hover:bg-gray-200 hover:text-gray-600 disabled:opacity-30 disabled:hover:bg-transparent">
                  <mat-icon class="text-[16px]">arrow_downward</mat-icon>
                </button>
              </div>
            }
          </div>
          <div class="flex flex-wrap gap-1.5 mt-3 pt-3 border-t border-gray-100">
            <button type="button" (click)="save()" [disabled]="busy() || !available()"
                    class="px-3 py-1.5 rounded-lg font-medium bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-40">{{ 'TABLE_PREFS.SAVE' | translate }}</button>
            <button type="button" (click)="load(true)" [disabled]="busy()"
                    class="px-3 py-1.5 rounded-lg font-medium text-gray-600 hover:bg-gray-100 disabled:opacity-40">{{ 'TABLE_PREFS.RELOAD' | translate }}</button>
            <button type="button" (click)="reset()" [disabled]="busy()"
                    class="px-3 py-1.5 rounded-lg font-medium text-gray-600 hover:bg-gray-100 disabled:opacity-40">{{ 'TABLE_PREFS.RESET' | translate }}</button>
          </div>
          <p role="status" class="text-xs text-gray-500 mt-2 min-h-4">{{ message() }}</p>
        </div>
      }
    </div>`
})
export class AdminTablePreferencesComponent implements OnInit {
  @Input({ required: true }) pageKey = '';
  @Input({ required: true }) columns: TablePrefColumn[] = [];
  @Input({ required: true }) read!: () => AdminTableSettings;
  /** Số dòng mặc định của trang — lúc khởi tạo, read() có thể đang mang số dòng khôi phục từ lần xem trước. */
  @Input() defaultPageSize?: number;
  @Output() apply = new EventEmitter<TablePrefsApply>();

  private service = inject(AdminPreferenceService);
  private translate = inject(TranslateService);
  private destroy = inject(DestroyRef);
  private host = inject(ElementRef<HTMLElement>);
  private revision = 0;
  private defaults!: AdminTableSettings;

  busy = signal(false);
  available = signal(false);
  open = signal(false);
  message = signal('');

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.open.set(false);
  }

  ngOnInit(): void {
    this.defaults = structuredClone(this.read());
    this.defaults.columns = defaultVisibleColumns(this.columns);
    if (this.defaultPageSize) this.defaults.pageSize = this.defaultPageSize;
    this.apply.emit({ settings: structuredClone(this.defaults), automatic: true });
    this.load(false);
  }

  label(key: string): string { return this.columns.find(c => c.key === key)?.label ?? key; }

  /** Cột đang hiện theo thứ tự đã chọn, rồi tới các cột đang ẩn. */
  orderedKeys(): string[] {
    const visible = this.read().columns;
    return [...visible, ...this.columns.map(c => c.key).filter(c => !visible.includes(c))];
  }

  toggle(key: string): void {
    const value = this.read();
    this.apply.emit({ settings: { ...value, columns: value.columns.includes(key) ? value.columns.filter(c => c !== key) : [...value.columns, key] }, automatic: false });
  }

  move(key: string, direction: number): void {
    const value = this.read();
    const columns = this.orderedKeys();
    const i = columns.indexOf(key), next = i + direction;
    if (next < 0 || next >= columns.length) return;
    [columns[i], columns[next]] = [columns[next], columns[i]];
    this.apply.emit({ settings: { ...value, columns: columns.filter(c => value.columns.includes(c)) }, automatic: false });
  }

  load(force: boolean): void {
    if (this.busy()) return;
    const before = JSON.stringify(this.read());
    this.busy.set(true);
    this.message.set(this.translate.instant('TABLE_PREFS.LOADING'));
    this.service.get(this.pageKey).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: response => {
        this.busy.set(false);
        this.available.set(true);
        this.revision = response?.revision ?? 0;
        // Người dùng đã đổi bộ lọc/cột trong lúc chờ tải — không ghi đè lựa chọn đang chỉnh.
        if (!force && JSON.stringify(this.read()) !== before) { this.message.set(this.translate.instant('TABLE_PREFS.KEPT_CURRENT')); return; }
        if (response?.settings?.version === 1)
          this.apply.emit({ settings: normalizeTableSettings(response.settings, this.columns.map(c => c.key), this.defaults), automatic: !force });
        else if (force) this.apply.emit({ settings: structuredClone(this.defaults), automatic: false });
        this.message.set(this.translate.instant(response?.settings ? 'TABLE_PREFS.LOADED' : 'TABLE_PREFS.NONE_SAVED'));
      },
      error: () => {
        this.busy.set(false);
        this.available.set(false);
        this.message.set(this.translate.instant('TABLE_PREFS.LOAD_FAILED'));
      }
    });
  }

  reset(): void {
    this.apply.emit({ settings: structuredClone(this.defaults), automatic: false });
    this.message.set(this.translate.instant('TABLE_PREFS.RESET_DONE'));
  }

  save(): void {
    if (this.busy() || !this.available()) return;
    const settings = normalizeTableSettings(this.read(), this.columns.map(c => c.key), this.defaults);
    this.busy.set(true);
    this.service.save(this.pageKey, this.revision, settings).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: response => {
        this.revision = response?.revision ?? this.revision + 1;
        this.busy.set(false);
        this.message.set(this.translate.instant('TABLE_PREFS.SAVED'));
      },
      error: e => {
        this.busy.set(false);
        if (e?.status === 409) this.available.set(false);
        this.message.set(this.translate.instant(e?.status === 409 ? 'TABLE_PREFS.CONFLICT' : 'TABLE_PREFS.SAVE_FAILED'));
      }
    });
  }
}
