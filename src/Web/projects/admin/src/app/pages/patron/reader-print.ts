import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Barcode } from '../../../../../shared/barcode';
import { Api, CrudPage, Reader, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ReaderCatalogs } from './reader-catalogs';

/** sessionStorage: danh sách publicId bạn đọc cần in (màn bạn đọc ghi trước khi mở trang in). */
export const PRINT_KEY = 'elib.print.readers';
export const MAX_PRINT = 200;

/**
 * In thẻ thư viện (ELIB-LRC: khối in-only của reader.html + directive appBarcode) — trang riêng ngoài khung admin để bản in chỉ có thẻ.
 * Khổ A4 dọc, 10 thẻ CR80 (85,6 × 54 mm) mỗi trang: tên thư viện, ảnh, họ tên in hoa, loại bạn đọc, khoá, lớp, thời hạn, mã vạch Code 128 số thẻ.
 */
@Component({
  selector: 'app-reader-print',
  imports: [Barcode, LDatePipe, RouterLink, TranslatePipe],
  providers: [ReaderCatalogs],
  styles: `
    :host { display: block; background: #f3f4f6; min-height: 100vh; }
    .sheet { display: grid; grid-template-columns: repeat(2, 85.6mm); gap: 4mm 6mm; justify-content: center; padding: 8mm 0; }
    .card { width: 85.6mm; height: 54mm; box-sizing: border-box; background: #fff; border: 0.3mm solid #cbd5e1; border-radius: 3mm;
            overflow: hidden; display: flex; flex-direction: column; break-inside: avoid; font-family: Inter, Arial, sans-serif; }
    .head { background: #1d4ed8; color: #fff; padding: 1.4mm 3mm; display: flex; align-items: center; gap: 2mm; }
    .head img { width: 6mm; height: 6mm; object-fit: contain; background: #fff; border-radius: 1mm; }
    .head .lib { font-size: 2.4mm; line-height: 1.15; flex: 1; min-width: 0; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
    .head .title { font-size: 2.6mm; font-weight: 700; letter-spacing: 0.3mm; }
    .body { flex: 1; display: flex; gap: 2.5mm; padding: 2mm 3mm 1mm; min-height: 0; }
    .photo { width: 19mm; height: 25mm; border: 0.2mm solid #cbd5e1; border-radius: 1mm; object-fit: cover; background: #f1f5f9; flex-shrink: 0;
             display: flex; align-items: center; justify-content: center; color: #cbd5e1; font-size: 10mm; }
    .info { min-width: 0; font-size: 2.5mm; line-height: 1.35; color: #334155; }
    .name { font-size: 3.2mm; font-weight: 700; color: #0f172a; text-transform: uppercase; margin-bottom: 0.6mm; overflow: hidden; display: -webkit-box;
            -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
    .code { padding: 0 3mm 1.6mm; text-align: center; }
    .code .no { font-family: ui-monospace, Consolas, monospace; font-size: 2.6mm; letter-spacing: 0.4mm; }
    @media print {
      @page { size: A4 portrait; margin: 8mm; }
      :host { background: #fff; }
      .no-print { display: none !important; }
      .sheet { padding: 0; }
    }
  `,
  template: `
    <div class="no-print sticky top-0 z-10 bg-white border-b border-gray-200 px-4 py-3 flex items-center gap-3 flex-wrap">
      <a routerLink="/readers" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">arrow_back</span> {{ 'MENU.READERS' | translate }}</a>
      <div class="font-semibold text-gray-800">{{ 'READER.PRINT_CARD' | translate }}</div>
      <span class="text-sm text-gray-500">{{ 'READER.PRINT_COUNT' | translate: { count: readers().length } }}</span>
      <span class="flex-1"></span>
      @if (error()) { <span class="text-sm text-red-600">{{ error() }}</span> }
      <button type="button" class="btn-primary" [disabled]="loading() || !readers().length" (click)="print()">
        <span class="material-icons text-[18px]">print</span> {{ 'READER.PRINT_NOW' | translate }}</button>
    </div>
    @if (loading()) {
      <div class="no-print flex justify-center py-24"><span class="material-icons animate-spin text-blue-600 text-4xl">autorenew</span></div>
    } @else if (!readers().length) {
      <p class="no-print text-center text-gray-500 py-24">{{ 'READER.PRINT_EMPTY' | translate }}</p>
    }
    <div class="sheet">
      @for (r of readers(); track r.publicId) {
        <div class="card" [attr.data-card]="r.cardNo">
          <div class="head">
            @if (logo()) { <img [src]="logo()" alt="" /> }
            <div class="lib">{{ libraryName() }}</div>
            <div class="title">{{ 'READER.LIBRARY_CARD' | translate }}</div>
          </div>
          <div class="body">
            @if (r.photoId && photos()[r.photoId]; as url) { <img class="photo" [src]="url" alt="" /> }
            @else { <div class="photo"><span class="material-icons" style="font-size: 12mm">person</span></div> }
            <div class="info">
              <div class="name">{{ r.fullName }}</div>
              @if (c.name(c.types(), r.readerTypeId); as t) { <div>{{ t }}</div> }
              @if (c.name(c.courses(), r.courseId); as k) { <div>{{ 'READER.COURSE' | translate }}: {{ k }}</div> }
              @if (c.name(c.classes(), r.classId); as l) { <div>{{ 'READER.ACADEMIC_CLASS' | translate }}: {{ l }}</div> }
              <div>{{ (r.issueDate | ldate) ?? '…' }} – {{ r.expireDate ? (r.expireDate | ldate) : ('READER.NO_EXPIRY' | translate) }}</div>
            </div>
          </div>
          <div class="code">
            <elib-barcode [value]="r.cardNo" [height]="30" cssHeight="9mm" />
            <div class="no">{{ r.cardNo }}</div>
          </div>
        </div>
      }
    </div>
  `,
})
export class ReaderPrint implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  protected readonly c = inject(ReaderCatalogs);

  protected readonly readers = signal<Reader[]>([]);
  protected readonly photos = signal<Record<string, string>>({});
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly libraryName = () => this.session.features()?.name ?? '';
  protected readonly logo = () => this.session.features()?.logoUrl ?? null;

  async ngOnInit(): Promise<void> {
    let ids: string[] = [];
    try {
      ids = (JSON.parse(sessionStorage.getItem(PRINT_KEY) ?? '[]') as string[]).slice(0, MAX_PRINT);
    } catch {
      ids = [];
    }
    try {
      await this.c.load();
      if (ids.length) {
        const page = await this.api.crud<Reader>('readers', 'patron').search({ publicIds: ids, pageIndex: 1, pageSize: MAX_PRINT }) as unknown as CrudPage<Reader>;
        // Giữ thứ tự đã chọn trên màn danh sách.
        const order = new Map(ids.map((id, i) => [id, i]));
        this.readers.set([...page.items].sort((a, b) => (order.get(a.publicId) ?? 0) - (order.get(b.publicId) ?? 0)));
        await this.loadPhotos();
      }
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected print(): void {
    window.print();
  }

  private async loadPhotos(): Promise<void> {
    const ids = [...new Set(this.readers().map((r) => r.photoId).filter((id): id is string => !!id))];
    for (let i = 0; i < ids.length; i += 100) {
      try {
        const urls = await this.api.fileUrls(ids.slice(i, i + 100));
        this.photos.update((all) => ({ ...all, ...Object.fromEntries(urls.map((u) => [u.id, u.url])) }));
      } catch {
        /* không lấy được ảnh — in khung trống */
      }
    }
  }
}
