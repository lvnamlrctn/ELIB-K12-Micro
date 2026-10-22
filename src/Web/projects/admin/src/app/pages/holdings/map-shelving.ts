import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Api, CrudPage, MapFloorNode, MapShelfNode, MapShelvingRow, MapShelvingSearch, PlaceResult, StoreOption, errorMessage,
} from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

type Mode = 'shelve' | 'reshelve';

/**
 * Xếp giá trực quan (monolith: /admin/map-shelving) và xếp giá lại (/admin/reshelving), quyền MAP_SHELVING. Trái: bản sách (chưa xếp giá, hoặc đã xếp giá để
 * xếp lại) kèm vị trí gợi ý theo DDC. Phải: sơ đồ các giá của kho theo tầng — chọn giá, chọn ngăn rồi "Xếp giá". Bản chưa xếp giá sang sẵn sàng phục vụ;
 * bản đã xếp giá chỉ đổi vị trí. Giá/ngăn lấy từ sơ đồ tầng của phân hệ Không gian (vẽ ở "Sơ đồ tầng"), không sửa ở đây.
 */
@Component({
  selector: 'app-map-shelving',
  imports: [FormsModule, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-4">
      <h4 class="page-title !mb-1">{{ (mode === 'shelve' ? 'MAP_SHELVING_PAGE.TITLE' : 'MAP_SHELVING_PAGE.RESHELVE_TITLE') | translate }}</h4>
      <p class="text-sm text-gray-500">{{ (mode === 'shelve' ? 'MAP_SHELVING_PAGE.HINT' : 'MAP_SHELVING_PAGE.RESHELVE_HINT') | translate }}</p>
    </div>

    <div class="panel grid grid-cols-2 lg:grid-cols-6 gap-3 items-end">
      <div class="col-span-2 lg:col-span-2">
        <label for="ms-store" class="field-label">{{ 'ITEMS_PAGE.STORE' | translate }} *</label>
        <select id="ms-store" class="input" [ngModel]="filter.storeId" (ngModelChange)="onStore($event)">
          <option [ngValue]="null">{{ 'MAP_SHELVING_PAGE.CHOOSE_STORE' | translate }}</option>
          @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
        </select>
      </div>
      @if (mode === 'reshelve') {
        <div>
          <label for="ms-status" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
          <select id="ms-status" class="input" [(ngModel)]="filter.status" (ngModelChange)="find()">
            <option value="I">{{ 'MAP_SHELVING_PAGE.UNSHELVED' | translate }}</option><option value="R">{{ 'MAP_SHELVING_PAGE.SHELVED' | translate }}</option>
            <option value="ALL">{{ 'COMMON.ALL' | translate }}</option>
          </select>
        </div>
      }
      <div><label for="ms-rc" class="field-label">{{ 'TRANSFER.RECEIPT' | translate }}</label><input id="ms-rc" class="input" [(ngModel)]="filter.receipt" (keydown.enter)="find()" /></div>
      <div><label for="ms-bf" class="field-label">{{ 'ITEMS_PAGE.BARCODE_FROM' | translate }}</label><input id="ms-bf" class="input font-mono" [(ngModel)]="filter.barcodeFrom" (keydown.enter)="find()" /></div>
      <div><label for="ms-bt" class="field-label">{{ 'ITEMS_PAGE.BARCODE_TO' | translate }}</label><input id="ms-bt" class="input font-mono" [(ngModel)]="filter.barcodeTo" (keydown.enter)="find()" /></div>
      <div class="col-span-2 lg:col-span-6 flex justify-end">
        <button type="button" class="btn-primary" [disabled]="!filter.storeId" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>
    @if (!filter.storeId) { <div class="alert-info !block mb-4 text-sm">{{ 'MAP_SHELVING_PAGE.PICK_STORE_HINT' | translate }}</div> }

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-5">
      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative self-start">
        @if (loading()) { <app-loading /> }
        <div class="overflow-x-auto w-full">
          <table class="w-full text-left border-collapse min-w-[520px]">
            <thead><tr>
              <th class="th w-10 !text-center"><input type="checkbox" class="rounded border-gray-300" [attr.aria-label]="'SHELVING_PAGE.SELECT_ALL' | translate" [checked]="allSelected()" (change)="toggleAll()" /></th>
              <th class="th w-32">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th><th class="th w-20">DDC</th>
              @if (mode === 'reshelve') { <th class="th w-44">{{ 'MAP_SHELVING_PAGE.CURRENT' | translate }}</th> }
              <th class="th w-44">{{ 'MAP_SHELVING_PAGE.SUGGESTED' | translate }}</th>
            </tr></thead>
            <tbody>
              @for (r of page()?.items ?? []; track r.publicId) {
                <tr class="hover:bg-gray-50/50 cursor-pointer" (click)="toggle(r)">
                  <td class="td text-center"><input type="checkbox" class="rounded border-gray-300" [attr.aria-label]="r.barcode" [checked]="selected().has(r.publicId)" (click)="$event.stopPropagation()" (change)="toggle(r)" /></td>
                  <td class="td font-mono text-[13px]">{{ r.barcode }}</td>
                  <td class="td">{{ r.title ?? '—' }}@if (r.onLoan) { <div class="text-[11px] text-amber-700">{{ 'MAP_SHELVING_PAGE.ON_LOAN' | translate }}</div> }</td>
                  <td class="td font-mono text-[13px]">{{ r.ddc ?? '' }}</td>
                  @if (mode === 'reshelve') {
                    <td class="td text-[13px]">
                      @if (r.shelfPublicId) {
                        {{ r.shelfName ?? '—' }}@if (r.rowIndex) { — {{ 'SPACE_FLOOR_MAP_PAGE.SHELF_ROW_N' | translate: { n: r.rowIndex } }} }
                        @if (r.shelfDeleted) { <div class="text-[11px] text-red-600">{{ 'MAP_SHELVING_PAGE.SHELF_DELETED' | translate }}</div> }
                      } @else { <span class="text-gray-400">{{ 'MAP_SHELVING_PAGE.NO_LOCATION' | translate }}</span> }
                    </td>
                  }
                  <td class="td text-[13px]">
                    @if (r.suggestedShelfPublicId) {
                      <span class="text-green-700">{{ r.suggestedShelfName }}@if (r.suggestedRowIndex) { — {{ 'SPACE_FLOOR_MAP_PAGE.SHELF_ROW_N' | translate: { n: r.suggestedRowIndex } }} }</span>
                    } @else { <span class="text-gray-400">{{ 'MAP_SHELVING_PAGE.UNKNOWN' | translate }}</span> }
                  </td>
                </tr>
              } @empty {
                <tr><td [attr.colspan]="mode === 'reshelve' ? 6 : 5" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('MAP_SHELVING_PAGE.NO_ITEMS' | translate) }}</td></tr>
              }
            </tbody>
          </table>
        </div>
        @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex" [pageSize]="search.pageSize" (pageChange)="onPage($event)" /> }
      </div>

      <div class="panel !mb-0 self-start">
        @if (!floors().length) {
          <p class="text-sm text-gray-500">{{ (filter.storeId ? 'MAP_SHELVING_PAGE.NO_SHELVES' : 'MAP_SHELVING_PAGE.PICK_STORE_HINT') | translate }}</p>
        } @else {
          <div class="flex flex-wrap gap-1.5 mb-3" role="tablist">
            @for (f of floors(); track f.floorPublicId; let i = $index) {
              <button type="button" role="tab" class="px-2.5 py-1 rounded-lg border text-[13px]" [attr.aria-selected]="i === floorIndex()"
                      [class]="i === floorIndex() ? 'bg-blue-600 border-blue-600 text-white' : 'border-gray-300 text-gray-700 hover:bg-gray-50'" (click)="floorIndex.set(i)">
                {{ f.buildingName ? f.buildingName + ' — ' : '' }}{{ f.name }}</button>
            }
          </div>
          @if (floor(); as f) {
            <!-- Sơ đồ chỉ đọc: vị trí/kích thước giá theo % khung (đúng như vẽ ở Sơ đồ tầng); chọn giá bằng cách bấm. -->
            <div class="relative w-full bg-gray-100 rounded-lg border border-gray-200 overflow-hidden select-none" [style.aspect-ratio]="f.canvasWidth + ' / ' + f.canvasHeight">
              @if (f.layoutImageUrl) {
                <img [src]="f.layoutImageUrl" [alt]="f.name" class="absolute inset-0 w-full h-full object-contain pointer-events-none" />
              } @else {
                <div class="absolute inset-0 pointer-events-none" style="background-image: linear-gradient(#cbd5e1 1px, transparent 1px), linear-gradient(90deg, #cbd5e1 1px, transparent 1px); background-size: 30px 30px;"></div>
              }
              @for (s of f.shelves; track s.shelfPublicId) {
                @if (s.positionX !== null && s.positionY !== null) {
                  <button type="button" class="absolute rounded-md border-2 flex flex-col items-center justify-center text-center text-xs font-medium overflow-hidden"
                          [style.left.%]="s.positionX" [style.top.%]="s.positionY" [style.width.%]="s.width ?? 12" [style.height.%]="s.height ?? 12"
                          [style.border-color]="shelfId() === s.shelfPublicId ? '#16a34a' : (s.colorHex ?? '#3b82f6')" [style.border-width.px]="shelfId() === s.shelfPublicId ? 3 : 2"
                          [style.background-color]="(s.colorHex ?? '#3b82f6') + '33'" [attr.aria-pressed]="shelfId() === s.shelfPublicId" (click)="pickShelf(s)">
                    <span class="material-icons text-[16px]">menu_book</span><span class="truncate px-1 max-w-full">{{ s.name }}</span>
                    <span class="text-[10px] text-gray-600">{{ s.items }}</span>
                  </button>
                }
              }
            </div>
            @if (unplaced(f).length) {
              <div class="mt-2 flex flex-wrap gap-1.5 items-center text-xs text-gray-500">{{ 'MAP_SHELVING_PAGE.UNPLACED' | translate }}
                @for (s of unplaced(f); track s.shelfPublicId) {
                  <button type="button" class="px-2 py-0.5 rounded border border-gray-300 hover:bg-gray-50" [class.!border-green-600]="shelfId() === s.shelfPublicId" (click)="pickShelf(s)">{{ s.name }}</button>
                }
              </div>
            }
          }

          @if (shelf(); as s) {
            <div class="mt-4 rounded-lg border border-green-200 bg-green-50 p-3">
              <div class="text-sm text-green-800 font-medium flex items-center gap-1"><span class="material-icons text-[18px]">check_circle</span> {{ 'MAP_SHELVING_PAGE.SELECTED_SHELF' | translate: { name: s.name } }}</div>
              @if (s.rows.length) {
                <label for="ms-row" class="field-label mt-2">{{ 'MAP_SHELVING_PAGE.ROW' | translate }}</label>
                <select id="ms-row" class="input" [ngModel]="rowId()" (ngModelChange)="rowId.set($event)">
                  <option [ngValue]="null">{{ 'MAP_SHELVING_PAGE.WHOLE_SHELF' | translate }}</option>
                  @for (r of s.rows; track r.rowPublicId) {
                    <option [ngValue]="r.rowPublicId">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_ROW_N' | translate: { n: r.rowIndex } }} ({{ r.ddcStart }} – {{ r.ddcEnd }}) · {{ r.items }}</option>
                  }
                </select>
              }
              <label for="ms-reason" class="field-label mt-2">{{ 'MAP_SHELVING_PAGE.REASON' | translate }}</label>
              <input id="ms-reason" class="input" maxlength="500" [(ngModel)]="reason" />
            </div>
          }
          @if (can()) {
            <button type="button" class="btn-primary w-full justify-center mt-4" [disabled]="busy() || !selected().size || !shelfId()" (click)="place()">
              <span class="material-icons text-[18px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'shelves' }}</span>
              {{ (mode === 'shelve' ? 'MAP_SHELVING_PAGE.PLACE_N' : 'MAP_SHELVING_PAGE.RESHELVE_N') | translate: { count: selected().size } }}
            </button>
          }
          @if (result(); as r) {
            <div class="mt-3 text-sm space-y-1">
              <div class="alert-success !block">{{ 'MAP_SHELVING_PAGE.PLACED' | translate: { count: r.placed } }}</div>
              @for (s of r.skipped; track s.barcode) { <div class="alert-error !block text-xs"><b class="font-mono">{{ s.barcode }}</b>: {{ s.reason }}</div> }
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class MapShelving implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly session = inject(Session);
  protected readonly mode: Mode = (inject(ActivatedRoute).snapshot.data['mode'] as Mode) ?? 'shelve';

  protected readonly stores = signal<StoreOption[]>([]);
  protected filter: { storeId: number | null; status: string; receipt: string; barcodeFrom: string; barcodeTo: string } =
    { storeId: null, status: 'I', receipt: '', barcodeFrom: '', barcodeTo: '' };
  protected search = { pageIndex: 1, pageSize: 20 };
  protected readonly page = signal<CrudPage<MapShelvingRow> | null>(null);
  protected readonly selected = signal<Set<string>>(new Set());
  protected readonly floors = signal<MapFloorNode[]>([]);
  protected readonly floorIndex = signal(0);
  protected readonly floor = computed(() => this.floors()[this.floorIndex()] ?? null);
  protected readonly shelfId = signal<string | null>(null);
  protected readonly rowId = signal<string | null>(null);
  protected readonly shelf = computed<MapShelfNode | null>(() => this.floors().flatMap((f) => f.shelves).find((s) => s.shelfPublicId === this.shelfId()) ?? null);
  protected reason = '';
  protected readonly result = signal<PlaceResult | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly can = computed(() => this.session.can('MAP_SHELVING:edit'));
  protected readonly allSelected = computed(() => {
    const items = this.page()?.items ?? [];
    return items.length > 0 && items.every((r) => this.selected().has(r.publicId));
  });

  ngOnInit(): void {
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected unplaced(f: MapFloorNode): MapShelfNode[] {
    return f.shelves.filter((s) => s.positionX === null || s.positionY === null);
  }

  protected async onStore(storeId: number | null): Promise<void> {
    this.filter.storeId = storeId;
    this.shelfId.set(null);
    this.rowId.set(null);
    this.floors.set([]);
    this.floorIndex.set(0);
    this.selected.set(new Set());
    this.page.set(null);
    if (!storeId) return;
    try {
      this.floors.set(await this.api.mapShelves(storeId));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
    this.find();
  }

  protected find(): void {
    this.search.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.search = { pageIndex: e.pageIndex, pageSize: e.pageSize };
    void this.load();
  }

  protected toggle(r: MapShelvingRow): void {
    const next = new Set(this.selected());
    if (next.has(r.publicId)) next.delete(r.publicId);
    else next.add(r.publicId);
    this.selected.set(next);
    this.autoPick();
  }

  protected toggleAll(): void {
    const items = this.page()?.items ?? [];
    this.selected.set(this.allSelected() ? new Set() : new Set(items.map((r) => r.publicId)));
    this.autoPick();
  }

  protected pickShelf(s: MapShelfNode): void {
    this.shelfId.set(s.shelfPublicId);
    // Mọi bản đã chọn cùng gợi ý vào đúng ngăn của giá này → chọn sẵn ngăn đó (như LRC).
    const picked = this.pickedRows();
    const rows = new Set(picked.filter((r) => r.suggestedShelfPublicId === s.shelfPublicId).map((r) => r.suggestedRowPublicId));
    this.rowId.set(picked.length > 0 && rows.size === 1 && picked.every((r) => r.suggestedShelfPublicId === s.shelfPublicId) ? ([...rows][0] ?? null) : null);
  }

  /** Các bản đã chọn có chung một giá gợi ý → chọn sẵn giá (và đổi sang tầng của giá đó). */
  private autoPick(): void {
    const picked = this.pickedRows();
    const shelves = new Set(picked.map((r) => r.suggestedShelfPublicId));
    if (!picked.length || shelves.size !== 1) return;
    const id = [...shelves][0];
    if (!id) return;
    const floors = this.floors();
    const fi = floors.findIndex((f) => f.shelves.some((s) => s.shelfPublicId === id));
    const target = fi >= 0 ? floors[fi].shelves.find((s) => s.shelfPublicId === id) : null;
    if (!target) return;
    this.floorIndex.set(fi);
    this.pickShelf(target);
  }

  private pickedRows(): MapShelvingRow[] {
    return (this.page()?.items ?? []).filter((r) => this.selected().has(r.publicId));
  }

  protected async place(): Promise<void> {
    const shelfPublicId = this.shelfId();
    if (!shelfPublicId || !this.selected().size) return;
    this.busy.set(true);
    try {
      const r = await this.api.placeItems({ ids: [...this.selected()], shelfPublicId, rowPublicId: this.rowId(), reason: this.reason.trim() || null });
      this.result.set(r);
      this.toastr.success(this.mode === 'shelve' ? 'MAP_SHELVING_PAGE.PLACED_TOAST' : 'MAP_SHELVING_PAGE.RESHELVED_TOAST', { count: r.placed });
      this.selected.set(new Set());
      if (this.filter.storeId) this.floors.set(await this.api.mapShelves(this.filter.storeId)); // số bản trên giá/ngăn
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    if (!this.filter.storeId) return;
    this.loading.set(true);
    try {
      const body: MapShelvingSearch = {
        status: this.mode === 'shelve' ? 'I' : this.filter.status, storeId: this.filter.storeId, receipt: this.filter.receipt.trim() || null,
        barcodeFrom: this.filter.barcodeFrom.trim() || null, barcodeTo: this.filter.barcodeTo.trim() || null, ...this.search,
      };
      this.page.set(await this.api.searchMapShelving(body));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
