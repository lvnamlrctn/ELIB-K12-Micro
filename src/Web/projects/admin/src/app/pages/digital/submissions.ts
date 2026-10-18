import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage as Page, DOCUMENT_SUBMISSION_STATUSES, DocumentSubmission, DocumentSubmissionSearch, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Modal, Paginator } from '../../shared/ui';

/**
 * Duyệt tài liệu bạn đọc nộp qua OPAC (LRC: DocumentSubmission — LRC chưa có màn duyệt, tự thiết kế theo quyết định #5 của plan
 * giai đoạn 2, quyền DIGITAL_SUBMISSIONS). Thủ thư chỉ tra cứu + Duyệt (tạo tài liệu số Ẩn) / Từ chối (lý do bắt buộc).
 */
@Component({
  selector: 'app-digital-submissions',
  imports: [FormsModule, LDatePipe, Loading, Modal, Paginator, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'DIGITAL_SUBMISSIONS_PAGE.TITLE' | translate }}</h4>
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
        <div>
          <label for="ds-status" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
          <select id="ds-status" class="input" [(ngModel)]="search.submissionStatus" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }
          </select>
        </div>
        <div class="flex items-end">
          <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1100px]">
          <thead><tr>
            <th class="th w-44">{{ 'DIGITAL_LOANS_PAGE.READER' | translate }}</th><th class="th">{{ 'DIGITAL_SUBMISSIONS_PAGE.TITLE_COL' | translate }}</th>
            <th class="th w-32">{{ 'DIGITAL_SUBMISSIONS_PAGE.DIG_TYPE' | translate }}</th><th class="th w-36">{{ 'DIGITAL_SUBMISSIONS_PAGE.SUBMITTED_AT' | translate }}</th>
            <th class="th w-32">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-40 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (s of page()?.items ?? []; track s.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ s.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ s.cardNo }}</div></td>
                <td class="td">{{ s.title }}<div class="text-[11px] text-gray-400">{{ s.author }}</div></td>
                <td class="td text-[13px]">{{ s.digTypeCode ?? '—' }}</td>
                <td class="td text-[13px]">{{ s.createdAt | ldate: 'dd/MM/yyyy HH:mm' }}</td>
                <td class="td"><span [class]="statusOf(s.status).badge">{{ statusOf(s.status).name | translate }}</span></td>
                <td class="td text-center">
                  <div class="flex items-center justify-center gap-1">
                    <button class="icon-btn text-blue-600 hover:bg-blue-50" [title]="'DIGITAL_SUBMISSIONS_PAGE.VIEW_FILE' | translate" (click)="viewFile(s)">
                      <span class="material-icons text-[18px]">description</span></button>
                    @if (can('edit') && s.status === 1) {
                      <button class="icon-btn text-green-600 hover:bg-green-50" [title]="'DIGITAL_SUBMISSIONS_PAGE.APPROVE' | translate" [disabled]="busy()" (click)="approve(s)">
                        <span class="material-icons text-[18px]">check_circle</span></button>
                      <button class="icon-btn text-red-600 hover:bg-red-50" [title]="'DIGITAL_SUBMISSIONS_PAGE.REJECT' | translate" (click)="rejecting.set(s)">
                        <span class="material-icons text-[18px]">cancel</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty { <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr> }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" /> }
    </div>

    @if (rejecting(); as s) {
      <app-modal [title]="'DIGITAL_SUBMISSIONS_PAGE.REJECT_TITLE' | translate" widthClass="max-w-md" (closed)="rejecting.set(null)">
        <p class="text-sm text-gray-600 mb-3">{{ s.title }}</p>
        <label for="ds-reason" class="form-label">{{ 'DIGITAL_LOANS_PAGE.REASON' | translate }} <span class="text-red-500">*</span></label>
        <input id="ds-reason" class="input" maxlength="500" [(ngModel)]="reason" [ngModelOptions]="{ standalone: true }" />
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="rejecting.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-danger" [disabled]="busy() || !reason.trim()" (click)="reject(s)">{{ 'DIGITAL_SUBMISSIONS_PAGE.REJECT' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class DigitalSubmissions implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly client = this.api.crud<DocumentSubmission>('document-submissions', 'digital');

  protected readonly statuses = DOCUMENT_SUBMISSION_STATUSES;
  protected search: DocumentSubmissionSearch = { submissionStatus: null, pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<Page<DocumentSubmission> | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly rejecting = signal<DocumentSubmission | null>(null);
  protected reason = '';

  ngOnInit(): void {
    void this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`DIGITAL_SUBMISSIONS:${action}`);
  }

  protected statusOf(code: number) {
    return this.statuses.find((s) => s.code === code) ?? { name: String(code), badge: 'badge-off' };
  }

  protected find(): void {
    this.search.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.search.pageIndex = e.pageIndex;
    this.search.pageSize = e.pageSize;
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.client.search(this.search));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected async viewFile(s: DocumentSubmission): Promise<void> {
    try {
      window.open(await this.api.fileUrl(s.mediaFileId), '_blank', 'noopener');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async approve(s: DocumentSubmission): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.approveDocumentSubmission(s.publicId);
      this.toastr.success('DIGITAL_SUBMISSIONS_PAGE.APPROVE_SUCCESS');
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async reject(s: DocumentSubmission): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.rejectDocumentSubmission(s.publicId, this.reason.trim());
      this.toastr.success('DIGITAL_SUBMISSIONS_PAGE.REJECT_SUCCESS');
      this.rejecting.set(null);
      this.reason = '';
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
