import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, ImportClient, ImportError, ImportResult, errorMessage, saveFile } from '../core/api';
import { ToastrService } from './toastr';
import { Modal } from './ui';

/**
 * Nhập danh mục từ Excel (monolith: nút Import của từng danh mục). Tải file mẫu → chọn file .xlsx → nhập.
 * Có dòng lỗi thì server không ghi dòng nào; hộp thoại liệt kê lỗi theo số dòng để sửa file rồi nhập lại.
 */
@Component({
  selector: 'app-import-dialog',
  imports: [FormsModule, Modal],
  template: `
    <app-modal [title]="'Nhập ' + title().toLowerCase() + ' từ Excel'" widthClass="max-w-lg" (closed)="closed.emit()">
      <ol class="text-sm text-gray-600 space-y-3 list-decimal pl-5">
        <li>
          Tải <button type="button" class="text-blue-600 hover:underline font-medium" (click)="downloadTemplate()" [disabled]="busy()">file mẫu</button>,
          điền mỗi dòng một mục (giữ nguyên dòng tiêu đề).
        </li>
        <li>
          Chọn file đã điền (.xlsx, tối đa 5 MB, 5.000 dòng):
          <input type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                 class="mt-2 block w-full text-sm file:mr-3 file:py-1.5 file:px-3 file:rounded-lg file:border-0 file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
                 (change)="pick($event)" />
        </li>
      </ol>
      <label class="flex items-center gap-2 text-sm text-gray-700 mt-4 cursor-pointer select-none">
        <input type="checkbox" class="rounded border-gray-300" [(ngModel)]="skipDuplicates" />
        Bỏ qua dòng trùng với mục đã có (không báo lỗi)
      </label>

      @if (errors().length) {
        <div class="mt-4 alert-error !block">
          <div class="font-medium mb-2">{{ summary() }}</div>
          <div class="max-h-56 overflow-y-auto bg-white rounded border border-red-100">
            <table class="w-full text-xs">
              <thead><tr class="text-left text-gray-500"><th class="px-2 py-1 w-16">Dòng</th><th class="px-2 py-1">Lỗi</th></tr></thead>
              <tbody>
                @for (e of errors(); track e.row) {
                  <tr class="border-t border-red-50"><td class="px-2 py-1 font-mono">{{ e.row }}</td><td class="px-2 py-1 text-gray-700">{{ e.message }}</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">Đóng</button>
        <button type="button" class="btn-primary" [disabled]="busy() || !file()" (click)="run()">
          <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'upload_file' }}</span> Nhập
        </button>
      </ng-container>
    </app-modal>
  `,
})
export class ImportDialog implements OnInit {
  readonly title = input.required<string>();
  readonly resource = input.required<string>();
  readonly service = input('tenant');
  readonly closed = output<void>();
  /** Đã nhập xong (danh sách cần tải lại). */
  readonly imported = output<void>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private client!: ImportClient;

  protected skipDuplicates = false;
  protected readonly file = signal<File | null>(null);
  protected readonly busy = signal(false);
  protected readonly errors = signal<ImportError[]>([]);
  protected readonly summary = signal('');

  ngOnInit(): void {
    this.client = this.api.importer(this.resource(), this.service());
  }

  protected pick(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
    this.errors.set([]);
  }

  protected async downloadTemplate(): Promise<void> {
    try {
      saveFile(await this.client.template(), `Mau nhap ${this.title()}.xlsx`);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async run(): Promise<void> {
    const file = this.file();
    if (!file) return;
    this.busy.set(true);
    this.errors.set([]);
    try {
      const result = await this.client.run(file, this.skipDuplicates);
      this.toastr.success(result.detail);
      this.imported.emit();
      this.closed.emit();
    } catch (e) {
      const body = e instanceof HttpErrorResponse ? (e.error as ImportResult | null) : null;
      if (body?.errors?.length) {
        this.errors.set(body.errors);
        this.summary.set(body.detail);
      } else {
        this.toastr.error(errorMessage(e));
      }
    } finally {
      this.busy.set(false);
    }
  }
}
