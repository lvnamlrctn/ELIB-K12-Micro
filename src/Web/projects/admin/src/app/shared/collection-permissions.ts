import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CollectionPermission, errorMessage } from '../core/api';
import { ToastrService } from './toastr';
import { Loading, Modal } from './ui';
import { ItemsApi } from '../pages/digital/items-api';

interface PermissionRow {
  readerTypeId: number; name: string; read: boolean; download: boolean; maxDocument: number | null; offlineDays: number | null; reservationExpireHours: number | null;
  /** Loại bạn đọc đã có chính sách riêng trên bộ sưu tập này. */
  saved: boolean;
}

/**
 * Phân quyền bộ sưu tập theo loại bạn đọc (LRC: ebook-collection → "Phân quyền"): Đọc, Tải xuống, số lượt tải, số ngày Offline, giờ hết hạn đặt mượn.
 * Loại bạn đọc chưa có chính sách thì mặc định được đọc, không được tải — chỉ lưu những dòng đã có chính sách hoặc người dùng đã đổi khác mặc định.
 */
@Component({
  selector: 'app-collection-permissions',
  imports: [FormsModule, Loading, Modal, TranslatePipe],
  template: `
    <app-modal title="DIGITAL_DOC.PERMISSION_TITLE" widthClass="max-w-3xl" (closed)="closed.emit()">
      <p class="text-sm text-gray-500 mb-3">{{ collectionName() }} — {{ 'DIGITAL_DOC.PERMISSION_HINT' | translate }}</p>
      <div class="relative min-h-[120px]">
        @if (loading()) { <app-loading /> }
        @if (loadError(); as e) { <p class="text-sm text-red-600 mb-2" role="alert">{{ e }}</p> }
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse text-sm">
            <thead>
              <tr class="uppercase text-xs text-gray-600" style="background:#f5f0e8">
                <th class="py-2 px-3">{{ 'DIGITAL_DOC.READER_TYPE' | translate }}</th>
                <th class="py-2 px-3 text-center">{{ 'DIGITAL_DOC.PERMISSION_READ' | translate }}</th>
                <th class="py-2 px-3 text-center">{{ 'DIGITAL_DOC.PERMISSION_DOWNLOAD' | translate }}</th>
                <th class="py-2 px-3 text-center">{{ 'DIGITAL_DOC.PERMISSION_MAX_DOCUMENT' | translate }}</th>
                <th class="py-2 px-3 text-center">{{ 'DIGITAL_DOC.PERMISSION_OFFLINE_DAYS' | translate }}</th>
                <th class="py-2 px-3 text-center">{{ 'DIGITAL_DOC.PERMISSION_RESERVATION_HOURS' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (r of rows(); track r.readerTypeId) {
                <tr class="border-b border-gray-100">
                  <td class="py-2 px-3 text-gray-800">{{ r.name }}</td>
                  <td class="py-2 px-3 text-center">
                    <input type="checkbox" class="rounded border-gray-300" [checked]="r.read" (change)="setRead(r, $any($event.target).checked)" [attr.aria-label]="'DIGITAL_DOC.PERMISSION_READ' | translate" />
                  </td>
                  <td class="py-2 px-3 text-center">
                    <input type="checkbox" class="rounded border-gray-300" [checked]="r.download" (change)="setDownload(r, $any($event.target).checked)" [attr.aria-label]="'DIGITAL_DOC.PERMISSION_DOWNLOAD' | translate" />
                  </td>
                  <td class="py-2 px-3 text-center"><input type="number" min="0" class="input !w-20 text-center" [(ngModel)]="r.maxDocument" [name]="'md' + r.readerTypeId" [attr.aria-label]="'DIGITAL_DOC.PERMISSION_MAX_DOCUMENT' | translate" /></td>
                  <td class="py-2 px-3 text-center"><input type="number" min="0" class="input !w-24 text-center" [(ngModel)]="r.offlineDays" [name]="'od' + r.readerTypeId" [placeholder]="'DIGITAL_DOC.NO_LIMIT' | translate" [attr.aria-label]="'DIGITAL_DOC.PERMISSION_OFFLINE_DAYS' | translate" /></td>
                  <td class="py-2 px-3 text-center"><input type="number" min="1" class="input !w-24 text-center" [(ngModel)]="r.reservationExpireHours" [name]="'rh' + r.readerTypeId" [placeholder]="'DIGITAL_DOC.NO_RESERVATION' | translate" [attr.aria-label]="'DIGITAL_DOC.PERMISSION_RESERVATION_HOURS' | translate" /></td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('DIGITAL_DOC.NO_READER_TYPES' | translate) }}</td></tr>
              }
            </tbody>
          </table>
        </div>
      </div>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="saving() || loading() || !!loadError()" (click)="save()">
          <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'security' }}</span> {{ 'DIGITAL_DOC.PERMISSION_SAVE' | translate }}
        </button>
      </ng-container>
    </app-modal>
  `,
})
export class CollectionPermissions implements OnInit {
  private readonly api = inject(Api);
  private readonly itemsApi = inject(ItemsApi);
  private readonly toastr = inject(ToastrService);

  readonly collectionPublicId = input.required<string>();
  readonly collectionName = input.required<string>();
  readonly closed = output<void>();

  protected readonly rows = signal<PermissionRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  /** Không tải được danh sách loại bạn đọc hoặc quyền hiện có → không cho lưu (lưu rỗng sẽ xoá hết quyền đang có). */
  protected readonly loadError = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected setRead(r: PermissionRow, value: boolean): void {
    r.read = value;
    if (!value) r.download = false; // không đọc được thì không tải được
  }

  protected setDownload(r: PermissionRow, value: boolean): void {
    r.download = value;
    if (value) r.read = true;
  }

  private async load(): Promise<void> {
    try {
      const [types, saved] = await Promise.all([
        this.api.crud<{ id: number; publicId: string; name: string }>('reader-types', 'patron').searchAll(),
        this.itemsApi.collectionPermissions(this.collectionPublicId()),
      ]);
      const byType = new Map(saved.map((s) => [s.readerTypeId, s]));
      const known = new Set(types.map((t) => t.id));
      const row = (id: number, name: string): PermissionRow => {
        const s = byType.get(id);
        return {
          readerTypeId: id, name, saved: !!s, read: s?.read ?? true, download: s?.download ?? false, maxDocument: s?.maxDocument ?? null,
          offlineDays: s?.offlineDays ?? null, reservationExpireHours: s?.reservationExpireHours ?? null,
        };
      };
      // Chính sách của loại bạn đọc không còn trong danh sách vẫn hiện (và được giữ khi lưu) thay vì bị xoá âm thầm.
      this.rows.set([...types.map((t) => row(t.id, t.name)), ...saved.filter((s) => !known.has(s.readerTypeId)).map((s) => row(s.readerTypeId, `#${s.readerTypeId}`))]);
    } catch (e) {
      this.loadError.set(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected async save(): Promise<void> {
    this.saving.set(true);
    try {
      const body: CollectionPermission[] = this.rows()
        .filter((r) => r.saved || !r.read || r.download || r.maxDocument != null || r.offlineDays != null || r.reservationExpireHours != null)
        .map((r) => ({
          readerTypeId: r.readerTypeId, read: r.read || r.download, download: r.download, maxDocument: r.maxDocument || null,
          offlineDays: r.offlineDays || null, reservationExpireHours: r.reservationExpireHours || null,
        }));
      await this.itemsApi.saveCollectionPermissions(this.collectionPublicId(), body);
      this.toastr.success('DIGITAL_DOC.PERMISSION_SAVED');
      this.closed.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }
}
