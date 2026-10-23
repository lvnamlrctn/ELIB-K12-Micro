import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage as Page, EbookItem, EbookItemSearch, ItemBulkResult, ItemSelection, STATUS_ACTIVE, STATUS_INACTIVE, errorMessage, saveFile } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';
import { ItemCover } from './item-cover';
import { ItemForm } from './item-form';
import { ItemExport, ItemImport, ItemMove, OptionItem, TOGGLE_CLASS, stamp } from './item-tools';
import { ItemsApi } from './items-api';

type TreeRow = { id: number; publicId: string; name: string; parentId: number | null; sortOrder: number };
type NamedRow = { id: number; publicId: string; name: string };

/** Bộ lọc của màn (năm là chuỗi từ ô chọn; ngày nộp là yyyy-MM-dd từ ô ngày). */
interface Filters {
  collectionId: number | null; subjectId: number | null; topicId: number | null; typeId: number | null;
  title: string; author: string; publisher: string; status: number | null;
  yearFrom: number | null; yearTo: number | null; submittedFrom: string; submittedTo: string;
}

const emptyFilters = (): Filters => ({
  collectionId: null, subjectId: null, topicId: null, typeId: null, title: '', author: '', publisher: '', status: null,
  yearFrom: null, yearTo: null, submittedFrom: '', submittedTo: '',
});

/**
 * Quản lý tài liệu số (LRC: ebook-document, quyền DIGITAL_ITEMS): bộ lọc đầy đủ, chọn nhiều dòng (hoặc cả kết quả lọc) để chuyển bộ sưu tập / xoá,
 * xuất Excel / metadata, nhập từ Excel / XML, bật-tắt xuất bản ngay trên dòng, ảnh bìa từ PDF, quản lý file và lượt mượn theo từng tài liệu.
 */
@Component({
  selector: 'app-digital-items',
  imports: [FormsModule, RouterLink, ConfirmDelete, ItemCover, ItemExport, ItemForm, ItemImport, ItemMove, Loading, Modal, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'DIGITAL_DOC.TITLE' | translate }}</h4></div>

    <div class="panel !p-0">
      <button type="button" class="w-full flex items-center justify-between px-4 py-3 text-left" (click)="filtersCollapsed.set(!filtersCollapsed())" [attr.aria-expanded]="!filtersCollapsed()">
        <span class="flex items-center gap-2 font-medium text-gray-800"><span class="material-icons text-[20px] text-blue-600">search</span> {{ 'DIGITAL_DOC.FILTERS' | translate }}</span>
        <span class="material-icons text-gray-500">{{ filtersCollapsed() ? 'expand_more' : 'expand_less' }}</span>
      </button>
      @if (!filtersCollapsed()) {
        <div class="px-4 pb-4 grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-3">
          <select class="input" [(ngModel)]="filters.collectionId" (ngModelChange)="find()" [attr.aria-label]="'DIGITAL_DOC.PICK_COLLECTION' | translate">
            <option [ngValue]="null">{{ 'DIGITAL_DOC.PICK_COLLECTION' | translate }}</option>@for (o of collectionOptions(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select>
          <select class="input" [(ngModel)]="filters.subjectId" (ngModelChange)="find()" [attr.aria-label]="'DIGITAL_DOC.PICK_SUBJECT' | translate">
            <option [ngValue]="null">{{ 'DIGITAL_DOC.PICK_SUBJECT' | translate }}</option>@for (o of subjectOptions(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select>
          <select class="input" [(ngModel)]="filters.topicId" (ngModelChange)="find()" [attr.aria-label]="'DIGITAL_DOC.PICK_TOPIC' | translate">
            <option [ngValue]="null">{{ 'DIGITAL_DOC.PICK_TOPIC' | translate }}</option>@for (o of topicOptions(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select>
          <select class="input" [(ngModel)]="filters.typeId" (ngModelChange)="find()" [attr.aria-label]="'DIGITAL_DOC.PICK_TYPE' | translate">
            <option [ngValue]="null">{{ 'DIGITAL_DOC.PICK_TYPE' | translate }}</option>@for (o of typeOptions(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select>

          <input class="input" [(ngModel)]="filters.title" (keydown.enter)="find()" [placeholder]="'DIGITAL_DOC.FILTER_TITLE' | translate" [attr.aria-label]="'DIGITAL_DOC.FILTER_TITLE' | translate" />
          <input class="input" [(ngModel)]="filters.author" (keydown.enter)="find()" [placeholder]="'DIGITAL_DOC.FILTER_AUTHOR' | translate" [attr.aria-label]="'DIGITAL_DOC.FILTER_AUTHOR' | translate" />
          <input class="input" [(ngModel)]="filters.publisher" (keydown.enter)="find()" [placeholder]="'DIGITAL_DOC.FILTER_PUBLISHER' | translate" [attr.aria-label]="'DIGITAL_DOC.FILTER_PUBLISHER' | translate" />
          <select class="input" [(ngModel)]="filters.status" (ngModelChange)="find()" [attr.aria-label]="'DIGITAL_DOC.PICK_STATUS' | translate">
            <option [ngValue]="null">{{ 'DIGITAL_DOC.PICK_STATUS' | translate }}</option>
            <option [ngValue]="2">{{ 'DIGITAL_DOC.STATUS_PUBLISHED' | translate }}</option><option [ngValue]="1">{{ 'DIGITAL_DOC.STATUS_UNPUBLISHED' | translate }}</option></select>

          <select class="input" [(ngModel)]="filters.yearFrom" (ngModelChange)="find()" [attr.aria-label]="'DIGITAL_DOC.YEAR_FROM' | translate">
            <option [ngValue]="null">{{ 'DIGITAL_DOC.YEAR_FROM' | translate }}</option>@for (y of years; track y) { <option [ngValue]="y">{{ y }}</option> }</select>
          <select class="input" [(ngModel)]="filters.yearTo" (ngModelChange)="find()" [attr.aria-label]="'DIGITAL_DOC.YEAR_TO' | translate">
            <option [ngValue]="null">{{ 'DIGITAL_DOC.YEAR_TO' | translate }}</option>@for (y of years; track y) { <option [ngValue]="y">{{ y }}</option> }</select>
          <input type="date" class="input" [(ngModel)]="filters.submittedFrom" (ngModelChange)="find()" [attr.aria-label]="'DIGITAL_DOC.SUBMITTED_FROM' | translate" />
          <input type="date" class="input" [(ngModel)]="filters.submittedTo" (ngModelChange)="find()" [attr.aria-label]="'DIGITAL_DOC.SUBMITTED_TO' | translate" />

          <div class="xl:col-span-3"></div>
          <button type="button" class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
        </div>
      }
    </div>

    <div class="panel flex flex-wrap gap-2 items-center">
      @if (can('view')) {
        <button type="button" class="btn-primary" [disabled]="busy()" (click)="exportDialog.set(true)"><span class="material-icons text-[18px]">file_download</span> {{ 'DIGITAL_DOC.EXPORT_EXCEL' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="busy()" (click)="exportMetadata()"><span class="material-icons text-[18px]">download</span> {{ 'DIGITAL_DOC.EXPORT_METADATA' | translate }}</button>
      }
      @if (can('add')) {
        <button type="button" class="btn-primary" (click)="importDialog.set(true)"><span class="material-icons text-[18px]">file_upload</span> {{ 'DIGITAL_DOC.IMPORT' | translate }}</button>
        <button type="button" class="btn-add" (click)="formOpen.set(true)"><span class="material-icons text-[18px]">add</span> {{ 'COMMON.ADD_BTN' | translate }}</button>
      }
      @if (can('delete')) {
        <button type="button" class="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-red-600 text-white text-sm font-medium hover:bg-red-700 disabled:opacity-50" [disabled]="!selectedCount() || busy()" (click)="confirmDelete.set(true)">
          <span class="material-icons text-[18px]">close</span> {{ 'DIGITAL_DOC.DELETE_SELECTED' | translate }}</button>
      }
      @if (can('edit')) {
        <button type="button" class="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-teal-600 text-white text-sm font-medium hover:bg-teal-700 disabled:opacity-50" [disabled]="!selectedCount() || busy()" (click)="bulkStatus(2)">
          <span class="material-icons text-[18px]">visibility</span> {{ 'DIGITAL_DOC.BULK_PUBLISH' | translate: { count: selectedCount() } }}</button>
        <button type="button" class="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-gray-600 text-white text-sm font-medium hover:bg-gray-700 disabled:opacity-50" [disabled]="!selectedCount() || busy()" (click)="bulkStatus(1)">
          <span class="material-icons text-[18px]">visibility_off</span> {{ 'DIGITAL_DOC.BULK_HIDE' | translate: { count: selectedCount() } }}</button>
        <button type="button" class="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50" [disabled]="!selectedCount() || busy()" (click)="moveDialog.set(true)">
          <span class="material-icons text-[18px]">drive_file_move</span> {{ 'DIGITAL_DOC.MOVE_COLLECTION' | translate: { count: selectedCount() } }}</button>
      }
      <span class="ml-auto text-sm text-gray-500">{{ 'COMMON.RECORDS_N' | translate: { count: page()?.totalCount ?? 0 } }}</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      @if (pageAllTicked() && !allResults() && (page()?.totalCount ?? 0) > (page()?.items?.length ?? 0)) {
        <div class="px-4 py-2 bg-blue-50 text-sm text-blue-800 flex items-center gap-2">
          {{ 'DIGITAL_DOC.PAGE_SELECTED' | translate: { count: page()!.items.length } }}
          <button type="button" class="underline font-medium" (click)="allResults.set(true)">{{ 'DIGITAL_DOC.SELECT_ALL_RESULTS' | translate: { count: page()!.totalCount } }}</button>
        </div>
      }
      @if (allResults()) {
        <div class="px-4 py-2 bg-blue-50 text-sm text-blue-800 flex items-center gap-2">
          {{ 'DIGITAL_DOC.ALL_RESULTS_SELECTED' | translate: { count: page()?.totalCount ?? 0 } }}
          <button type="button" class="underline font-medium" (click)="clearSelection()">{{ 'DIGITAL_DOC.CLEAR_SELECTION' | translate }}</button>
        </div>
      }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-10 !text-center">
              <input type="checkbox" class="rounded border-gray-300" [checked]="pageAllTicked()" [indeterminate]="somePageTicked() && !pageAllTicked()" (change)="toggleAll()" [attr.aria-label]="'DIGITAL_DOC.SELECT_PAGE' | translate" />
            </th>
            <th class="th w-12 !text-center">{{ 'DIGITAL_DOC.COL_STT' | translate }}</th>
            <th class="th w-40">{{ 'DIGITAL_DOC.COLLECTION' | translate }}</th>
            <th class="th">{{ 'DIGITAL_DOC.COL_INFO' | translate }}</th>
            <th class="th w-24 !text-center">{{ 'DIGITAL_DOC.COL_VIEWS' | translate }}</th>
            <th class="th w-40 !text-center">{{ 'DIGITAL_DOC.COL_FILES' | translate }}</th>
            <th class="th w-28 !text-center">{{ 'COMMON.STATUS' | translate }}</th>
            <th class="th w-44 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (i of page()?.items ?? []; track i.publicId; let n = $index) {
              <tr class="hover:bg-gray-50/50 cursor-pointer" [class.bg-blue-50]="isTicked(i)" (click)="toggleRow(i)">
                <td class="td text-center" (click)="$event.stopPropagation()">
                  <input type="checkbox" class="rounded border-gray-300" [checked]="isTicked(i)" (change)="toggleRow(i)" [attr.aria-label]="i.title" />
                </td>
                <td class="td text-center text-sm text-gray-500">{{ offset() + n + 1 }}</td>
                <td class="td text-sm">{{ i.collectionName ?? '—' }}</td>
                <td class="td">
                  <div class="flex gap-3">
                    @if (i.coverUrl) { <img [src]="i.coverUrl" class="w-10 h-14 object-cover rounded border border-gray-200 shrink-0" referrerpolicy="no-referrer" alt="" /> }
                    @else { <div class="w-10 h-14 rounded border border-gray-200 bg-gray-100 flex items-center justify-center shrink-0"><span class="material-icons text-gray-400">menu_book</span></div> }
                    <div class="min-w-0">
                      <div class="text-sm font-semibold text-blue-700 line-clamp-2">{{ i.title }}</div>
                      @if (i.author) { <div class="text-xs text-gray-600 flex items-center gap-1"><span class="material-icons text-[13px]">person</span> {{ i.author }}</div> }
                      @if (i.publishYear || i.publishDate || i.publisher) {
                        <div class="text-xs text-gray-500 flex items-center gap-1"><span class="material-icons text-[13px]">calendar_today</span>
                          {{ i.publishYear ?? i.publishDate }}@if (i.publisher) { <span> · {{ i.publisher }}</span> }</div>
                      }
                    </div>
                  </div>
                </td>
                <td class="td text-center text-sm">{{ i.totalView || 0 }}</td>
                <td class="td text-center" (click)="$event.stopPropagation()">
                  <a [routerLink]="['/digital-item-files', i.publicId]" class="text-sm text-blue-600 hover:underline inline-flex items-center gap-1">
                    <span class="material-icons text-[16px]">folder_open</span> {{ 'DIGITAL_DOC.MANAGE_FILES_N' | translate: { count: i.totalFile } }}</a>
                </td>
                <td class="td text-center" (click)="$event.stopPropagation()">
                  <label class="relative inline-flex items-center" [class.cursor-pointer]="can('edit')">
                    <input type="checkbox" class="sr-only peer" [checked]="i.status === 2" [disabled]="!can('edit')" (change)="setStatus(i)" [attr.aria-label]="'DIGITAL_DOC.PUBLISHED' | translate" />
                    <span [class]="toggle"></span>
                  </label>
                </td>
                <td class="td" (click)="$event.stopPropagation()">
                  <div class="flex items-center justify-center gap-1">
                    @if (can('edit')) {
                      <button type="button" class="w-7 h-7 rounded bg-purple-50 text-purple-600 hover:bg-purple-100 flex items-center justify-center" (click)="coverFor.set(i)" [title]="'DIGITAL_DOC.COVER_FROM_PDF' | translate"><span class="material-icons text-[15px] leading-none">image</span></button>
                      <button type="button" class="w-7 h-7 rounded bg-blue-50 text-blue-600 hover:bg-blue-100 flex items-center justify-center" (click)="edit(i)" [title]="'COMMON.EDIT' | translate"><span class="material-icons text-[15px] leading-none">edit</span></button>
                    }
                    <a [routerLink]="['/digital-loans']" [queryParams]="{ item: i.publicId }" class="w-7 h-7 rounded bg-amber-50 text-amber-600 hover:bg-amber-100 flex items-center justify-center" [title]="'DIGITAL_DOC.LOANS' | translate"><span class="material-icons text-[15px] leading-none">assignment_return</span></a>
                    @if (can('delete')) {
                      <button type="button" class="w-7 h-7 rounded bg-red-50 text-red-600 hover:bg-red-100 flex items-center justify-center" (click)="deleting.set(i)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[15px] leading-none">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="8" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" /> }
    </div>

    @if (formOpen()) {
      <app-item-form [item]="editing()" [collections]="collectionOptions()" [subjects]="subjectOptions()" [topics]="topicOptions()" [types]="typeOptions()"
        [publisherHints]="publisherHints()" [authorHints]="authorHints()" (closed)="closeForm()" (saved)="load()" />
    }
    @if (coverFor(); as c) { <app-item-cover [item]="c" (closed)="coverFor.set(null)" (saved)="load()" /> }
    @if (importDialog()) {
      <app-item-import [collections]="collectionOptions()" [subjects]="subjectOptions()" [topics]="topicOptions()" (closed)="importDialog.set(false)" (imported)="find()" />
    }
    @if (exportDialog()) { <app-item-export [selection]="exportSelection()" (closed)="exportDialog.set(false)" /> }
    @if (moveDialog()) {
      <app-item-move [selection]="selection()!" [count]="selectedCount()" [collections]="collectionOptions()" (closed)="moveDialog.set(false)" (moved)="afterBulk()" />
    }

    @if (confirmDelete()) {
      <app-confirm-delete [busy]="busy()" message="DIGITAL_DOC.CONFIRM_BULK_DELETE" (confirmed)="bulkDelete()" (cancelled)="confirmDelete.set(false)" />
    }
    @if (deleting(); as d) {
      <app-confirm-delete [busy]="busy()" message="COMMON.DELETE_CONFIRM_IRREVERSIBLE" (confirmed)="deleteOne(d)" (cancelled)="deleting.set(null)" />
    }
    @if (skipped(); as list) {
      <app-modal title="DIGITAL_DOC.SKIPPED_TITLE" widthClass="max-w-lg" (closed)="skipped.set(null)">
        <p class="text-sm text-gray-600 mb-2">{{ 'DIGITAL_DOC.SKIPPED_HINT' | translate }}</p>
        <ul class="max-h-64 overflow-y-auto text-sm divide-y divide-gray-100">
          @for (s of list; track s.publicId) { <li class="py-1.5"><span class="font-medium">{{ s.title }}</span><div class="text-xs text-red-600">{{ s.reason }}</div></li> }
        </ul>
        <ng-container footer><button type="button" class="btn-primary" (click)="skipped.set(null)">{{ 'COMMON.CLOSE' | translate }}</button></ng-container>
      </app-modal>
    }
  `,
})
export class DigitalItems implements OnInit {
  private readonly api = inject(Api);
  private readonly itemsApi = inject(ItemsApi);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly client = this.api.crud<EbookItem>('items', 'digital');

  protected readonly toggle = TOGGLE_CLASS;
  protected readonly years = Array.from({ length: new Date().getFullYear() + 1 - 1900 + 1 }, (_, i) => new Date().getFullYear() + 1 - i);
  protected filters: Filters = emptyFilters();
  protected search: EbookItemSearch = { pageIndex: 1, pageSize: 10 };
  /** Bộ lọc đã dùng ở lần tải gần nhất — là "tất cả kết quả lọc" khi chọn cả kết quả và là phạm vi xuất khi không chọn dòng nào. */
  private lastSearch: EbookItemSearch = {};

  protected readonly filtersCollapsed = signal(false);
  protected readonly page = signal<Page<EbookItem> | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly ticked = signal<Set<string>>(new Set());
  protected readonly allResults = signal(false);

  protected readonly formOpen = signal(false);
  protected readonly editing = signal<EbookItem | null>(null);
  protected readonly coverFor = signal<EbookItem | null>(null);
  protected readonly importDialog = signal(false);
  protected readonly exportDialog = signal(false);
  protected readonly moveDialog = signal(false);
  protected readonly confirmDelete = signal(false);
  protected readonly deleting = signal<EbookItem | null>(null);
  protected readonly skipped = signal<ItemBulkResult['skipped'] | null>(null);

  protected readonly collectionOptions = signal<OptionItem[]>([]);
  protected readonly subjectOptions = signal<OptionItem[]>([]);
  protected readonly topicOptions = signal<OptionItem[]>([]);
  protected readonly typeOptions = signal<OptionItem[]>([]);

  protected readonly offset = computed(() => ((this.search.pageIndex ?? 1) - 1) * (this.search.pageSize ?? 10));
  protected readonly publisherHints = computed(() => [...new Set((this.page()?.items ?? []).map((i) => i.publisher).filter((x): x is string => !!x))]);
  protected readonly authorHints = computed(() => [...new Set((this.page()?.items ?? []).flatMap((i) => i.authors.map((a) => a.last ?? '')).filter(Boolean))]);
  protected readonly pageAllTicked = computed(() => {
    const items = this.page()?.items ?? [];
    return items.length > 0 && (this.allResults() || items.every((i) => this.ticked().has(i.publicId)));
  });
  protected readonly somePageTicked = computed(() => this.allResults() || (this.page()?.items ?? []).some((i) => this.ticked().has(i.publicId)));
  protected readonly selectedCount = computed(() => (this.allResults() ? (this.page()?.totalCount ?? 0) : this.ticked().size));
  protected readonly selection = computed<ItemSelection | null>(() => {
    if (this.allResults()) return { search: this.lastSearch };
    const ids = [...this.ticked()];
    return ids.length ? { publicIds: ids } : null;
  });
  /** Xuất: các dòng đã chọn; chưa chọn dòng nào thì xuất theo bộ lọc hiện tại (như LRC). */
  protected readonly exportSelection = computed<ItemSelection>(() => this.selection() ?? { search: this.lastSearch });

  ngOnInit(): void {
    void this.load();
    void this.loadOptions();
  }

  protected can(action: string): boolean {
    return this.session.can(`DIGITAL_ITEMS:${action}`);
  }

  protected find(): void {
    // Đổi bộ lọc thì vùng chọn cũ (kể cả "tất cả kết quả lọc") không còn đúng nữa — nếu giữ, Xoá/Chuyển sẽ tác động lên tập khác.
    this.clearSelection();
    this.search.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.search.pageIndex = e.pageIndex;
    this.search.pageSize = e.pageSize;
    void this.load();
  }

  protected isTicked(i: EbookItem): boolean {
    return this.allResults() || this.ticked().has(i.publicId);
  }

  protected toggleRow(i: EbookItem): void {
    if (this.allResults()) this.allResults.set(false);
    const next = new Set(this.ticked());
    if (!next.delete(i.publicId)) next.add(i.publicId);
    this.ticked.set(next);
  }

  protected toggleAll(): void {
    const items = this.page()?.items ?? [];
    if (this.pageAllTicked()) {
      this.clearSelection();
      return;
    }
    this.ticked.set(new Set([...this.ticked(), ...items.map((i) => i.publicId)]));
  }

  protected clearSelection(): void {
    this.allResults.set(false);
    this.ticked.set(new Set());
  }

  private flattenTree(rows: TreeRow[]): OptionItem[] {
    const byParent = new Map<number | null, TreeRow[]>();
    for (const r of rows) {
      const k = r.parentId ?? null;
      (byParent.get(k) ?? byParent.set(k, []).get(k)!).push(r);
    }
    const out: OptionItem[] = [];
    const walk = (parentId: number | null, depth: number) =>
      (byParent.get(parentId) ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id).forEach((r) => {
        out.push({ id: r.id, label: `${'    '.repeat(depth)}${r.name}` });
        walk(r.id, depth + 1);
      });
    walk(null, 0);
    return out;
  }

  private async loadOptions(): Promise<void> {
    try {
      const [collections, subjects, topics, types] = await Promise.all([
        this.api.crud<TreeRow>('collections', 'digital').searchAll(),
        this.api.crud<TreeRow>('subjects', 'digital').searchAll(),
        this.api.crud<TreeRow>('topics', 'digital').searchAll(),
        this.api.crud<NamedRow & { code: string }>('dig-types', 'digital').searchAll(),
      ]);
      this.collectionOptions.set(this.flattenTree(collections));
      this.subjectOptions.set(this.flattenTree(subjects));
      this.topicOptions.set(this.flattenTree(topics));
      this.typeOptions.set(types.map((t) => ({ id: t.id, label: t.name || t.code })));
    } catch {
      /* thiếu quyền xem danh mục liên quan — ô chọn để trống */
    }
  }

  private buildSearch(): EbookItemSearch {
    const f = this.filters;
    return {
      collectionId: f.collectionId, subjectId: f.subjectId, topicId: f.topicId, typeId: f.typeId, status: f.status,
      title: f.title.trim() || null, author: f.author.trim() || null, publisher: f.publisher.trim() || null,
      publishYearFrom: f.yearFrom, publishYearTo: f.yearTo, submittedFrom: f.submittedFrom || null, submittedTo: f.submittedTo || null,
    };
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      const filter = this.buildSearch();
      this.lastSearch = filter;
      this.page.set(await this.client.search({ ...filter, pageIndex: this.search.pageIndex, pageSize: this.search.pageSize }));
      if (!this.allResults()) {
        // Giữ tích chỉ với các dòng còn tồn tại trong kết quả hiện tại của các trang đã duyệt: tải lại làm số liệu đổi nên bỏ chọn cho gọn.
        this.ticked.set(new Set([...this.ticked()].filter((id) => (this.page()?.items ?? []).some((i) => i.publicId === id))));
      }
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected edit(item: EbookItem): void {
    this.editing.set(item);
    this.formOpen.set(true);
  }

  protected closeForm(): void {
    this.formOpen.set(false);
    this.editing.set(null);
  }

  protected async setStatus(item: EbookItem): Promise<void> {
    const next = item.status === STATUS_ACTIVE ? STATUS_INACTIVE : STATUS_ACTIVE;
    try {
      await this.client.changeStatus(item.publicId, next);
      item.status = next;
    } catch (e) {
      this.toastr.error(errorMessage(e));
      await this.load();
    }
  }

  protected async exportMetadata(): Promise<void> {
    this.busy.set(true);
    try {
      saveFile(await this.itemsApi.exportItemsMetadata(this.exportSelection()), `dspace-metadata-${stamp()}.zip`);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async bulkStatus(status: number): Promise<void> {
    const selection = this.selection();
    if (!selection) return;
    this.busy.set(true);
    try {
      await this.itemsApi.itemsBulkStatus(selection, status);
      this.toastr.success('DIGITAL_DOC.BULK_STATUS_DONE');
      this.afterBulk();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected afterBulk(): void {
    this.clearSelection();
    void this.load();
  }

  protected async bulkDelete(): Promise<void> {
    const selection = this.selection();
    if (!selection) return;
    this.busy.set(true);
    try {
      const result = await this.itemsApi.itemsBulkDelete(selection);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.confirmDelete.set(false);
      if (result.skipped.length) this.skipped.set(result.skipped);
      this.afterBulk();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async deleteOne(item: EbookItem): Promise<void> {
    this.busy.set(true);
    try {
      await this.client.delete(item.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
