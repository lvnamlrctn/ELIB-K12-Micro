import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { Library } from '../core/library';
import { Bib, BibDetail as Detail, SearchApi, languageName } from '../core/search';

/** Chi tiết tài liệu (monolith: OPAC /book/:publicId): thông tin mô tả, bản sách ở các kho và tình trạng, tài liệu liên quan. */
@Component({
  selector: 'opac-bib-detail',
  imports: [DatePipe, RouterLink],
  template: `
    <div class="max-w-6xl mx-auto px-4 py-6">
      <a routerLink="/tim-kiem" class="text-sm text-blue-600 hover:underline inline-flex items-center gap-1">
        <span class="material-icons text-[18px]">arrow_back</span> Tra cứu</a>

      @if (loading()) {
        <div class="flex justify-center py-24"><span class="material-icons animate-spin text-blue-600 text-4xl">autorenew</span></div>
      } @else if (notFound()) {
        <div class="text-center py-24 text-slate-600">
          <span class="material-icons text-6xl text-slate-300">find_in_page</span>
          <p class="mt-3">Không tìm thấy tài liệu (có thể đã bị ẩn hoặc xoá).</p>
        </div>
      } @else if (bib(); as b) {
        <div class="mt-4 grid gap-6 lg:grid-cols-[1fr_320px]">
          <article class="bg-white border border-slate-200 rounded-xl p-6">
            <div class="flex gap-5">
              <div class="w-24 h-32 rounded-lg bg-gradient-to-br from-blue-100 to-sky-50 text-blue-500 flex items-center justify-center shrink-0">
                <span class="material-icons text-4xl">menu_book</span>
              </div>
              <div class="min-w-0">
                <h1 class="text-xl md:text-2xl font-bold text-slate-900">{{ b.title }}</h1>
                @if (b.author) { <div class="mt-1 text-slate-700">{{ b.author }}</div> }
                <div class="mt-3 flex gap-2 flex-wrap text-xs">
                  @if (b.materialType) { <span class="px-2 py-0.5 rounded bg-slate-100 text-slate-600">{{ b.materialType }}</span> }
                  @if (b.copies === 0) { <span class="px-2 py-0.5 rounded bg-slate-100 text-slate-500">Chưa có bản phục vụ</span> }
                  @else if (b.available > 0) { <span class="px-2 py-0.5 rounded bg-green-50 text-green-700">Còn {{ b.available }}/{{ b.copies }} bản</span> }
                  @else { <span class="px-2 py-0.5 rounded bg-amber-50 text-amber-700">Đã mượn hết</span> }
                </div>
              </div>
            </div>

            <dl class="mt-6 grid sm:grid-cols-[160px_1fr] gap-x-4 gap-y-2 text-sm">
              @for (row of rows(); track row[0]) {
                <dt class="text-slate-500">{{ row[0] }}</dt><dd class="text-slate-800 whitespace-pre-line">{{ row[1] }}</dd>
              }
            </dl>
            @if (b.summary) {
              <h2 class="mt-6 font-semibold text-slate-900">Tóm tắt</h2>
              <p class="mt-2 text-sm text-slate-700 whitespace-pre-line">{{ b.summary }}</p>
            }

            <h2 class="mt-8 font-semibold text-slate-900">Bản sách ({{ b.copies }})</h2>
            @if (b.holdings.length) {
              <div class="mt-3 overflow-x-auto">
                <table class="w-full text-sm min-w-[420px]">
                  <thead><tr class="text-left text-slate-500 border-b border-slate-200">
                    <th class="py-2 pr-3 font-medium">Số ĐKCB</th><th class="py-2 pr-3 font-medium">Kho</th><th class="py-2 font-medium">Tình trạng</th>
                  </tr></thead>
                  <tbody>
                    @for (c of b.holdings; track c.barcode) {
                      <tr class="border-b border-slate-100">
                        <td class="py-2 pr-3 font-mono">{{ c.barcode }}</td>
                        <td class="py-2 pr-3">{{ c.storeName ?? '—' }}</td>
                        <td class="py-2">
                          <span [class]="c.status === 'available' ? 'text-green-700' : c.status === 'on-loan' ? 'text-amber-700' : 'text-slate-500'">{{ c.statusName }}</span>
                          @if (c.dueAt) { <span class="text-slate-500"> — hạn trả {{ c.dueAt | date: 'dd/MM/yyyy' }}</span> }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            } @else {
              <p class="mt-2 text-sm text-slate-500">Thư viện chưa có bản sách phục vụ cho tài liệu này.</p>
            }
          </article>

          <aside class="space-y-4">
            @if (library.has('CIRCULATION')) {
              <div class="bg-white border border-slate-200 rounded-xl p-4 text-sm text-slate-600">
                <div class="font-medium text-slate-900 mb-1">Mượn tài liệu</div>
                Mang thẻ bạn đọc đến quầy để mượn, hoặc nhờ thủ thư đặt giữ khi sách đã mượn hết. Đặt mượn trực tuyến sẽ có khi mở đăng nhập bạn đọc.
              </div>
            }
            @if (similar().length) {
              <div class="bg-white border border-slate-200 rounded-xl p-4">
                <div class="font-medium text-slate-900 mb-3">Tài liệu liên quan</div>
                <ul class="space-y-3">
                  @for (s of similar(); track s.publicId) {
                    <li>
                      <a [routerLink]="['/tai-lieu', s.publicId]" class="text-sm font-medium text-slate-800 hover:text-blue-700">{{ s.title }}</a>
                      <div class="text-xs text-slate-500">{{ s.author ?? '' }}@if (s.publishYear) { · {{ s.publishYear }} }</div>
                    </li>
                  }
                </ul>
              </div>
            }
          </aside>
        </div>
      }
    </div>
  `,
})
export class BibDetail {
  private readonly api = inject(SearchApi);
  private readonly title = inject(Title);
  protected readonly library = inject(Library);

  readonly publicId = input.required<string>();

  protected readonly bib = signal<Detail | null>(null);
  protected readonly similar = signal<Bib[]>([]);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  constructor() {
    effect(() => void this.load(this.publicId()));
  }

  protected rows(): [string, string][] {
    const b = this.bib();
    if (!b) return [];
    const publication = [b.publishPlace, b.publisher, b.publishYear].filter(Boolean).join(' : ');
    const all: [string, string | null][] = [
      ['Tác giả khác', b.otherAuthors],
      ['Xuất bản', publication || null],
      ['Lần xuất bản', b.edition],
      ['Mô tả vật lý', b.physicalDescription],
      ['Tùng thư', b.series],
      ['ISBN', b.isbns],
      ['Phân loại', [b.ddc, b.cutter].filter(Boolean).join(' ') || null],
      ['Ngôn ngữ', languageName(b.language) || null],
      ['Từ khoá', b.keywords],
    ];
    return all.filter((r): r is [string, string] => !!r[1]);
  }

  private async load(publicId: string): Promise<void> {
    this.loading.set(true);
    this.notFound.set(false);
    this.similar.set([]);
    try {
      const bib = await this.api.detail(publicId);
      this.bib.set(bib);
      this.title.setTitle(`${bib.title} — ${this.library.name()}`);
      this.api.similar(publicId).then((s) => this.similar.set(s), () => this.similar.set([]));
    } catch {
      this.bib.set(null);
      this.notFound.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
