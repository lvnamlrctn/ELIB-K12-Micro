import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { MediaLibraryService } from '../../../services/cms/media-library.service';
import { MediaFile } from '../../../models/cms/media-file';
import { resolveMediaUrl } from '../../utils/media-url';

@Component({
  selector: 'app-media-library-picker',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, TranslateModule],
  templateUrl: './media-library-picker.html'
})
export class MediaLibraryPickerComponent {
  private mediaLibraryService = inject(MediaLibraryService);

  @Output() picked = new EventEmitter<string>();

  isOpen = signal(false);
  isLoading = signal(false);
  items = signal<MediaFile[]>([]);
  keyword = signal('');
  page = signal(1);
  total = signal(0);
  pageSize = 24;

  resolveUrl = resolveMediaUrl;

  open(): void {
    this.keyword.set('');
    this.page.set(1);
    this.isOpen.set(true);
    this.load();
  }

  close(): void {
    this.isOpen.set(false);
  }

  search(): void {
    this.page.set(1);
    this.load();
  }

  prevPage(): void {
    if (this.page() <= 1) return;
    this.page.set(this.page() - 1);
    this.load();
  }

  nextPage(): void {
    if (this.page() * this.pageSize >= this.total()) return;
    this.page.set(this.page() + 1);
    this.load();
  }

  private load(): void {
    this.isLoading.set(true);
    this.mediaLibraryService.list({ keyword: this.keyword(), page: this.page(), pageSize: this.pageSize }).subscribe(res => {
      this.items.set(res.items);
      this.total.set(res.total);
      this.isLoading.set(false);
    });
  }

  choose(file: MediaFile): void {
    this.picked.emit(resolveMediaUrl(file.path));
    this.close();
  }
}
