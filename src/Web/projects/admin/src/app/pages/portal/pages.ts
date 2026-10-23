import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage as Page, PortalPageItem, STATUS_ACTIVE, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { RichText } from '../../shared/rich-text';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator, StatusBadge } from '../../shared/ui';

interface PageForm { publicId: string | null; title: string; slug: string; content: string; language: string; metaDescription: string; status: number; }

/**
 * Trang tĩnh của cổng thông tin (LRC: Page, quyền PORTAL_PAGES) — hiện ở /trang/{mã}. Đăng/gỡ và sửa trang đã đăng cần quyền "Đăng bài" (publish).
 */
@Component({
  selector: 'app-portal-pages',
  imports: [FormsModule, Modal, ConfirmDelete, Loading, Paginator, StatusBadge, RichText, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'PORTAL_PAGES_PAGE.TITLE' | translate }}</h4></div>
    <div class="panel flex gap-3 items-end justify-between flex-wrap">
      <div class="flex items-end gap-2">
        <div><label class="field-label" for="pg-kw">{{ 'COMMON.SEARCH' | translate }}</label><input id="pg-kw" class="input" [(ngModel)]="keyword" (keydown.enter)="find()" /></div>
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
      @if (can('add')) { <button (click)="openCreate()" class="btn-add"><span class="material-icons text-[18px]">add</span> {{ 'COMMON.ADD_BTN' | translate }}</button> }
    </div>

    <div class="panel !p-0 relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead><tr><th class="th">{{ 'PORTAL_PAGES_PAGE.NAME' | translate }}</th><th class="th">{{ 'PORTAL_PAGES_PAGE.SLUG' | translate }}</th><th class="th !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th !text-center">{{ 'COMMON.ACTIONS' | translate }}</th></tr></thead>
          <tbody>
            @for (p of page()?.items ?? []; track p.publicId) {
              <tr class="hover:bg-gray-50">
                <td class="td font-medium text-gray-800">{{ p.title }}</td><td class="td font-mono text-sm">/trang/{{ p.slug }}</td>
                <td class="td text-center"><app-status-badge [status]="p.status" /></td>
                <td class="td"><div class="flex items-center justify-center gap-1">
                  @if (can('publish')) {
                    <button (click)="toggle(p)" class="icon-btn bg-green-50 text-green-700 hover:bg-green-100" [title]="(p.status === 2 ? 'PORTAL_PAGES_PAGE.UNPUBLISH' : 'PORTAL_PAGES_PAGE.PUBLISH') | translate">
                      <span class="material-icons text-[18px]">{{ p.status === 2 ? 'visibility_off' : 'publish' }}</span></button>
                  }
                  @if (can('edit') && (p.status !== 2 || can('publish'))) { <button (click)="openEdit(p)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate"><span class="material-icons text-[18px]">edit</span></button> }
                  @if (can('delete') && (p.status !== 2 || can('publish'))) { <button (click)="deleting.set(p)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">delete</span></button> }
                </div></td>
              </tr>
            } @empty { <tr><td colspan="4" class="td text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" /> }
    </div>

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'COMMON.EDIT' : 'COMMON.ADD_BTN'" widthClass="max-w-4xl" (closed)="form.set(null)">
        <form id="page-form" class="grid grid-cols-2 gap-4" (ngSubmit)="save()">
          <div class="col-span-2"><label class="form-label" for="p-title">{{ 'PORTAL_PAGES_PAGE.NAME' | translate }} *</label><input id="p-title" class="input" name="title" required maxlength="300" [(ngModel)]="f.title" /></div>
          <div><label class="form-label" for="p-slug">{{ 'PORTAL_PAGES_PAGE.SLUG' | translate }}</label><input id="p-slug" class="input font-mono" name="slug" maxlength="150" [(ngModel)]="f.slug" [placeholder]="'PORTAL_PAGES_PAGE.SLUG_HINT' | translate" /></div>
          <div><label class="form-label" for="p-lang">{{ 'PORTAL_NEWS_PAGE.LANGUAGE' | translate }}</label>
            <select id="p-lang" class="input" name="language" [(ngModel)]="f.language"><option value="">{{ 'PORTAL_NEWS_PAGE.ALL_LANGUAGES' | translate }}</option><option value="vi">{{ 'PORTAL_NEWS_PAGE.LANG_VI' | translate }}</option><option value="en">{{ 'PORTAL_NEWS_PAGE.LANG_EN' | translate }}</option></select></div>
          <div class="col-span-2"><label class="form-label">{{ 'PORTAL_NEWS_PAGE.CONTENT' | translate }}</label><app-rich-text [(value)]="f.content" /></div>
          <div><label class="form-label" for="p-md">{{ 'PORTAL_NEWS_PAGE.META_DESCRIPTION' | translate }}</label><input id="p-md" class="input" name="metaDescription" maxlength="500" [(ngModel)]="f.metaDescription" /></div>
          <div><label class="form-label" for="p-status">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="p-status" class="input" name="status" [(ngModel)]="f.status" [disabled]="!can('publish')">
              <option [ngValue]="1">{{ 'PORTAL_NEWS_PAGE.DRAFT' | translate }}</option><option [ngValue]="2">{{ 'PORTAL_NEWS_PAGE.PUBLISHED' | translate }}</option></select></div>
        </form>
        <ng-container footer>
          <button type="button" (click)="form.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="page-form" [disabled]="saving()" class="btn-primary"><span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span> {{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (deleting(); as d) { <app-confirm-delete [busy]="saving()" message="COMMON.DELETE_CONFIRM_IRREVERSIBLE" (confirmed)="doDelete(d)" (cancelled)="deleting.set(null)" /> }
  `,
})
export class PortalPages implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly client = this.api.crud<PortalPageItem>('pages', 'portal');

  protected keyword = '';
  protected pageIndex = 1;
  protected pageSize = 10;
  protected readonly page = signal<Page<PortalPageItem> | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly form = signal<PageForm | null>(null);
  protected readonly deleting = signal<PortalPageItem | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`PORTAL_PAGES:${action}`);
  }

  protected find(): void {
    this.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.load();
  }

  protected openCreate(): void {
    this.form.set({ publicId: null, title: '', slug: '', content: '', language: '', metaDescription: '', status: 1 });
  }

  protected openEdit(p: PortalPageItem): void {
    this.form.set({ publicId: p.publicId, title: p.title, slug: p.slug, content: p.content ?? '', language: p.language ?? '', metaDescription: p.metaDescription ?? '', status: p.status });
  }

  protected async toggle(p: PortalPageItem): Promise<void> {
    try {
      await this.client.changeStatus(p.publicId, p.status === STATUS_ACTIVE ? 1 : STATUS_ACTIVE);
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
      const body = { slug: f.slug.trim() || null, title: f.title, content: f.content || null, language: f.language || null, metaDescription: f.metaDescription || null, status: f.status };
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

  protected async doDelete(p: PortalPageItem): Promise<void> {
    this.saving.set(true);
    try {
      await this.client.delete(p.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.client.search({ keyword: this.keyword.trim() || undefined, pageIndex: this.pageIndex, pageSize: this.pageSize }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
