import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, CrudPage, Item, ItemSearch, ShelveResult, Store, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

/**
 * Xếp giá (monolith: /admin/shelving, quyền MAP_SHELVING): bản sách mới đăng ký (chưa xếp giá) → sẵn sàng phục vụ.
 * Quét/dán số ĐKCB hoặc tích chọn trong danh sách; chọn kho thì chuyển luôn kho.
 */
@Component({
  selector: 'app-shelving',
  imports: [FormsModule, Loading, Paginator],
  template: `
    <div class="mb-5"><h4 class="page-title">Xếp giá</h4></div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-5">
      <div class="panel !mb-0 lg:col-span-1">
        <label for="sh-codes" class="form-label">Quét hoặc dán số ĐKCB (mỗi dòng một mã)</label>
        <textarea id="sh-codes" rows="8" class="input font-mono text-[13px]" [(ngModel)]="codes" placeholder="VV000001&#10;VV000002"></textarea>
        <label for="sh-store" class="form-label mt-3">Chuyển vào kho</label>
        <select id="sh-store" class="input" [(ngModel)]="storeId">
          <option [ngValue]="null">Giữ kho hiện tại</option>
          @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
        </select>
        @if (can()) {
          <button type="button" class="btn-primary w-full justify-center mt-4" [disabled]="busy() || (!codeList().length && !selected().size)" (click)="shelve()">
            <span class="material-icons text-[18px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'shelves' }}</span>
            Xếp giá {{ codeList().length + selected().size }} bản
          </button>
        }
        @if (result(); as r) {
          <div class="mt-4 text-sm space-y-2">
            <div class="alert-success !block">Đã xếp giá <b>{{ r.shelved }}</b> bản.</div>
            @if (r.skipped.length) { <div class="alert-error !block">Không ở trạng thái chưa xếp giá: <span class="font-mono text-xs">{{ r.skipped.join(', ') }}</span></div> }
            @if (r.notFound.length) { <div class="alert-error !block">Không có ĐKCB: <span class="font-mono text-xs">{{ r.notFound.join(', ') }}</span></div> }
          </div>
        }
      </div>

      <div class="lg:col-span-2">
        <div class="panel flex flex-wrap gap-3 items-end">
          <div class="flex-1 min-w-[200px]">
            <label for="sh-kw" class="field-label">Bản sách chưa xếp giá — số ĐKCB / nhan đề</label>
            <input id="sh-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" />
          </div>
          <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> Tìm</button>
        </div>
        <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
          @if (loading()) { <app-loading /> }
          <table class="w-full text-left border-collapse">
            <thead><tr>
              <th class="th w-10 !text-center"><input type="checkbox" class="rounded border-gray-300" aria-label="Chọn tất cả" [checked]="allSelected()" (change)="toggleAll()" /></th>
              <th class="th w-36">Số ĐKCB</th><th class="th">Nhan đề</th><th class="th w-40">Kho</th>
            </tr></thead>
            <tbody>
              @for (it of page()?.items ?? []; track it.publicId) {
                <tr class="hover:bg-gray-50/50 cursor-pointer" (click)="toggle(it)">
                  <td class="td text-center"><input type="checkbox" class="rounded border-gray-300" [attr.aria-label]="'Chọn ' + it.barcode" [checked]="selected().has(it.publicId)" (click)="$event.stopPropagation()" (change)="toggle(it)" /></td>
                  <td class="td font-mono text-[13px]">{{ it.barcode }}</td>
                  <td class="td">{{ it.title ?? '—' }} <span class="text-[11px] text-gray-400">MFN {{ it.mfn }}</span></td>
                  <td class="td">{{ it.storeCode ?? '—' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="4" class="py-8 text-center text-gray-500">{{ loading() ? '' : 'Không còn bản sách nào chưa xếp giá' }}</td></tr>
              }
            </tbody>
          </table>
          @if (page(); as p) {
            <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
          }
        </div>
      </div>
    </div>
  `,
})
export class Shelving implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected codes = '';
  protected storeId: number | null = null;
  protected search: ItemSearch = { keyword: '', itemStatus: 'I', pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<CrudPage<Item> | null>(null);
  protected readonly stores = signal<Store[]>([]);
  protected readonly selected = signal<Set<string>>(new Set());
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly result = signal<ShelveResult | null>(null);
  protected readonly allSelected = computed(() => {
    const items = this.page()?.items ?? [];
    return items.length > 0 && items.every((i) => this.selected().has(i.publicId));
  });

  async ngOnInit(): Promise<void> {
    void this.load();
    try {
      this.stores.set(await this.api.crud<Store>('stores', 'holdings').searchAll());
    } catch {
      /* thiếu quyền xem kho — chỉ xếp giá giữ nguyên kho */
    }
  }

  protected can(): boolean {
    return this.session.can('MAP_SHELVING:edit');
  }

  protected codeList(): string[] {
    return [...new Set(this.codes.split(/[\s,;]+/).map((c) => c.trim()).filter((c) => c))];
  }

  protected toggle(it: Item): void {
    const next = new Set(this.selected());
    if (next.has(it.publicId)) next.delete(it.publicId);
    else next.add(it.publicId);
    this.selected.set(next);
  }

  protected toggleAll(): void {
    this.selected.set(this.allSelected() ? new Set() : new Set((this.page()?.items ?? []).map((i) => i.publicId)));
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

  protected async shelve(): Promise<void> {
    this.busy.set(true);
    try {
      const r = await this.api.shelveItems({ ids: [...this.selected()], barcodes: this.codeList(), storeId: this.storeId });
      this.result.set(r);
      if (r.shelved > 0) this.toastr.success(`Đã xếp giá ${r.shelved} bản.`);
      this.codes = '';
      this.selected.set(new Set());
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.api.lookupItems({ ...this.search, keyword: this.search.keyword?.trim() || undefined }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
