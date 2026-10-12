import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, Bib, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';

/**
 * Ảnh bìa của biểu ghi (monolith: Bib.Images + "Lấy ảnh bìa theo ISBN"): upload ảnh, tra theo ISBN ở Google Books / Open Library, hoặc dán
 * đường dẫn ảnh. Ảnh ngoài được media lấy về kho của hệ thống trước (CSP chỉ cho hiện ảnh cùng origin; bạn đọc không gọi ra bên thứ ba).
 * Lưu riêng với MARC — đổi ảnh không cần lưu lại biểu ghi.
 */
@Component({
  selector: 'app-bib-cover',
  imports: [FormsModule, TranslatePipe],
  template: `
    <div class="px-5 py-4 border-t border-gray-100 flex gap-4 items-start flex-wrap">
      @if (preview() ?? bib().coverUrl; as url) {
        <img [src]="url" [alt]="'BIB_COVER.ALT' | translate" referrerpolicy="no-referrer" class="w-24 h-32 object-cover rounded border border-gray-200 bg-gray-50" />
      } @else {
        <div class="w-24 h-32 rounded border border-dashed border-gray-300 text-gray-400 flex flex-col items-center justify-center text-xs">
          <span class="material-icons">image</span>{{ 'BIB_COVER.NONE' | translate }}</div>
      }
      <div class="flex-1 min-w-[260px] space-y-2">
        <div class="font-medium text-gray-800">{{ 'BIB_COVER.TITLE' | translate }}</div>
        @if (preview(); as url) {
          <div class="text-sm text-gray-600">{{ 'BIB_COVER.FOUND_HINT' | translate }}</div>
          <div class="flex gap-2">
            <button type="button" class="btn-primary !py-1.5" [disabled]="busy()" (click)="save(url)">{{ 'BIB_COVER.USE' | translate }}</button>
            <button type="button" class="btn-secondary !py-1.5" (click)="preview.set(null)">{{ 'BIB_COVER.SKIP' | translate }}</button>
          </div>
        } @else if (editable()) {
          <div class="flex gap-2 flex-wrap">
            <label class="btn-secondary !py-1.5 cursor-pointer" [class.opacity-50]="busy()">
              <span class="material-icons text-[18px]">upload</span> {{ 'BIB_COVER.UPLOAD' | translate }}
              <input type="file" class="hidden" accept="image/png,image/jpeg,image/webp" [disabled]="busy()" (change)="upload($event)" />
            </label>
            <button type="button" class="btn-secondary !py-1.5" [disabled]="busy() || !isbn()" (click)="lookup()"
                    [title]="(isbn() ? 'BIB_COVER.FIND_TIP' : 'BIB_COVER.NO_ISBN') | translate">
              <span class="material-icons text-[18px]">travel_explore</span> {{ 'BIB_COVER.FIND' | translate }}</button>
            @if (bib().coverUrl) {
              <button type="button" class="btn-outline-danger" [disabled]="busy()" (click)="save(null)">{{ 'BIB_COVER.REMOVE' | translate }}</button>
            }
          </div>
          <div class="flex gap-2">
            <input class="input !py-1.5" [placeholder]="'BIB_COVER.URL_PH' | translate" [(ngModel)]="url" [attr.aria-label]="'BIB_COVER.URL_LABEL' | translate" />
            <button type="button" class="btn-secondary !py-1.5" [disabled]="busy() || !url.trim()" (click)="saveFromUrl()">{{ 'COMMON.SAVE' | translate }}</button>
          </div>
          <div class="text-xs text-gray-500">{{ 'BIB_COVER.HINT' | translate }}</div>
        }
      </div>
    </div>
  `,
})
export class BibCover {
  readonly bib = input.required<Bib>();
  readonly editable = input(false);
  readonly changed = output<Bib>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly busy = signal(false);
  protected readonly preview = signal<string | null>(null);
  protected url = '';

  /** ISBN đầu tiên đã lưu của biểu ghi. */
  protected isbn(): string | null {
    return this.bib().isbns[0] ?? null;
  }

  protected async upload(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.busy.set(true);
    try {
      const uploaded = await this.api.upload('bib-cover', file);
      if (!uploaded.url) throw new Error('BIB_COVER.NO_URL');
      await this.save(uploaded.url);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async lookup(): Promise<void> {
    const isbn = this.isbn();
    if (!isbn) return;
    this.busy.set(true);
    try {
      const found = await this.api.lookupCover(isbn);
      if (!found.url) {
        this.toastr.warning('BIB_COVER.NOT_FOUND', { isbn });
        return;
      }
      const stored = await this.api.importMedia('bib-cover', found.url);
      if (stored.url) this.preview.set(stored.url);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Ảnh theo đường dẫn: media lấy về kho rồi mới gắn vào biểu ghi. */
  protected async saveFromUrl(): Promise<void> {
    this.busy.set(true);
    try {
      const stored = await this.api.importMedia('bib-cover', this.url.trim());
      if (!stored.url) throw new Error('BIB_COVER.NO_URL');
      await this.save(stored.url);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async save(url: string | null): Promise<void> {
    this.busy.set(true);
    try {
      const saved = await this.api.setBibCover(this.bib().publicId, url);
      this.preview.set(null);
      this.url = '';
      this.changed.emit(saved);
      this.toastr.success(url ? 'BIB_COVER.UPDATED' : 'BIB_COVER.REMOVED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
