import { Component, ElementRef, HostListener, inject, input, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ColumnPrefs } from '../core/preferences';

/**
 * Nút chọn cột hiện/ẩn của bảng (ELIB-LRC: AdminPreference) — lựa chọn nhớ theo tài khoản qua `columnPrefs()`.
 * Đặt ở thanh công cụ phía phải (menu mở về bên trái, không tràn màn hình).
 */
@Component({
  selector: 'app-column-picker',
  imports: [TranslatePipe],
  template: `
    <div class="relative">
      <button type="button" class="btn-secondary !py-1.5 !px-2" [title]="'COMMON.COLUMNS' | translate" (click)="open.set(!open())">
        <span class="material-icons text-[18px]">view_column</span></button>
      @if (open()) {
        <div class="absolute right-0 mt-1 w-60 max-h-[70vh] overflow-y-auto bg-white border border-gray-200 rounded-lg shadow-lg p-2 z-30">
          <div class="flex items-center justify-between px-1 pb-1">
            <span class="text-xs font-semibold text-gray-500">{{ 'COMMON.COLUMNS' | translate }}</span>
            <button type="button" class="text-xs text-blue-600 hover:underline" (click)="prefs().reset()">{{ 'COMMON.COLUMNS_RESET' | translate }}</button>
          </div>
          @for (col of prefs().columns; track col.key) {
            <label class="flex items-center gap-2 px-1 py-0.5 text-sm text-gray-700 cursor-pointer select-none">
              <input type="checkbox" class="rounded border-gray-300" [checked]="prefs().shown().has(col.key)" (change)="prefs().toggle(col.key)" /> {{ col.label | translate }}
            </label>
          }
        </div>
      }
    </div>
  `,
})
export class ColumnPicker {
  readonly prefs = input.required<ColumnPrefs<string>>();
  protected readonly open = signal(false);
  private readonly host = inject(ElementRef<HTMLElement>);

  @HostListener('document:click', ['$event'])
  protected outside(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.open.set(false);
  }
}
