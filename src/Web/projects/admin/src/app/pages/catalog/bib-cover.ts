import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, Bib, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';

/**
 * Ảnh bìa của biểu ghi (monolith: Bib.Images + "Lấy ảnh bìa theo ISBN"): upload ảnh (media, hiện công khai trên OPAC), tra theo ISBN ở
 * Google Books / Open Library, hoặc dán đường dẫn https. Lưu riêng với MARC — đổi ảnh không cần lưu lại biểu ghi.
 */
@Component({
  selector: 'app-bib-cover',
  imports: [FormsModule],
  template: `
    <div class="px-5 py-4 border-t border-gray-100 flex gap-4 items-start flex-wrap">
      @if (preview() ?? bib().coverUrl; as url) {
        <img [src]="url" alt="Ảnh bìa" referrerpolicy="no-referrer" class="w-24 h-32 object-cover rounded border border-gray-200 bg-gray-50" />
      } @else {
        <div class="w-24 h-32 rounded border border-dashed border-gray-300 text-gray-400 flex flex-col items-center justify-center text-xs">
          <span class="material-icons">image</span>Chưa có bìa</div>
      }
      <div class="flex-1 min-w-[260px] space-y-2">
        <div class="font-medium text-gray-800">Ảnh bìa</div>
        @if (preview(); as url) {
          <div class="text-sm text-gray-600">Ảnh tìm được theo ISBN — kiểm tra đúng sách rồi bấm dùng.</div>
          <div class="flex gap-2">
            <button type="button" class="btn-primary !py-1.5" [disabled]="busy()" (click)="save(url)">Dùng ảnh này</button>
            <button type="button" class="btn-secondary !py-1.5" (click)="preview.set(null)">Bỏ qua</button>
          </div>
        } @else if (editable()) {
          <div class="flex gap-2 flex-wrap">
            <label class="btn-secondary !py-1.5 cursor-pointer" [class.opacity-50]="busy()">
              <span class="material-icons text-[18px]">upload</span> Tải ảnh lên
              <input type="file" class="hidden" accept="image/png,image/jpeg,image/webp" [disabled]="busy()" (change)="upload($event)" />
            </label>
            <button type="button" class="btn-secondary !py-1.5" [disabled]="busy() || !isbn()" (click)="lookup()"
                    [title]="isbn() ? 'Tìm ở Google Books, Open Library' : 'Biểu ghi chưa có ISBN (020$a)'">
              <span class="material-icons text-[18px]">travel_explore</span> Tìm theo ISBN</button>
            @if (bib().coverUrl) {
              <button type="button" class="btn-outline-danger" [disabled]="busy()" (click)="save(null)">Bỏ ảnh bìa</button>
            }
          </div>
          <div class="flex gap-2">
            <input class="input !py-1.5" placeholder="Hoặc dán đường dẫn ảnh https://..." [(ngModel)]="url" aria-label="Đường dẫn ảnh bìa" />
            <button type="button" class="btn-secondary !py-1.5" [disabled]="busy() || !url.trim()" (click)="save(url.trim())">Lưu</button>
          </div>
          <div class="text-xs text-gray-500">PNG, JPG hoặc WebP, tối đa 2 MB. Ảnh hiện trên trang tra cứu OPAC.</div>
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
      if (!uploaded.url) throw new Error('Không nhận được đường dẫn ảnh.');
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
      if (found.url) this.preview.set(found.url);
      else this.toastr.warning(`Không tìm thấy ảnh bìa cho ISBN ${isbn}.`);
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
      this.toastr.success(url ? 'Đã cập nhật ảnh bìa.' : 'Đã bỏ ảnh bìa.');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
