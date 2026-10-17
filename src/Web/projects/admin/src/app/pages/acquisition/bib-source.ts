import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import {
  AcqBib, Api, Bib, STATUS_INACTIVE, BibType, IsbnMatch, MarcField, MarcPreviewRecord, Z3950ServerOption, Z3950ServerResult, errorMessage,
} from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { BibPicker } from './bib-picker';

export type Source = 'existing' | 'new' | 'file' | 'paste' | 'z3950';

/** Trường MARC của nguồn bỏ 001/003/005 — catalog tự sinh MFN, mã đơn vị, thời điểm (như khi sao từ Z39.50 sang màn biên mục). */
export function ownFields(fields: MarcField[]): MarcField[] {
  return fields.filter((f) => !['001', '003', '005'].includes(f.tag));
}

/** Biểu ghi ở catalog có cùng ISBN (020$a đầu tiên) — hỏi lại trước khi tạo bản mới. */
export async function isbnDupes(api: Api, fields: MarcField[]): Promise<IsbnMatch[]> {
  const isbn = fields.find((f) => f.tag === '020')?.subfields?.find((s) => s.code === 'a' && s.value.trim())?.value;
  return isbn ? api.checkIsbn(isbn) : [];
}

/** Biểu ghi catalog → biểu ghi chọn cho dòng đơn. */
export function toAcqBib(bib: Bib): AcqBib {
  return {
    bibPublicId: bib.publicId, mfn: bib.mfn, title: bib.title, author: bib.author, publisher: bib.publisher, publishYear: bib.publishYear,
    isbns: bib.isbns.length ? bib.isbns.join(', ') : null,
  };
}

/** Tạo biểu ghi ở catalog (mặc định ẩn trên OPAC tới khi biên mục viên bật) và trả về như biểu ghi chọn cho dòng đơn. */
export async function createCatalogBib(api: Api, leader: string | null, fields: MarcField[], bibTypeId: number | null,
                                       extra: { worksheetId?: number | null; status?: number } = {}): Promise<AcqBib> {
  return toAcqBib(await api.crud<Bib>('bibs', 'catalog').add({
    bibTypeId, worksheetId: extra.worksheetId ?? null, leader: leader?.trim() || null, fields, status: extra.status ?? STATUS_INACTIVE,
  }));
}

const SOURCES: { code: Source; label: string; icon: string }[] = [
  { code: 'existing', label: 'ACQ_SOURCE.EXISTING', icon: 'search' },
  { code: 'new', label: 'ACQ_SOURCE.NEW', icon: 'add_circle' },
  { code: 'file', label: 'ACQ_SOURCE.FILE', icon: 'upload_file' },
  { code: 'paste', label: 'ACQ_SOURCE.PASTE', icon: 'content_paste' },
  { code: 'z3950', label: 'ACQ_SOURCE.Z3950', icon: 'travel_explore' },
];

/** Trường MARC21 tối thiểu của biểu ghi nhập nhanh: 020 ISBN, 100 tác giả, 245 nhan đề, 260 xuất bản. */
function quickFields(f: { title: string; author: string; publisher: string; year: string; isbn: string }): MarcField[] {
  const sub = (code: string, value: string) => ({ code, value: value.trim() });
  const fields: MarcField[] = [];
  if (f.isbn.trim()) fields.push({ tag: '020', ind1: ' ', ind2: ' ', subfields: [sub('a', f.isbn)] });
  if (f.author.trim()) fields.push({ tag: '100', ind1: '1', ind2: ' ', subfields: [sub('a', f.author)] });
  fields.push({ tag: '245', ind1: f.author.trim() ? '1' : '0', ind2: '0', subfields: [sub('a', f.title)] });
  const pub = [f.publisher.trim() ? sub('b', f.publisher) : null, f.year.trim() ? sub('c', f.year) : null].filter((x) => x !== null);
  if (pub.length) fields.push({ tag: '260', ind1: ' ', ind2: ' ', subfields: pub });
  return fields;
}

/**
 * Nguồn biểu ghi cho dòng phiếu nhận (ELIB-LRC: chọn biểu ghi có sẵn / tạo mới / nhập file MARC / dán MARC / Z39.50). Biểu ghi mới luôn
 * được tạo ở catalog (cùng API trình biên mục — trùng ISBN thì hỏi lại), rồi trả về như biểu ghi đã chọn; phiếu nhận chỉ giữ MFN.
 * Biểu ghi mới <b>ẩn trên OPAC</b> (chưa có bản nào xếp giá) — biên mục viên bật hiện trong màn biên mục khi sách sẵn sàng.
 * Các nguồn tạo mới cần quyền CATALOG_BIBS:add; thiếu quyền thì chỉ còn "có sẵn".
 */
@Component({
  selector: 'app-acq-bib-source',
  imports: [FormsModule, TranslatePipe, BibPicker],
  template: `
    @if (selected(); as b) {
      <app-acq-bib-picker [inputId]="inputId()" [selected]="b" (picked)="picked.emit($event)" />
    } @else {
      @if (canCreate() && sources().length > 1) {
        <div class="flex flex-wrap gap-1.5 mb-2" role="group">
          @for (s of sources(); track s.code) {
            <button type="button" class="px-2.5 py-1 rounded-lg border text-[13px] flex items-center gap-1"
                    [class]="source() === s.code ? 'bg-blue-600 border-blue-600 text-white' : 'border-gray-300 text-gray-700 hover:bg-gray-50'" (click)="open(s.code)">
              <span class="material-icons text-[16px]">{{ s.icon }}</span> {{ s.label | translate }}</button>
          }
        </div>
      }
      @switch (source()) {
        @case ('existing') { <app-acq-bib-picker [inputId]="inputId()" [selected]="null" (picked)="picked.emit($event)" /> }
        @case ('new') {
          <div class="grid grid-cols-2 gap-2 rounded-lg border border-gray-200 p-3">
            <div class="col-span-2"><label for="bs-title" class="form-label">{{ 'ITEMS_PAGE.TITLE_COL' | translate }} *</label><input id="bs-title" class="input" maxlength="500" [(ngModel)]="form.title" /></div>
            <div><label for="bs-au" class="form-label">{{ 'ITEMS_PAGE.AUTHOR' | translate }}</label><input id="bs-au" class="input" maxlength="300" [(ngModel)]="form.author" /></div>
            <div><label for="bs-isbn" class="form-label">ISBN</label><input id="bs-isbn" class="input" maxlength="20" [(ngModel)]="form.isbn" /></div>
            <div><label for="bs-pub" class="form-label">{{ 'ACQ.PUBLISHER' | translate }}</label><input id="bs-pub" class="input" maxlength="300" [(ngModel)]="form.publisher" /></div>
            <div><label for="bs-year" class="form-label">{{ 'ACQ.YEAR' | translate }}</label><input id="bs-year" class="input" maxlength="4" [(ngModel)]="form.year" /></div>
            @if (types().length) {
              <div class="col-span-2"><label for="bs-type" class="form-label">{{ 'ACQ_SOURCE.BIB_TYPE' | translate }}</label>
                <select id="bs-type" class="input" [(ngModel)]="bibTypeId">@for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
            }
            <div class="col-span-2 flex justify-end"><button type="button" class="btn-primary" [disabled]="busy()" (click)="createQuick()">{{ 'ACQ_SOURCE.CREATE' | translate }}</button></div>
          </div>
        }
        @case ('file') {
          <div class="rounded-lg border border-gray-200 p-3">
            <input type="file" class="input" accept=".mrc,.xml,.txt,.marc" (change)="onFile($event)" />
            <p class="text-[11px] text-gray-400 mt-1">{{ 'ACQ_SOURCE.FILE_HINT' | translate }}</p>
          </div>
        }
        @case ('paste') {
          <div class="rounded-lg border border-gray-200 p-3">
            <textarea rows="6" class="input font-mono text-[12px]" [(ngModel)]="pasted" [placeholder]="'ACQ_SOURCE.PASTE_PH' | translate"></textarea>
            <div class="flex justify-end mt-2"><button type="button" class="btn-secondary" [disabled]="busy() || !pasted.trim()" (click)="previewPasted()">{{ 'ACQ_SOURCE.READ' | translate }}</button></div>
          </div>
        }
        @case ('z3950') {
          <div class="rounded-lg border border-gray-200 p-3 grid grid-cols-2 gap-2">
            <div class="col-span-2"><label for="bs-zs" class="form-label">{{ 'ACQ_SOURCE.Z_SERVER' | translate }}</label>
              <select id="bs-zs" class="input" [(ngModel)]="serverId">@for (s of servers(); track s.publicId) { <option [ngValue]="s.publicId">{{ s.name }}</option> }</select></div>
            <div><label for="bs-zt" class="form-label">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</label><input id="bs-zt" class="input" [(ngModel)]="z.title" (keydown.enter)="searchZ()" /></div>
            <div><label for="bs-zi" class="form-label">ISBN</label><input id="bs-zi" class="input" [(ngModel)]="z.isbn" (keydown.enter)="searchZ()" /></div>
            <div class="col-span-2 flex justify-end"><button type="button" class="btn-secondary" [disabled]="busy() || !serverId" (click)="searchZ()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button></div>
            @if (zResult(); as r) {
              <div class="col-span-2">
                @if (r.error) { <p class="text-sm text-red-600">{{ r.error }}</p> }
                @for (h of r.hits; track h.position) {
                  <button type="button" class="block w-full text-left px-3 py-2 text-sm hover:bg-gray-50 border-b border-gray-100" [disabled]="busy()" (click)="createFrom(h.record.leader, h.record.fields)">
                    {{ h.title }} <span class="text-[12px] text-gray-500">@if (h.author) { — {{ h.author }} }@if (h.year) { · {{ h.year }} }@if (h.isbn) { · ISBN {{ h.isbn }} }</span></button>
                } @empty { <p class="text-sm text-gray-500">{{ 'ACQ.BIB_NONE' | translate }}</p> }
              </div>
            }
          </div>
        }
      }
      @if (records().length) {
        <div class="mt-2 max-h-60 overflow-y-auto rounded-lg border border-gray-200 bg-white">
          @for (r of records(); track r.record) {
            <button type="button" class="block w-full text-left px-3 py-2 text-sm hover:bg-gray-50 border-b border-gray-100 last:border-0 disabled:opacity-50" [disabled]="!!r.error || busy() || used().has(r.record)"
                    (click)="createFrom(r.leader, r.fields, r.bibTypeId, r.record)">
              <span class="font-mono text-[12px] text-gray-500">#{{ r.record }}</span> {{ r.title ?? ('ACQ_SOURCE.NO_TITLE' | translate) }}
              @if (r.error) { <span class="text-[12px] text-red-600"> — {{ r.error }}</span> }
              @if (used().has(r.record)) { <span class="text-[12px] text-green-600"> — {{ 'ACQ_SOURCE.USED' | translate }}</span> }
            </button>
          }
        </div>
      }
      @if (dupes().length) {
        <div class="mt-2 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-sm">
          <div class="font-medium text-amber-800 mb-1">{{ 'ACQ_SOURCE.DUPLICATE_ISBN' | translate }}</div>
          @for (d of dupes(); track d.publicId) {
            <button type="button" class="block text-left text-blue-700 hover:underline" (click)="useExisting(d)">MFN {{ d.mfn }} — {{ d.title }}</button>
          }
          <div class="mt-2 flex gap-2">
            <button type="button" class="btn-secondary !py-1 !px-3" (click)="cancelDuplicate()">{{ 'COMMON.CANCEL' | translate }}</button>
            <button type="button" class="btn-primary !py-1 !px-3" [disabled]="busy()" (click)="createAnyway()">{{ 'ACQ_SOURCE.CREATE_ANYWAY' | translate }}</button>
          </div>
        </div>
      }
    }
  `,
})
export class BibSource implements OnInit {
  readonly inputId = input('acq-bib');
  readonly selected = input<AcqBib | null>(null);
  /** Chỉ các nguồn này (nút riêng kiểu ELIB-LRC: "Thêm sách lẻ", "Thêm sách từ Z3950", "Thêm sách từ file Marc"); null = mọi nguồn. */
  readonly only = input<Source[] | null>(null);
  readonly picked = output<AcqBib | null>();

  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected readonly sources = computed(() => {
    const only = this.only();
    return only ? SOURCES.filter((s) => only.includes(s.code)) : SOURCES;
  });
  protected readonly canCreate = computed(() => this.session.can('CATALOG_BIBS:add'));
  protected readonly source = signal<Source>('existing');
  protected readonly types = signal<BibType[]>([]);
  protected readonly servers = signal<Z3950ServerOption[]>([]);
  protected readonly zResult = signal<Z3950ServerResult | null>(null);
  protected readonly records = signal<MarcPreviewRecord[]>([]);
  protected readonly dupes = signal<IsbnMatch[]>([]);
  protected readonly busy = signal(false);
  /** Số thứ tự các biểu ghi trong file đã được tạo — chọn tiếp biểu ghi khác mà không phải nạp lại file. */
  protected readonly used = signal<ReadonlySet<number>>(new Set());
  protected form = { title: '', author: '', publisher: '', year: '', isbn: '' };
  protected z = { title: '', isbn: '' };
  protected bibTypeId: number | null = null;
  protected serverId: string | null = null;
  protected pasted = '';
  /** Biểu ghi chờ xác nhận khi ISBN đã có trong catalog. */
  private pending: { leader: string | null; fields: MarcField[]; bibTypeId: number | null; record: number | null } | null = null;
  private typesLoaded = false;

  ngOnInit(): void {
    const first = this.only()?.[0];
    if (first) this.open(first);
  }

  protected open(source: Source): void {
    this.source.set(source);
    this.records.set([]);
    this.cancelDuplicate();
    if (source !== 'existing' && !this.typesLoaded) {
      this.typesLoaded = true;
      this.api.crud<BibType>('bib-types', 'catalog').searchAll().then((t) => {
        this.types.set(t);
        this.bibTypeId ??= t.find((x) => x.code === 'SACH')?.id ?? t[0]?.id ?? null;
      }, () => undefined);
    }
    if (source === 'z3950' && !this.servers().length) {
      this.api.z3950Servers().then((s) => {
        this.servers.set(s);
        this.serverId ??= s[0]?.publicId ?? null;
      }, (e) => this.toastr.error(errorMessage(e)));
    }
  }

  protected createQuick(): void {
    if (!this.form.title.trim()) {
      this.toastr.warning('ACQ_SOURCE.TITLE_REQUIRED');
      return;
    }
    void this.createFrom(null, quickFields(this.form), this.bibTypeId);
  }

  protected async onFile(e: Event): Promise<void> {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file) await this.preview(file);
  }

  protected previewPasted(): Promise<void> {
    return this.preview(new File([this.pasted], 'paste.txt', { type: 'text/plain' }));
  }

  private async preview(file: File): Promise<void> {
    this.busy.set(true);
    try {
      const p = await this.api.previewMarc(file);
      this.records.set(p.records);
      this.used.set(new Set());
      if (!p.records.length) this.toastr.warning('ACQ_SOURCE.NO_RECORDS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async searchZ(): Promise<void> {
    if (!this.serverId) return;
    this.busy.set(true);
    try {
      const [r] = await this.api.z3950Search({ title: this.z.title.trim() || null, isbn: this.z.isbn.trim() || null, serverIds: [this.serverId], page: 1, pageSize: 10 });
      this.zResult.set(r ?? null);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Tạo biểu ghi ở catalog từ các trường MARC; ISBN đã có → hỏi dùng bản có sẵn hay vẫn tạo. */
  protected async createFrom(leader: string | null, fields: MarcField[], bibTypeId: number | null = this.bibTypeId, record: number | null = null): Promise<void> {
    const own = ownFields(fields);
    this.pending = { leader, fields: own, bibTypeId: bibTypeId ?? this.bibTypeId, record };
    this.busy.set(true);
    try {
      const dupes = await isbnDupes(this.api, own);
      this.dupes.set(dupes);
      if (!dupes.length) await this.save();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async createAnyway(): Promise<void> {
    this.busy.set(true);
    try {
      await this.save();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async save(): Promise<void> {
    const p = this.pending;
    if (!p) return;
    const bib = await createCatalogBib(this.api, p.leader, p.fields, p.bibTypeId);
    this.pending = null;
    this.dupes.set([]);
    if (p.record !== null) this.used.update((u) => new Set(u).add(p.record!));
    this.toastr.success('ACQ_SOURCE.CREATED', { mfn: bib.mfn });
    this.picked.emit(bib);
  }

  protected cancelDuplicate(): void {
    this.dupes.set([]);
    this.pending = null;
  }

  protected useExisting(d: IsbnMatch): void {
    this.cancelDuplicate();
    this.picked.emit({ bibPublicId: d.publicId, mfn: d.mfn, title: d.title, author: null, publisher: null, publishYear: null, isbns: null });
  }
}
