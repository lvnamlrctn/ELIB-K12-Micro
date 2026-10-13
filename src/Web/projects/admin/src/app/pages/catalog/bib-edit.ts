import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Router, RouterLink } from '@angular/router';
import { Api, Bib, BibType, CrudClient, IsbnMatch, MarcField, MarcFieldDef, MarcPreviewRecord, STATUS_ACTIVE, Worksheet, errorMessage } from '../../core/api';
import { Language } from '../../core/i18n';
import { MarcDrafts } from '../../core/marc-draft';
import { Session } from '../../core/session';
import { MarcEditor } from '../../shared/marc-editor';
import { ToastrService } from '../../shared/toastr';
import { Loading } from '../../shared/ui';
import { BibItems } from '../holdings/bib-items';
import { BibCover } from './bib-cover';
import { BibHistory } from './bib-history';
import { MarcPick } from './marc-tools';

/** Trường khi chưa có biểu mẫu nào (monolith: defaultMarcFields). */
const BLANK_FIELDS: MarcField[] = [
  { tag: '020', ind1: ' ', ind2: ' ', subfields: [{ code: 'a', value: '' }] },
  { tag: '100', ind1: '0', ind2: ' ', subfields: [{ code: 'a', value: '' }] },
  { tag: '245', ind1: '1', ind2: '0', subfields: [{ code: 'a', value: '' }, { code: 'c', value: '' }] },
  { tag: '260', ind1: ' ', ind2: ' ', subfields: [{ code: 'a', value: '' }, { code: 'b', value: '' }, { code: 'c', value: '' }] },
];

/** Biên mục một biểu ghi (monolith: /admin/catalog-bibs/new, /admin/catalog-bibs/edit/:mfn). */
@Component({
  selector: 'app-bib-edit',
  imports: [FormsModule, RouterLink, Loading, MarcEditor, MarcPick, BibItems, BibCover, BibHistory, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <a routerLink="/catalog-bibs" class="icon-btn text-gray-500 hover:bg-gray-100" [title]="'BIB_EDITOR.BACK' | translate"><span class="material-icons">arrow_back</span></a>
      <h4 class="page-title">{{ (bib() ? 'BIB_EDITOR.EDIT_TITLE' : 'BIB_EDITOR.NEW_TITLE') | translate }}</h4>
      @if (bib(); as b) {
        <span class="text-sm text-gray-500">MFN <b class="font-mono">{{ b.mfn }}</b> · {{ b.title }}</span>
        <button type="button" class="btn-secondary !py-1.5" (click)="showHistory.set(true)"><span class="material-icons text-[18px]">history</span> {{ 'BIB_EDITOR.HISTORY' | translate }}</button>
      }
      @if (canSave()) {
        @if (!bib() && hasSearch()) {
          <a routerLink="/z3950-search" class="btn-secondary ml-auto"><span class="material-icons text-[18px]">travel_explore</span> Tra Z39.50</a>
        }
        <button type="button" class="btn-secondary" [class.ml-auto]="bib() || !hasSearch()" (click)="picking.set(true)"><span class="material-icons text-[18px]">upload_file</span> {{ 'BIB_EDITOR.LOAD_MARC' | translate }}</button>
      }
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 relative">
      @if (loading()) { <app-loading /> }
      <div class="px-5 py-4 border-b border-gray-100 grid grid-cols-1 md:grid-cols-4 gap-4">
        <div>
          <label for="be-type" class="form-label">{{ 'BIB_EDITOR.RECORD_TYPE' | translate }}</label>
          <select id="be-type" class="input" [(ngModel)]="bibTypeId" (ngModelChange)="onTypeChange()">
            <option [ngValue]="null">—</option>
            @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
          </select>
        </div>
        <div>
          <label for="be-ws" class="form-label">{{ 'BIB_EDITOR.WORKSHEET' | translate }}</label>
          <select id="be-ws" class="input" [ngModel]="worksheetId()" (ngModelChange)="applyWorksheet($event)">
            <option [ngValue]="null">—</option>
            @for (w of worksheets(); track w.id) { <option [ngValue]="w.id">{{ w.name }}</option> }
          </select>
        </div>
        <div>
          <label for="be-status" class="form-label">{{ 'BIB_EDITOR.OPAC_VISIBLE' | translate }}</label>
          <select id="be-status" class="input" [(ngModel)]="status">
            <option [ngValue]="2">{{ 'BIB_EDITOR.SHOWN' | translate }}</option><option [ngValue]="1">{{ 'BIB_EDITOR.HIDDEN' | translate }}</option>
          </select>
        </div>
        <div>
          <label for="be-leader" class="form-label">Leader</label>
          <input id="be-leader" class="input font-mono text-[13px]" maxlength="24" [(ngModel)]="leader" [placeholder]="'BIB_EDITOR.LEADER_PH' | translate" />
        </div>
      </div>

      <div class="p-5 bg-gray-50/50">
        <app-marc-editor [(fields)]="fields" [defs]="defs()" [suggest]="suggest" />
      </div>

      @if (dupes().length) {
        <div class="mx-5 mt-4 alert-error !block">
          <div class="font-medium mb-1">{{ 'BIB_EDITOR.ISBN_DUPES' | translate: { count: dupes().length } }}</div>
          <ul class="list-disc pl-5 text-sm">
            @for (d of dupes(); track d.publicId) { <li><a [routerLink]="['/catalog-bibs', d.mfn]" class="underline">MFN {{ d.mfn }}</a> — {{ d.title }}</li> }
          </ul>
          <button type="button" class="btn-secondary !py-1 !px-3 mt-2" (click)="save(true)">{{ 'BIB_EDITOR.SAVE_ANYWAY' | translate }}</button>
        </div>
      }

      @if (bib(); as b) {
        <app-bib-cover [bib]="b" [editable]="session.can('CATALOG_BIBS:edit')" (changed)="bib.set($event)" />
        @if (hasHoldings()) { <app-bib-items [mfn]="b.mfn" /> }
      }

      <div class="flex flex-wrap gap-3 px-5 py-4 border-t border-gray-100">
        <a routerLink="/catalog-bibs" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</a>
        @if (bib() && canSave()) {
          <input id="be-reason" class="input !w-auto flex-1 min-w-[12rem]" maxlength="500" [(ngModel)]="reason"
                 [placeholder]="'BIB_EDITOR.REASON_PH' | translate" [attr.aria-label]="'BIB_EDITOR.REASON' | translate" />
        }
        @if (canSave()) {
          <button type="button" class="btn-primary ml-auto" [disabled]="saving()" (click)="save(false)">
            <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span> {{ 'BIB_EDITOR.SAVE' | translate }}
          </button>
        }
      </div>
    </div>

    @if (picking()) { <app-marc-pick (chosen)="useRecord($event)" (closed)="picking.set(false)" /> }
    @if (showHistory() && bib(); as b) { <app-bib-history [publicId]="b.publicId" [defs]="defs()" (closed)="showHistory.set(false)" /> }
  `,
})
export class BibEdit implements OnInit {
  /** Tham số route: "new" hoặc MFN. */
  readonly mfn = input<string>();

  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly language = inject(Language);
  private readonly drafts = inject(MarcDrafts);
  private readonly client: CrudClient<Bib> = this.api.crud<Bib>('bibs', 'catalog');

  protected readonly bib = signal<Bib | null>(null);
  protected readonly types = signal<BibType[]>([]);
  protected readonly worksheets = signal<Worksheet[]>([]);
  protected readonly worksheetId = signal<number | null>(null);
  protected readonly defs = signal<MarcFieldDef[]>([]);
  protected readonly fields = signal<MarcField[]>([]);
  protected readonly dupes = signal<IsbnMatch[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly picking = signal(false);
  protected readonly showHistory = signal(false);
  protected reason = '';
  /** Gợi ý từ điển cho trình soạn MARC. */
  protected readonly suggest = (kind: string, q: string) => this.api.dictionarySuggest(kind, q);
  protected bibTypeId: number | null = null;
  protected status = STATUS_ACTIVE;
  protected leader = '';

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const [types, defs] = await Promise.allSettled([this.api.crud<BibType>('bib-types', 'catalog').searchAll(), this.api.marc21Fields()]);
      if (types.status === 'fulfilled') this.types.set(types.value);
      if (defs.status === 'fulfilled') this.defs.set(defs.value);

      const mfn = Number(this.mfn());
      if (mfn > 0) {
        const bib = await this.api.bibByMfn(mfn);
        this.bib.set(bib);
        this.bibTypeId = bib.bibTypeId;
        this.status = bib.status;
        this.leader = bib.leader;
        this.fields.set(structuredClone(bib.fields));
        this.worksheetId.set(bib.worksheetId);
        await this.loadWorksheets();
      } else {
        // Biên mục mới: mặc định loại "Sách" và biểu mẫu đầu tiên của loại đó.
        this.bibTypeId = this.types().find((t) => t.code === 'SACH')?.id ?? this.types()[0]?.id ?? null;
        await this.loadWorksheets();
        const draft = this.drafts.take();
        const first = this.worksheets()[0];
        if (draft) {
          // Bản ghi sao từ Z39.50: giữ Leader/06–07 của nguồn để loại biểu ghi khớp, MFN/003/005 hệ thống tự sinh khi lưu.
          this.leader = draft.leader.trim().length === 24 ? draft.leader : '';
          this.fields.set(structuredClone(draft.fields));
          this.toastr.success('BIB_EDITOR.DRAFT_LOADED', { source: draft.source });
        } else if (first) this.useWorksheet(first);
        else this.fields.set(structuredClone(BLANK_FIELDS));
      }
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  /** Đơn vị có phân hệ Tra cứu → tra Z39.50 để sao biểu ghi. */
  protected hasSearch(): boolean {
    return (this.session.features()?.modules ?? []).includes('SEARCH');
  }

  /** Đơn vị có phân hệ Quản lý kho → hiện khung đăng ký cá biệt. */
  protected hasHoldings(): boolean {
    return (this.session.features()?.modules ?? []).includes('HOLDINGS');
  }

  protected canSave(): boolean {
    return this.session.can(`CATALOG_BIBS:${this.bib() ? 'edit' : 'add'}`);
  }

  protected async onTypeChange(): Promise<void> {
    await this.loadWorksheets();
    this.leader = ''; // sinh lại Leader/06–07 theo loại mới
    if (!this.bib() && !this.hasData() && this.worksheets()[0]) this.useWorksheet(this.worksheets()[0]);
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

  protected async save(force: boolean): Promise<void> {
    const isNew = !this.bib();
    this.saving.set(true);
    try {
      if (isNew && !force) {
        const isbn = this.fields().find((f) => f.tag === '020')?.subfields?.find((s) => s.code === 'a' && s.value.trim())?.value;
        const dupes = isbn ? await this.api.checkIsbn(isbn) : [];
        this.dupes.set(dupes);
        if (dupes.length) return;
      }
      this.dupes.set([]);
      const body = { bibTypeId: this.bibTypeId, worksheetId: this.worksheetId(), leader: this.leader.trim() || null, fields: this.fields(), status: this.status };
      const current = this.bib();
      const saved = current ? await this.client.update(current.publicId, body, this.reason.trim() || null) : await this.client.add(body);
      this.reason = '';
      if (isNew) this.toastr.success('BIB_EDITOR.SAVED_NEW', { mfn: saved.mfn });
      else this.toastr.success('BIB_EDITOR.UPDATED');
      if (isNew) {
        await this.router.navigate(['/catalog-bibs', saved.mfn], { replaceUrl: true });
      }
      this.bib.set(saved);
      this.leader = saved.leader;
      this.fields.set(structuredClone(saved.fields));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  /** Nạp biểu ghi đọc từ file: thay toàn bộ trường, Leader và loại biểu ghi (nếu nhận ra theo Leader). */
  protected async useRecord(r: MarcPreviewRecord): Promise<void> {
    this.picking.set(false);
    if (r.bibTypeId != null && r.bibTypeId !== this.bibTypeId) {
      this.bibTypeId = r.bibTypeId;
      await this.loadWorksheets();
    }
    this.worksheetId.set(null);
    this.leader = r.leader;
    this.fields.set(structuredClone(r.fields));
    this.dupes.set([]);
    this.toastr.success('BIB_EDITOR.FIELDS_LOADED', { count: r.fields.length });
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
      this.worksheets.set([]); // thiếu quyền xem biểu mẫu — vẫn biên mục được bằng tay
    }
  }
}
