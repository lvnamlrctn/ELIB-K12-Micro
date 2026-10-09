import { Component, computed, input, output } from '@angular/core';

/** Phân trang — kiểu mat-paginator đã chỉnh style của frontend monolith (số dòng/trang, "1 – 10 / 55", nút trang). */
@Component({
  selector: 'app-paginator',
  template: `
    <div class="flex flex-col sm:flex-row items-center justify-between gap-4 px-6 py-3 border-t border-gray-100">
      <div class="flex items-center gap-2">
        <span class="text-sm font-medium text-gray-500">Số dòng mỗi trang</span>
        <select class="px-3 py-1.5 text-sm font-semibold text-gray-700 bg-gray-50 border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                [value]="pageSize()" (change)="changeSize($any($event.target).value)">
          @for (size of sizes(); track size) { <option [value]="size">{{ size }}</option> }
        </select>
      </div>
      <div class="flex items-center gap-1">
        <span class="text-sm font-medium text-gray-600 mx-4">{{ range() }}</span>
        @for (b of buttons; track b.icon) {
          <button type="button" [disabled]="b.disabled()" (click)="go(b.target())"
                  class="w-9 h-9 flex items-center justify-center text-gray-500 bg-white border border-gray-200 rounded-lg shadow-sm transition-colors hover:enabled:bg-gray-100 hover:enabled:text-gray-700 disabled:opacity-50 disabled:bg-gray-50 disabled:shadow-none">
            <span class="material-icons text-[20px]">{{ b.icon }}</span>
          </button>
        }
      </div>
    </div>
  `,
})
export class Paginator {
  readonly total = input.required<number>();
  /** Trang bắt đầu từ 1 (như PageIndex của API). */
  readonly pageIndex = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly sizes = input<number[]>([10, 25, 50, 100]);
  readonly pageChange = output<{ pageIndex: number; pageSize: number }>();

  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));
  protected readonly range = computed(() => {
    if (this.total() === 0) return '0 / 0';
    const from = (this.pageIndex() - 1) * this.pageSize() + 1;
    return `${from} – ${Math.min(this.total(), from + this.pageSize() - 1)} / ${this.total()}`;
  });

  protected readonly buttons = [
    { icon: 'first_page', target: () => 1, disabled: () => this.pageIndex() <= 1 },
    { icon: 'chevron_left', target: () => this.pageIndex() - 1, disabled: () => this.pageIndex() <= 1 },
    { icon: 'chevron_right', target: () => this.pageIndex() + 1, disabled: () => this.pageIndex() >= this.pages() },
    { icon: 'last_page', target: () => this.pages(), disabled: () => this.pageIndex() >= this.pages() },
  ];

  protected go(page: number): void {
    this.pageChange.emit({ pageIndex: page, pageSize: this.pageSize() });
  }

  protected changeSize(value: string): void {
    this.pageChange.emit({ pageIndex: 1, pageSize: Number(value) });
  }
}

/** Hộp thoại có tiêu đề, thân cuộn được và chân nút — như modal thêm/sửa của các màn admin cũ. */
@Component({
  selector: 'app-modal',
  template: `
    <div class="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl shadow-2xl w-full max-h-[92vh] overflow-hidden flex flex-col" [class]="widthClass()">
        <div class="p-5 border-b border-gray-100 flex justify-between items-center bg-gray-50 shrink-0">
          <h3 class="text-lg font-bold text-gray-800">{{ title() }}</h3>
          <button type="button" (click)="closed.emit()" class="w-8 h-8 flex items-center justify-center rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors">
            <span class="material-icons text-[18px]">close</span>
          </button>
        </div>
        <div class="overflow-y-auto flex-1 p-6"><ng-content /></div>
        <div class="p-4 border-t border-gray-100 flex gap-3 justify-end shrink-0 bg-gray-50"><ng-content select="[footer]" /></div>
      </div>
    </div>
  `,
})
export class Modal {
  readonly title = input.required<string>();
  readonly widthClass = input('max-w-md');
  readonly closed = output<void>();
}

/** Xác nhận xoá — biểu tượng cảnh báo đỏ như bản cũ. */
@Component({
  selector: 'app-confirm-delete',
  template: `
    <div class="fixed inset-0 z-[110] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl shadow-2xl w-full max-w-sm overflow-hidden">
        <div class="p-6 text-center">
          <div class="w-16 h-16 bg-red-50 text-red-600 rounded-full flex items-center justify-center mx-auto mb-4">
            <span class="material-icons text-3xl">warning</span>
          </div>
          <h3 class="text-lg font-bold text-gray-800 mb-2">{{ title() }}</h3>
          <p class="text-gray-600 text-sm mb-2">{{ message() }}</p>
          @if (warning()) {
            <p class="text-amber-600 text-xs mt-2 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">{{ warning() }}</p>
          }
          <div class="flex gap-3 mt-5">
            <button type="button" (click)="cancelled.emit()" class="btn-secondary flex-1">Huỷ</button>
            <button type="button" (click)="confirmed.emit()" [disabled]="busy()" class="btn-danger flex-1">{{ confirmText() }}</button>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class ConfirmDelete {
  readonly title = input('Xác nhận xoá');
  readonly message = input('Bạn có chắc chắn muốn xoá? Thao tác này không thể hoàn tác.');
  readonly warning = input<string | null>(null);
  readonly confirmText = input('Xoá');
  readonly busy = input(false);
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();
}

/** Lớp phủ khi đang tải bảng. */
@Component({
  selector: 'app-loading',
  template: `
    <div class="absolute inset-0 bg-white/60 z-10 flex flex-col items-center justify-center backdrop-blur-[1px]">
      <span class="material-icons animate-spin text-blue-600 text-4xl mb-2">autorenew</span>
      <p class="text-blue-600 font-medium">Đang tải dữ liệu...</p>
    </div>
  `,
})
export class Loading {}

/** Nhãn trạng thái 2 = hoạt động / 1 = không hoạt động. */
@Component({
  selector: 'app-status-badge',
  template: `
    @if (status() === 2) {
      <span class="badge-on"><span class="w-1.5 h-1.5 rounded-full bg-green-500"></span>{{ activeText() }}</span>
    } @else {
      <span class="badge-off"><span class="w-1.5 h-1.5 rounded-full bg-gray-400"></span>{{ inactiveText() }}</span>
    }
  `,
})
export class StatusBadge {
  readonly status = input.required<number>();
  readonly activeText = input('Hoạt động');
  readonly inactiveText = input('Không hoạt động');
}
