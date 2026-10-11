import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../core/i18n';
import { Api, ImportClient, ImportError, ImportResult, errorMessage, saveFile } from '../core/api';
import { ToastrService } from './toastr';
import { Modal } from './ui';

/**
 * Nhập danh mục từ Excel (monolith: nút Import của từng danh mục). Tải file mẫu → chọn file .xlsx → nhập.
 * Có dòng lỗi thì server không ghi dòng nào; hộp thoại liệt kê lỗi theo số dòng để sửa file rồi nhập lại.
 */
@Component({
  selector: 'app-import-dialog',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal [title]="language.t('IMPORT.TITLE', { name: language.t(title()) })" widthClass="max-w-lg" (closed)="closed.emit()">
      <ol class="text-sm text-gray-600 space-y-3 list-decimal pl-5">
        <li>
          {{ 'IMPORT.STEP_TEMPLATE_BEFORE' | translate }}
          <button type="button" class="text-blue-600 hover:underline font-medium" (click)="downloadTemplate()" [disabled]="busy()">{{ 'IMPORT.TEMPLATE_FILE' | translate }}</button>{{ 'IMPORT.STEP_TEMPLATE_AFTER' | translate }}
        </li>
        <li>
          {{ 'IMPORT.STEP_FILE' | translate }}
          <input type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                 class="mt-2 block w-full text-sm file:mr-3 file:py-1.5 file:px-3 file:rounded-lg file:border-0 file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
                 (change)="pick($event)" />
        </li>
      </ol>
      <label class="flex items-center gap-2 text-sm text-gray-700 mt-4 cursor-pointer select-none">
        <input type="checkbox" class="rounded border-gray-300" [(ngModel)]="skipDuplicates" />
        {{ 'IMPORT.SKIP_DUPLICATES' | translate }}
      </label>

      @if (errors().length) {
        <div class="mt-4 alert-error !block">
          <div class="font-medium mb-2">{{ summary() }}</div>
          <div class="max-h-56 overflow-y-auto bg-white rounded border border-red-100">
            <table class="w-full text-xs">
              <thead><tr class="text-left text-gray-500"><th class="px-2 py-1 w-16">{{ 'IMPORT.ROW' | translate }}</th><th class="px-2 py-1">{{ 'IMPORT.ERROR' | translate }}</th></tr></thead>
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
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CLOSE' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="busy() || !file()" (click)="run()">
          <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'upload_file' }}</span> {{ 'IMPORT.RUN' | translate }}
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
  protected readonly language = inject(Language);
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
      saveFile(await this.client.template(), `${this.language.t('IMPORT.TEMPLATE_FILE_NAME')} ${this.language.t(this.title())}.xlsx`);
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
