import { Component, DestroyRef, computed, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, ReaderImportColumns, ReaderImportJob, errorMessage, saveFile } from '../../core/api';
import { Language } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { ReaderCatalogs } from './reader-catalogs';

/** Nhãn trường nhập (khoá trường của patron → khoá dịch kiểu ELIB-LRC; LRC đặt FIRST_NAME = Họ đệm, LAST_NAME = Tên). */
const FIELD_LABELS: Record<string, string> = {
  cardno: 'READER.CARD_CODE', lastname: 'READER.FIRST_NAME', firstname: 'READER.LAST_NAME', fullname: 'READER.FULL_NAME',
  birthdate: 'READER.BIRTH_DATE', sex: 'READER.GENDER', readertype: 'READER.READER_TYPE', class: 'READER.ACADEMIC_CLASS',
  course: 'READER.COURSE', org: 'READER.ORG', ethnicity: 'READER.ETHNIC', degree: 'READER.DEGREE', academictitle: 'READER.PROF',
  email: 'READER.EMAIL', phone: 'READER.PHONE', address: 'READER.ADDRESS', citizenid: 'READER.CITIZEN_ID', carduid: 'READER.CARD_UID',
  issuedate: 'READER.ISSUED_DATE', expiredate: 'READER.EXPIRED_DATE',
};

const MAX_BYTES = 20 * 1024 * 1024;

/**
 * Nhập bạn đọc từ Excel kiểu ELIB-LRC, 3 bước: chọn file (tự tìm dòng tiêu đề, gợi ý ghép cột) → ghép cột + tuỳ chọn (ghi đè, loại bạn đọc
 * cho cả file, tự tạo lớp/khoá) → xem trước số dòng thêm/sửa/lỗi rồi chạy nền, theo dõi tiến độ, tải file "Dòng cần sửa".
 */
@Component({
  selector: 'app-reader-import',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal [title]="'READER_IMPORT.TITLE' | translate" widthClass="max-w-3xl" (closed)="closed.emit()">
      <ol class="flex items-center gap-2 text-xs mb-4">
        @for (s of [1, 2, 3]; track s) {
          <li class="flex items-center gap-2">
            <span class="w-6 h-6 rounded-full grid place-items-center font-semibold"
                  [class]="step() >= s ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-500'">{{ s }}</span>
            <span [class.font-medium]="step() === s">{{ 'READER_IMPORT.STEP_' + s | translate }}</span>
            @if (s < 3) { <span class="w-6 border-t border-gray-300"></span> }
          </li>
        }
      </ol>

      @switch (step()) {
        @case (1) {
          <p class="text-sm text-gray-600 mb-3">{{ 'READER_IMPORT.FILE_HINT' | translate }}</p>
          <input type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                 class="block w-full text-sm file:mr-3 file:py-1.5 file:px-3 file:rounded-lg file:border-0 file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
                 (change)="pick($event)" />
          <p class="text-xs text-gray-500 mt-2">{{ 'READER_IMPORT.FILE_LIMITS' | translate }}
            <button type="button" class="text-blue-600 hover:underline" (click)="downloadTemplate()">{{ 'READER_IMPORT.TEMPLATE' | translate }}</button></p>
        }
        @case (2) {
          @if (columns(); as c) {
            <p class="text-sm text-gray-600 mb-3">{{ 'READER_IMPORT.DETECTED' | translate: { file: file()?.name, row: c.headerRow, count: c.dataRows } }}</p>
            <div class="max-h-[45vh] overflow-y-auto border border-gray-200 rounded-lg">
              <table class="w-full text-sm">
                <thead class="bg-gray-50 sticky top-0"><tr class="text-left text-gray-500">
                  <th class="px-3 py-2">{{ 'READER_IMPORT.FIELD' | translate }}</th>
                  <th class="px-3 py-2 w-1/2">{{ 'READER_IMPORT.FILE_COLUMN' | translate }}</th>
                </tr></thead>
                <tbody>
                  @for (f of c.fields; track f.key) {
                    <tr class="border-t border-gray-100">
                      <td class="px-3 py-1.5">{{ label(f.key) | translate }}@if (f.required) { <span class="text-red-600"> *</span> }</td>
                      <td class="px-3 py-1.5">
                        <select class="input !py-1" [attr.aria-label]="label(f.key) | translate" [ngModel]="mapping()[f.key] || 0"
                                (ngModelChange)="map(f.key, +$event)" [disabled]="f.key === 'readertype' && readerTypeId !== null">
                          <option [ngValue]="0">{{ 'READER_IMPORT.NOT_MAPPED' | translate }}</option>
                          @for (h of c.headers; track h.index) { <option [ngValue]="h.index">{{ h.index }}. {{ h.title }}</option> }
                        </select>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-4">
              <div>
                <label class="form-label" for="ri-type">{{ 'READER_IMPORT.TYPE_FOR_ALL' | translate }}</label>
                <select id="ri-type" class="input" [(ngModel)]="readerTypeId">
                  <option [ngValue]="null">{{ 'READER_IMPORT.TYPE_FROM_FILE' | translate }}</option>
                  @for (t of catalogs.types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
                </select>
                <!-- Như ELIB-LRC: cột Lớp/Khoá học/Đơn vị theo Tên (tra tên) hay theo Mã (ô chứa thẳng số id có sẵn, không tra tên). -->
                <div class="mt-3 grid grid-cols-3 gap-2 text-xs">
                  @for (o of byCodeOptions; track o.key) {
                    <div role="group" [attr.aria-label]="o.label | translate">
                      <span class="block text-gray-500 mb-1">{{ o.label | translate }}</span>
                      <div class="flex border border-gray-300 rounded-lg overflow-hidden">
                        <button type="button" class="flex-1 py-1.5 transition-colors" [attr.aria-pressed]="!byCode[o.key]"
                                [class]="!byCode[o.key] ? 'bg-blue-600 text-white' : 'bg-white text-gray-500 hover:bg-gray-50'"
                                (click)="byCode[o.key] = false">{{ 'READER_IMPORT.BY_NAME' | translate }}</button>
                        <button type="button" class="flex-1 py-1.5 border-l border-gray-300 transition-colors" [attr.aria-pressed]="byCode[o.key]"
                                [class]="byCode[o.key] ? 'bg-blue-600 text-white' : 'bg-white text-gray-500 hover:bg-gray-50'"
                                (click)="byCode[o.key] = true">{{ 'READER_IMPORT.BY_CODE' | translate }}</button>
                      </div>
                    </div>
                  }
                </div>
                <p class="text-[11px] text-gray-500 mt-1">{{ 'READER_IMPORT.BY_CODE_HINT' | translate }}</p>
              </div>
              <div class="space-y-2 text-sm">
                <label class="flex items-start gap-2 cursor-pointer select-none">
                  <input type="checkbox" class="mt-1 rounded border-gray-300" [(ngModel)]="overwrite" />
                  <span>{{ 'READER_IMPORT.OVERWRITE' | translate }}<span class="block text-xs text-gray-500">{{ 'READER_IMPORT.OVERWRITE_HINT' | translate }}</span></span>
                </label>
                <label class="flex items-start gap-2 cursor-pointer select-none">
                  <input type="checkbox" class="mt-1 rounded border-gray-300" [(ngModel)]="autoCreate" />
                  <span>{{ 'READER_IMPORT.AUTO_CREATE' | translate }}<span class="block text-xs text-gray-500">{{ 'READER_IMPORT.AUTO_CREATE_HINT' | translate }}</span></span>
                </label>
                <label class="flex items-start gap-2 cursor-pointer select-none">
                  <input id="ri-opac" type="checkbox" class="mt-1 rounded border-gray-300" [(ngModel)]="opacCardNo" />
                  <span>{{ 'READER_IMPORT.OPAC_CARDNO' | translate }}<span class="block text-xs text-gray-500">{{ 'READER_IMPORT.OPAC_CARDNO_HINT' | translate }}</span></span>
                </label>
              </div>
            </div>
          }
        }
        @case (3) {
          @if (job(); as j) {
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center">
              <div class="rounded-lg bg-gray-50 p-2"><div class="text-xl font-semibold">{{ j.total }}</div><div class="text-xs text-gray-500">{{ 'READER_IMPORT.ROWS' | translate }}</div></div>
              <div class="rounded-lg bg-green-50 p-2"><div class="text-xl font-semibold text-green-700">{{ running() || finished() ? j.created : j.toCreate }}</div><div class="text-xs text-gray-500">{{ 'READER_IMPORT.TO_CREATE' | translate }}</div></div>
              <div class="rounded-lg bg-blue-50 p-2"><div class="text-xl font-semibold text-blue-700">{{ running() || finished() ? j.updated : j.toUpdate }}</div><div class="text-xs text-gray-500">{{ 'READER_IMPORT.TO_UPDATE' | translate }}</div></div>
              <div class="rounded-lg bg-red-50 p-2"><div class="text-xl font-semibold text-red-700">{{ finished() ? j.failed : j.errorCount }}</div><div class="text-xs text-gray-500">{{ 'READER_IMPORT.ERRORS' | translate }}</div></div>
            </div>

            @if (running() || finished()) {
              <div class="mt-4">
                <div class="flex justify-between text-xs text-gray-600 mb-1">
                  <span>{{ ('READER_IMPORT.STATUS_' + j.status) | translate }}</span><span>{{ j.processed }} / {{ j.total }}</span>
                </div>
                <div class="h-2 rounded-full bg-gray-100 overflow-hidden" role="progressbar" [attr.aria-valuenow]="percent()" aria-valuemin="0" aria-valuemax="100">
                  <div class="h-full bg-blue-600 transition-all" [style.width.%]="percent()"></div>
                </div>
              </div>
            }
            @if (j.status === 'Failed' && j.message) { <div class="alert-error mt-3">{{ j.message }}</div> }
            @if (j.status === 'Done') { <div class="alert-success mt-3">{{ 'READER_IMPORT.DONE' | translate: { created: j.created, updated: j.updated, failed: j.failed } }}</div> }

            @if (j.errors.length) {
              <div class="mt-4">
                <div class="flex items-center justify-between mb-1">
                  <span class="text-sm font-medium text-gray-700">{{ 'READER_IMPORT.ERROR_LIST' | translate: { shown: j.errors.length, total: finished() ? j.failed : j.errorCount } }}</span>
                  @if (j.hasErrorFile) {
                    <button type="button" class="text-blue-600 hover:underline text-sm" (click)="downloadErrors()">
                      <span class="material-icons text-[16px] align-middle">download</span> {{ 'READER_IMPORT.ERROR_FILE' | translate }}</button>
                  }
                </div>
                <div class="max-h-56 overflow-y-auto rounded border border-red-100">
                  <table class="w-full text-xs">
                    <thead><tr class="text-left text-gray-500"><th class="px-2 py-1 w-16">{{ 'IMPORT.ROW' | translate }}</th><th class="px-2 py-1">{{ 'IMPORT.ERROR' | translate }}</th></tr></thead>
                    <tbody>
                      @for (e of j.errors; track $index) {
                        <tr class="border-t border-red-50"><td class="px-2 py-1 font-mono">{{ e.row }}</td><td class="px-2 py-1 text-gray-700">{{ e.message }}</td></tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }
          }
        }
      }

      <ng-container footer>
        @if (step() === 2 || (step() === 3 && job()?.status === 'Ready')) {
          <button type="button" class="btn-secondary mr-auto" [disabled]="busy()" (click)="step.set(step() - 1)">{{ 'READER_IMPORT.BACK' | translate }}</button>
        }
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CLOSE' | translate }}</button>
        @switch (step()) {
          @case (1) {
            <button type="button" class="btn-primary" [disabled]="busy() || !file()" (click)="inspect()">
              <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'arrow_forward' }}</span> {{ 'READER_IMPORT.NEXT' | translate }}</button>
          }
          @case (2) {
            <button type="button" class="btn-primary" [disabled]="busy() || !mapping()['cardno']" (click)="preview()">
              <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'preview' }}</span> {{ 'READER_IMPORT.PREVIEW' | translate }}</button>
          }
          @case (3) {
            @if (job()?.status === 'Ready') {
              <button type="button" class="btn-primary" [disabled]="busy() || !canRun()" (click)="start()">
                <span class="material-icons text-[16px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'play_arrow' }}</span> {{ 'READER_IMPORT.RUN' | translate }}</button>
            }
          }
        }
      </ng-container>
    </app-modal>
  `,
})
export class ReaderImport {
  readonly closed = output<void>();
  /** Có bạn đọc được thêm/sửa (danh sách cần tải lại). */
  readonly imported = output<void>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly language = inject(Language);
  protected readonly catalogs = inject(ReaderCatalogs);

  protected readonly step = signal(1);
  protected readonly busy = signal(false);
  protected readonly file = signal<File | null>(null);
  protected readonly columns = signal<ReaderImportColumns | null>(null);
  protected readonly mapping = signal<Record<string, number>>({});
  protected readonly job = signal<ReaderImportJob | null>(null);
  protected overwrite = false;
  protected autoCreate = false;
  protected opacCardNo = false;
  protected readerTypeId: number | null = null;
  protected readonly byCode = { classByCode: false, courseByCode: false, orgByCode: false };
  protected readonly byCodeOptions = [
    { key: 'classByCode', label: 'READER.ACADEMIC_CLASS' },
    { key: 'courseByCode', label: 'READER.COURSE' },
    { key: 'orgByCode', label: 'READER.ORG' },
  ] as const;

  protected readonly running = computed(() => ['Queued', 'Running'].includes(this.job()?.status ?? ''));
  protected readonly finished = computed(() => ['Done', 'Failed'].includes(this.job()?.status ?? ''));
  protected readonly canRun = computed(() => { const j = this.job(); return !!j && j.toCreate + j.toUpdate > 0; });
  protected readonly percent = computed(() => { const j = this.job(); return j && j.total ? Math.round(j.processed * 100 / j.total) : 0; });

  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected label(key: string): string { return FIELD_LABELS[key] ?? key; }

  protected pick(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (file && file.size > MAX_BYTES) {
      this.toastr.error('READER_IMPORT.FILE_TOO_LARGE');
      this.file.set(null);
      return;
    }
    this.file.set(file);
    this.columns.set(null);
    this.job.set(null);
  }

  protected map(key: string, index: number): void {
    this.mapping.update(m => {
      const next = { ...m };
      if (index) next[key] = index; else delete next[key];
      return next;
    });
  }

  protected async inspect(): Promise<void> {
    const file = this.file();
    if (!file) return;
    await this.run(async () => {
      const columns = await this.api.readerImportColumns(file);
      this.columns.set(columns);
      this.mapping.set({ ...columns.suggested });
      this.step.set(2);
    });
  }

  protected async preview(): Promise<void> {
    const file = this.file(), columns = this.columns();
    if (!file || !columns) return;
    const mapping = { ...this.mapping() };
    if (this.readerTypeId !== null) delete mapping['readertype'];
    await this.run(async () => {
      this.job.set(await this.api.previewReaderImport(file, {
        headerRow: columns.headerRow, columns: mapping, overwrite: this.overwrite, readerTypeId: this.readerTypeId, autoCreateRefs: this.autoCreate,
        opacCardNoPassword: this.opacCardNo, ...this.byCode,
      }));
      this.step.set(3);
    });
  }

  protected async start(): Promise<void> {
    const job = this.job();
    if (!job) return;
    await this.run(async () => {
      this.job.set(await this.api.startReaderImport(job.id));
      this.poll();
    });
  }

  protected async downloadTemplate(): Promise<void> {
    await this.run(async () => saveFile(await this.api.importer('readers', 'patron').template(),
      `${this.language.t('IMPORT.TEMPLATE_FILE_NAME')} ${this.language.t('MENU.READERS')}.xlsx`));
  }

  protected async downloadErrors(): Promise<void> {
    const job = this.job();
    if (!job) return;
    await this.run(async () => saveFile(await this.api.readerImportErrors(job.id),
      `${this.language.t('READER_IMPORT.ERROR_FILE_NAME')} ${job.fileName.replace(/\.xlsx$/i, '')}.xlsx`));
  }

  /** Theo dõi tiến độ mỗi giây tới khi lượt nhập xong hoặc lỗi. */
  private poll(): void {
    this.timer = setTimeout(async () => {
      const job = this.job();
      if (!job) return;
      try {
        const current = await this.api.readerImportJob(job.id);
        this.job.set(current);
        if (current.status === 'Done' || current.status === 'Failed') {
          if (current.created + current.updated > 0) this.imported.emit();
          return;
        }
      } catch (e) {
        this.toastr.error(errorMessage(e));
        return;
      }
      this.poll();
    }, 1000);
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
