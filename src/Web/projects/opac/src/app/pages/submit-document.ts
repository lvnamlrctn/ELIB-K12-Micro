import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { DigitalApi, DigitalDigType, DigitalSubmission, DigitalSubmissionStatus } from '../core/digital';
import { Language } from '../core/i18n';
import { ReaderAuth } from '../core/reader-auth';

const MAX_SIZE = 50 * 1024 * 1024;
const ACCEPTED = ['application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'];

/**
 * Nộp tài liệu bạn đọc (LRC: DocumentSubmission, không có luồng duyệt — tự thiết kế thêm Duyệt/Từ chối ở màn quản trị, xem
 * quyết định #5 của plan giai đoạn 2). Cần đăng nhập bạn đọc. Luồng tải lên 3 bước: xin vé → PUT thẳng vào kho → hoàn tất
 * (DigitalApi.submitDocument lo hết, khuôn admin's upload nhưng gọi API bạn đọc thay vì media trực tiếp).
 */
@Component({
  selector: 'opac-submit-document',
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <div class="max-w-3xl mx-auto px-4 py-6">
      <h1 class="text-xl font-bold text-slate-900">{{ 'OPAC.SUBMIT_DOC.TITLE' | translate }}</h1>
      <p class="mt-1 text-sm text-slate-600">{{ 'OPAC.SUBMIT_DOC.INTRO' | translate }}</p>

      @if (!auth.loggedIn()) {
        <div class="mt-6 bg-white border border-slate-200 rounded-xl p-6 text-center">
          <p class="text-slate-600">{{ 'OPAC.SUBMIT_DOC.LOGIN_REQUIRED' | translate }}</p>
          <button type="button" class="mt-3 inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700"
                  (click)="auth.login(router.url)">
            <span class="material-icons text-[18px]">login</span>{{ 'OPAC.NAVBAR.LOGIN' | translate }}</button>
        </div>
      } @else {
        <form class="mt-6 bg-white border border-slate-200 rounded-xl p-6 space-y-4" (ngSubmit)="submit()">
          <div>
            <label for="sd-title" class="block text-sm font-medium text-slate-700 mb-1">{{ 'OPAC.SUBMIT_DOC.DOC_TITLE' | translate }} *</label>
            <input id="sd-title" required maxlength="1000" [(ngModel)]="title" name="title" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm" />
          </div>
          <div class="grid sm:grid-cols-2 gap-4">
            <div>
              <label for="sd-author" class="block text-sm font-medium text-slate-700 mb-1">{{ 'OPAC.SUBMIT_DOC.AUTHOR' | translate }}</label>
              <input id="sd-author" maxlength="500" [(ngModel)]="author" name="author" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm" />
            </div>
            <div>
              <label for="sd-type" class="block text-sm font-medium text-slate-700 mb-1">{{ 'OPAC.SUBMIT_DOC.DOC_TYPE' | translate }}</label>
              <select id="sd-type" [(ngModel)]="digTypeId" name="digTypeId" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm">
                <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
                @for (t of digTypes(); track t.id) { <option [ngValue]="t.id">{{ t.descriptionVn || t.code }}</option> }
              </select>
            </div>
          </div>
          <div>
            <label for="sd-abstract" class="block text-sm font-medium text-slate-700 mb-1">{{ 'OPAC.SUBMIT_DOC.ABSTRACT' | translate }}</label>
            <textarea id="sd-abstract" rows="3" maxlength="4000" [(ngModel)]="abstractText" name="abstractText"
                      class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm"></textarea>
          </div>
          <div>
            <label for="sd-file" class="block text-sm font-medium text-slate-700 mb-1">{{ 'OPAC.SUBMIT_DOC.FILE' | translate }} *</label>
            <input id="sd-file" type="file" accept=".pdf,.doc,.docx" (change)="onFileSelect($event)" class="block w-full text-sm" />
            <p class="mt-1 text-xs text-slate-500">{{ 'OPAC.SUBMIT_DOC.FILE_HINT' | translate }}</p>
          </div>
          @if (error(); as err) { <p class="text-red-600 text-sm">{{ err }}</p> }
          @if (success()) { <p class="text-green-700 text-sm">{{ 'OPAC.SUBMIT_DOC.SUBMIT_SUCCESS' | translate }}</p> }
          <button type="submit" [disabled]="busy() || !file || !title.trim()"
                  class="px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50">
            {{ busy() ? ('OPAC.SUBMIT_DOC.UPLOADING' | translate) : ('OPAC.SUBMIT_DOC.SUBMIT' | translate) }}</button>
        </form>

        <h2 class="mt-8 font-semibold text-slate-900">{{ 'OPAC.SUBMIT_DOC.MY_SUBMISSIONS' | translate }}</h2>
        @if (submissions().length) {
          <ul class="mt-3 divide-y divide-slate-100 bg-white border border-slate-200 rounded-xl">
            @for (s of submissions(); track s.publicId) {
              <li class="p-4">
                <div class="flex items-center justify-between gap-3 flex-wrap">
                  <div class="min-w-0">
                    <div class="font-medium text-slate-900">{{ s.title }}</div>
                    @if (s.author) { <div class="text-sm text-slate-500">{{ s.author }}</div> }
                  </div>
                  <span class="px-2 py-0.5 rounded text-xs shrink-0"
                        [class.bg-amber-50]="s.status === SubmissionStatus.Pending" [class.text-amber-700]="s.status === SubmissionStatus.Pending"
                        [class.bg-green-50]="s.status === SubmissionStatus.Approved" [class.text-green-700]="s.status === SubmissionStatus.Approved"
                        [class.bg-red-50]="s.status === SubmissionStatus.Rejected" [class.text-red-700]="s.status === SubmissionStatus.Rejected">
                    {{ statusLabel(s.status) | translate }}
                  </span>
                </div>
                @if (s.status === SubmissionStatus.Approved && s.createdItemPublicId) {
                  <a [routerLink]="['/thu-vien-so', s.createdItemPublicId]" class="mt-2 inline-block text-sm text-blue-600 hover:underline">
                    {{ 'OPAC.SUBMIT_DOC.VIEW_ITEM' | translate }}</a>
                }
                @if (s.status === SubmissionStatus.Rejected && s.rejectReason) {
                  <p class="mt-2 text-sm text-red-600">{{ 'OPAC.SUBMIT_DOC.REJECT_REASON' | translate: { reason: s.rejectReason } }}</p>
                }
              </li>
            }
          </ul>
        } @else {
          <p class="mt-2 text-sm text-slate-500">{{ 'OPAC.SUBMIT_DOC.NO_SUBMISSIONS' | translate }}</p>
        }
      }
    </div>
  `,
})
export class SubmitDocument implements OnInit {
  private readonly api = inject(DigitalApi);
  protected readonly auth = inject(ReaderAuth);
  protected readonly router = inject(Router);
  private readonly language = inject(Language);

  protected readonly SubmissionStatus = DigitalSubmissionStatus;
  private static readonly STATUS_KEYS: Record<number, string> = {
    [DigitalSubmissionStatus.Pending]: 'OPAC.SUBMIT_DOC.STATUS_1',
    [DigitalSubmissionStatus.Approved]: 'OPAC.SUBMIT_DOC.STATUS_2',
    [DigitalSubmissionStatus.Rejected]: 'OPAC.SUBMIT_DOC.STATUS_3',
  };
  protected readonly digTypes = signal<DigitalDigType[]>([]);
  protected readonly submissions = signal<DigitalSubmission[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly success = signal(false);

  protected title = '';
  protected author = '';
  protected digTypeId: number | null = null;
  protected abstractText = '';
  protected file: File | null = null;

  async ngOnInit(): Promise<void> {
    this.digTypes.set(await this.api.digTypes().catch(() => []));
    if (this.auth.loggedIn()) await this.loadSubmissions();
  }

  protected statusLabel(status: number): string {
    return SubmitDocument.STATUS_KEYS[status] ?? '';
  }

  protected onFileSelect(e: Event): void {
    const input = e.target as HTMLInputElement;
    const picked = input.files?.[0] ?? null;
    this.error.set(null);
    if (picked && !ACCEPTED.includes(picked.type)) {
      this.error.set(this.language.t('OPAC.SUBMIT_DOC.FILE_TYPE_INVALID'));
      this.file = null;
      input.value = '';
      return;
    }
    if (picked && picked.size > MAX_SIZE) {
      this.error.set(this.language.t('OPAC.SUBMIT_DOC.FILE_TOO_LARGE'));
      this.file = null;
      input.value = '';
      return;
    }
    this.file = picked;
  }

  protected async submit(): Promise<void> {
    if (!this.file) return;
    this.busy.set(true);
    this.error.set(null);
    this.success.set(false);
    try {
      await this.api.submitDocument(this.file, {
        title: this.title.trim(), author: this.author.trim() || null, digTypeId: this.digTypeId, abstract: this.abstractText.trim() || null,
      });
      this.success.set(true);
      this.title = '';
      this.author = '';
      this.digTypeId = null;
      this.abstractText = '';
      this.file = null;
      await this.loadSubmissions();
    } catch (e) {
      this.error.set(this.language.apiError(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async loadSubmissions(): Promise<void> {
    this.submissions.set(await this.api.myDocumentSubmissions().catch(() => []));
  }
}
