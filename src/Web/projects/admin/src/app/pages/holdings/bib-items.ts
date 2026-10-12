import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, Item, ItemSearch, NextBarcode, Store, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete } from '../../shared/ui';
import { itemStatus } from './items';

const PREFIX_KEY = 'elib.items.prefix';

/**
 * Đăng ký cá biệt cho một biểu ghi (monolith: khung ĐKCB ở màn biên mục — Items, RegisterBarcodes, RegisterSingleBarcode,
 * DeleteBarcode). Theo lô: tiền tố + số đệm 0, đánh tiếp sau số lớn nhất của tiền tố; hoặc nhập tay một mã.
 */
@Component({
  selector: 'app-bib-items',
  imports: [FormsModule, ConfirmDelete, TranslatePipe],
  template: `
    <div class="px-5 py-4 border-t border-gray-100">
      <div class="flex items-center gap-2 mb-3">
        <h5 class="font-semibold text-gray-800">{{ 'BIB_ITEMS.TITLE' | translate }}</h5>
        <span class="text-sm text-gray-500">{{ 'BIB_ITEMS.COUNT' | translate: { count: items().length } }}</span>
      </div>

      @if (canEdit()) {
        <div class="grid grid-cols-2 md:grid-cols-6 gap-3 items-end bg-gray-50 rounded-lg p-3 mb-3">
          <div>
            <label for="bi-prefix" class="form-label">{{ 'BIB_ITEMS.PREFIX' | translate }}</label>
            <input id="bi-prefix" class="input font-mono uppercase" maxlength="20" [(ngModel)]="prefix" (change)="preview()" placeholder="VD: VV" />
          </div>
          <div>
            <label for="bi-digits" class="form-label">{{ 'BIB_ITEMS.DIGITS' | translate }}</label>
            <input id="bi-digits" type="number" min="1" max="18" class="input" [(ngModel)]="digits" (change)="preview()" />
          </div>
          <div>
            <label for="bi-qty" class="form-label">{{ 'BIB_ITEMS.QUANTITY' | translate }}</label>
            <input id="bi-qty" type="number" min="1" max="1000" class="input" [(ngModel)]="quantity" />
          </div>
          <div>
            <label for="bi-start" class="form-label">{{ 'BIB_ITEMS.START' | translate }}</label>
            <input id="bi-start" type="number" min="1" class="input" [(ngModel)]="startNumber" [placeholder]="next()?.number?.toString() ?? ('BIB_ITEMS.AUTO' | translate)" />
          </div>
          <div>
            <label for="bi-store" class="form-label">Kho</label>
            <select id="bi-store" class="input" [(ngModel)]="storeId">
              <option [ngValue]="null">—</option>
              @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
            </select>
          </div>
          <button type="button" class="btn-primary justify-center" [disabled]="busy() || !(quantity > 0)" (click)="register()">
            <span class="material-icons text-[18px]">playlist_add</span> {{ 'BIB_ITEMS.REGISTER' | translate }}
          </button>
          <p class="col-span-2 md:col-span-6 text-xs text-gray-500">
            @if (next(); as n) { {{ 'BIB_ITEMS.NEXT_CODE' | translate }} <b class="font-mono">{{ startNumber ? format(startNumber) : n.barcode }}</b>. }
            {{ 'BIB_ITEMS.OR_MANUAL' | translate }}
            <input class="input !inline-block !w-40 !py-1 font-mono ml-1" [attr.aria-label]="'BIB_ITEMS.MANUAL_LABEL' | translate" [(ngModel)]="single" (keydown.enter)="addSingle()" [placeholder]="'BIB_ITEMS.BARCODE' | translate" />
            <button type="button" class="text-blue-600 hover:underline ml-2" [disabled]="busy() || !single.trim()" (click)="addSingle()">{{ 'BIB_ITEMS.ADD' | translate }}</button>
          </p>
        </div>
      }

      @if (items().length) {
        <div class="flex flex-wrap gap-2">
          @for (it of items(); track it.publicId) {
            <span class="inline-flex items-center gap-1.5 border border-gray-200 rounded-lg pl-2.5 pr-1 py-1 text-sm bg-white">
              <span class="font-mono">{{ it.barcode }}</span>
              <span class="text-[11px] text-gray-400">{{ it.storeCode ?? '' }}</span>
              <span [class]="status(it.status, it.onLoan).badge + ' !text-[10px]'">{{ status(it.status, it.onLoan).name | translate }}</span>
              @if (session.can('CATALOG_BIBS:delete')) {
                <button type="button" class="icon-btn !w-6 !h-6 text-gray-400 hover:text-red-600" [title]="'BIB_ITEMS.DELETE_TIP' | translate: { barcode: it.barcode }" (click)="deleting.set(it)">
                  <span class="material-icons text-[16px]">close</span></button>
              }
            </span>
          }
        </div>
      } @else {
        <p class="text-sm text-gray-500">{{ 'BIB_ITEMS.EMPTY' | translate }}</p>
      }
    </div>
    @if (deleting(); as it) {
      <app-confirm-delete [busy]="busy()" message="BIB_ITEMS.DELETE_CONFIRM" [messageParams]="{ barcode: it.barcode }" (confirmed)="remove(it)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class BibItems implements OnInit {
  readonly mfn = input.required<number>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected readonly items = signal<Item[]>([]);
  protected readonly stores = signal<Store[]>([]);
  protected readonly next = signal<NextBarcode | null>(null);
  protected readonly busy = signal(false);
  protected readonly deleting = signal<Item | null>(null);
  protected prefix = readPrefix();
  protected digits = 6;
  protected quantity = 1;
  protected startNumber: number | null = null;
  protected storeId: number | null = null;
  protected single = '';

  async ngOnInit(): Promise<void> {
    void this.load();
    try {
      const stores = await this.api.crud<Store>('stores', 'holdings').searchAll();
      this.stores.set(stores);
      this.storeId = stores[0]?.id ?? null;
    } catch {
      /* thiếu quyền xem kho — đăng ký không gán kho */
    }
    if (this.canEdit()) await this.preview();
  }

  protected canEdit(): boolean {
    return this.session.can('CATALOG_BIBS:edit');
  }

  protected status(code: string, onLoan = false) {
    return itemStatus(code, onLoan);
  }

  protected format(n: number): string {
    return this.prefix.trim().toUpperCase() + String(n).padStart(this.digits, '0');
  }

  protected async preview(): Promise<void> {
    try {
      this.next.set(await this.api.nextBarcode(this.prefix.trim(), this.digits));
    } catch {
      this.next.set(null); // tiền tố sai — báo lỗi khi bấm Đăng ký
    }
  }

  protected async register(): Promise<void> {
    await this.run(async () => {
      const created = await this.api.registerItems({
        mfn: this.mfn(), quantity: this.quantity, prefix: this.prefix.trim(), digits: this.digits, startNumber: this.startNumber || null, storeId: this.storeId,
      });
      savePrefix(this.prefix.trim());
      this.startNumber = null;
      this.toastr.success(created.length > 1 ? 'BIB_ITEMS.REGISTERED_RANGE' : 'BIB_ITEMS.REGISTERED_ONE',
        { count: created.length, first: created[0].barcode, last: created[created.length - 1].barcode });
    });
  }

  protected async addSingle(): Promise<void> {
    const barcode = this.single.trim();
    if (!barcode) return;
    await this.run(async () => {
      await this.api.crud<Item>('items', 'holdings').add({ mfn: this.mfn(), barcode, storeId: this.storeId });
      this.single = '';
      this.toastr.success('BIB_ITEMS.ADDED', { barcode });
    });
  }

  protected async remove(it: Item): Promise<void> {
    await this.run(async () => {
      await this.api.crud<Item>('items', 'holdings').delete(it.publicId);
      this.deleting.set(null);
      this.toastr.success('BIB_ITEMS.DELETED', { barcode: it.barcode });
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
      await Promise.all([this.load(), this.preview()]);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.items.set(await this.api.crud<Item>('items', 'holdings').searchAll({ mfn: this.mfn() } as ItemSearch));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }
}

function readPrefix(): string {
  try {
    return localStorage.getItem(PREFIX_KEY) ?? '';
  } catch {
    return '';
  }
}

function savePrefix(prefix: string): void {
  try {
    localStorage.setItem(PREFIX_KEY, prefix);
  } catch {
    /* trình duyệt chặn lưu — lần sau nhập lại tiền tố */
  }
}
