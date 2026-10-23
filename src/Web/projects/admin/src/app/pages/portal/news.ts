import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage as Page, MediaFile, PortalAttachment, PortalNews, PortalNewsSearch, STATUS_ACTIVE, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { MediaPicker } from '../../shared/media-picker';
import { RichText } from '../../shared/rich-text';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator, StatusBadge } from '../../shared/ui';
import { DateInput } from '../../shared/date-input';

interface NewsForm {
  publicId: string | null; id: number | null;
  title: string; slug: string; brief: string; content: string; images: string | null; categoryId: number | null; eventId: number | null; language: string;
  author: string; source: string; keyword: string; featured: boolean; allowComment: boolean; startTime: string; endTime: string;
  metaTitle: string; metaKeyword: string; metaDescription: string; status: number;
}

type Option = { id: number; label: string };

/** ISO → giá trị ô datetime-local (giờ máy người dùng); trống = chưa hẹn. */
const toLocal = (iso: string | null): string => {
  if (!iso) return '';
  const d = new Date(iso);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
};
const fromLocal = (value: string): string | null => (value ? new Date(value).toISOString() : null);

/**
 * Tin tức cổng thông tin (LRC: News, quyền PORTAL_NEWS). Người chỉ có quyền Sửa soạn bản nháp; đăng/gỡ và sửa tin đã đăng cần quyền "Đăng bài" (publish).
 * Nội dung soạn bằng trình soạn HTML (server làm sạch lại trước khi lưu); ảnh đại diện chọn từ thư viện ảnh của đơn vị.
 */
@Component({
  selector: 'app-portal-news',
  imports: [DateInput, FormsModule, LDatePipe, Modal, ConfirmDelete, Loading, Paginator, StatusBadge, RichText, MediaPicker, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'PORTAL_NEWS_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
        <div class="md:col-span-2">
          <label class="field-label" for="nw-kw">{{ 'COMMON.SEARCH' | translate }}</label>
          <input id="nw-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" />
        </div>
        <div>
          <label class="field-label" for="nw-cat">{{ 'PORTAL_NEWS_PAGE.CATEGORY' | translate }}</label>
          <select id="nw-cat" class="input" [(ngModel)]="search.categoryId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (o of categories(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }
          </select>
        </div>
        <div>
          <label class="field-label" for="nw-status">{{ 'COMMON.STATUS' | translate }}</label>
          <select id="nw-status" class="input" [(ngModel)]="search.status" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option [ngValue]="2">{{ 'PORTAL_NEWS_PAGE.PUBLISHED' | translate }}</option>
            <option [ngValue]="1">{{ 'PORTAL_NEWS_PAGE.DRAFT' | translate }}</option>
          </select>
        </div>
      </div>
    </div>

    <div class="panel flex gap-3 items-center justify-between flex-wrap">
      @if (can('add')) {
        <button (click)="openCreate()" class="btn-add"><span class="material-icons text-[18px]">add</span> {{ 'COMMON.ADD_BTN' | translate }}</button>
      }
      <span class="text-sm text-gray-500">{{ 'COMMON.RECORDS_N' | translate: { count: page()?.totalCount ?? 0 } }}</span>
    </div>

    <div class="panel !p-0 relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead><tr>
            <th class="th">{{ 'PORTAL_NEWS_PAGE.TITLE_COL' | translate }}</th><th class="th">{{ 'PORTAL_NEWS_PAGE.CATEGORY' | translate }}</th>
            <th class="th">{{ 'PORTAL_NEWS_PAGE.PUBLISHED_AT' | translate }}</th><th class="th !text-right">{{ 'PORTAL_NEWS_PAGE.VIEWS' | translate }}</th>
            <th class="th !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (n of page()?.items ?? []; track n.publicId) {
              <tr class="hover:bg-gray-50">
                <td class="td max-w-[360px]"><div class="font-medium text-gray-800 truncate" [title]="n.title">{{ n.title }}</div><div class="text-xs text-gray-400 font-mono truncate">{{ n.slug }}</div></td>
                <td class="td">{{ n.categoryName }}</td>
                <td class="td whitespace-nowrap">{{ n.publishedAt ? (n.publishedAt | ldate: 'dd/MM/yyyy HH:mm') : '' }}</td>
                <td class="td text-right">{{ n.totalView }}</td>
                <td class="td text-center"><app-status-badge [status]="n.status" /></td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1">
                    @if (can('publish')) {
                      <button (click)="toggle(n)" class="icon-btn bg-green-50 text-green-700 hover:bg-green-100" [title]="(n.status === 2 ? 'PORTAL_NEWS_PAGE.UNPUBLISH' : 'PORTAL_NEWS_PAGE.PUBLISH') | translate">
                        <span class="material-icons text-[18px]">{{ n.status === 2 ? 'visibility_off' : 'publish' }}</span></button>
                    }
                    @if (can('edit') && (n.status !== 2 || can('publish'))) {
                      <button (click)="openEdit(n)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate"><span class="material-icons text-[18px]">edit</span></button>
                    }
                    @if (can('delete') && (n.status !== 2 || can('publish'))) {
                      <button (click)="deleting.set(n)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty { <tr><td colspan="6" class="td text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" /> }
    </div>

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'COMMON.EDIT' : 'COMMON.ADD_BTN'" widthClass="max-w-5xl" (closed)="form.set(null)">
        <form id="news-form" class="grid grid-cols-2 gap-4" (ngSubmit)="save()">
          <div class="col-span-2"><label class="form-label" for="n-title">{{ 'PORTAL_NEWS_PAGE.TITLE_COL' | translate }} *</label><input id="n-title" class="input" name="title" required maxlength="500" [(ngModel)]="f.title" /></div>
          <div><label class="form-label" for="n-slug">{{ 'PORTAL_NEWS_PAGE.SLUG' | translate }}</label><input id="n-slug" class="input font-mono" name="slug" maxlength="150" [(ngModel)]="f.slug" [placeholder]="'PORTAL_NEWS_PAGE.SLUG_HINT' | translate" /></div>
          <div><label class="form-label" for="n-cat">{{ 'PORTAL_NEWS_PAGE.CATEGORY' | translate }}</label>
            <select id="n-cat" class="input" name="category" [(ngModel)]="f.categoryId"><option [ngValue]="null">—</option>@for (o of categories(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select></div>
          <div class="col-span-2"><label class="form-label" for="n-brief">{{ 'PORTAL_NEWS_PAGE.BRIEF' | translate }}</label><textarea id="n-brief" rows="2" class="input" name="brief" maxlength="2000" [(ngModel)]="f.brief"></textarea></div>

          <div class="col-span-2 flex items-center gap-3">
            @if (f.images) { <img [src]="f.images" alt="" class="h-16 w-24 object-cover rounded border border-gray-200" /> }
            <button type="button" class="btn-secondary" (click)="pickingCover.set(true)"><span class="material-icons text-[18px]">image</span> {{ 'PORTAL_NEWS_PAGE.COVER' | translate }}</button>
            @if (f.images) { <button type="button" class="text-red-600 text-sm hover:underline" (click)="f.images = null">{{ 'COMMON.DELETE' | translate }}</button> }
          </div>

          <div class="col-span-2">
            <label class="form-label">{{ 'PORTAL_NEWS_PAGE.CONTENT' | translate }}</label>
            <app-rich-text [(value)]="f.content" />
          </div>

          <div><label class="form-label" for="n-author">{{ 'PORTAL_NEWS_PAGE.AUTHOR' | translate }}</label><input id="n-author" class="input" name="author" maxlength="200" [(ngModel)]="f.author" /></div>
          <div><label class="form-label" for="n-source">{{ 'PORTAL_NEWS_PAGE.SOURCE' | translate }}</label><input id="n-source" class="input" name="source" maxlength="300" [(ngModel)]="f.source" /></div>
          <div><label class="form-label" for="n-kw">{{ 'PORTAL_NEWS_PAGE.KEYWORD' | translate }}</label><input id="n-kw" class="input" name="keyword" maxlength="500" [(ngModel)]="f.keyword" /></div>
          <div><label class="form-label" for="n-lang">{{ 'PORTAL_NEWS_PAGE.LANGUAGE' | translate }}</label>
            <select id="n-lang" class="input" name="language" [(ngModel)]="f.language"><option value="">{{ 'PORTAL_NEWS_PAGE.ALL_LANGUAGES' | translate }}</option><option value="vi">{{ 'PORTAL_NEWS_PAGE.LANG_VI' | translate }}</option><option value="en">{{ 'PORTAL_NEWS_PAGE.LANG_EN' | translate }}</option></select></div>
          <div><label class="form-label" for="n-start">{{ 'PORTAL_NEWS_PAGE.START' | translate }}</label><app-date-input fieldId="n-start" name="start" [(ngModel)]="f.startTime" withTime></app-date-input></div>
          <div><label class="form-label" for="n-end">{{ 'PORTAL_NEWS_PAGE.END' | translate }}</label><app-date-input fieldId="n-end" name="end" [(ngModel)]="f.endTime" withTime></app-date-input></div>
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" name="featured" [(ngModel)]="f.featured" /> {{ 'PORTAL_NEWS_PAGE.FEATURED' | translate }}</label>
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" name="allowComment" [(ngModel)]="f.allowComment" /> {{ 'PORTAL_NEWS_PAGE.ALLOW_COMMENT' | translate }}</label>
          <div><label class="form-label" for="n-mt">{{ 'PORTAL_NEWS_PAGE.META_TITLE' | translate }}</label><input id="n-mt" class="input" name="metaTitle" maxlength="250" [(ngModel)]="f.metaTitle" /></div>
          <div><label class="form-label" for="n-md">{{ 'PORTAL_NEWS_PAGE.META_DESCRIPTION' | translate }}</label><input id="n-md" class="input" name="metaDescription" maxlength="500" [(ngModel)]="f.metaDescription" /></div>
          <div>
            <label class="form-label" for="n-status">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="n-status" class="input" name="status" [(ngModel)]="f.status" [disabled]="!can('publish')">
              <option [ngValue]="1">{{ 'PORTAL_NEWS_PAGE.DRAFT' | translate }}</option><option [ngValue]="2">{{ 'PORTAL_NEWS_PAGE.PUBLISHED' | translate }}</option>
            </select>
          </div>

          @if (f.publicId) {
            <div class="col-span-2 border border-gray-200 rounded-lg p-3">
              <div class="flex items-center justify-between mb-2">
                <label class="form-label !mb-0">{{ 'PORTAL_NEWS_PAGE.ATTACHMENTS' | translate }}</label>
                <label class="btn-secondary !py-1 !px-2.5 cursor-pointer">
                  <span class="material-icons text-[16px]">attach_file</span> {{ 'PORTAL_NEWS_PAGE.ADD_ATTACHMENT' | translate }}
                  <input type="file" accept="application/pdf,.doc,.docx" class="hidden" (change)="onAttach($event, f)" />
                </label>
              </div>
              <ul class="divide-y divide-gray-100">
                @for (a of attachments(); track a.publicId) {
                  <li class="py-1.5 flex items-center justify-between text-sm"><a [href]="a.url" target="_blank" rel="noopener noreferrer" class="text-blue-700 hover:underline">{{ a.name }}</a>
                    <button type="button" class="text-red-600 hover:underline text-xs" (click)="removeAttachment(a)">{{ 'COMMON.DELETE' | translate }}</button></li>
                } @empty { <li class="py-2 text-xs text-gray-400">{{ 'PORTAL_NEWS_PAGE.NO_ATTACHMENTS' | translate }}</li> }
              </ul>
            </div>
          }
        </form>
        <ng-container footer>
          <button type="button" (click)="form.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="news-form" [disabled]="saving()" class="btn-primary">
            <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span> {{ 'COMMON.SAVE' | translate }}
          </button>
        </ng-container>
      </app-modal>
      @if (pickingCover()) { <app-media-picker purpose="cms-image" (picked)="setCover($event, f)" (closed)="pickingCover.set(false)" /> }
    }

    @if (deleting(); as d) {
      <app-confirm-delete [busy]="saving()" message="COMMON.DELETE_CONFIRM_IRREVERSIBLE" (confirmed)="doDelete(d)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class PortalNewsPage implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly client = this.api.crud<PortalNews>('news', 'portal');
  private readonly attachClient = this.api.crud<PortalAttachment>('news-attachments', 'portal');

  protected search: PortalNewsSearch = { keyword: '', categoryId: null, status: null, pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<Page<PortalNews> | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly form = signal<NewsForm | null>(null);
  protected readonly deleting = signal<PortalNews | null>(null);
  protected readonly categories = signal<Option[]>([]);
  protected readonly attachments = signal<PortalAttachment[]>([]);
  protected readonly pickingCover = signal(false);

  ngOnInit(): void {
    void this.load();
    void this.loadCategories();
  }

  protected can(action: string): boolean {
    return this.session.can(`PORTAL_NEWS:${action}`);
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

  protected openCreate(): void {
    this.attachments.set([]);
    this.form.set({
      publicId: null, id: null, title: '', slug: '', brief: '', content: '', images: null, categoryId: null, eventId: null, language: '', author: '', source: '', keyword: '',
      featured: false, allowComment: false, startTime: '', endTime: '', metaTitle: '', metaKeyword: '', metaDescription: '', status: 1,
    });
  }

  protected async openEdit(n: PortalNews): Promise<void> {
    this.form.set({
      publicId: n.publicId, id: n.id, title: n.title, slug: n.slug, brief: n.brief ?? '', content: n.content ?? '', images: n.images, categoryId: n.categoryId, eventId: n.eventId,
      language: n.language ?? '', author: n.author ?? '', source: n.source ?? '', keyword: n.keyword ?? '', featured: n.featured, allowComment: n.allowComment,
      startTime: toLocal(n.startTime), endTime: toLocal(n.endTime), metaTitle: n.metaTitle ?? '', metaKeyword: n.metaKeyword ?? '', metaDescription: n.metaDescription ?? '',
      status: n.status,
    });
    this.attachments.set([]);
    try {
      this.attachments.set((await this.attachClient.search({ newsId: n.id, pageSize: 100 })).items);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected setCover(file: MediaFile, f: NewsForm): void {
    f.images = file.url;
    this.pickingCover.set(false);
  }

  protected async onAttach(event: Event, f: NewsForm): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || f.id == null) return;
    try {
      const uploaded = await this.api.upload('cms-attachment', file);
      if (!uploaded.url) return;
      await this.attachClient.add({ newsId: f.id, name: file.name, url: uploaded.url, fileSize: file.size });
      this.attachments.set((await this.attachClient.search({ newsId: f.id, pageSize: 100 })).items);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async removeAttachment(a: PortalAttachment): Promise<void> {
    try {
      await this.attachClient.delete(a.publicId);
      this.attachments.set(this.attachments().filter((x) => x.publicId !== a.publicId));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async toggle(n: PortalNews): Promise<void> {
    try {
      await this.client.changeStatus(n.publicId, n.status === STATUS_ACTIVE ? 1 : STATUS_ACTIVE);
      this.toastr.success('COMMON.STATUS_CHANGED');
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async save(): Promise<void> {
    const f = this.form();
    if (!f) return;
    this.saving.set(true);
    try {
      const body = {
        title: f.title, slug: f.slug.trim() || null, brief: f.brief || null, content: f.content || null, images: f.images, thumb: f.images, categoryId: f.categoryId,
        eventId: f.eventId, language: f.language || null, keyword: f.keyword || null, author: f.author || null, source: f.source || null, featured: f.featured,
        allowComment: f.allowComment, startTime: fromLocal(f.startTime), endTime: fromLocal(f.endTime), metaTitle: f.metaTitle || null, metaKeyword: f.metaKeyword || null,
        metaDescription: f.metaDescription || null, status: f.status,
      };
      if (f.publicId) await this.client.update(f.publicId, body);
      else await this.client.add(body);
      this.toastr.success(f.publicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async doDelete(n: PortalNews): Promise<void> {
    this.saving.set(true);
    try {
      await this.client.delete(n.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  private async loadCategories(): Promise<void> {
    try {
      const rows = await this.api.crud<{ id: number; publicId: string; name: string; parentId: number | null; level: number; sortOrder: number }>('news-categories', 'portal').searchAll();
      const byParent = new Map<number | null, typeof rows>();
      for (const r of rows) (byParent.get(r.parentId) ?? byParent.set(r.parentId, []).get(r.parentId)!).push(r);
      const out: Option[] = [];
      const walk = (parent: number | null, depth: number): void =>
        (byParent.get(parent) ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id).forEach((r) => {
          out.push({ id: r.id, label: `${'    '.repeat(depth)}${r.name}` });
          walk(r.id, depth + 1);
        });
      walk(null, 0);
      this.categories.set(out);
    } catch {
      this.categories.set([]);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.client.search({ ...this.search, keyword: this.search.keyword?.trim() || undefined }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
