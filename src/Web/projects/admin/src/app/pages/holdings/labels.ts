import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Barcode } from '../../../../../shared/barcode';
import { Api, ITEM_STATUSES, ItemLabel, ItemLabelRequest, Store, errorMessage } from '../../core/api';
import { ReportMeta } from '../../core/report-meta';
import { ToastrService } from '../../shared/toastr';

/** sessionStorage: nhãn cần in — màn chọn nhãn ghi trước khi mở trang in. */
export const PRINT_KEY = 'elib.print.labels';
export const HEADER_PREF = 'labels.header';
/** sessionStorage: danh sách ĐKCB do màn khác (đơn nhận) chuyển sang để in nhãn ngay — đọc một lần rồi xoá. */
export const LABEL_BARCODES_KEY = 'elib.labels.barcodes';

/** Số nhãn mỗi trang A4 ngang: 5 hàng × 5 cột (ELIB-LRC: CLASS_LABELS_PER_PAGE). */
export const LABELS_PER_PAGE = 25;

/** Số ĐKCB tối đa backend nhận mỗi lần gọi in nhãn (Holdings: ItemResource.MaxLabels). */
const LABELS_PER_REQUEST = 2000;

/** barcode: nhãn mã vạch ĐKCB; spine: nhãn gáy / môn loại (DDC + Cutter). */
export type LabelKind = 'barcode' | 'spine';

/** Dòng tiêu đề in đầu nhãn gáy (cơ quan chủ quản / tên thư viện) — nhớ theo tài khoản. */
interface LabelHeader { parent: string; library: string; }

export interface LabelJob { kind: LabelKind; labels: ItemLabel[]; header: LabelHeader; skip: number; }

/**
 * In nhãn (ELIB-LRC: In mã vạch + In nhãn môn loại): chọn ĐKCB (quét/dán danh sách, khoảng ĐKCB, biểu ghi, kho, trạng thái) → in nhãn mã vạch
 * hoặc nhãn gáy DDC/Cutter, khổ A4 ngang 25 nhãn/trang. "Bỏ qua ô đầu" để in tiếp lên tờ nhãn đã dùng dở. Mở từ khung đăng ký cá biệt
 * (?mfn=…&from=…&to=…) để in ngay lô vừa đăng ký.
 */
@Component({
  selector: 'app-labels',
  imports: [FormsModule, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'LABELS_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="flex gap-2 mb-4" role="group">
        @for (k of kinds; track k.code) {
          <button type="button" class="px-3 py-1.5 rounded-lg border text-sm flex items-center gap-1.5"
                  [class]="kind() === k.code ? 'bg-blue-600 border-blue-600 text-white' : 'border-gray-300 text-gray-700 hover:bg-gray-50'" (click)="kind.set(k.code)">
            <span class="material-icons text-[18px]">{{ k.icon }}</span> {{ k.label | translate }}</button>
        }
      </div>
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div>
          <label for="lb-list" class="field-label">{{ 'LABELS_PAGE.LIST' | translate }}</label>
          <textarea id="lb-list" rows="7" class="input font-mono text-[13px]" [(ngModel)]="list" [placeholder]="'LABELS_PAGE.LIST_PH' | translate"></textarea>
        </div>
        <div class="lg:col-span-2 grid grid-cols-2 md:grid-cols-3 gap-4 content-start">
          <div>
            <label for="lb-from" class="field-label">{{ 'LABELS_PAGE.FROM' | translate }}</label>
            <input id="lb-from" class="input font-mono" [(ngModel)]="request.barcodeFrom" />
          </div>
          <div>
            <label for="lb-to" class="field-label">{{ 'LABELS_PAGE.TO' | translate }}</label>
            <input id="lb-to" class="input font-mono" [(ngModel)]="request.barcodeTo" />
          </div>
          <div>
            <label for="lb-mfn" class="field-label">MFN</label>
            <input id="lb-mfn" type="number" min="1" class="input" [(ngModel)]="request.mfn" />
          </div>
          <div>
            <label for="lb-store" class="field-label">{{ 'LABELS_PAGE.STORE' | translate }}</label>
            <select id="lb-store" class="input" [(ngModel)]="request.storeId">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
            </select>
          </div>
          <div>
            <label for="lb-status" class="field-label">{{ 'LABELS_PAGE.STATUS' | translate }}</label>
            <select id="lb-status" class="input" [(ngModel)]="request.itemStatus">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              @for (st of statuses; track st.code) { <option [ngValue]="st.code">{{ st.name | translate }}</option> }
            </select>
          </div>
          <div class="flex items-end">
            <button type="button" class="btn-primary w-full justify-center" [disabled]="loading()" (click)="load()">
              <span class="material-icons text-[18px]" [class.animate-spin]="loading()">{{ loading() ? 'autorenew' : 'search' }}</span> {{ 'LABELS_PAGE.LOAD' | translate }}</button>
          </div>
        </div>
      </div>
    </div>

    @if (labels().length) {
      <div class="panel">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
          @if (kind() === 'spine') {
            <div>
              <label for="lb-parent" class="field-label">{{ 'LABELS_PAGE.HEADER_PARENT' | translate }}</label>
              <input id="lb-parent" class="input" maxlength="120" [ngModel]="header().parent" (ngModelChange)="setHeader({ parent: $event })" />
            </div>
          }
          <div>
            <label for="lb-lib" class="field-label">{{ 'LABELS_PAGE.HEADER_LIBRARY' | translate }}</label>
            <input id="lb-lib" class="input" maxlength="120" [ngModel]="header().library" (ngModelChange)="setHeader({ library: $event })" />
          </div>
          <div>
            <label for="lb-skip" class="field-label">{{ 'LABELS_PAGE.SKIP' | translate }}</label>
            <input id="lb-skip" type="number" min="0" [max]="perPage - 1" class="input" [(ngModel)]="skip" />
          </div>
          <div class="flex gap-2 justify-end" [class.md:col-span-2]="kind() === 'barcode'">
            <span class="text-sm text-gray-500 self-center">{{ 'LABELS_PAGE.COUNT' | translate: { count: labels().length, pages: pages() } }}</span>
            <button type="button" class="btn-primary" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'LABELS_PAGE.PRINT' | translate }}</button>
          </div>
        </div>
      </div>

      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse min-w-[760px]">
            <thead><tr>
              <th class="th w-12">#</th><th class="th w-36">{{ 'LABELS_PAGE.BARCODE' | translate }}</th><th class="th w-36">{{ 'LABELS_PAGE.CALL_NO' | translate }}</th>
              <th class="th">{{ 'LABELS_PAGE.TITLE_COL' | translate }}</th><th class="th w-32">{{ 'LABELS_PAGE.STORE' | translate }}</th><th class="th w-12"></th>
            </tr></thead>
            <tbody>
              @for (l of labels(); track l.barcode; let i = $index) {
                <tr class="hover:bg-gray-50/50">
                  <td class="td text-gray-400">{{ i + 1 }}</td>
                  <td class="td font-mono text-[13px]">{{ l.barcode }}</td>
                  <td class="td font-mono text-[13px]">{{ l.ddc ?? '—' }}@if (l.cutter) { <span class="text-gray-500"> / {{ l.cutter }}</span> }</td>
                  <td class="td">{{ l.title ?? '—' }}@if (l.author) { <span class="text-gray-500"> — {{ l.author }}</span> }</td>
                  <td class="td text-[13px]">{{ l.storeCode ?? '—' }}</td>
                  <td class="td"><button type="button" class="icon-btn text-gray-400 hover:text-red-600" [title]="'LABELS_PAGE.REMOVE' | translate" (click)="remove(i)"><span class="material-icons text-[18px]">close</span></button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    } @else if (loaded()) {
      <div class="panel text-center text-gray-500 py-10">{{ 'LABELS_PAGE.EMPTY' | translate }}</div>
    }
  `,
})
export class Labels implements OnInit {
  /** Query param từ khung đăng ký cá biệt. */
  readonly mfn = input<string>();
  readonly from = input<string>();
  readonly to = input<string>();

  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly reportMeta = inject(ReportMeta);
  private readonly toastr = inject(ToastrService);

  protected readonly kinds: { code: LabelKind; label: string; icon: string }[] = [
    { code: 'barcode', label: 'LABELS_PAGE.KIND_BARCODE', icon: 'qr_code_2' },
    { code: 'spine', label: 'LABELS_PAGE.KIND_SPINE', icon: 'label' },
  ];
  protected readonly statuses = ITEM_STATUSES;
  protected readonly perPage = LABELS_PER_PAGE;
  protected readonly kind = signal<LabelKind>('barcode');
  protected readonly stores = signal<Store[]>([]);
  protected readonly labels = signal<ItemLabel[]>([]);
  protected readonly loading = signal(false);
  protected readonly loaded = signal(false);
  /** Cơ quan chủ quản / tên thư viện điền sẵn từ tham số của đơn vị (sửa tại chỗ chỉ áp dụng cho phiên này). */
  protected readonly header = computed<LabelHeader>(() => ({ parent: this.reportMeta.parent(), library: this.reportMeta.library() }));
  protected readonly pages = computed(() => Math.ceil((this.labels().length + this.skip) / LABELS_PER_PAGE));
  protected request: ItemLabelRequest = { barcodeFrom: '', barcodeTo: '', mfn: null, storeId: null, itemStatus: null };
  protected list = '';
  protected skip = 0;

  async ngOnInit(): Promise<void> {
    this.reportMeta.resetEdits();
    const handed = this.takeHandedBarcodes();
    const mfn = Number(this.mfn());
    if (handed.length) {
      this.list = handed.join('\n');
      void this.load();
    } else if (mfn > 0) {
      this.request = { ...this.request, mfn, barcodeFrom: this.from() ?? '', barcodeTo: this.to() ?? '' };
      void this.load();
    }
    try {
      this.stores.set(await this.api.crud<Store>('stores', 'holdings').searchAll());
    } catch {
      /* thiếu quyền xem kho — chỉ mất bộ lọc kho */
    }
  }

  private takeHandedBarcodes(): string[] {
    try {
      const raw = sessionStorage.getItem(LABEL_BARCODES_KEY);
      sessionStorage.removeItem(LABEL_BARCODES_KEY);
      const list = JSON.parse(raw ?? '[]') as unknown;
      return Array.isArray(list) ? list.filter((b): b is string => typeof b === 'string') : [];
    } catch {
      return [];
    }
  }

  protected setHeader(change: Partial<LabelHeader>): void {
    if (change.parent !== undefined) this.reportMeta.setParent(change.parent);
    if (change.library !== undefined) this.reportMeta.setLibrary(change.library);
  }

  protected async load(): Promise<void> {
    const barcodes = this.list.split(/[\s,;]+/).map((b) => b.trim()).filter(Boolean);
    const r = this.request;
    this.loading.set(true);
    try {
      const query = { mfn: r.mfn || null, storeId: r.storeId, itemStatus: r.itemStatus, barcodeFrom: r.barcodeFrom?.trim() || null, barcodeTo: r.barcodeTo?.trim() || null };
      if (barcodes.length <= LABELS_PER_REQUEST) {
        this.labels.set(await this.api.itemLabels({ ...query, barcodes: barcodes.length ? barcodes : null }));
      } else {
        // Backend nhận tối đa LABELS_PER_REQUEST ĐKCB mỗi lần — chia lô, gọi lần lượt rồi nối lại (giữ thứ tự nhập).
        const all: ItemLabel[] = [];
        for (let i = 0; i < barcodes.length; i += LABELS_PER_REQUEST) all.push(...await this.api.itemLabels({ ...query, barcodes: barcodes.slice(i, i + LABELS_PER_REQUEST) }));
        this.labels.set(all);
      }
      this.loaded.set(true);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected remove(index: number): void {
    this.labels.update((all) => all.filter((_, i) => i !== index));
  }

  protected print(): void {
    const job: LabelJob = { kind: this.kind(), labels: this.labels(), header: this.header(), skip: Math.min(Math.max(0, Math.floor(this.skip || 0)), LABELS_PER_PAGE - 1) };
    try {
      sessionStorage.setItem(PRINT_KEY, JSON.stringify(job));
    } catch {
      this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
      return;
    }
    void this.router.navigateByUrl('/print/labels');
  }
}

/**
 * Trang in nhãn (ngoài khung admin): A4 ngang, 25 nhãn/trang (5 × 5), ngắt trang sau mỗi nhóm để hàng cuối không bị cắt (ELIB-LRC:
 * class-label-sheet). Nhãn mã vạch: tên thư viện, Code 128, số ĐKCB, nhan đề; nhãn gáy: cơ quan chủ quản / thư viện, DDC — Cutter, tác giả – nhan đề.
 */
@Component({
  selector: 'app-label-print',
  imports: [Barcode, RouterLink, TranslatePipe],
  styles: `
    :host { display: block; background: #f3f4f6; min-height: 100vh; }
    .page { width: 281mm; height: 194mm; margin: 6mm auto; background: #fff; display: grid; grid-template-columns: repeat(5, 1fr);
            grid-template-rows: repeat(5, 1fr); gap: 2mm; box-sizing: border-box; font-family: Inter, Arial, sans-serif; color: #0f172a; }
    .cell { box-sizing: border-box; overflow: hidden; display: flex; flex-direction: column; align-items: center; justify-content: center;
            text-align: center; padding: 1.2mm 2mm; border: 0.2mm dashed #cbd5e1; border-radius: 1.5mm; }
    .cell.empty { border-color: transparent; }
    .lib { font-size: 2.3mm; line-height: 1.2; text-transform: uppercase; max-width: 100%; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .parent { font-size: 2.4mm; font-weight: 600; line-height: 1.2; text-transform: uppercase; max-width: 100%; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .lib.under { text-decoration: underline; }
    .code { width: 100%; margin-top: 0.8mm; }
    .no { font-family: ui-monospace, Consolas, monospace; font-size: 3mm; letter-spacing: 0.4mm; font-weight: 600; }
    .title { font-size: 2.2mm; line-height: 1.2; color: #475569; max-width: 100%; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .ddc, .cutter { font-size: 5mm; font-weight: 700; line-height: 1.15; }
    .ddc { margin-top: 0.8mm; }
    .rule { width: 12mm; border-top: 0.3mm dashed #64748b; margin: 0.6mm 0; }
    @media print {
      @page { size: A4 landscape; margin: 8mm; }
      :host { background: #fff; }
      .no-print { display: none !important; }
      .page { margin: 0; break-after: page; }
      .page:last-child { break-after: auto; }
      .cell { border-color: transparent; }
    }
  `,
  template: `
    <div class="no-print sticky top-0 z-10 bg-white border-b border-gray-200 px-4 py-3 flex items-center gap-3 flex-wrap">
      <a routerLink="/print-labels" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">arrow_back</span> {{ 'LABELS_PAGE.TITLE' | translate }}</a>
      <div class="font-semibold text-gray-800">{{ (job()?.kind === 'spine' ? 'LABELS_PAGE.KIND_SPINE' : 'LABELS_PAGE.KIND_BARCODE') | translate }}</div>
      <span class="text-sm text-gray-500">{{ 'LABELS_PAGE.COUNT' | translate: { count: job()?.labels?.length ?? 0, pages: sheets().length } }}</span>
      <span class="flex-1"></span>
      <button type="button" class="btn-primary" [disabled]="!sheets().length" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'LABELS_PAGE.PRINT' | translate }}</button>
    </div>
    @if (!sheets().length) { <p class="no-print text-center text-gray-500 py-24">{{ 'LABELS_PAGE.EMPTY' | translate }}</p> }
    @if (job(); as j) {
      @for (sheet of sheets(); track $index) {
        <div class="page">
          @for (l of sheet; track $index) {
            @if (l) {
              <div class="cell" [attr.data-label]="l.barcode">
                @if (j.kind === 'spine') {
                  @if (j.header.parent) { <div class="parent">{{ j.header.parent }}</div> }
                  @if (j.header.library) { <div class="lib under">{{ j.header.library }}</div> }
                  <div class="ddc">{{ l.ddc || '—' }}</div>
                  <div class="rule"></div>
                  <div class="cutter">{{ l.cutter || '' }}</div>
                  <div class="title">{{ caption(l) }}</div>
                } @else {
                  @if (j.header.library) { <div class="lib">{{ j.header.library }}</div> }
                  <div class="code"><elib-barcode [value]="l.barcode" [height]="30" cssHeight="11mm" /></div>
                  <div class="no">{{ l.barcode }}</div>
                  @if (l.title) { <div class="title">{{ l.title }}</div> }
                }
              </div>
            } @else {
              <div class="cell empty"></div>
            }
          }
        </div>
      }
    }
  `,
})
export class LabelPrint implements OnInit {
  protected readonly job = signal<LabelJob | null>(null);

  /** Mỗi tờ 25 ô; "bỏ qua ô đầu" để trống các ô đã dùng của tờ đầu. */
  protected readonly sheets = computed(() => {
    const j = this.job();
    if (!j?.labels.length) return [];
    const cells: (ItemLabel | null)[] = [...Array<null>(j.skip).fill(null), ...j.labels];
    const out: (ItemLabel | null)[][] = [];
    for (let i = 0; i < cells.length; i += LABELS_PER_PAGE) {
      const sheet = cells.slice(i, i + LABELS_PER_PAGE);
      while (sheet.length < LABELS_PER_PAGE) sheet.push(null);
      out.push(sheet);
    }
    return out;
  });

  ngOnInit(): void {
    try {
      const job = JSON.parse(sessionStorage.getItem(PRINT_KEY) ?? 'null') as LabelJob | null;
      if (job && Array.isArray(job.labels)) this.job.set({ ...job, skip: Math.min(Math.max(0, job.skip | 0), LABELS_PER_PAGE - 1) });
    } catch {
      /* chưa chọn nhãn — hiện trang trống */
    }
  }

  protected caption(l: ItemLabel): string {
    return [l.author, l.title].filter(Boolean).join(' - ');
  }

  protected print(): void {
    window.print();
  }
}
