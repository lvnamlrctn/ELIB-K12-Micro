import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, BibLinkOption, EbookItem, MetadataField, STATUS_ACTIVE, STATUS_INACTIVE, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { ItemCover } from './item-cover';
import { OptionItem, TOGGLE_CLASS } from './item-tools';

interface PersonRow { last: string; first: string; title: string; }
interface IdRow { type: string; value: string; }
interface MetaRow { fieldPublicId: string; value: string; language: string; }

interface Form {
  publicId: string | null;
  published: boolean; allowDownload: boolean; free: boolean; printCopies: number | null; offlineDays: number | null;
  subjectId: number | null; topicId: number | null; share: number; collectionId: number | null;
  title: string; otherTitles: string[]; publisher: string; publishDay: number | null; publishMonth: number | null; publishYear: string; originalDate: string | null; originalYear: number | null;
  authors: PersonRow[]; page: string; coverUrl: string | null; typeId: number | null; language: string; journalName: string;
  advisors: PersonRow[]; keywords: string[]; identifiers: IdRow[]; abstract: string; description: string;
  linkedBibPublicId: string | null; linkedBibLabel: string | null; xml: string | null; show: number | null; indexContent: number | null; reason: string;
}

const LANGUAGES = [
  { value: 'vi', label: 'DIGITAL_DOC.LANG_VI' }, { value: 'en', label: 'DIGITAL_DOC.LANG_EN' }, { value: 'fr', label: 'DIGITAL_DOC.LANG_FR' },
  { value: 'de', label: 'DIGITAL_DOC.LANG_DE' }, { value: 'zh', label: 'DIGITAL_DOC.LANG_ZH' }, { value: 'ja', label: 'DIGITAL_DOC.LANG_JA' },
  { value: 'ru', label: 'DIGITAL_DOC.LANG_RU' }, { value: 'es', label: 'DIGITAL_DOC.LANG_ES' },
];

const ID_TYPES = [
  { value: 'issn', label: 'ISSN' }, { value: 'ismn', label: 'ISMN' }, { value: 'isbn', label: 'ISBN' }, { value: 'uri', label: 'URI' },
  { value: 'govdoc', label: "Gov't Doc #" }, { value: 'other', label: 'Other' },
];

const LABEL = 'block text-sm font-semibold text-blue-700 mb-1';

/** Tách chuỗi "a; b" thành danh sách (ít nhất một dòng trống để nhập). */
function split(value: string | null): string[] {
  const parts = (value ?? '').split(';').map((x) => x.trim()).filter(Boolean);
  return parts.length ? parts : [''];
}

const blank = (): Form => ({
  publicId: null, published: true, allowDownload: true, free: true, printCopies: null, offlineDays: null, subjectId: null, topicId: null, share: 0, collectionId: null,
  title: '', otherTitles: [''], publisher: '', publishDay: null, publishMonth: null, publishYear: '', originalDate: null, originalYear: null,
  authors: [{ last: '', first: '', title: '' }], page: '', coverUrl: null, typeId: null, language: 'vi', journalName: '',
  advisors: [{ last: '', first: '', title: '' }], keywords: [''], identifiers: [{ type: 'issn', value: '' }], abstract: '', description: '',
  linkedBibPublicId: null, linkedBibLabel: null, xml: null, show: null, indexContent: null, reason: '',
});

/**
 * Thêm/sửa tài liệu số — một form cuộn như ELIB-LRC (cờ xuất bản/tải/miễn phí, phân loại, nhan đề, nhà xuất bản + ngày tháng năm, tác giả họ/tên,
 * người hướng dẫn, từ khoá, số định danh, tóm tắt, mô tả, siêu dữ liệu, tài liệu in liên kết). Khác LRC: sửa được mọi trường ở cả hai chế độ.
 * File đính kèm quản lý ở màn riêng (Quản lý File).
 */
@Component({
  selector: 'app-item-form',
  imports: [FormsModule, ItemCover, Modal, TranslatePipe],
  template: `
    @let f = form();
    <app-modal [title]="f.publicId ? 'DIGITAL_DOC.EDIT_TITLE' : 'DIGITAL_DOC.ADD_TITLE'" widthClass="max-w-4xl" (closed)="closed.emit()">
      <form id="item-form" (ngSubmit)="save()" class="space-y-4">
        <div class="flex flex-wrap gap-x-8 gap-y-2 text-sm text-gray-700">
          <label class="flex items-center gap-2 cursor-pointer select-none"><span class="relative inline-flex items-center"><input type="checkbox" class="sr-only peer" name="published" [(ngModel)]="f.published" /><span [class]="toggle"></span></span>{{ 'DIGITAL_DOC.PUBLISHED' | translate }}</label>
          <label class="flex items-center gap-2 cursor-pointer select-none"><span class="relative inline-flex items-center"><input type="checkbox" class="sr-only peer" name="allowDownload" [(ngModel)]="f.allowDownload" /><span [class]="toggle"></span></span>{{ 'DIGITAL_DOC.ALLOW_DOWNLOAD' | translate }}</label>
          <label class="flex items-center gap-2 cursor-pointer select-none"><span class="relative inline-flex items-center"><input type="checkbox" class="sr-only peer" name="free" [(ngModel)]="f.free" /><span [class]="toggle"></span></span>{{ 'DIGITAL_DOC.FREE' | translate }}</label>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div><label [class]="label" for="f-print">{{ 'DIGITAL_DOC.PRINT_COPIES' | translate }}</label>
            <input id="f-print" name="printCopies" type="number" min="0" class="input" [(ngModel)]="f.printCopies" /><p class="text-xs text-gray-400 mt-0.5">{{ 'DIGITAL_DOC.ZERO_NO_LIMIT' | translate }}</p></div>
          <div><label [class]="label" for="f-offline">{{ 'DIGITAL_DOC.OFFLINE_DAYS' | translate }}</label>
            <input id="f-offline" name="offlineDays" type="number" min="0" class="input" [(ngModel)]="f.offlineDays" /><p class="text-xs text-gray-400 mt-0.5">{{ 'DIGITAL_DOC.ZERO_NO_LIMIT' | translate }}</p></div>
          <div><label [class]="label" for="f-subject">{{ 'DIGITAL_DOC.SUBJECT' | translate }}</label>
            <select id="f-subject" name="subjectId" class="input" [(ngModel)]="f.subjectId"><option [ngValue]="null">—</option>@for (o of subjects(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select></div>
          <div><label [class]="label" for="f-topic">{{ 'DIGITAL_DOC.TOPIC' | translate }}</label>
            <select id="f-topic" name="topicId" class="input" [(ngModel)]="f.topicId"><option [ngValue]="null">—</option>@for (o of topics(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select></div>
          <div><label [class]="label" for="f-share">{{ 'DIGITAL_DOC.SHARE' | translate }}</label>
            <select id="f-share" name="share" class="input" [(ngModel)]="f.share">
              <option [ngValue]="0">{{ 'DIGITAL_DOC.SHARE_NONE' | translate }}</option><option [ngValue]="1">{{ 'DIGITAL_DOC.SHARE_PUBLIC' | translate }}</option><option [ngValue]="2">{{ 'DIGITAL_DOC.SHARE_CONTROLLED' | translate }}</option></select></div>
          <div><label [class]="label" for="f-coll">{{ 'DIGITAL_DOC.COLLECTION' | translate }}</label>
            <select id="f-coll" name="collectionId" class="input" [(ngModel)]="f.collectionId"><option [ngValue]="null">—</option>@for (o of collections(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select></div>
        </div>

        <div class="border border-gray-200 rounded-lg p-3">
          <div [class]="label + ' !text-emerald-700 uppercase !text-xs'">{{ 'DIGITAL_DOC.LINKED_BIB' | translate }}</div>
          @if (f.linkedBibPublicId) {
            <div class="flex items-center justify-between gap-2 text-sm">
              <span class="font-medium text-gray-800">{{ f.linkedBibLabel }}</span>
              <button type="button" class="text-red-600 hover:underline text-xs" (click)="unlinkBib(f)">{{ 'DIGITAL_DOC.UNLINK' | translate }}</button>
            </div>
          } @else {
            <div class="flex gap-2">
              <input class="input" [(ngModel)]="bibQuery" name="bibQuery" [ngModelOptions]="{ standalone: true }" (keydown.enter)="searchBib($event)" [placeholder]="'DIGITAL_DOC.LINK_BIB_PH' | translate" />
              <button type="button" class="px-3 py-1.5 rounded-lg bg-emerald-600 text-white text-sm hover:bg-emerald-700 shrink-0" (click)="searchBib()">+ {{ 'DIGITAL_DOC.LINK_BIB' | translate }}</button>
            </div>
            @if (bibResults().length) {
              <ul class="mt-2 max-h-36 overflow-y-auto border border-gray-100 rounded-lg divide-y divide-gray-100">
                @for (b of bibResults(); track b.bibPublicId) {
                  <li class="px-2 py-1.5 text-sm hover:bg-gray-50 cursor-pointer" (click)="pickBib(f, b)">{{ b.title }} <span class="text-gray-400 text-xs">{{ b.author }}</span></li>
                }
              </ul>
            }
          }
        </div>

        <div><label [class]="label" for="f-title">{{ 'DIGITAL_DOC.TITLE_FIELD' | translate }} <span class="text-red-500">*</span></label>
          <input id="f-title" name="title" class="input" maxlength="1000" [(ngModel)]="f.title" required /></div>

        <div>
          <label [class]="label">{{ 'DIGITAL_DOC.OTHER_TITLES' | translate }}</label>
          @for (t of f.otherTitles; track $index) {
            <div class="flex gap-2 mb-1.5">
              <input class="input" [name]="'ot' + $index" [(ngModel)]="f.otherTitles[$index]" [placeholder]="'DIGITAL_DOC.OTHER_TITLE_PH' | translate" [attr.aria-label]="'DIGITAL_DOC.OTHER_TITLES' | translate" />
              @if (f.otherTitles.length > 1) { <button type="button" class="icon-btn text-red-600" (click)="f.otherTitles.splice($index, 1)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">remove_circle</span></button> }
              @if ($last) { <button type="button" class="icon-btn text-blue-600" (click)="f.otherTitles.push('')" [title]="'COMMON.ADD_BTN' | translate"><span class="material-icons text-[18px]">add_circle</span></button> }
            </div>
          }
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div><label [class]="label" for="f-pub">{{ 'DIGITAL_DOC.PUBLISHER' | translate }}</label>
            <input id="f-pub" name="publisher" class="input" maxlength="500" [(ngModel)]="f.publisher" list="publishers" />
            <datalist id="publishers">@for (p of publisherHints(); track p) { <option [value]="p"></option> }</datalist></div>
          <div>
            <label [class]="label">{{ 'DIGITAL_DOC.PUBLISH_DATE' | translate }}</label>
            <div class="grid grid-cols-3 gap-2">
              <input name="publishDay" type="number" min="1" max="31" class="input" [(ngModel)]="f.publishDay" [placeholder]="'DIGITAL_DOC.DAY' | translate" [attr.aria-label]="'DIGITAL_DOC.DAY' | translate" />
              <select name="publishMonth" class="input" [(ngModel)]="f.publishMonth" [attr.aria-label]="'DIGITAL_DOC.MONTH' | translate">
                <option [ngValue]="null">{{ 'DIGITAL_DOC.MONTH' | translate }}</option>
                @for (m of months; track m) { <option [ngValue]="m">{{ 'DIGITAL_DOC.MONTH_N' | translate: { n: m } }}</option> }
              </select>
              <input name="publishYear" class="input" maxlength="4" inputmode="numeric" [(ngModel)]="f.publishYear" [placeholder]="'DIGITAL_DOC.YEAR' | translate" [attr.aria-label]="'DIGITAL_DOC.YEAR' | translate" />
            </div>
          </div>
        </div>

        <div>
          <label [class]="label">{{ 'DIGITAL_DOC.AUTHORS' | translate }}</label>
          @for (a of f.authors; track $index) {
            <div class="grid grid-cols-12 gap-2 mb-1.5">
              <input class="input col-span-5" [name]="'al' + $index" [(ngModel)]="a.last" [placeholder]="'DIGITAL_DOC.LAST_NAME' | translate" [attr.aria-label]="'DIGITAL_DOC.LAST_NAME' | translate" list="author-hints" />
              <input class="input col-span-5" [name]="'af' + $index" [(ngModel)]="a.first" [placeholder]="'DIGITAL_DOC.FIRST_NAME' | translate" [attr.aria-label]="'DIGITAL_DOC.FIRST_NAME' | translate" />
              <div class="col-span-2 flex">
                @if (f.authors.length > 1) { <button type="button" class="icon-btn text-red-600" (click)="f.authors.splice($index, 1)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">remove_circle</span></button> }
                @if ($last) { <button type="button" class="icon-btn text-blue-600" (click)="f.authors.push({ last: '', first: '', title: '' })" [title]="'COMMON.ADD_BTN' | translate"><span class="material-icons text-[18px]">add_circle</span></button> }
              </div>
            </div>
          }
          <datalist id="author-hints">@for (p of authorHints(); track p) { <option [value]="p"></option> }</datalist>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label [class]="label" for="f-page">{{ 'DIGITAL_DOC.PAGES' | translate }}</label>
            <input id="f-page" name="page" class="input" maxlength="50" [(ngModel)]="f.page" />
          </div>
          <div>
            <label [class]="label">{{ 'DIGITAL_DOC.COVER' | translate }}</label>
            <div class="flex items-center gap-3 flex-wrap">
              <label class="px-3 py-1.5 rounded-lg border border-gray-300 text-sm cursor-pointer hover:bg-gray-50">{{ 'DIGITAL_DOC.BROWSE' | translate }}
                <input type="file" accept="image/png,image/jpeg,image/webp" class="hidden" (change)="onCover($event, f)" /></label>
              @if (f.coverUrl) { <img [src]="f.coverUrl" class="h-16 w-12 object-cover rounded border border-gray-200" referrerpolicy="no-referrer" alt="" /> }
              @if (uploadingCover()) { <span class="material-icons animate-spin text-[18px] text-blue-600">autorenew</span> }
              @if (f.publicId && item(); as it) {
                <button type="button" class="px-3 py-1.5 rounded-lg bg-purple-600 text-white text-sm hover:bg-purple-700" (click)="coverDialog.set(true)">{{ 'DIGITAL_DOC.COVER_FROM_PDF' | translate }}</button>
              }
            </div>
          </div>
          <div><label [class]="label" for="f-type">{{ 'DIGITAL_DOC.DOC_TYPE' | translate }}</label>
            <select id="f-type" name="typeId" class="input" [(ngModel)]="f.typeId"><option [ngValue]="null">—</option>@for (o of types(); track o.id) { <option [ngValue]="o.id">{{ o.label }}</option> }</select></div>
          <div><label [class]="label" for="f-lang">{{ 'DIGITAL_DOC.LANGUAGE' | translate }}</label>
            <select id="f-lang" name="language" class="input" [(ngModel)]="f.language">@for (l of languages; track l.value) { <option [value]="l.value">{{ l.label | translate }}</option> }</select></div>
          <div class="md:col-span-2"><label [class]="label" for="f-journal">{{ 'DIGITAL_DOC.JOURNAL' | translate }}</label>
            <input id="f-journal" name="journalName" class="input" maxlength="500" [(ngModel)]="f.journalName" /></div>
        </div>

        <div>
          <label [class]="label">{{ 'DIGITAL_DOC.ADVISORS' | translate }}</label>
          @for (a of f.advisors; track $index) {
            <div class="grid grid-cols-12 gap-2 mb-1.5">
              <input class="input col-span-4" [name]="'vl' + $index" [(ngModel)]="a.last" [placeholder]="'DIGITAL_DOC.LAST_NAME' | translate" [attr.aria-label]="'DIGITAL_DOC.LAST_NAME' | translate" />
              <input class="input col-span-4" [name]="'vf' + $index" [(ngModel)]="a.first" [placeholder]="'DIGITAL_DOC.FIRST_NAME' | translate" [attr.aria-label]="'DIGITAL_DOC.FIRST_NAME' | translate" />
              <input class="input col-span-2" [name]="'vt' + $index" [(ngModel)]="a.title" [placeholder]="'DIGITAL_DOC.ADVISOR_TITLE' | translate" [attr.aria-label]="'DIGITAL_DOC.ADVISOR_TITLE' | translate" />
              <div class="col-span-2 flex">
                @if (f.advisors.length > 1) { <button type="button" class="icon-btn text-red-600" (click)="f.advisors.splice($index, 1)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">remove_circle</span></button> }
                @if ($last) { <button type="button" class="icon-btn text-blue-600" (click)="f.advisors.push({ last: '', first: '', title: '' })" [title]="'COMMON.ADD_BTN' | translate"><span class="material-icons text-[18px]">add_circle</span></button> }
              </div>
            </div>
          }
        </div>

        <div>
          <label [class]="label">{{ 'DIGITAL_DOC.KEYWORDS' | translate }}</label>
          @for (k of f.keywords; track $index) {
            <div class="flex gap-2 mb-1.5">
              <input class="input" [name]="'kw' + $index" [(ngModel)]="f.keywords[$index]" [attr.aria-label]="'DIGITAL_DOC.KEYWORDS' | translate" />
              @if (f.keywords.length > 1) { <button type="button" class="icon-btn text-red-600" (click)="f.keywords.splice($index, 1)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">remove_circle</span></button> }
              @if ($last) { <button type="button" class="icon-btn text-blue-600" (click)="f.keywords.push('')" [title]="'COMMON.ADD_BTN' | translate"><span class="material-icons text-[18px]">add_circle</span></button> }
            </div>
          }
        </div>

        <div>
          <label [class]="label">{{ 'DIGITAL_DOC.NUMBERS' | translate }}</label>
          @for (n of f.identifiers; track $index) {
            <div class="grid grid-cols-12 gap-2 mb-1.5">
              <select class="input col-span-3" [name]="'it' + $index" [(ngModel)]="n.type" [attr.aria-label]="'DIGITAL_DOC.NUMBERS' | translate">@for (t of idTypes; track t.value) { <option [value]="t.value">{{ t.label }}</option> }</select>
              <input class="input col-span-7" [name]="'iv' + $index" [(ngModel)]="n.value" maxlength="300" [placeholder]="'DIGITAL_DOC.VALUE_PH' | translate" [attr.aria-label]="'DIGITAL_DOC.VALUE_PH' | translate" />
              <div class="col-span-2 flex">
                @if (f.identifiers.length > 1) { <button type="button" class="icon-btn text-red-600" (click)="f.identifiers.splice($index, 1)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">remove_circle</span></button> }
                @if ($last) { <button type="button" class="icon-btn text-blue-600" (click)="f.identifiers.push({ type: 'issn', value: '' })" [title]="'COMMON.ADD_BTN' | translate"><span class="material-icons text-[18px]">add_circle</span></button> }
              </div>
            </div>
          }
        </div>

        <div><label [class]="label" for="f-abstract">{{ 'DIGITAL_DOC.ABSTRACT' | translate }}</label>
          <textarea id="f-abstract" name="abstract" rows="3" class="input" maxlength="20000" [(ngModel)]="f.abstract" [placeholder]="'DIGITAL_DOC.ABSTRACT_PH' | translate"></textarea></div>
        <div><label [class]="label" for="f-desc">{{ 'DIGITAL_DOC.DESCRIPTION' | translate }}</label>
          <textarea id="f-desc" name="description" rows="3" class="input" maxlength="20000" [(ngModel)]="f.description" [placeholder]="'DIGITAL_DOC.DESCRIPTION_PH' | translate"></textarea></div>

        @if (f.publicId && metaFields().length) {
          <div class="border border-gray-200 rounded-lg p-3">
            <div class="flex items-center justify-between mb-2">
              <label [class]="label + ' !mb-0'">{{ 'DIGITAL_DOC.METADATA' | translate }}</label>
              <button type="button" class="btn-secondary !py-1 !px-2.5" (click)="addMeta()"><span class="material-icons text-[16px]">add</span> {{ 'DIGITAL_DOC.ADD_METADATA' | translate }}</button>
            </div>
            @for (m of metaRows; track $index) {
              <div class="grid grid-cols-12 gap-2 mb-1.5 items-start">
                <select class="input col-span-4" [(ngModel)]="m.fieldPublicId" [name]="'mf' + $index" [attr.aria-label]="'DIGITAL_DOC.METADATA' | translate">
                  <option value="">{{ 'DIGITAL_DOC.PICK_FIELD' | translate }}</option>
                  @for (mf of metaFields(); track mf.publicId) { <option [value]="mf.publicId">{{ mf.schemaShortId }}:{{ mf.field }}{{ mf.subfield ? '.' + mf.subfield : '' }} — {{ mf.descriptionVn }}</option> }
                </select>
                <input class="input col-span-6" [(ngModel)]="m.value" [name]="'mv' + $index" maxlength="2000" [placeholder]="'DIGITAL_DOC.VALUE_PH' | translate" [attr.aria-label]="'DIGITAL_DOC.VALUE_PH' | translate" />
                <select class="input col-span-1" [(ngModel)]="m.language" [name]="'ml' + $index" aria-label="Language"><option value="">—</option><option value="vi">vi</option><option value="en">en</option></select>
                <button type="button" class="icon-btn text-red-600 col-span-1" (click)="metaRows.splice($index, 1)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">close</span></button>
              </div>
            }
          </div>
        }
      </form>
      <ng-container footer>
        @if (f.publicId) {
          <input class="input !w-64 mr-auto" name="reason" maxlength="500" [(ngModel)]="f.reason" [ngModelOptions]="{ standalone: true }" [placeholder]="'DIGITAL_DOC.REASON_PH' | translate" [attr.aria-label]="'DIGITAL_DOC.REASON_PH' | translate" />
        }
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        <button type="submit" form="item-form" class="btn-primary" [disabled]="saving()">
          <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span> {{ 'DIGITAL_DOC.SAVE' | translate }}
        </button>
      </ng-container>
    </app-modal>

    @if (coverDialog() && item(); as it) {
      <app-item-cover [item]="it" (closed)="coverDialog.set(false)" (saved)="onCoverFromPdf($event)" />
    }
  `,
})
export class ItemForm implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly client = this.api.crud<EbookItem>('items', 'digital');

  /** Tài liệu đang sửa; bỏ trống = thêm mới. */
  readonly item = input<EbookItem | null>(null);
  readonly collections = input<OptionItem[]>([]);
  readonly subjects = input<OptionItem[]>([]);
  readonly topics = input<OptionItem[]>([]);
  readonly types = input<OptionItem[]>([]);
  /** Gợi ý tự hoàn thành lấy từ trang đang hiển thị. */
  readonly publisherHints = input<string[]>([]);
  readonly authorHints = input<string[]>([]);
  readonly closed = output<void>();
  readonly saved = output<void>();

  protected readonly toggle = TOGGLE_CLASS;
  protected readonly label = LABEL;
  protected readonly languages = LANGUAGES;
  protected readonly idTypes = ID_TYPES;
  protected readonly months = Array.from({ length: 12 }, (_, i) => i + 1);
  protected readonly form = signal<Form>(blank());
  protected readonly saving = signal(false);
  protected readonly uploadingCover = signal(false);
  protected readonly coverDialog = signal(false);
  protected readonly metaFields = signal<MetadataField[]>([]);
  protected metaRows: MetaRow[] = [];
  protected bibQuery = '';
  protected readonly bibResults = signal<BibLinkOption[]>([]);

  ngOnInit(): void {
    const it = this.item();
    if (!it) return;
    const people = (list: { last: string | null; first: string | null; title?: string | null }[]): PersonRow[] =>
      list.length ? list.map((p) => ({ last: p.last ?? '', first: p.first ?? '', title: p.title ?? '' })) : [{ last: '', first: '', title: '' }];
    this.form.set({
      publicId: it.publicId, published: it.status === STATUS_ACTIVE, allowDownload: it.allowDownload, free: it.free, printCopies: it.printCopies, offlineDays: it.offlineDays,
      subjectId: it.subjectId, topicId: it.topicId, share: it.share ?? 0, collectionId: it.collectionId, title: it.title, otherTitles: split(it.otherTitle),
      publisher: it.publisher ?? '', publishDay: it.publishDay, publishMonth: it.publishMonth, publishYear: it.publishYear ? String(it.publishYear) : (it.publishDate ?? ''),
      originalDate: it.publishDate, originalYear: it.publishYear, authors: people(it.authors), page: it.page ?? '', coverUrl: it.coverUrl, typeId: it.typeId,
      language: it.language ?? 'vi', journalName: it.journalName ?? '', advisors: people(it.advisors), keywords: split(it.keyword),
      identifiers: it.identifiers.length ? it.identifiers.map((n) => ({ type: n.type, value: n.value })) : [{ type: 'issn', value: '' }],
      abstract: it.abstract ?? '', description: it.description ?? '', linkedBibPublicId: it.linkedBibPublicId, linkedBibLabel: it.linkedBibPublicId,
      xml: it.xml, show: it.show, indexContent: it.indexContent, reason: '',
    });
    void this.loadMetadata(it.publicId);
  }

  private async loadMetadata(publicId: string): Promise<void> {
    try {
      this.metaFields.set((await this.api.crud<MetadataField>('metadata-fields', 'digital').searchAll()).filter((x) => x.status === STATUS_ACTIVE));
      if (this.metaFields().length) {
        this.metaRows = (await this.api.ebookMetadata(publicId)).map((r) => ({ fieldPublicId: r.fieldPublicId, value: r.value, language: r.language ?? '' }));
      }
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected addMeta(): void {
    this.metaRows.push({ fieldPublicId: '', value: '', language: '' });
  }

  protected async onCover(event: Event, f: Form): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.uploadingCover.set(true);
    try {
      f.coverUrl = (await this.api.upload('ebook-cover', file)).url;
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.uploadingCover.set(false);
    }
  }

  protected onCoverFromPdf(url: string): void {
    this.form().coverUrl = url;
    this.saved.emit(); // ảnh bìa đã lưu vào tài liệu — danh sách cần tải lại
  }

  protected async searchBib(event?: Event): Promise<void> {
    event?.preventDefault();
    try {
      this.bibResults.set(await this.api.ebookBibOptions(this.bibQuery.trim()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected pickBib(f: Form, b: BibLinkOption): void {
    f.linkedBibPublicId = b.bibPublicId;
    f.linkedBibLabel = b.title;
    if (!f.publicId) {
      if (!f.title.trim()) f.title = b.title;
      if (!f.publisher.trim() && b.publisher) f.publisher = b.publisher;
      if (!f.publishYear.trim() && b.publishYear) f.publishYear = b.publishYear;
      if (b.author && !f.authors.some((a) => a.last.trim() || a.first.trim())) f.authors = [{ last: b.author, first: '', title: '' }];
    }
    this.bibResults.set([]);
  }

  protected unlinkBib(f: Form): void {
    f.linkedBibPublicId = null;
    f.linkedBibLabel = null;
  }

  private body(f: Form): Record<string, unknown> {
    const clean = (list: string[]) => list.map((x) => x.trim()).filter(Boolean);
    const person = (p: PersonRow) => ({ last: p.last.trim() || null, first: p.first.trim() || null, title: p.title.trim() || null });
    const year = f.publishYear.trim();
    const numeric = /^\d{4}$/.test(year) ? Number(year) : null;
    // Năm không đổi so với lúc mở form → giữ nguyên chuỗi ngày gốc (có thể là "05/2010"); đổi hoặc nhập mới → chuỗi năm.
    const keepDate = f.originalDate != null && numeric === f.originalYear && numeric != null;
    return {
      title: f.title.trim(), collectionId: f.collectionId, subjectId: f.subjectId, topicId: f.topicId, typeId: f.typeId,
      publisher: f.publisher.trim() || null, publishDate: keepDate ? f.originalDate : (year || null), publishDay: f.publishDay, publishMonth: f.publishMonth, publishYear: numeric,
      keyword: clean(f.keywords).join('; ') || null, otherTitle: clean(f.otherTitles).join('; ') || null, page: f.page.trim() || null,
      authors: f.authors.map(person).filter((p) => p.last || p.first), advisors: f.advisors.map(person).filter((p) => p.last || p.first),
      identifiers: f.identifiers.map((n) => ({ type: n.type, value: n.value.trim() })).filter((n) => n.value),
      language: f.language || null, journalName: f.journalName.trim() || null, abstract: f.abstract.trim() || null, description: f.description.trim() || null,
      free: f.free, allowDownload: f.allowDownload, printCopies: f.printCopies || null, offlineDays: f.offlineDays || null, share: f.share,
      coverUrl: f.coverUrl, linkedBibPublicId: f.linkedBibPublicId, status: f.published ? STATUS_ACTIVE : STATUS_INACTIVE,
      // Trường không có trên form giữ nguyên giá trị cũ (cập nhật là thay cả bản ghi).
      xml: f.xml, show: f.show, indexContent: f.indexContent,
    };
  }

  protected async save(): Promise<void> {
    const f = this.form();
    if (!f.title.trim()) {
      this.toastr.warning('DIGITAL_ITEMS_PAGE.TITLE_REQUIRED');
      return;
    }
    this.saving.set(true);
    try {
      const body = this.body(f);
      if (f.publicId) {
        await this.client.update(f.publicId, body, f.reason);
        if (this.metaFields().length) {
          await this.api.saveEbookMetadata(f.publicId, this.metaRows.filter((m) => m.fieldPublicId && m.value.trim())
            .map((m) => ({ fieldPublicId: m.fieldPublicId, value: m.value.trim(), language: m.language || null })));
        }
      } else await this.client.add(body);
      this.toastr.success(f.publicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      this.saved.emit();
      this.closed.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }
}
