import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, Bib, BibType, CrudClient, IsbnMatch, MarcField, MarcFieldDef, MarcPreviewRecord, STATUS_ACTIVE, Worksheet, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { MarcEditor } from '../../shared/marc-editor';
import { ToastrService } from '../../shared/toastr';
import { Loading } from '../../shared/ui';
import { BibItems } from '../holdings/bib-items';
import { BibCover } from './bib-cover';
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
  imports: [FormsModule, RouterLink, Loading, MarcEditor, MarcPick, BibItems, BibCover],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <a routerLink="/catalog-bibs" class="icon-btn text-gray-500 hover:bg-gray-100" title="Quay lại"><span class="material-icons">arrow_back</span></a>
      <h4 class="page-title">{{ bib() ? 'Sửa biểu ghi' : 'Biên mục mới' }}</h4>
      @if (bib(); as b) { <span class="text-sm text-gray-500">MFN <b class="font-mono">{{ b.mfn }}</b> · {{ b.title }}</span> }
      @if (canSave()) {
        <button type="button" class="btn-secondary ml-auto" (click)="picking.set(true)"><span class="material-icons text-[18px]">upload_file</span> Nạp từ file MARC</button>
      }
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 relative">
      @if (loading()) { <app-loading /> }
      <div class="px-5 py-4 border-b border-gray-100 grid grid-cols-1 md:grid-cols-4 gap-4">
        <div>
          <label for="be-type" class="form-label">Loại biểu ghi</label>
          <select id="be-type" class="input" [(ngModel)]="bibTypeId" (ngModelChange)="onTypeChange()">
            <option [ngValue]="null">—</option>
            @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
          </select>
        </div>
        <div>
          <label for="be-ws" class="form-label">Biểu mẫu biên mục</label>
          <select id="be-ws" class="input" [ngModel]="worksheetId()" (ngModelChange)="applyWorksheet($event)">
            <option [ngValue]="null">—</option>
            @for (w of worksheets(); track w.id) { <option [ngValue]="w.id">{{ w.name }}</option> }
          </select>
        </div>
        <div>
          <label for="be-status" class="form-label">Hiển thị trên OPAC</label>
          <select id="be-status" class="input" [(ngModel)]="status">
            <option [ngValue]="2">Hiện</option><option [ngValue]="1">Ẩn</option>
          </select>
        </div>
        <div>
          <label for="be-leader" class="form-label">Leader</label>
          <input id="be-leader" class="input font-mono text-[13px]" maxlength="24" [(ngModel)]="leader" placeholder="Tự sinh theo loại biểu ghi" />
        </div>
      </div>

      <div class="p-5 bg-gray-50/50">
        <app-marc-editor [(fields)]="fields" [defs]="defs()" />
      </div>

      @if (dupes().length) {
        <div class="mx-5 mt-4 alert-error !block">
          <div class="font-medium mb-1">ISBN này đã có ở {{ dupes().length }} biểu ghi khác:</div>
          <ul class="list-disc pl-5 text-sm">
            @for (d of dupes(); track d.publicId) { <li><a [routerLink]="['/catalog-bibs', d.mfn]" class="underline">MFN {{ d.mfn }}</a> — {{ d.title }}</li> }
          </ul>
          <button type="button" class="btn-secondary !py-1 !px-3 mt-2" (click)="save(true)">Vẫn lưu biểu ghi mới</button>
        </div>
      }

      @if (bib(); as b) {
        <app-bib-cover [bib]="b" [editable]="session.can('CATALOG_BIBS:edit')" (changed)="bib.set($event)" />
        @if (hasHoldings()) { <app-bib-items [mfn]="b.mfn" /> }
      }

      <div class="flex gap-3 px-5 py-4 border-t border-gray-100">
        <a routerLink="/catalog-bibs" class="btn-secondary">Huỷ</a>
        @if (canSave()) {
          <button type="button" class="btn-primary ml-auto" [disabled]="saving()" (click)="save(false)">
            <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span> Lưu biểu ghi
          </button>
        }
      </div>
    </div>

    @if (picking()) { <app-marc-pick (chosen)="useRecord($event)" (closed)="picking.set(false)" /> }
  `,
})
export class BibEdit implements OnInit {
  /** Tham số route: "new" hoặc MFN. */
  readonly mfn = input<string>();

  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
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
        const first = this.worksheets()[0];
        if (first) this.useWorksheet(first);
        else this.fields.set(structuredClone(BLANK_FIELDS));
      }
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
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
    if (this.hasData() && !confirm('Áp biểu mẫu sẽ thay các trường đang nhập. Tiếp tục?')) {
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
      const saved = current ? await this.client.update(current.publicId, body) : await this.client.add(body);
      this.toastr.success(isNew ? `Đã lưu biểu ghi MFN ${saved.mfn}.` : 'Đã cập nhật biểu ghi.');
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
    this.toastr.success(`Đã nạp ${r.fields.length} trường từ file — kiểm tra rồi bấm Lưu biểu ghi.`);
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
