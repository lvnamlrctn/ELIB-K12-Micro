import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, ExportField, ItemImportResult, ItemSelection, errorMessage, saveFile } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { ItemsApi } from './items-api';

export interface OptionItem { id: number; label: string; }

/** Công tắc bật/tắt kiểu LRC (peer-checked) — dùng chung cho hộp thoại nhập. */
export const TOGGLE_CLASS = 'w-10 h-5 bg-gray-300 rounded-full peer-checked:bg-blue-600 relative after:content-[\'\'] after:absolute after:top-0.5 after:left-0.5 after:bg-white after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:after:translate-x-5';

// ───────────────────────── Nhập dữ liệu ─────────────────────────

/**
 * Nhập tài liệu số từ Excel hoặc Dublin Core XML (LRC: Import dữ liệu). Tuỳ chọn phân loại là giá trị mặc định cho dòng/file không có ô tương ứng;
 * mặc định tài liệu nhập vào ở trạng thái ẩn. Kết quả báo thành công / lỗi theo dòng (hoặc theo tên file) và cảnh báo (ảnh bìa bị bỏ).
 */
@Component({
  selector: 'app-item-import',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal title="DIGITAL_DOC.IMPORT_TITLE" widthClass="max-w-lg" (closed)="closed.emit()">
      @if (result(); as r) {
        <div class="text-center mb-4">
          <span class="material-icons text-[48px]" [class]="r.failed === 0 ? 'text-emerald-500' : 'text-amber-500'">{{ r.failed === 0 ? 'check_circle' : 'warning' }}</span>
          <div class="font-semibold text-gray-800">{{ 'DIGITAL_DOC.IMPORT_RESULT' | translate }}</div>
          <div class="text-sm text-gray-500">{{ r.message }}</div>
        </div>
        <div class="grid grid-cols-2 gap-3 mb-3">
          <div class="rounded-lg bg-emerald-50 text-emerald-700 text-center py-3"><div class="text-2xl font-bold">{{ r.imported }}</div><div class="text-xs">{{ 'DIGITAL_DOC.IMPORT_OK' | translate }}</div></div>
          <div class="rounded-lg bg-red-50 text-red-700 text-center py-3"><div class="text-2xl font-bold">{{ r.failed }}</div><div class="text-xs">{{ 'DIGITAL_DOC.IMPORT_FAILED' | translate }}</div></div>
        </div>
        @if (r.errors.length) {
          <div class="text-sm font-medium text-gray-700 mb-1">{{ 'DIGITAL_DOC.IMPORT_ERRORS' | translate }}</div>
          <ul class="max-h-40 overflow-y-auto text-xs text-red-700 space-y-0.5 mb-3">@for (e of r.errors; track $index) { <li>• {{ e }}</li> }</ul>
        }
        @if (r.warnings.length) {
          <div class="text-sm font-medium text-gray-700 mb-1">{{ 'DIGITAL_DOC.IMPORT_WARNINGS' | translate }}</div>
          <ul class="max-h-28 overflow-y-auto text-xs text-amber-700 space-y-0.5">@for (w of r.warnings; track $index) { <li>• {{ w }}</li> }</ul>
        }
      } @else {
        <div class="flex border-b border-gray-200 mb-3" role="tablist">
          @for (k of kinds; track k) {
            <button type="button" role="tab" class="px-4 py-2 text-sm font-medium border-b-2 -mb-px" [attr.aria-selected]="kind() === k"
              [class]="kind() === k ? 'border-blue-600 text-blue-700' : 'border-transparent text-gray-500 hover:text-gray-700'" (click)="setKind(k)">
              {{ k === 'excel' ? 'Excel' : 'XML' }}</button>
          }
        </div>
        <p class="text-xs text-gray-500 flex gap-1.5 mb-3"><span class="material-icons text-[16px]">info</span>
          {{ (kind() === 'excel' ? 'DIGITAL_DOC.IMPORT_HINT_EXCEL' : 'DIGITAL_DOC.IMPORT_HINT_XML') | translate }}</p>
        <label class="flex flex-col items-center justify-center py-5 border-2 border-dashed border-gray-300 rounded-xl cursor-pointer hover:bg-gray-50 text-gray-500 mb-4">
          <span class="material-icons text-[28px]">upload_file</span>
          @if (files().length) { <span class="text-sm text-emerald-700 font-medium text-center px-3">{{ names() }}</span> }
          @else { <span class="text-sm">{{ (kind() === 'excel' ? 'DIGITAL_DOC.IMPORT_PICK_EXCEL' : 'DIGITAL_DOC.IMPORT_PICK_XML') | translate }}</span> }
          <input type="file" class="hidden" [accept]="kind() === 'excel' ? '.xlsx' : '.xml,text/xml'" [multiple]="kind() === 'xml'" (change)="pick($event)" />
        </label>
        <div class="text-sm font-semibold text-blue-700 mb-2">{{ 'DIGITAL_DOC.IMPORT_OPTIONS' | translate }}</div>
        <div class="grid grid-cols-3 gap-3 mb-3 text-sm text-gray-700">
          <label class="flex items-center gap-2 cursor-pointer select-none"><span class="relative inline-flex items-center"><input type="checkbox" class="sr-only peer" [(ngModel)]="published" name="published" /><span [class]="toggle"></span></span>{{ 'DIGITAL_DOC.PUBLISHED' | translate }}</label>
          <label class="flex items-center gap-2 cursor-pointer select-none"><span class="relative inline-flex items-center"><input type="checkbox" class="sr-only peer" [(ngModel)]="free" name="free" /><span [class]="toggle"></span></span>{{ 'DIGITAL_DOC.FREE' | translate }}</label>
          <label class="flex items-center gap-2 cursor-pointer select-none"><span class="relative inline-flex items-center"><input type="checkbox" class="sr-only peer" [(ngModel)]="allowDownload" name="allowDownload" /><span [class]="toggle"></span></span>{{ 'DIGITAL_DOC.ALLOW_DOWNLOAD' | translate }}</label>
        </div>
        <div class="grid grid-cols-2 gap-3">
          <div><label class="form-label" for="imp-coll">{{ 'DIGITAL_DOC.COLLECTION' | translate }}</label>
            <select id="imp-coll" class="input" [(ngModel)]="collectionId" name="collectionId"><option [ngValue]="null">{{ 'DIGITAL_DOC.NONE_CHOSEN' | translate }}</option>
              @for (o of collections(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select></div>
          <div><label class="form-label" for="imp-subj">{{ 'DIGITAL_DOC.SUBJECT' | translate }}</label>
            <select id="imp-subj" class="input" [(ngModel)]="subjectId" name="subjectId"><option [ngValue]="null">{{ 'DIGITAL_DOC.NONE_CHOSEN' | translate }}</option>
              @for (o of subjects(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select></div>
          <div class="col-span-2"><label class="form-label" for="imp-topic">{{ 'DIGITAL_DOC.TOPIC' | translate }}</label>
            <select id="imp-topic" class="input" [(ngModel)]="topicId" name="topicId"><option [ngValue]="null">{{ 'DIGITAL_DOC.NONE_CHOSEN' | translate }}</option>
              @for (o of topics(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select></div>
        </div>
        @if (running()) { <p class="mt-3 text-sm text-blue-600 flex items-center gap-2"><span class="material-icons animate-spin text-[18px]">autorenew</span> {{ 'DIGITAL_DOC.IMPORTING' | translate }}</p> }
      }
      <ng-container footer>
        @if (result()) {
          <button type="button" class="btn-secondary" (click)="again()">{{ 'DIGITAL_DOC.IMPORT_AGAIN' | translate }}</button>
          <button type="button" class="btn-primary" (click)="done()">{{ 'COMMON.CLOSE' | translate }}</button>
        } @else {
          <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="running() || !files().length" (click)="run()">
            <span class="material-icons text-[16px]">file_upload</span> {{ 'DIGITAL_DOC.IMPORT_BTN' | translate }}</button>
        }
      </ng-container>
    </app-modal>
  `,
})
export class ItemImport {
  private readonly api = inject(Api);
  private readonly itemsApi = inject(ItemsApi);
  private readonly toastr = inject(ToastrService);

  readonly collections = input<OptionItem[]>([]);
  readonly subjects = input<OptionItem[]>([]);
  readonly topics = input<OptionItem[]>([]);
  readonly closed = output<void>();
  /** Có ít nhất một tài liệu được nhập — danh sách cần tải lại. */
  readonly imported = output<void>();

  protected readonly toggle = TOGGLE_CLASS;
  protected readonly kinds: ('excel' | 'xml')[] = ['excel', 'xml'];
  protected readonly kind = signal<'excel' | 'xml'>('excel');
  protected readonly files = signal<File[]>([]);
  protected readonly running = signal(false);
  protected readonly result = signal<ItemImportResult | null>(null);
  protected published = false;
  protected free = false;
  protected allowDownload = false;
  protected collectionId: number | null = null;
  protected subjectId: number | null = null;
  protected topicId: number | null = null;

  protected names(): string {
    const files = this.files();
    return files.length <= 3 ? files.map((f) => f.name).join(', ') : `${files.slice(0, 3).map((f) => f.name).join(', ')} … (${files.length})`;
  }

  protected setKind(kind: 'excel' | 'xml'): void {
    this.kind.set(kind);
    this.files.set([]);
  }

  protected pick(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.files.set(Array.from(input.files ?? []));
    input.value = '';
  }

  protected async run(): Promise<void> {
    this.running.set(true);
    try {
      this.result.set(await this.itemsApi.importItems(this.kind(), this.files(), {
        collectionId: this.collectionId, subjectId: this.subjectId, topicId: this.topicId, published: this.published, free: this.free, allowDownload: this.allowDownload,
      }, null));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.running.set(false);
    }
  }

  protected again(): void {
    this.result.set(null);
    this.files.set([]);
  }

  protected done(): void {
    if ((this.result()?.imported ?? 0) > 0) this.imported.emit();
    this.closed.emit();
  }
}

// ───────────────────────── Xuất Excel ─────────────────────────

/** Chọn cột rồi xuất Excel (LRC: Xuất dữ liệu Excel) — theo các dòng đã chọn hoặc theo bộ lọc hiện tại. */
@Component({
  selector: 'app-item-export',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal title="DIGITAL_DOC.EXPORT_TITLE" widthClass="max-w-md" (closed)="closed.emit()">
      @if (loading()) {
        <p class="text-sm text-gray-500 flex items-center gap-2"><span class="material-icons animate-spin text-[18px]">autorenew</span> {{ 'DIGITAL_DOC.EXPORT_LOADING' | translate }}</p>
      } @else if (!fields().length) {
        <p class="text-sm text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</p>
      } @else {
        <label class="flex items-center justify-between text-sm font-medium text-gray-700 mb-3 cursor-pointer select-none">
          <span class="flex items-center gap-2"><input type="checkbox" class="rounded border-gray-300" [checked]="all()" (change)="toggleAll()" /> {{ 'DIGITAL_DOC.SELECT_ALL' | translate }}</span>
          <span class="text-xs text-gray-500">{{ selected().size }}/{{ fields().length }}</span>
        </label>
        <div class="grid grid-cols-2 gap-x-4 gap-y-2">
          @for (f of fields(); track f.code) {
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
              <input type="checkbox" class="rounded border-gray-300" [checked]="selected().has(f.code)" (change)="toggle(f.code)" /> {{ f.name }}
            </label>
          }
        </div>
      }
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="exporting() || !selected().size" (click)="run()">
          <span class="material-icons text-[16px]" [class.animate-spin]="exporting()">{{ exporting() ? 'autorenew' : 'file_download' }}</span> {{ 'DIGITAL_DOC.EXPORT_BTN' | translate }}</button>
      </ng-container>
    </app-modal>
  `,
})
export class ItemExport implements OnInit {
  private readonly api = inject(Api);
  private readonly itemsApi = inject(ItemsApi);
  private readonly toastr = inject(ToastrService);

  readonly selection = input<ItemSelection | null>(null);
  readonly closed = output<void>();

  protected readonly fields = signal<ExportField[]>([]);
  protected readonly selected = signal<Set<string>>(new Set());
  protected readonly loading = signal(true);
  protected readonly exporting = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  protected all(): boolean {
    return this.fields().length > 0 && this.selected().size === this.fields().length;
  }

  protected toggleAll(): void {
    this.selected.set(this.all() ? new Set() : new Set(this.fields().map((f) => f.code)));
  }

  protected toggle(code: string): void {
    const next = new Set(this.selected());
    if (!next.delete(code)) next.add(code);
    this.selected.set(next);
  }

  private async load(): Promise<void> {
    try {
      const fields = await this.itemsApi.itemExportFields();
      this.fields.set(fields);
      this.selected.set(new Set(fields.map((f) => f.code)));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected async run(): Promise<void> {
    this.exporting.set(true);
    try {
      const codes = this.fields().filter((f) => this.selected().has(f.code)).map((f) => f.code);
      saveFile(await this.itemsApi.exportItems(this.selection(), codes), `tai-lieu-so_${stamp()}.xlsx`);
      this.closed.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.exporting.set(false);
    }
  }
}

/** Dấu thời gian cho tên file tải về: yyyyMMddHHmmss (giờ máy người dùng). */
export function stamp(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}`;
}

// ───────────────────────── Chuyển bộ sưu tập ─────────────────────────

/** Chuyển các tài liệu đã chọn sang một bộ sưu tập khác (LRC: BulkMoveCollection). */
@Component({
  selector: 'app-item-move',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal title="DIGITAL_DOC.MOVE_TITLE" widthClass="max-w-sm" (closed)="closed.emit()">
      <p class="text-sm text-gray-500 mb-3">{{ 'DIGITAL_DOC.MOVE_HINT' | translate: { count: count() } }}</p>
      <label class="form-label" for="mv-coll">{{ 'DIGITAL_DOC.MOVE_TARGET' | translate }}</label>
      <select id="mv-coll" class="input" [(ngModel)]="target" name="target">
        <option [ngValue]="null">{{ 'DIGITAL_DOC.NONE_CHOSEN' | translate }}</option>
        @for (o of collections(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }
      </select>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="busy() || target == null" (click)="run()">
          <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'drive_file_move' }}</span> {{ 'DIGITAL_DOC.MOVE_BTN' | translate }}</button>
      </ng-container>
    </app-modal>
  `,
})
export class ItemMove {
  private readonly api = inject(Api);
  private readonly itemsApi = inject(ItemsApi);
  private readonly toastr = inject(ToastrService);

  readonly selection = input.required<ItemSelection>();
  readonly count = input.required<number>();
  readonly collections = input<OptionItem[]>([]);
  readonly closed = output<void>();
  readonly moved = output<void>();

  protected target: number | null = null;
  protected readonly busy = signal(false);

  protected async run(): Promise<void> {
    if (this.target == null) return;
    this.busy.set(true);
    try {
      await this.itemsApi.itemsMoveCollection(this.selection(), this.target);
      this.toastr.success('DIGITAL_DOC.MOVE_DONE');
      this.moved.emit();
      this.closed.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
