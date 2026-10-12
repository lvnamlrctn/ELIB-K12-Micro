import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { RouterLink } from '@angular/router';
import { Api, Bib, BibSearch, BibType, CrudClient, CrudPage, STATUS_ACTIVE, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Paginator } from '../../shared/ui';
import { MarcExport, MarcImport } from './marc-tools';

/** Biên mục biểu ghi — danh sách (monolith: /admin/catalog-bibs, quyền CATALOG_BIBS). */
@Component({
  selector: 'app-bibs',
  imports: [FormsModule, RouterLink, Loading, Paginator, ConfirmDelete, MarcImport, MarcExport, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'BIBS_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="md:col-span-2">
          <label for="b-kw" class="field-label">{{ 'BIBS_PAGE.SEARCH_LABEL' | translate }}</label>
          <input id="b-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" [placeholder]="'BIBS_PAGE.SEARCH_PH' | translate" />
        </div>
        <div>
          <label for="b-type" class="field-label">{{ 'BIBS_PAGE.RECORD_TYPE' | translate }}</label>
          <select id="b-type" class="input" [(ngModel)]="search.bibTypeId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
          </select>
        </div>
        <div>
          <label for="b-isbn" class="field-label">ISBN</label>
          <input id="b-isbn" class="input font-mono" [(ngModel)]="search.isbn" (keydown.enter)="find()" />
        </div>
      </div>
      <div class="flex justify-end mt-4">
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>

    <div class="panel flex gap-2 items-center justify-between flex-wrap">
      <div class="flex gap-2 flex-wrap">
        @if (can('add')) {
          <a routerLink="/catalog-bibs/new" class="btn-add"><span class="material-icons text-[18px]">add</span> {{ 'BIBS_PAGE.NEW' | translate }}</a>
          <button type="button" class="btn-secondary" (click)="importing.set(true)"><span class="material-icons text-[18px]">upload_file</span> {{ 'BIBS_PAGE.IMPORT_MARC' | translate }}</button>
        }
        <button type="button" class="btn-secondary" (click)="exporting.set(true)" [disabled]="!page()?.totalCount">
          <span class="material-icons text-[18px]">download</span> {{ 'BIBS_PAGE.EXPORT_MARC' | translate }}</button>
      </div>
      <span class="text-sm text-gray-500">{{ 'BIBS_PAGE.COUNT' | translate: { count: page()?.totalCount ?? 0 } }}</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-20">MFN</th><th class="th">{{ 'BIBS_PAGE.TITLE_COL' | translate }}</th><th class="th w-48">{{ 'BIBS_PAGE.AUTHOR' | translate }}</th><th class="th w-48">{{ 'BIBS_PAGE.PUBLISHER' | translate }}</th>
            <th class="th w-16">{{ 'BIBS_PAGE.YEAR' | translate }}</th><th class="th w-24">DDC</th><th class="th w-24 !text-center">OPAC</th><th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (b of page()?.items ?? []; track b.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td font-mono text-[13px]">{{ b.mfn }}</td>
                <td class="td font-medium text-gray-800">
                  <a [routerLink]="['/catalog-bibs', b.mfn]" class="hover:text-blue-600">{{ b.title }}</a>
                  <div class="text-[11px] text-gray-400">{{ typeName(b.bibTypeId) | translate }}@if (b.isbns.length) { · ISBN {{ b.isbns.join(', ') }} }</div>
                </td>
                <td class="td">{{ b.author ?? '—' }}</td>
                <td class="td">{{ b.publisher ?? '—' }}</td>
                <td class="td">{{ b.publishYear ?? '—' }}</td>
                <td class="td font-mono text-[13px]">{{ b.ddc ?? '—' }}</td>
                <td class="td text-center">
                  @if (b.status === active) { <span class="badge-on">{{ 'BIBS_PAGE.SHOWN' | translate }}</span> } @else { <span class="badge-off">{{ 'BIBS_PAGE.HIDDEN' | translate }}</span> }
                </td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    <a [routerLink]="['/catalog-bibs', b.mfn]" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="(can('edit') ? 'COMMON.EDIT' : 'BIBS_PAGE.VIEW') | translate">
                      <span class="material-icons text-[18px]">{{ can('edit') ? 'edit' : 'visibility' }}</span></a>
                    @if (can('delete')) {
                      <button (click)="deleting.set(b)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="8" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('BIBS_PAGE.EMPTY' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>

    @if (deleting(); as b) {
      <app-confirm-delete [busy]="saving()" message="BIBS_PAGE.DELETE_CONFIRM" [messageParams]="{ mfn: b.mfn, title: b.title }" (confirmed)="remove(b)" (cancelled)="deleting.set(null)" />
    }
    @if (importing()) { <app-marc-import [types]="types()" (imported)="find()" (closed)="importing.set(false)" /> }
    @if (exporting()) { <app-marc-export [search]="search" [total]="page()?.totalCount ?? 0" (closed)="exporting.set(false)" /> }
  `,
})
export class Bibs implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly client: CrudClient<Bib> = this.api.crud<Bib>('bibs', 'catalog');

  protected readonly active = STATUS_ACTIVE;
  protected search: BibSearch = { keyword: '', bibTypeId: null, isbn: '', pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<CrudPage<Bib> | null>(null);
  protected readonly types = signal<BibType[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly deleting = signal<Bib | null>(null);
  protected readonly importing = signal(false);
  protected readonly exporting = signal(false);

  async ngOnInit(): Promise<void> {
    void this.load();
    try {
      this.types.set(await this.api.crud<BibType>('bib-types', 'catalog').searchAll());
    } catch {
      /* thiếu quyền xem loại biểu ghi — chỉ mất tên loại */
    }
  }

  protected can(action: string): boolean {
    return this.session.can(`CATALOG_BIBS:${action}`);
  }

  protected typeName(id: number | null): string {
    return id == null ? 'BIBS_PAGE.NO_TYPE' : (this.types().find((t) => t.id === id)?.name ?? '');
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

  protected async remove(b: Bib): Promise<void> {
    this.saving.set(true);
    try {
      await this.client.delete(b.publicId);
      this.toastr.success('BIBS_PAGE.DELETED');
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
      const s = this.search;
      this.page.set(await this.client.search({ ...s, keyword: s.keyword?.trim() || undefined, isbn: s.isbn?.trim() || null } as BibSearch) as unknown as CrudPage<Bib>);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
