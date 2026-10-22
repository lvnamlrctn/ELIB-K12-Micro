import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Api, BibLinkOption, CrudPage as Page, EbookFile, EbookFileSearch, EbookItem, EbookItemSearch, MetadataField, STATUS_ACTIVE, errorMessage,
} from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator, StatusBadge } from '../../shared/ui';

interface MetaRow { fieldPublicId: string; value: string; language: string }

type TreeRow = { id: number; publicId: string; name: string; parentId: number | null; sortOrder: number };
type NamedRow = { id: number; publicId: string; name: string };

interface ItemForm {
  publicId: string | null;
  title: string; author: string; publisher: string; publishDate: string; otherTitle: string; page: string; oldAuthor: string; keyword: string;
  language: string; collectionId: number | null; subjectId: number | null; topicId: number | null; typeId: number | null;
  free: boolean; allowDownload: boolean; printCopies: number | null; offlineDays: number | null; status: number;
  coverUrl: string | null; linkedBibPublicId: string | null; linkedBibLabel: string | null;
}

/**
 * Tài liệu số — biểu ghi (LRC: EbookItemController, quyền DIGITAL_ITEMS). Trang bespoke (không dùng CrudConfig) vì có ảnh bìa,
 * liên kết sách in và quản lý file đính kèm ngay trong màn sửa.
 */
@Component({
  selector: 'app-digital-items',
  imports: [FormsModule, Modal, ConfirmDelete, Loading, Paginator, StatusBadge, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'DIGITAL_ITEMS_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-5 gap-4 items-end">
        <div class="xl:col-span-2">
          <label class="field-label" for="it-kw">{{ 'COMMON.SEARCH' | translate }}</label>
          <input id="it-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" />
        </div>
        <div>
          <label class="field-label" for="it-coll">{{ 'DIGITAL_CAT.COLLECTION' | translate }}</label>
          <select id="it-coll" class="input" [(ngModel)]="search.collectionId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (o of collectionOptions(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }
          </select>
        </div>
        <div>
          <label class="field-label" for="it-status">{{ 'COMMON.STATUS' | translate }}</label>
          <select id="it-status" class="input" [(ngModel)]="search.status" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option [ngValue]="2">{{ 'COMMON.ACTIVE' | translate }}</option>
            <option [ngValue]="1">{{ 'DIGITAL_CAT.STATUS_HIDDEN' | translate }}</option>
          </select>
        </div>
        <div class="flex items-end">
          <button (click)="find()" class="btn-primary w-full"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
        </div>
      </div>
    </div>

    <div class="panel flex gap-3 items-center justify-between flex-wrap">
      @if (can('add')) {
        <button (click)="openCreate()" class="btn-add"><span class="material-icons text-[18px]">add</span> {{ 'COMMON.ADD_BTN' | translate }}</button>
      }
      <span class="text-sm text-gray-500">{{ 'COMMON.RECORDS_N' | translate: { count: page()?.totalCount ?? 0 } }}</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th">{{ 'DIGITAL_ITEMS_PAGE.TITLE_COL' | translate }}</th>
            <th class="th w-40">{{ 'DIGITAL_CAT.COLLECTION' | translate }}</th>
            <th class="th w-28 !text-center">{{ 'DIGITAL_ITEMS_PAGE.FREE' | translate }}</th>
            <th class="th w-36 !text-center">{{ 'COMMON.STATUS' | translate }}</th>
            <th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (i of page()?.items ?? []; track i.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td"><span class="font-medium text-gray-800">{{ i.title }}</span>
                  @if (i.author) { <div class="text-[11px] text-gray-500">{{ i.author }}</div> }</td>
                <td class="td text-sm">{{ i.collectionName ?? '—' }}</td>
                <td class="td text-center">
                  @if (i.free) { <span class="badge-info">{{ 'DIGITAL_ITEMS_PAGE.FREE' | translate }}</span> } @else { <span class="text-gray-300">—</span> }
                </td>
                <td class="td text-center"><app-status-badge [status]="i.status" inactiveText="DIGITAL_CAT.STATUS_HIDDEN" /></td>
                <td class="td text-center">
                  <div class="flex items-center justify-center gap-1">
                    @if (can('edit')) {
                      <button (click)="openEdit(i)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate">
                        <span class="material-icons text-[18px]">edit</span></button>
                    }
                    @if (can('delete')) {
                      <button (click)="deleting.set(i)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate">
                        <span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" /> }
    </div>

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'COMMON.EDIT' : 'COMMON.ADD_BTN'" widthClass="max-w-3xl" (closed)="form.set(null)">
        <form id="item-form" (ngSubmit)="save()" class="grid grid-cols-2 gap-4">
          <div class="col-span-2">
            <label class="form-label" for="f-title">{{ 'DIGITAL_ITEMS_PAGE.TITLE_COL' | translate }} <span class="text-red-500">*</span></label>
            <input id="f-title" name="title" class="input" [(ngModel)]="f.title" required />
          </div>
          <div><label class="form-label" for="f-author">{{ 'DIGITAL_ITEMS_PAGE.AUTHOR' | translate }}</label><input id="f-author" name="author" class="input" [(ngModel)]="f.author" /></div>
          <div><label class="form-label" for="f-pub">{{ 'DIGITAL_ITEMS_PAGE.PUBLISHER' | translate }}</label><input id="f-pub" name="publisher" class="input" [(ngModel)]="f.publisher" /></div>
          <div><label class="form-label" for="f-date">{{ 'DIGITAL_ITEMS_PAGE.PUBLISH_DATE' | translate }}</label><input id="f-date" name="publishDate" class="input" [(ngModel)]="f.publishDate" /></div>
          <div><label class="form-label" for="f-other">{{ 'DIGITAL_ITEMS_PAGE.OTHER_TITLE' | translate }}</label><input id="f-other" name="otherTitle" class="input" [(ngModel)]="f.otherTitle" /></div>
          <div><label class="form-label" for="f-page">{{ 'DIGITAL_ITEMS_PAGE.PAGE' | translate }}</label><input id="f-page" name="page" class="input" [(ngModel)]="f.page" /></div>
          <div><label class="form-label" for="f-old">{{ 'DIGITAL_ITEMS_PAGE.OLD_AUTHOR' | translate }}</label><input id="f-old" name="oldAuthor" class="input" [(ngModel)]="f.oldAuthor" /></div>
          <div class="col-span-2"><label class="form-label" for="f-kw">{{ 'DIGITAL_CAT.KEYWORD' | translate }}</label><input id="f-kw" name="keyword" class="input" [(ngModel)]="f.keyword" /></div>

          <div>
            <label class="form-label" for="f-coll">{{ 'DIGITAL_CAT.COLLECTION' | translate }}</label>
            <select id="f-coll" name="collectionId" class="input" [(ngModel)]="f.collectionId">
              <option [ngValue]="null">—</option>
              @for (o of collectionOptions(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }
            </select>
          </div>
          <div>
            <label class="form-label" for="f-subj">{{ 'DIGITAL_CAT.SUBJECTS_TITLE' | translate }}</label>
            <select id="f-subj" name="subjectId" class="input" [(ngModel)]="f.subjectId">
              <option [ngValue]="null">—</option>
              @for (o of subjectOptions(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }
            </select>
          </div>
          <div>
            <label class="form-label" for="f-topic">{{ 'DIGITAL_CAT.TOPICS_TITLE' | translate }}</label>
            <select id="f-topic" name="topicId" class="input" [(ngModel)]="f.topicId">
              <option [ngValue]="null">—</option>
              @for (o of topicOptions(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }
            </select>
          </div>
          <div>
            <label class="form-label" for="f-type">{{ 'DIGITAL_CAT.DIG_TYPES_TITLE' | translate }}</label>
            <select id="f-type" name="typeId" class="input" [(ngModel)]="f.typeId">
              <option [ngValue]="null">—</option>
              @for (o of typeOptions(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }
            </select>
          </div>

          <div><label class="form-label" for="f-lang">{{ 'DIGITAL_CAT.LANGUAGE' | translate }}</label><input id="f-lang" name="language" class="input" [(ngModel)]="f.language" placeholder="vi" /></div>
          <div class="flex items-center gap-5 pt-6">
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
              <input type="checkbox" name="free" class="rounded border-gray-300" [(ngModel)]="f.free" /> {{ 'DIGITAL_ITEMS_PAGE.FREE' | translate }}
            </label>
            <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
              <input type="checkbox" name="allowDownload" class="rounded border-gray-300" [(ngModel)]="f.allowDownload" /> {{ 'DIGITAL_ITEMS_PAGE.ALLOW_DOWNLOAD' | translate }}
            </label>
          </div>
          <div><label class="form-label" for="f-print">{{ 'DIGITAL_ITEMS_PAGE.PRINT_COPIES' | translate }}</label>
            <input id="f-print" name="printCopies" type="number" min="0" class="input" [(ngModel)]="f.printCopies" [placeholder]="'DIGITAL_CAT.NO_LIMIT' | translate" /></div>
          <div><label class="form-label" for="f-offline">{{ 'DIGITAL_ITEMS_PAGE.OFFLINE_DAYS' | translate }}</label>
            <input id="f-offline" name="offlineDays" type="number" min="0" class="input" [(ngModel)]="f.offlineDays" [placeholder]="'DIGITAL_CAT.NO_LIMIT' | translate" /></div>
          <div>
            <label class="form-label" for="f-status">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="f-status" name="status" class="input" [(ngModel)]="f.status">
              <option [ngValue]="2">{{ 'COMMON.ACTIVE' | translate }}</option>
              <option [ngValue]="1">{{ 'DIGITAL_CAT.STATUS_HIDDEN' | translate }}</option>
            </select>
          </div>

          <div class="col-span-2">
            <label class="form-label">{{ 'DIGITAL_CAT.COVER' | translate }}</label>
            <div class="flex items-center gap-3">
              @if (f.coverUrl) { <img [src]="f.coverUrl" class="w-14 h-18 object-cover rounded border border-gray-200" referrerpolicy="no-referrer" alt="" /> }
              <input type="file" accept="image/png,image/jpeg,image/webp" (change)="onCoverSelect($event, f)" />
              @if (uploadingCover()) { <span class="material-icons animate-spin text-[18px] text-blue-600">autorenew</span> }
            </div>
          </div>

          <div class="col-span-2 border border-gray-200 rounded-lg p-3">
            <label class="form-label">{{ 'DIGITAL_ITEMS_PAGE.LINKED_BIB' | translate }}</label>
            @if (f.linkedBibPublicId) {
              <div class="flex items-center justify-between gap-2 text-sm">
                <span>{{ f.linkedBibLabel }}</span>
                <button type="button" class="text-red-600 hover:underline text-xs" (click)="unlinkBib(f)">{{ 'DIGITAL_ITEMS_PAGE.UNLINK' | translate }}</button>
              </div>
            } @else {
              <div class="flex gap-2">
                <input class="input" [(ngModel)]="bibQuery" name="bibQuery" [ngModelOptions]="{ standalone: true }" (keydown.enter)="searchBib()" [placeholder]="'DIGITAL_ITEMS_PAGE.LINK_BIB_PH' | translate" />
                <button type="button" class="btn-secondary !py-1.5 !px-3" (click)="searchBib()">{{ 'COMMON.SEARCH_BTN' | translate }}</button>
              </div>
              @if (bibResults().length) {
                <ul class="mt-2 max-h-36 overflow-y-auto border border-gray-100 rounded-lg divide-y divide-gray-100">
                  @for (b of bibResults(); track b.bibPublicId) {
                    <li class="px-2 py-1.5 text-sm hover:bg-gray-50 cursor-pointer" (click)="pickBib(f, b)">
                      {{ b.title }} <span class="text-gray-400 text-xs">{{ b.author }}</span>
                    </li>
                  }
                </ul>
              }
            }
          </div>

          @if (f.publicId && metaFields().length) {
            <div class="col-span-2 border border-gray-200 rounded-lg p-3">
              <div class="flex items-center justify-between mb-2">
                <label class="form-label !mb-0">{{ 'DIGITAL_ITEMS_PAGE.METADATA' | translate }}</label>
                <button type="button" class="btn-secondary !py-1 !px-2.5" (click)="addMeta()">
                  <span class="material-icons text-[16px]">add</span> {{ 'DIGITAL_ITEMS_PAGE.ADD_METADATA' | translate }}</button>
              </div>
              @for (m of metaRows; track $index) {
                <div class="grid grid-cols-12 gap-2 mb-1.5 items-start">
                  <select class="input col-span-4" [(ngModel)]="m.fieldPublicId" [name]="'mf' + $index" [attr.aria-label]="'DIGITAL_ITEMS_PAGE.METADATA' | translate">
                    @for (mf of metaFields(); track mf.publicId) { <option [ngValue]="mf.publicId">{{ mf.schemaShortId }}:{{ mf.field }}{{ mf.subfield ? '.' + mf.subfield : '' }} — {{ mf.descriptionVn }}</option> }
                  </select>
                  <input class="input col-span-6" [(ngModel)]="m.value" [name]="'mv' + $index" maxlength="2000" [attr.aria-label]="'DIGITAL_ITEMS_PAGE.METADATA_VALUE' | translate" />
                  <select class="input col-span-1" [(ngModel)]="m.language" [name]="'ml' + $index" aria-label="Language">
                    <option value="">—</option><option value="vi">vi</option><option value="en">en</option>
                  </select>
                  <button type="button" class="icon-btn text-red-600 col-span-1" (click)="metaRows.splice($index, 1)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">close</span></button>
                </div>
              }
            </div>
          }

          @if (f.publicId) {
            <div class="col-span-2 border border-gray-200 rounded-lg p-3">
              <div class="flex items-center justify-between mb-2">
                <label class="form-label !mb-0">{{ 'DIGITAL_ITEMS_PAGE.FILES' | translate }}</label>
                <label class="btn-secondary !py-1 !px-2.5 cursor-pointer">
                  <span class="material-icons text-[16px]">upload_file</span> {{ 'DIGITAL_ITEMS_PAGE.ADD_FILE' | translate }}
                  <input type="file" accept="application/pdf" class="hidden" (change)="onFileSelect($event, f)" />
                </label>
              </div>
              @if (uploadingFile()) { <p class="text-xs text-blue-600"><span class="material-icons animate-spin text-[14px] align-middle">autorenew</span> {{ 'DIGITAL_ITEMS_PAGE.UPLOADING' | translate }}</p> }
              <ul class="divide-y divide-gray-100">
                @for (file of files(); track file.publicId) {
                  <li class="py-1.5 flex items-center justify-between text-sm">
                    <span><span class="material-icons text-[16px] align-middle text-gray-400">picture_as_pdf</span> {{ file.description || file.fileExt || file.publicId }}
                      @if (file.fileSize != null) { <span class="text-gray-400 text-xs ml-1">({{ (file.fileSize / 1024 / 1024).toFixed(1) }} MB)</span> }
                    </span>
                    <button type="button" class="text-red-600 hover:underline text-xs" (click)="deleteFile(file)">{{ 'COMMON.DELETE' | translate }}</button>
                  </li>
                } @empty { <li class="py-2 text-xs text-gray-400">{{ 'DIGITAL_ITEMS_PAGE.NO_FILES' | translate }}</li> }
              </ul>
            </div>
          }
        </form>
        <ng-container footer>
          <button type="button" (click)="form.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="item-form" [disabled]="saving()" class="btn-primary">
            <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span> {{ 'COMMON.SAVE' | translate }}
          </button>
        </ng-container>
      </app-modal>
    }

    @if (deleting(); as d) {
      <app-confirm-delete [busy]="saving()" message="COMMON.DELETE_CONFIRM_IRREVERSIBLE" (confirmed)="doDelete(d)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class DigitalItems implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly client = this.api.crud<EbookItem>('items', 'digital');
  private readonly fileClient = this.api.crud<EbookFile>('files', 'digital');

  protected search: EbookItemSearch = { keyword: '', collectionId: null, status: null, pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<Page<EbookItem> | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly uploadingCover = signal(false);
  protected readonly uploadingFile = signal(false);
  protected readonly form = signal<ItemForm | null>(null);
  protected readonly deleting = signal<EbookItem | null>(null);
  protected readonly files = signal<EbookFile[]>([]);
  protected readonly metaFields = signal<MetadataField[]>([]);
  protected metaRows: MetaRow[] = [];

  protected readonly collectionOptions = signal<{ id: number; label: string }[]>([]);
  protected readonly subjectOptions = signal<{ id: number; label: string }[]>([]);
  protected readonly topicOptions = signal<{ id: number; label: string }[]>([]);
  protected readonly typeOptions = signal<{ id: number; label: string }[]>([]);

  protected bibQuery = '';
  protected readonly bibResults = signal<BibLinkOption[]>([]);

  ngOnInit(): void {
    void this.load();
    void this.loadOptions();
  }

  protected can(action: string): boolean {
    return this.session.can(`DIGITAL_ITEMS:${action}`);
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

  private flattenTree(rows: TreeRow[]): { id: number; label: string }[] {
    const byParent = new Map<number | null, TreeRow[]>();
    for (const r of rows) {
      const k = r.parentId ?? null;
      (byParent.get(k) ?? byParent.set(k, []).get(k)!).push(r);
    }
    const out: { id: number; label: string }[] = [];
    const walk = (parentId: number | null, depth: number) =>
      (byParent.get(parentId) ?? [])
        .slice()
        .sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id)
        .forEach((r) => {
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

  protected openCreate(): void {
    this.form.set({
      publicId: null, title: '', author: '', publisher: '', publishDate: '', otherTitle: '', page: '', oldAuthor: '', keyword: '', language: 'vi',
      collectionId: null, subjectId: null, topicId: null, typeId: null, free: false, allowDownload: false, printCopies: null, offlineDays: null,
      status: STATUS_ACTIVE, coverUrl: null, linkedBibPublicId: null, linkedBibLabel: null,
    });
    this.files.set([]);
    this.metaRows = [];
    this.bibQuery = '';
    this.bibResults.set([]);
  }

  protected async openEdit(item: EbookItem): Promise<void> {
    this.form.set({
      publicId: item.publicId, title: item.title, author: item.author ?? '', publisher: item.publisher ?? '', publishDate: item.publishDate ?? '',
      otherTitle: item.otherTitle ?? '', page: item.page ?? '', oldAuthor: item.oldAuthor ?? '', keyword: item.keyword ?? '', language: item.language ?? 'vi',
      collectionId: item.collectionId, subjectId: item.subjectId, topicId: item.topicId, typeId: item.typeId, free: item.free,
      allowDownload: item.allowDownload, printCopies: item.printCopies, offlineDays: item.offlineDays, status: item.status, coverUrl: item.coverUrl,
      linkedBibPublicId: item.linkedBibPublicId, linkedBibLabel: item.linkedBibPublicId,
    });
    this.bibQuery = '';
    this.bibResults.set([]);
    this.metaRows = [];
    try {
      this.files.set((await this.fileClient.search({ ebookItemPublicId: item.publicId, pageSize: 200 } as EbookFileSearch)).items);
      this.metaFields.set((await this.api.crud<MetadataField>('metadata-fields', 'digital').searchAll()).filter((x) => x.status === STATUS_ACTIVE));
      if (this.metaFields().length) {
        this.metaRows = (await this.api.ebookMetadata(item.publicId)).map((r) => ({ fieldPublicId: r.fieldPublicId, value: r.value, language: r.language ?? '' }));
      }
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected addMeta(): void {
    this.metaRows.push({ fieldPublicId: this.metaFields()[0]?.publicId ?? '', value: '', language: '' });
  }

  protected async onCoverSelect(event: Event, f: ItemForm): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.uploadingCover.set(true);
    try {
      const uploaded = await this.api.upload('ebook-cover', file);
      f.coverUrl = uploaded.url;
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.uploadingCover.set(false);
    }
  }

  protected async searchBib(): Promise<void> {
    try {
      this.bibResults.set(await this.api.ebookBibOptions(this.bibQuery.trim()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected pickBib(f: ItemForm, b: BibLinkOption): void {
    f.linkedBibPublicId = b.bibPublicId;
    f.linkedBibLabel = b.title;
    if (!f.publicId) {
      if (!f.title.trim()) f.title = b.title;
      if (!f.author.trim() && b.author) f.author = b.author;
      if (!f.publisher.trim() && b.publisher) f.publisher = b.publisher;
      if (!f.publishDate.trim() && b.publishYear) f.publishDate = b.publishYear;
    }
    this.bibResults.set([]);
  }

  protected unlinkBib(f: ItemForm): void {
    f.linkedBibPublicId = null;
    f.linkedBibLabel = null;
  }

  protected async onFileSelect(event: Event, f: ItemForm): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || !f.publicId) return;
    this.uploadingFile.set(true);
    try {
      const uploaded = await this.api.upload('ebook-file', file);
      const ext = file.name.includes('.') ? file.name.slice(file.name.lastIndexOf('.') + 1).toLowerCase() : null;
      await this.fileClient.add({
        ebookItemPublicId: f.publicId, mediaFileId: uploaded.id, fileType: file.type || null, fileExt: ext, fileSize: file.size,
        description: file.name, sortOrder: this.files().length,
      });
      this.files.set((await this.fileClient.search({ ebookItemPublicId: f.publicId, pageSize: 200 } as EbookFileSearch)).items);
      this.toastr.success('COMMON.ADD_SUCCESS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.uploadingFile.set(false);
    }
  }

  protected async deleteFile(file: EbookFile): Promise<void> {
    try {
      await this.fileClient.delete(file.publicId);
      this.files.set(this.files().filter((x) => x.publicId !== file.publicId));
      this.toastr.success('COMMON.DELETE_SUCCESS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async save(): Promise<void> {
    const f = this.form();
    if (!f) return;
    if (!f.title.trim()) {
      this.toastr.warning('DIGITAL_ITEMS_PAGE.TITLE_REQUIRED');
      return;
    }
    this.saving.set(true);
    try {
      const body = {
        title: f.title, author: f.author || null, publisher: f.publisher || null, publishDate: f.publishDate || null, otherTitle: f.otherTitle || null,
        page: f.page || null, oldAuthor: f.oldAuthor || null, language: f.language || null, keyword: f.keyword || null, collectionId: f.collectionId,
        subjectId: f.subjectId, topicId: f.topicId, typeId: f.typeId, free: f.free, allowDownload: f.allowDownload, printCopies: f.printCopies,
        offlineDays: f.offlineDays, coverUrl: f.coverUrl, linkedBibPublicId: f.linkedBibPublicId, status: f.status,
      };
      if (f.publicId) {
        await this.client.update(f.publicId, body);
        if (this.metaFields().length) {
          await this.api.saveEbookMetadata(f.publicId, this.metaRows.filter((m) => m.fieldPublicId && m.value.trim())
            .map((m) => ({ fieldPublicId: m.fieldPublicId, value: m.value.trim(), language: m.language || null })));
        }
      } else await this.client.add(body);
      this.toastr.success(f.publicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async doDelete(item: EbookItem): Promise<void> {
    this.saving.set(true);
    try {
      await this.client.delete(item.publicId);
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
      this.page.set(await this.client.search({ ...this.search, keyword: this.search.keyword?.trim() || undefined }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
