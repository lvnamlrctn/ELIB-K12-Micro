import { Component, DestroyRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, ReaderImportJob, ReaderSearch, ReaderSelection, ReaderSelectionPreview, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { ReaderCatalogs } from './reader-catalogs';
import { DateInput } from '../../shared/date-input';

export type ReaderBatchMode = 'update' | 'groups';

interface BatchChange {
  readerTypeId: number | null; classId: number | null; courseId: number | null; orgId: number | null; status: number | null;
  issueDate: string; expireDate: string;
}

/** Kết quả sửa hàng loạt: ngay (≤ 2.000 bạn đọc) hoặc lượt chạy nền đã xong. */
interface BatchOutcome { updated: number; failed: number; errors: string[]; }

/**
 * Thao tác hàng loạt kiểu ELIB-LRC (BatchUpdate / ReaderInGroup): áp cho bạn đọc đã tích hoặc TOÀN BỘ kết quả lọc hiện tại. Theo bộ lọc thì
 * bấm "Xem trước" để biết số bạn đọc bị ảnh hưởng — server trả mã xác nhận 15 phút gắn với đúng bộ lọc đó. Sửa trên 2.000 bạn đọc thì
 * server chạy nền: hộp thoại hiện tiến độ tới khi xong; bạn đọc vi phạm quy tắc được bỏ qua và liệt kê.
 */
@Component({
  selector: 'app-reader-batch',
  imports: [DateInput, FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal [title]="(mode() === 'update' ? 'READER_BATCH.TITLE_UPDATE' : 'READER_BATCH.TITLE_GROUPS') | translate" widthClass="max-w-2xl" (closed)="closed.emit()">
      @if (job() || outcome()) {
        @if (job(); as j) {
          @if (j.status !== 'Done' && j.status !== 'Failed') {
            <p class="text-sm text-gray-600 mb-2">{{ 'READER_BATCH.BACKGROUND' | translate: { count: j.total } }}</p>
            <div class="flex justify-between text-xs text-gray-600 mb-1">
              <span>{{ ('READER_IMPORT.STATUS_' + j.status) | translate }}</span><span>{{ j.processed }} / {{ j.total }}</span>
            </div>
            <div class="h-2 rounded-full bg-gray-100 overflow-hidden" role="progressbar" [attr.aria-valuenow]="percent()" aria-valuemin="0" aria-valuemax="100">
              <div class="h-full bg-blue-600 transition-all" [style.width.%]="percent()"></div>
            </div>
          }
          @if (j.status === 'Failed') { <div class="alert-error">{{ j.message }}</div> }
        }
        @if (outcome(); as o) {
          <div class="alert-success">{{ 'READER.BULK_DONE' | translate: { count: o.updated } }}</div>
          @if (o.failed) {
            <div class="mt-3">
              <p class="text-sm font-medium text-red-700 mb-1">{{ 'READER_BATCH.SKIPPED' | translate: { count: o.failed } }}</p>
              <ul class="max-h-56 overflow-y-auto rounded border border-red-100 text-xs text-gray-700 divide-y divide-red-50">
                @for (e of o.errors; track $index) { <li class="px-2 py-1">{{ e }}</li> }
              </ul>
            </div>
          }
        }
      } @else {
      <fieldset class="mb-4">
        <legend class="form-label">{{ 'READER_BATCH.APPLY_TO' | translate }}</legend>
        <div class="flex flex-col sm:flex-row gap-2 sm:gap-6 text-sm">
          <label class="flex items-center gap-2 cursor-pointer" [class.opacity-50]="!selectedIds().length">
            <input type="radio" name="scope" [checked]="scope() === 'selected'" [disabled]="!selectedIds().length" (change)="setScope('selected')" />
            {{ 'READER_BATCH.SCOPE_SELECTED' | translate: { count: selectedIds().length } }}</label>
          <label class="flex items-center gap-2 cursor-pointer">
            <input type="radio" name="scope" [checked]="scope() === 'filter'" (change)="setScope('filter')" />
            {{ 'READER_BATCH.SCOPE_FILTER' | translate: { count: total() } }}</label>
        </div>
      </fieldset>

      @if (mode() === 'update') {
        <p class="text-xs text-gray-500 mb-3">{{ 'READER.BULK_HINT' | translate }}</p>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div><label class="form-label" for="b-type">{{ 'READER.READER_TYPE' | translate }}</label>
            <select id="b-type" class="input" [(ngModel)]="change.readerTypeId"><option [ngValue]="null">{{ 'READER.KEEP' | translate }}</option>
              @for (t of c.types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
          <div><label class="form-label" for="b-class">{{ 'READER.ACADEMIC_CLASS' | translate }}</label>
            <select id="b-class" class="input" [(ngModel)]="change.classId"><option [ngValue]="null">{{ 'READER.KEEP' | translate }}</option>
              @for (o of c.classes(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="b-course">{{ 'READER.COURSE' | translate }}</label>
            <select id="b-course" class="input" [(ngModel)]="change.courseId"><option [ngValue]="null">{{ 'READER.KEEP' | translate }}</option>
              @for (o of c.courses(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="b-status">{{ 'READER.STATUS' | translate }}</label>
            <select id="b-status" class="input" [(ngModel)]="change.status"><option [ngValue]="null">{{ 'READER.KEEP' | translate }}</option>
              <option [ngValue]="2">{{ 'COMMON.ACTIVE' | translate }}</option><option [ngValue]="1">{{ 'READER.LOCKED' | translate }}</option></select></div>
          <div class="sm:col-span-2"><label class="form-label" for="b-org">{{ 'READER.ORG' | translate }}</label>
            <select id="b-org" class="input" [(ngModel)]="change.orgId"><option [ngValue]="null">{{ 'READER.KEEP' | translate }}</option>
              @for (o of c.orgOptions(); track o.id) { <option [ngValue]="o.id">{{ indent(o.depth) }}{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="b-issue">{{ 'READER.ISSUED_DATE' | translate }}</label><app-date-input fieldId="b-issue" [(ngModel)]="change.issueDate"></app-date-input></div>
          <div><label class="form-label" for="b-exp">{{ 'READER.EXPIRED_DATE' | translate }}</label><app-date-input fieldId="b-exp" [(ngModel)]="change.expireDate"></app-date-input></div>
          <div class="sm:col-span-2"><label class="form-label" for="b-reason">{{ 'READER_HISTORY.REASON' | translate }}</label>
            <input id="b-reason" class="input" maxlength="500" [(ngModel)]="reason" [placeholder]="'READER_HISTORY.REASON_PH' | translate" /></div>
        </div>
      } @else {
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div><label class="form-label" for="b-group">{{ 'READER_BATCH.GROUP' | translate }}</label>
            <select id="b-group" class="input" [(ngModel)]="groupId"><option [ngValue]="null">—</option>
              @for (g of c.groups(); track g.id) { <option [ngValue]="g.id">{{ g.name }}</option> }</select></div>
          <fieldset><legend class="form-label">{{ 'READER_BATCH.GROUP_ACTION' | translate }}</legend>
            <div class="flex gap-4 text-sm pt-2">
              <label class="flex items-center gap-2 cursor-pointer"><input type="radio" name="gact" [checked]="add" (change)="add = true" /> {{ 'READER_BATCH.GROUP_ADD' | translate }}</label>
              <label class="flex items-center gap-2 cursor-pointer"><input type="radio" name="gact" [checked]="!add" (change)="add = false" /> {{ 'READER_BATCH.GROUP_REMOVE' | translate }}</label>
            </div></fieldset>
        </div>
        @if (!c.groups().length) { <p class="text-xs text-amber-700 mt-2">{{ 'READER_BATCH.NO_GROUPS' | translate }}</p> }
      }

      @if (scope() === 'filter') {
        <div class="mt-4 rounded-lg border px-4 py-3 text-sm" [class]="preview() ? 'border-amber-300 bg-amber-50 text-amber-800' : 'border-gray-200 bg-gray-50 text-gray-600'">
          @if (preview(); as p) {
            <span class="material-icons text-[18px] align-middle">warning</span> {{ 'READER_BATCH.PREVIEW_RESULT' | translate: { count: p.count } }}
          } @else { {{ 'READER_BATCH.PREVIEW_HINT' | translate }} }
        </div>
      }
      }

      <ng-container footer>
        @if (job() || outcome()) {
          <button type="button" class="btn-primary" [disabled]="!finished()" (click)="done.emit()">{{ 'COMMON.CLOSE' | translate }}</button>
        } @else {
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        @if (scope() === 'filter' && !preview()) {
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="runPreview()">
            <span class="material-icons text-[16px]">preview</span> {{ 'READER_BATCH.PREVIEW' | translate }}</button>
        } @else {
          <button type="button" class="btn-primary" [disabled]="busy() || !ready()" (click)="apply()">
            <span class="material-icons text-[16px]">done_all</span> {{ 'READER_BATCH.APPLY' | translate: { count: affected() } }}</button>
        }
        }
      </ng-container>
    </app-modal>
  `,
})
export class ReaderBatch implements OnInit {
  readonly mode = input.required<ReaderBatchMode>();
  readonly selectedIds = input.required<string[]>();
  /** Bộ lọc hiện tại của danh sách (đã gom đơn vị con). */
  readonly search = input.required<ReaderSearch>();
  /** Tổng số kết quả lọc đang hiển thị. */
  readonly total = input.required<number>();
  readonly closed = output<void>();
  readonly done = output<void>();

  protected readonly c = inject(ReaderCatalogs);
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly scope = signal<'selected' | 'filter'>('filter');
  protected readonly preview = signal<ReaderSelectionPreview | null>(null);
  protected readonly busy = signal(false);
  protected change: BatchChange = { readerTypeId: null, classId: null, courseId: null, orgId: null, status: null, issueDate: '', expireDate: '' };
  protected groupId: number | null = null;
  protected add = true;
  protected reason = '';
  protected readonly job = signal<ReaderImportJob | null>(null);
  protected readonly outcome = signal<BatchOutcome | null>(null);
  protected readonly finished = computed(() => {
    const status = this.job()?.status;
    return !this.job() || status === 'Done' || status === 'Failed';
  });
  protected readonly percent = computed(() => {
    const j = this.job();
    return j?.total ? Math.round((j.processed * 100) / j.total) : 0;
  });
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected readonly affected = computed(() => this.scope() === 'selected' ? this.selectedIds().length : this.preview()?.count ?? 0);

  ngOnInit(): void {
    if (this.selectedIds().length) this.scope.set('selected');
  }

  protected indent(depth: number): string {
    return '   '.repeat(depth);
  }

  protected setScope(scope: 'selected' | 'filter'): void {
    this.scope.set(scope);
    this.preview.set(null);
  }

  protected ready(): boolean {
    if (this.affected() === 0) return false;
    if (this.mode() === 'groups') return this.groupId !== null;
    const c = this.change;
    return [c.readerTypeId, c.classId, c.courseId, c.orgId, c.status].some((v) => v !== null) || !!c.issueDate || !!c.expireDate;
  }

  protected async runPreview(): Promise<void> {
    await this.run(async () => this.preview.set(await this.api.previewReaderSelection(this.search())));
  }

  protected async apply(): Promise<void> {
    const p = this.preview();
    const selection: ReaderSelection = this.scope() === 'selected'
      ? { publicIds: this.selectedIds() }
      : { search: this.search(), token: p?.token ?? null };
    await this.run(async () => {
      if (this.mode() === 'groups') {
        const { count } = await this.api.changeReaderGroups(selection, this.groupId!, this.add);
        this.toastr.success(this.add ? 'READER_BATCH.GROUP_ADDED' : 'READER_BATCH.GROUP_REMOVED', { count });
      } else {
        const c = this.change;
        const result = await this.api.batchUpdateReaders({
          selection, readerTypeId: c.readerTypeId, classId: c.classId, courseId: c.courseId, orgId: c.orgId, status: c.status,
          issueDate: c.issueDate || null, expireDate: c.expireDate || null,
        }, this.reason);
        if (result.jobId) {
          this.job.set(await this.api.readerBatchJob(result.jobId));
          this.poll();
          return;
        }
        if (result.failed) {
          this.outcome.set({ updated: result.count, failed: result.failed, errors: result.errors ?? [] });
          return;
        }
        this.toastr.success('READER.BULK_DONE', { count: result.count });
      }
      this.done.emit();
    }, true);
  }

  /** Theo dõi lượt chạy nền mỗi giây tới khi xong; xong thì hiện số đã sửa và bạn đọc bị bỏ qua. */
  private poll(): void {
    this.timer = setTimeout(async () => {
      const job = this.job();
      if (!job) return;
      try {
        const current = await this.api.readerBatchJob(job.id);
        this.job.set(current);
        if (current.status === 'Done') {
          this.outcome.set({ updated: current.updated, failed: current.failed, errors: current.errors.map((e) => e.message) });
          return;
        }
        if (current.status === 'Failed') return;
      } catch (e) {
        this.toastr.error(errorMessage(e));
        return;
      }
      this.poll();
    }, 1000);
  }

  /** Mã xem trước hết hạn / bộ lọc đổi → xem trước lại. */
  private async run(action: () => Promise<void>, resetPreviewOnError = false): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (e) {
      this.toastr.error(errorMessage(e));
      if (resetPreviewOnError) this.preview.set(null);
    } finally {
      this.busy.set(false);
    }
  }
}
