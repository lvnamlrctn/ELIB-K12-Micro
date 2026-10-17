import { Component, OnInit, computed, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { AcqBib, AcqBibRow, AcqBibSearch, Api, CrudPage, IsbnMatch, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { createCatalogBib, isbnDupes, ownFields } from './bib-source';

const PAGE_SIZE = 10;

function emptySearch(): AcqBibSearch {
  return { mfnFrom: null, mfnTo: null, publishYear: null, publisher: null, title: null, author: null, bibTypeId: null, pageIndex: 1, pageSize: PAGE_SIZE };
}

/**
 * "Tra trùng" như ELIB-LRC (ab-receipt-detail: dialog chọn biểu ghi có sẵn): tìm theo MFN từ–đến, năm XB, NXB, nhan đề, tác giả, loại tài
 * liệu (không phân biệt dấu), 10 dòng mỗi trang, cờ "Đã có đơn nhận / Đã có đơn đặt". "Chọn" dùng biểu ghi đó; "Copy" tạo biểu ghi mới ở
 * catalog bằng MARC của biểu ghi đó (kiểm trùng ISBN như các nguồn tạo mới). Tìm trên bản sao biểu ghi của acquisition — không cần quyền catalog.
 */
@Component({
  selector: 'app-acq-bib-lookup',
  imports: [FormsModule, TranslatePipe],
  template: `
    <form class="grid grid-cols-2 md:grid-cols-4 gap-3" (ngSubmit)="search(1)">
      <div><label for="dl-mf" class="form-label">{{ 'ACQ_DUP.MFN_FROM' | translate }}</label><input id="dl-mf" name="mf" type="number" min="1" class="input" [(ngModel)]="q.mfnFrom" /></div>
      <div><label for="dl-mt" class="form-label">{{ 'ACQ_DUP.TO' | translate }}</label><input id="dl-mt" name="mt" type="number" min="1" class="input" [(ngModel)]="q.mfnTo" /></div>
      <div><label for="dl-yr" class="form-label">{{ 'ACQ_DUP.YEAR' | translate }}</label><input id="dl-yr" name="yr" class="input" maxlength="4" [(ngModel)]="q.publishYear" /></div>
      <div><label for="dl-pub" class="form-label">{{ 'ACQ_DUP.PUBLISHER' | translate }}</label><input id="dl-pub" name="pub" class="input" [(ngModel)]="q.publisher" /></div>
      <div class="col-span-2"><label for="dl-ti" class="form-label">{{ 'ACQ_DUP.TITLE' | translate }}</label><input id="dl-ti" name="ti" class="input" [(ngModel)]="q.title" /></div>
      <div><label for="dl-au" class="form-label">{{ 'ACQ_DUP.AUTHOR' | translate }}</label><input id="dl-au" name="au" class="input" [(ngModel)]="q.author" /></div>
      <div><label for="dl-ty" class="form-label">{{ 'ACQ_DUP.BIB_TYPE' | translate }}</label>
        <select id="dl-ty" name="ty" class="input" [(ngModel)]="q.bibTypeId"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
      <div class="col-span-2 md:col-span-4 flex justify-end gap-2">
        <button type="button" class="btn-secondary" (click)="reset()"><span class="material-icons text-[18px]">restart_alt</span> {{ 'ACQ_DUP.NEW_QUERY' | translate }}</button>
        <button type="submit" class="btn-primary" [disabled]="busy()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </form>

    @if (dupes().length) {
      <div class="mt-3 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-sm">
        <div class="font-medium text-amber-800 mb-1">{{ 'ACQ_SOURCE.DUPLICATE_ISBN' | translate }}</div>
        @for (d of dupes(); track d.publicId) { <div class="text-gray-700">MFN {{ d.mfn }} — {{ d.title }}</div> }
        <div class="mt-2 flex gap-2">
          <button type="button" class="btn-secondary !py-1 !px-3" (click)="cancelCopy()">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary !py-1 !px-3" [disabled]="busy()" (click)="copyAnyway()">{{ 'ACQ_DUP.SAVE_ANYWAY' | translate }}</button>
        </div>
      </div>
    }

    <div class="mt-4 border border-gray-100 rounded-lg overflow-x-auto">
      <table class="w-full text-left border-collapse min-w-[860px]">
        <thead><tr>
          <th class="th w-20">MFN</th><th class="th">{{ 'ACQ_DUP.TITLE' | translate }}</th><th class="th w-44">{{ 'ACQ_DUP.AUTHOR' | translate }}</th>
          <th class="th w-44">{{ 'ACQ_DUP.PUBLISHER' | translate }}</th><th class="th w-20">{{ 'ACQ_DUP.YEAR_SHORT' | translate }}</th>
          <th class="th w-40">{{ 'ACQ_DUP.USED' | translate }}</th><th class="th w-40"></th>
        </tr></thead>
        <tbody>
          @for (b of page()?.items ?? []; track b.bibPublicId) {
            <tr class="hover:bg-gray-50/50">
              <td class="td font-mono text-[13px]">{{ b.mfn }}</td>
              <td class="td">{{ b.title }}@if (b.bibTypeName) { <div class="text-[11px] text-gray-400">{{ b.bibTypeName }}@if (b.isbns) { · ISBN {{ b.isbns }} }</div> }</td>
              <td class="td">{{ b.author ?? '' }}</td>
              <td class="td">{{ b.publisher ?? '' }}</td>
              <td class="td">{{ b.publishYear ?? '' }}</td>
              <td class="td"><div class="flex flex-wrap gap-1">
                @if (b.hasReceipt) { <span class="badge-on !text-[11px]">{{ 'ACQ_DUP.HAS_RECEIPT' | translate }}</span> }
                @if (b.hasOrder) { <span class="badge-info !text-[11px]">{{ 'ACQ_DUP.HAS_ORDER' | translate }}</span> }
              </div></td>
              <td class="td"><div class="flex gap-1 justify-end">
                <button type="button" class="btn-primary !py-1 !px-2" [disabled]="busy()" (click)="choose(b)">{{ 'ACQ_DUP.CHOOSE' | translate }}</button>
                @if (canCopy()) {
                  <button type="button" class="btn-secondary !py-1 !px-2" [disabled]="busy()" [title]="'ACQ_DUP.COPY_TIP' | translate" (click)="copy(b)">{{ 'ACQ_DUP.COPY' | translate }}</button>
                }
              </div></td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="py-8 text-center text-gray-500 text-sm">{{ (busy() ? 'COMMON.LOADING' : 'COMMON.NO_DATA') | translate }}</td></tr>
          }
        </tbody>
      </table>
    </div>
    @if (page(); as p) {
      @if (p.totalCount > p.pageSize) {
        <div class="flex items-center justify-end gap-2 mt-2 text-sm text-gray-600">
          <span>{{ range() }} / {{ p.totalCount }}</span>
          <button type="button" class="btn-secondary !py-1 !px-2" [disabled]="busy() || p.pageIndex <= 1" (click)="search(p.pageIndex - 1)">{{ 'ACQ_DUP.PREV' | translate }}</button>
          <button type="button" class="btn-secondary !py-1 !px-2" [disabled]="busy() || p.pageIndex * p.pageSize >= p.totalCount" (click)="search(p.pageIndex + 1)">{{ 'ACQ_DUP.NEXT' | translate }}</button>
        </div>
      }
    }
  `,
})
export class BibLookupDialog implements OnInit {
  readonly picked = output<AcqBib>();

  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected q = emptySearch();
  protected readonly page = signal<CrudPage<AcqBibRow> | null>(null);
  protected readonly types = signal<{ id: number; name: string }[]>([]);
  protected readonly dupes = signal<IsbnMatch[]>([]);
  protected readonly busy = signal(false);
  /** Copy cần đọc MARC (CATALOG_BIBS:view) và tạo biểu ghi (CATALOG_BIBS:add) ở catalog. */
  protected readonly canCopy = computed(() => this.session.can('CATALOG_BIBS:view') && this.session.can('CATALOG_BIBS:add'));
  protected readonly range = computed(() => {
    const p = this.page();
    return p ? `${(p.pageIndex - 1) * p.pageSize + 1}–${Math.min(p.pageIndex * p.pageSize, p.totalCount)}` : '';
  });
  private pendingCopy: { leader: string | null; fields: Parameters<typeof createCatalogBib>[2]; bibTypeId: number | null } | null = null;

  ngOnInit(): void {
    this.api.acqBibTypes().then((t) => this.types.set(t), () => undefined);
    void this.search(1);
  }

  protected reset(): void {
    this.q = emptySearch();
    void this.search(1);
  }

  protected async search(pageIndex: number): Promise<void> {
    const text = (v: string | null) => v?.trim() || null;
    const num = (v: number | null) => (v === null || (v as unknown) === '' ? null : Number(v));
    this.busy.set(true);
    try {
      this.page.set(await this.api.searchAcqBibs({
        ...this.q, mfnFrom: num(this.q.mfnFrom), mfnTo: num(this.q.mfnTo), publishYear: text(this.q.publishYear), publisher: text(this.q.publisher),
        title: text(this.q.title), author: text(this.q.author), pageIndex, pageSize: PAGE_SIZE,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected choose(b: AcqBibRow): void {
    this.picked.emit({ bibPublicId: b.bibPublicId, mfn: b.mfn, title: b.title, author: b.author, publisher: b.publisher, publishYear: b.publishYear, isbns: b.isbns });
  }

  /** Copy (ELIB-LRC): biểu ghi mới ở catalog mang MARC của biểu ghi đã chọn — trùng ISBN thì hỏi "Vẫn lưu". */
  protected async copy(b: AcqBibRow): Promise<void> {
    this.busy.set(true);
    try {
      const source = await this.api.bibByMfn(b.mfn);
      this.pendingCopy = { leader: source.leader, fields: ownFields(source.fields), bibTypeId: source.bibTypeId };
      const dupes = await isbnDupes(this.api, this.pendingCopy.fields);
      this.dupes.set(dupes);
      if (!dupes.length) await this.saveCopy();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async copyAnyway(): Promise<void> {
    this.busy.set(true);
    try {
      await this.saveCopy();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected cancelCopy(): void {
    this.pendingCopy = null;
    this.dupes.set([]);
  }

  private async saveCopy(): Promise<void> {
    const p = this.pendingCopy;
    if (!p) return;
    const bib = await createCatalogBib(this.api, p.leader, p.fields, p.bibTypeId);
    this.cancelCopy();
    this.toastr.success('ACQ_SOURCE.CREATED', { mfn: bib.mfn });
    this.picked.emit(bib);
  }
}
