import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../../core/i18n';
import { Api, ExportField, ReaderSearch, errorMessage, saveFile } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { readZip } from '../../shared/zip';

/** Xuất danh sách bạn đọc ra Excel (monolith: Reader/Export) theo bộ lọc đang chọn ở màn danh sách. */
@Component({
  selector: 'app-reader-export',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal title="READER.EXPORT_DIALOG_TITLE" widthClass="max-w-lg" (closed)="closed.emit()">
      <p class="text-sm text-gray-600 mb-3">{{ 'READER.EXPORT_TEXT' | translate: { count: total() } }}</p>
      <div class="flex justify-between items-center mb-2">
        <span class="form-label !mb-0">{{ 'READER.EXPORT_FIELDS' | translate }}</span>
        <button type="button" class="text-xs text-blue-600 hover:underline" (click)="toggleAll()">{{ (allChecked() ? 'READER.EXPORT_UNSELECT_ALL' : 'READER.EXPORT_SELECT_ALL') | translate }}</button>
      </div>
      <div class="grid grid-cols-2 gap-x-4 gap-y-1.5">
        @for (f of fields(); track f.code) {
          <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
            <input type="checkbox" class="rounded border-gray-300" [checked]="checked().has(f.code)" (change)="toggle(f.code)" /> {{ fieldLabel(f) | translate }}
          </label>
        }
      </div>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CLOSE' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="busy() || checked().size === 0" (click)="run()">
          <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'download' }}</span> {{ 'READER.EXPORT_CONFIRM' | translate }}
        </button>
      </ng-container>
    </app-modal>
  `,
})
export class ReaderExport implements OnInit {
  readonly search = input.required<ReaderSearch>();
  readonly total = input(0);
  readonly closed = output<void>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly language = inject(Language);
  protected readonly fields = signal<ExportField[]>([]);
  protected readonly checked = signal<Set<string>>(new Set());
  protected readonly busy = signal(false);
  protected readonly allChecked = computed(() => this.fields().every((f) => this.checked().has(f.code)));

  /** Tên trường theo ngôn ngữ (mã trường của patron → khoá dịch); trường lạ hiện tên backend gửi. */
  protected fieldLabel(f: ExportField): string {
    return EXPORT_LABELS[f.code] ?? f.name;
  }

  async ngOnInit(): Promise<void> {
    try {
      const fields = await this.api.readerExportFields();
      this.fields.set(fields);
      this.checked.set(new Set(fields.filter((f) => f.code !== 'lockreason').map((f) => f.code)));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected toggle(code: string): void {
    const next = new Set(this.checked());
    if (next.has(code)) next.delete(code);
    else next.add(code);
    this.checked.set(next);
  }

  protected toggleAll(): void {
    this.checked.set(this.allChecked() ? new Set() : new Set(this.fields().map((f) => f.code)));
  }

  protected async run(): Promise<void> {
    this.busy.set(true);
    try {
      const { pageIndex: _p, pageSize: _s, ...search } = this.search();
      const fields = this.fields().map((f) => f.code).filter((c) => this.checked().has(c));
      saveFile(await this.api.exportReaders({ ...search, keyword: search.keyword?.trim() || undefined }, fields), `${this.language.t('READER.EXPORT_FILE_NAME')}.xlsx`);
      this.closed.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Mã trường xuất của patron → khoá dịch. */
const EXPORT_LABELS: Record<string, string> = {
  cardno: 'READER.CARD_CODE', lastname: 'READER.FIRST_NAME', firstname: 'READER.LAST_NAME', birthdate: 'READER.BIRTH_DATE', sex: 'READER.GENDER',
  readertype: 'READER.READER_TYPE', class: 'READER.ACADEMIC_CLASS', course: 'READER.COURSE', org: 'READER.ORG', ethnicity: 'READER.ETHNIC',
  degree: 'READER.DEGREE', academictitle: 'READER.PROF', email: 'READER.EMAIL', phone: 'READER.PHONE', address: 'READER.ADDRESS',
  citizenid: 'READER.CITIZEN_ID', carduid: 'READER.CARD_UID', issuedate: 'READER.ISSUED_DATE', expiredate: 'READER.EXPIRED_DATE',
  status: 'READER.STATUS', lockreason: 'READER.LOCK_REASON',
};

const PHOTO_TYPES: Record<string, string> = { jpg: 'image/jpeg', jpeg: 'image/jpeg', png: 'image/png', webp: 'image/webp' };
const MAX_PHOTO_BYTES = 2 * 1024 * 1024;
const PARALLEL = 4;

interface PhotoItem { name: string; cardNo: string; size: number; read: () => Promise<Blob>; }
interface PhotoFailure { name: string; error: string; }

/**
 * Tải ảnh thẻ hàng loạt (monolith: UploadPhotosZip). Tên file (bỏ đuôi) = số thẻ. Chọn một file .zip hoặc nhiều ảnh;
 * trình duyệt giải nén, upload từng ảnh lên media (kiểm tra nội dung ở server) rồi gán theo số thẻ ở patron.
 */
@Component({
  selector: 'app-reader-photos',
  imports: [Modal, TranslatePipe],
  template: `
    <app-modal title="READER.PHOTOS_TITLE" widthClass="max-w-lg" (closed)="closed.emit()">
      <p class="text-sm text-gray-600">{{ 'READER.PHOTOS_HINT' | translate }}</p>
      <input type="file" multiple accept=".zip,application/zip,image/jpeg,image/png,image/webp" [disabled]="busy()"
             class="mt-3 block w-full text-sm file:mr-3 file:py-1.5 file:px-3 file:rounded-lg file:border-0 file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
             (change)="pick($event)" />
      @if (items().length) { <p class="text-sm text-gray-700 mt-3">{{ 'READER.PHOTOS_SELECTED' | translate: { count: items().length } }}</p> }

      @if (busy()) {
        <div class="mt-3">
          <div class="h-2 bg-gray-100 rounded"><div class="h-2 bg-blue-500 rounded transition-all" [style.width.%]="progress()"></div></div>
          <p class="text-xs text-gray-500 mt-1">{{ 'READER.PHOTOS_PROGRESS' | translate: { done: done(), total: items().length } }}</p>
        </div>
      }

      @if (result(); as r) {
        <div class="mt-4 text-sm space-y-2">
          <div class="alert-success !block">{{ 'READER.PHOTOS_MATCHED' | translate: { count: r.matched } }}</div>
          @if (r.notFound.length) {
            <div class="alert-error !block">{{ 'READER.PHOTOS_NOT_FOUND' | translate: { count: r.notFound.length } }} <span class="font-mono text-xs">{{ r.notFound.join(', ') }}</span></div>
          }
          @if (r.failed.length) {
            <div class="alert-error !block">
              <div class="font-medium mb-1">{{ 'READER.PHOTOS_FAILED' | translate: { count: r.failed.length } }}</div>
              <ul class="max-h-40 overflow-y-auto text-xs list-disc pl-5">
                @for (f of r.failed; track f.name) { <li><span class="font-mono">{{ f.name }}</span>: {{ f.error | translate }}</li> }
              </ul>
            </div>
          }
        </div>
      }

      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()" [disabled]="busy()">{{ 'COMMON.CLOSE' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="busy() || !items().length" (click)="run()">
          <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'cloud_upload' }}</span> {{ 'READER.PHOTOS_UPLOAD' | translate }}
        </button>
      </ng-container>
    </app-modal>
  `,
})
export class ReaderPhotos {
  readonly closed = output<void>();
  /** Đã gán xong ít nhất một ảnh. */
  readonly uploaded = output<void>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly items = signal<PhotoItem[]>([]);
  protected readonly busy = signal(false);
  protected readonly done = signal(0);
  protected readonly progress = computed(() => (this.items().length ? (100 * this.done()) / this.items().length : 0));
  protected readonly result = signal<{ matched: number; notFound: string[]; failed: PhotoFailure[] } | null>(null);

  protected async pick(event: Event): Promise<void> {
    this.result.set(null);
    const items: PhotoItem[] = [];
    try {
      for (const file of Array.from((event.target as HTMLInputElement).files ?? [])) {
        if (/\.zip$/i.test(file.name)) {
          for (const entry of await readZip(file)) items.push(item(entry.name, entry.size, entry.read));
        } else {
          items.push(item(file.name, file.size, () => Promise.resolve(file)));
        }
      }
    } catch (e) {
      this.toastr.error(e instanceof Error ? e.message : 'READER.ZIP_UNREADABLE');
    }
    this.items.set(items);
  }

  protected async run(): Promise<void> {
    const queue = [...this.items()];
    const uploaded: { cardNo: string; fileId: string }[] = [];
    const failed: PhotoFailure[] = [];
    this.busy.set(true);
    this.done.set(0);
    const worker = async () => {
      for (let it = queue.shift(); it; it = queue.shift()) {
        try {
          const type = PHOTO_TYPES[it.name.split('.').pop()?.toLowerCase() ?? ''];
          if (!type) throw new Error('READER.PHOTO_TYPE_INVALID');
          if (!it.cardNo) throw new Error('READER.PHOTO_NO_CARD');
          if (it.size > MAX_PHOTO_BYTES) throw new Error('READER.PHOTO_TOO_BIG');
          const file = await this.api.upload('reader-photo', new File([await it.read()], it.name, { type }));
          uploaded.push({ cardNo: it.cardNo, fileId: file.id });
        } catch (e) {
          failed.push({ name: it.name, error: e instanceof Error ? e.message : errorMessage(e) });
        }
        this.done.update((n) => n + 1);
      }
    };
    try {
      await Promise.all(Array.from({ length: PARALLEL }, worker));
      let matched = 0;
      const notFound: string[] = [];
      for (let i = 0; i < uploaded.length; i += 2000) {
        const r = await this.api.assignReaderPhotos(uploaded.slice(i, i + 2000));
        matched += r.matched;
        notFound.push(...r.notFound);
      }
      this.result.set({ matched, notFound, failed });
      if (matched > 0) this.uploaded.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

function item(name: string, size: number, read: () => Promise<Blob>): PhotoItem {
  return { name, size, read, cardNo: name.replace(/\.[^.]*$/, '').trim().toUpperCase() };
}
