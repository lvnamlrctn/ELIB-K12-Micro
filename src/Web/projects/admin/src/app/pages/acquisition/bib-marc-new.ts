import { Component, OnInit, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { AcqBib, Api, BibType, IsbnMatch, MarcField, MarcFieldDef, STATUS_ACTIVE, STATUS_INACTIVE, Worksheet, errorMessage } from '../../core/api';
import { Language } from '../../core/i18n';
import { Session } from '../../core/session';
import { BLANK_FIELDS, MarcEditor } from '../../shared/marc-editor';
import { ToastrService } from '../../shared/toastr';
import { Loading } from '../../shared/ui';
import { BibCover, firstIsbn } from '../catalog/bib-cover';
import { createCatalogBib, isbnDupes, ownFields } from './bib-source';

/**
 * "Thêm sách lẻ" ở phiếu nhận (ELIB-LRC): biên mục MARC đầy đủ ngay trong hộp thoại — loại biểu ghi, biểu mẫu, hiện trên OPAC, Leader và
 * thông tin dòng (nơi gọi chiếu vào qua ng-content), ảnh bìa ở trên, trình soạn MARC ở dưới. `submit()` kiểm trùng ISBN, tạo biểu ghi ở
 * catalog, gắn ảnh bìa đã chọn rồi phát `created`.
 */
@Component({
  selector: 'app-acq-bib-marc-new',
  imports: [FormsModule, TranslatePipe, MarcEditor, BibCover, Loading],
  template: `
    <div class="relative">
      @if (loading()) { <app-loading /> }
      <div class="grid grid-cols-2 md:grid-cols-4 gap-3 mb-4">
        <div><label for="bm-type" class="form-label">{{ 'BIB_EDITOR.RECORD_TYPE' | translate }}</label>
          <select id="bm-type" class="input" [(ngModel)]="bibTypeId" (ngModelChange)="onTypeChange()">
            <option [ngValue]="null">—</option>
            @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
          </select></div>
        <div><label for="bm-ws" class="form-label">{{ 'BIB_EDITOR.WORKSHEET' | translate }}</label>
          <select id="bm-ws" class="input" [ngModel]="worksheetId()" (ngModelChange)="applyWorksheet($event)">
            <option [ngValue]="null">—</option>
            @for (w of worksheets(); track w.id) { <option [ngValue]="w.id">{{ w.name }}</option> }
          </select></div>
        <div><label for="bm-status" class="form-label">{{ 'BIB_EDITOR.OPAC_VISIBLE' | translate }}</label>
          <select id="bm-status" class="input" [(ngModel)]="status">
            <option [ngValue]="shown">{{ 'BIB_EDITOR.SHOWN' | translate }}</option><option [ngValue]="hidden">{{ 'BIB_EDITOR.HIDDEN' | translate }}</option>
          </select></div>
        <div><label for="bm-leader" class="form-label">Leader</label>
          <input id="bm-leader" class="input font-mono text-[13px]" maxlength="24" [(ngModel)]="leader" [placeholder]="'BIB_EDITOR.LEADER_PH' | translate" /></div>
        <ng-content />
      </div>

      @if (session.can('CATALOG_BIBS:edit')) {
        <app-bib-cover class="block mb-4" [isbnHint]="isbn()" [editable]="true" boxClass="rounded-lg border border-gray-200 px-4 py-3" (pending)="cover = $event" />
      }

      @if (dupes().length) {
        <div class="alert-error !block mb-4">
          <div class="font-medium mb-1">{{ 'BIB_EDITOR.ISBN_DUPES' | translate: { count: dupes().length } }}</div>
          <ul class="list-disc pl-5 text-sm">
            @for (d of dupes(); track d.publicId) {
              <li><button type="button" class="underline" (click)="useExisting(d)">MFN {{ d.mfn }}</button> — {{ d.title }}</li>
            }
          </ul>
          <div class="text-[12px] mt-1">{{ 'ACQ_RECEIPT.DUP_USE_HINT' | translate }}</div>
          <button type="button" class="btn-secondary !py-1 !px-3 mt-2" [disabled]="busy()" (click)="submit(true)">{{ 'BIB_EDITOR.SAVE_ANYWAY' | translate }}</button>
        </div>
      }

      <div class="rounded-lg bg-gray-50/50 border border-gray-100 p-3">
        <app-marc-editor [(fields)]="fields" [defs]="defs()" [suggest]="suggest" />
      </div>
    </div>
  `,
})
export class BibMarcNew implements OnInit {
  readonly created = output<AcqBib>();
  readonly busy = signal(false);

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly language = inject(Language);
  protected readonly session = inject(Session);

  protected readonly shown = STATUS_ACTIVE;
  protected readonly hidden = STATUS_INACTIVE;
  protected readonly loading = signal(false);
  protected readonly types = signal<BibType[]>([]);
  protected readonly worksheets = signal<Worksheet[]>([]);
  protected readonly worksheetId = signal<number | null>(null);
  protected readonly defs = signal<MarcFieldDef[]>([]);
  protected readonly fields = signal<MarcField[]>(structuredClone(BLANK_FIELDS));
  protected readonly dupes = signal<IsbnMatch[]>([]);
  protected readonly suggest = (kind: string, q: string) => this.api.dictionarySuggest(kind, q);
  protected bibTypeId: number | null = null;
  /** Biểu ghi nhập từ phiếu nhận mặc định ẩn trên OPAC tới khi có bản xếp giá (như các nguồn tạo mới khác). */
  protected status = STATUS_INACTIVE;
  protected leader = '';
  /** Ảnh bìa chọn trước, gắn vào biểu ghi ngay sau khi tạo. */
  protected cover: string | null = null;
  private baseline: string | null = null;

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const [types, defs] = await Promise.allSettled([this.api.crud<BibType>('bib-types', 'catalog').searchAll(), this.api.marc21Fields()]);
      if (types.status === 'fulfilled') this.types.set(types.value);
      if (defs.status === 'fulfilled') this.defs.set(defs.value);
      this.bibTypeId = this.types().find((t) => t.code === 'SACH')?.id ?? this.types()[0]?.id ?? null;
      await this.loadWorksheets();
      const first = this.worksheets()[0];
      if (first) this.useWorksheet(first);
    } finally {
      this.baseline = this.snapshot();
      this.loading.set(false);
    }
  }

  /** Đã soạn gì so với lúc mở (đóng hộp thoại thì hỏi lại như ELIB-LRC). */
  isDirty(): boolean {
    return this.baseline !== null && (this.snapshot() !== this.baseline || this.cover !== null);
  }

  private snapshot(): string {
    return JSON.stringify([this.bibTypeId, this.worksheetId(), this.leader.trim(), this.status, this.fields()]);
  }

  protected isbn(): string | null {
    return firstIsbn(this.fields());
  }

  protected async onTypeChange(): Promise<void> {
    await this.loadWorksheets();
    this.leader = '';
    if (!this.hasData() && this.worksheets()[0]) this.useWorksheet(this.worksheets()[0]);
  }

  protected applyWorksheet(id: number | null): void {
    const sheet = this.worksheets().find((w) => w.id === id);
    if (!sheet) {
      this.worksheetId.set(null);
      return;
    }
    if (this.hasData() && !confirm(this.language.t('BIB_EDITOR.APPLY_WORKSHEET_CONFIRM'))) {
      this.worksheetId.set(this.worksheetId());
      return;
    }
    this.useWorksheet(sheet);
  }

  /** "Thêm mới": tạo biểu ghi (trùng ISBN thì hỏi lại trừ khi force), gắn ảnh bìa, phát `created`. */
  async submit(force = false): Promise<void> {
    if (this.busy()) return;
    const fields = ownFields(this.fields());
    const title = fields.find((f) => f.tag === '245')?.subfields?.find((s) => s.code === 'a')?.value.trim();
    if (!title) {
      this.toastr.warning('ACQ_SOURCE.TITLE_REQUIRED');
      return;
    }
    this.busy.set(true);
    try {
      if (!force) {
        const dupes = await isbnDupes(this.api, fields);
        this.dupes.set(dupes);
        if (dupes.length) return;
      }
      this.dupes.set([]);
      const bib = await createCatalogBib(this.api, this.leader, fields, this.bibTypeId, { worksheetId: this.worksheetId(), status: this.status });
      this.toastr.success('ACQ_SOURCE.CREATED', { mfn: bib.mfn });
      this.baseline = null; // biểu ghi đã lưu ở catalog — đóng hộp thoại không cần hỏi
      if (this.cover) {
        try {
          await this.api.setBibCover(bib.bibPublicId, this.cover);
        } catch (e) {
          this.toastr.error(errorMessage(e)); // biểu ghi đã tạo — gắn lại ảnh ở màn biên mục
        }
      }
      this.created.emit(bib);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Dùng biểu ghi trùng ISBN đã có thay vì tạo bản mới. */
  protected useExisting(d: IsbnMatch): void {
    this.dupes.set([]);
    this.baseline = null;
    this.created.emit({ bibPublicId: d.publicId, mfn: d.mfn, title: d.title, author: null, publisher: null, publishYear: null, isbns: null });
  }

  private hasData(): boolean {
    return this.fields().some((f) => (f.value ?? '').trim() || (f.subfields ?? []).some((s) => s.value.trim()));
  }

  private useWorksheet(sheet: Worksheet): void {
    this.worksheetId.set(sheet.id);
    this.fields.set(structuredClone(sheet.fields));
  }

  private async loadWorksheets(): Promise<void> {
    try {
      this.worksheets.set(this.bibTypeId == null ? [] : await this.api.worksheetsByBibType(this.bibTypeId));
    } catch {
      this.worksheets.set([]);
    }
  }
}
