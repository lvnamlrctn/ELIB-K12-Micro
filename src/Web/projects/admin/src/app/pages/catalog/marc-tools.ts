import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, BibSearch, BibType, MarcFormat, MarcImportResult, MarcPreviewRecord, STATUS_ACTIVE, errorMessage, saveFile } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';

const MARC_ACCEPT = '.mrc,.iso,.marc,.dat,.xml,.txt';
const MAX_MARC_BYTES = 20 * 1024 * 1024;

/**
 * Nhập biểu ghi hàng loạt từ file MARC — đưa dữ liệu biên mục của phần mềm cũ vào hệ mới.
 * Nhận ISO2709 (.mrc), MARCXML (.xml) và file text MARC của hệ cũ; định dạng nhận theo nội dung file.
 */
@Component({
  selector: 'app-marc-import',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal title="MARC_TOOLS.IMPORT_TITLE" widthClass="max-w-xl" (closed)="closed.emit()">
      <p class="text-sm text-gray-600" [innerHTML]="'MARC_TOOLS.IMPORT_HINT' | translate"></p>
      <input type="file" [accept]="accept" [disabled]="busy()" aria-label="File MARC"
             class="mt-3 block w-full text-sm file:mr-3 file:py-1.5 file:px-3 file:rounded-lg file:border-0 file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
             (change)="pick($event)" />

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
        <div>
          <label for="mi-type" class="form-label">{{ 'MARC_TOOLS.RECORD_TYPE' | translate }}</label>
          <select id="mi-type" class="input" [(ngModel)]="bibTypeId" [disabled]="busy()">
            <option [ngValue]="null">{{ 'MARC_TOOLS.AUTO_TYPE' | translate }}</option>
            @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
          </select>
        </div>
        <div>
          <label for="mi-status" class="form-label">{{ 'MARC_TOOLS.OPAC_VISIBLE' | translate }}</label>
          <select id="mi-status" class="input" [(ngModel)]="status" [disabled]="busy()">
            <option [ngValue]="2">{{ 'MARC_TOOLS.SHOWN' | translate }}</option><option [ngValue]="1">{{ 'MARC_TOOLS.HIDDEN_REVIEW' | translate }}</option>
          </select>
        </div>
      </div>
      <div class="mt-3 space-y-1.5">
        <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
          <input type="checkbox" class="rounded border-gray-300" [(ngModel)]="skipDuplicates" [disabled]="busy()" />
          {{ 'MARC_TOOLS.SKIP_DUPES' | translate }}
        </label>
        <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
          <input type="checkbox" class="rounded border-gray-300" [(ngModel)]="skipInvalid" [disabled]="busy()" />
          {{ 'MARC_TOOLS.SKIP_INVALID' | translate }}
        </label>
      </div>

      @if (result(); as r) {
        <div class="mt-4 text-sm space-y-2">
          <div [class]="r.rejected ? 'alert-error !block' : 'alert-success !block'">{{ r.detail }}</div>
          @if (r.errors.length) {
            <div class="alert-error !block">
              <div class="font-medium mb-1">{{ (r.errors.length < r.failed ? 'MARC_TOOLS.FAILED_SHOWN' : 'MARC_TOOLS.FAILED_N') | translate: { count: r.failed, shown: r.errors.length } }}</div>
              <ul class="max-h-48 overflow-y-auto text-xs list-disc pl-5">
                @for (e of r.errors; track e.record) { <li>{{ 'MARC_TOOLS.RECORD_N' | translate: { n: e.record } }}@if (e.title) { — <i>{{ e.title }}</i> }: {{ e.message }}</li> }
              </ul>
            </div>
          }
        </div>
      }

      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()" [disabled]="busy()">{{ 'COMMON.CLOSE' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="busy() || !file()" (click)="run()">
          <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'upload_file' }}</span> {{ 'MARC_TOOLS.IMPORT' | translate }}
        </button>
      </ng-container>
    </app-modal>
  `,
})
export class MarcImport {
  readonly types = input<BibType[]>([]);
  readonly closed = output<void>();
  /** Đã nhập ít nhất một biểu ghi. */
  readonly imported = output<void>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly accept = MARC_ACCEPT;
  protected readonly file = signal<File | null>(null);
  protected readonly busy = signal(false);
  protected readonly result = signal<MarcImportResult | null>(null);
  protected bibTypeId: number | null = null;
  protected status = STATUS_ACTIVE;
  protected skipDuplicates = true;
  protected skipInvalid = false;

  protected pick(event: Event): void {
    this.result.set(null);
    this.file.set(checkFile(event, this.toastr));
  }

  protected async run(): Promise<void> {
    const file = this.file();
    if (!file) return;
    this.busy.set(true);
    try {
      const r = await this.api.importMarc(file, { bibTypeId: this.bibTypeId, status: this.status, skipDuplicates: this.skipDuplicates, skipInvalid: this.skipInvalid });
      this.result.set(r);
      if (r.imported > 0) this.imported.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Xuất biểu ghi theo bộ lọc của màn danh sách ra ISO2709 hoặc MARCXML (chuyển sang hệ khác, sao lưu). */
@Component({
  selector: 'app-marc-export',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal title="MARC_TOOLS.EXPORT_TITLE" widthClass="max-w-lg" (closed)="closed.emit()">
      <p class="text-sm text-gray-600 mb-3" [innerHTML]="'MARC_TOOLS.EXPORT_HINT' | translate: { count: total() }"></p>
      <div class="space-y-2">
        <label class="flex items-start gap-2 text-sm text-gray-700 cursor-pointer select-none">
          <input type="radio" name="mx-format" value="iso2709" [(ngModel)]="format" class="mt-0.5" />
          <span [innerHTML]="'MARC_TOOLS.ISO_DESC' | translate"></span>
        </label>
        <label class="flex items-start gap-2 text-sm text-gray-700 cursor-pointer select-none">
          <input type="radio" name="mx-format" value="marcxml" [(ngModel)]="format" class="mt-0.5" />
          <span [innerHTML]="'MARC_TOOLS.XML_DESC' | translate"></span>
        </label>
      </div>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CLOSE' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="busy() || total() === 0" (click)="run()">
          <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'download' }}</span> {{ 'MARC_TOOLS.EXPORT' | translate }}
        </button>
      </ng-container>
    </app-modal>
  `,
})
export class MarcExport {
  readonly search = input.required<BibSearch>();
  readonly total = input(0);
  readonly closed = output<void>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly busy = signal(false);
  protected format: MarcFormat = 'iso2709';

  protected async run(): Promise<void> {
    this.busy.set(true);
    try {
      const { pageIndex: _p, pageSize: _s, ...search } = this.search();
      const blob = await this.api.exportMarc({ ...search, keyword: search.keyword?.trim() || undefined, isbn: search.isbn?.trim() || null }, this.format);
      saveFile(blob, this.format === 'marcxml' ? 'bieu-ghi.xml' : 'bieu-ghi.mrc');
      this.closed.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Nạp một biểu ghi từ file MARC vào màn biên mục (monolith: "Thêm sách từ file Marc"). File nhiều biểu ghi → chọn một. */
@Component({
  selector: 'app-marc-pick',
  imports: [Modal, TranslatePipe],
  template: `
    <app-modal title="MARC_TOOLS.PICK_TITLE" widthClass="max-w-2xl" (closed)="closed.emit()">
      <p class="text-sm text-gray-600">{{ 'MARC_TOOLS.PICK_HINT' | translate }}</p>
      <input type="file" [accept]="accept" [disabled]="busy()" aria-label="File MARC"
             class="mt-3 block w-full text-sm file:mr-3 file:py-1.5 file:px-3 file:rounded-lg file:border-0 file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
             (change)="pick($event)" />
      @if (busy()) { <p class="text-sm text-gray-500 mt-3">{{ 'MARC_TOOLS.READING' | translate }}</p> }
      @if (records().length) {
        <p class="text-sm text-gray-700 mt-3">
          <span [innerHTML]="(total() > records().length ? 'MARC_TOOLS.FILE_HAS_SHOWN' : 'MARC_TOOLS.FILE_HAS') | translate: { total: total(), shown: records().length }"></span>
        </p>
        <ul class="mt-2 max-h-80 overflow-y-auto divide-y divide-gray-100 border border-gray-100 rounded-lg">
          @for (r of records(); track r.record) {
            <li class="flex items-center gap-3 px-3 py-2 text-sm">
              <span class="font-mono text-xs text-gray-400 w-8">{{ r.record }}</span>
              <span class="flex-1 min-w-0">
                <span class="font-medium text-gray-800">{{ r.title ?? ('MARC_TOOLS.NO_TITLE' | translate) }}</span>
                @if (r.error) { <span class="block text-xs text-red-600">{{ r.error }}</span> }
              </span>
              <button type="button" class="btn-secondary !py-1 !px-3" [disabled]="!r.fields.length" (click)="chosen.emit(r)">{{ 'MARC_TOOLS.LOAD' | translate }}</button>
            </li>
          }
        </ul>
      }
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CLOSE' | translate }}</button>
      </ng-container>
    </app-modal>
  `,
})
export class MarcPick {
  readonly closed = output<void>();
  readonly chosen = output<MarcPreviewRecord>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly accept = MARC_ACCEPT;
  protected readonly busy = signal(false);
  protected readonly total = signal(0);
  protected readonly records = signal<MarcPreviewRecord[]>([]);

  protected async pick(event: Event): Promise<void> {
    this.records.set([]);
    const file = checkFile(event, this.toastr);
    if (!file) return;
    this.busy.set(true);
    try {
      const preview = await this.api.previewMarc(file);
      if (preview.records.length === 1 && !preview.records[0].error) {
        this.chosen.emit(preview.records[0]); // một biểu ghi — nạp luôn
        return;
      }
      this.total.set(preview.total);
      this.records.set(preview.records);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

function checkFile(event: Event, toastr: ToastrService): File | null {
  const file = (event.target as HTMLInputElement).files?.[0] ?? null;
  if (file && file.size > MAX_MARC_BYTES) {
    toastr.error('MARC_TOOLS.TOO_BIG');
    (event.target as HTMLInputElement).value = '';
    return null;
  }
  return file;
}
