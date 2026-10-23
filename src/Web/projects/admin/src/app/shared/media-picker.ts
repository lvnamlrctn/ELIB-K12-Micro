import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, MediaFile, errorMessage } from '../core/api';
import { ToastrService } from './toastr';
import { Loading } from './ui';

/**
 * Thư viện ảnh công khai của đơn vị (media, mục đích công khai như cms-image): chọn lại ảnh đã upload hoặc upload ảnh mới.
 * Phát ra URL công khai của ảnh để lưu vào tin, banner, album… (portal chỉ giữ URL).
 */
@Component({
  selector: 'app-media-picker',
  imports: [Loading, TranslatePipe],
  template: `
    <div class="fixed inset-0 z-[120] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4" role="dialog" aria-modal="true">
      <div class="bg-white rounded-2xl shadow-2xl w-full max-w-3xl max-h-[85vh] flex flex-col overflow-hidden">
        <div class="flex items-center justify-between gap-3 px-5 py-3 border-b border-gray-100">
          <h3 class="font-semibold text-gray-800">{{ 'MEDIA_PICKER.TITLE' | translate }}</h3>
          <div class="flex items-center gap-2">
            <label class="btn-secondary cursor-pointer">
              <span class="material-icons text-[18px]">upload</span> {{ 'MEDIA_PICKER.UPLOAD' | translate }}
              <input type="file" class="hidden" [accept]="accept()" (change)="upload($event)" />
            </label>
            <button type="button" class="icon-btn" [attr.aria-label]="'COMMON.CLOSE' | translate" (click)="closed.emit()"><span class="material-icons">close</span></button>
          </div>
        </div>
        <div class="relative flex-1 overflow-auto p-4">
          @if (busy()) { <app-loading /> }
          @if (error(); as err) { <p class="text-red-600 text-sm mb-2" role="alert">{{ err }}</p> }
          <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
            @for (f of files(); track f.id) {
              <button type="button" class="group border border-gray-200 rounded-lg overflow-hidden hover:border-blue-400 focus:outline-none focus:ring-2 focus:ring-blue-500" (click)="pick(f)">
                <img [src]="f.url" [alt]="f.fileName" loading="lazy" class="w-full h-28 object-cover bg-gray-50" />
                <span class="block text-xs text-gray-500 truncate px-2 py-1">{{ f.fileName }}</span>
              </button>
            } @empty {
              @if (!busy()) { <p class="col-span-full text-center text-gray-500 py-10">{{ 'MEDIA_PICKER.EMPTY' | translate }}</p> }
            }
          </div>
        </div>
        <div class="flex items-center justify-between px-5 py-2 border-t border-gray-100 text-sm text-gray-500">
          <span>{{ total() }}</span>
          <div class="flex gap-2">
            <button type="button" class="btn-secondary" [disabled]="page() <= 1 || busy()" (click)="go(page() - 1)">‹</button>
            <button type="button" class="btn-secondary" [disabled]="page() * size >= total() || busy()" (click)="go(page() + 1)">›</button>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class MediaPicker implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  /** Mục đích công khai của media (cms-image…). */
  readonly purpose = input('cms-image');
  readonly accept = input('image/png,image/jpeg,image/webp,image/gif');
  readonly picked = output<MediaFile>();
  readonly closed = output<void>();

  protected readonly files = signal<MediaFile[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly size = 24;

  ngOnInit(): void {
    void this.load();
  }

  protected go(page: number): void {
    this.page.set(page);
    void this.load();
  }

  protected pick(file: MediaFile): void {
    this.picked.emit(file);
  }

  protected async upload(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      this.picked.emit(await this.api.upload(this.purpose(), file));
    } catch (e) {
      this.error.set(errorMessage(e));
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const page = await this.api.mediaLibrary(this.purpose(), this.page(), this.size);
      this.files.set(page.items.filter((f) => !!f.url));
      this.total.set(page.total);
    } catch (e) {
      this.error.set(errorMessage(e));
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
