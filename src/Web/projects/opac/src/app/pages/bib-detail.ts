import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { Library } from '../core/library';
import { Bib, BibDetail as Detail, MarcField, OpacMarc, SearchApi, languageName, searchErrorMessage } from '../core/search';

type Tab = 'info' | 'isbd' | 'marc';

/**
 * Chi tiết tài liệu (monolith: OPAC /book/:publicId): ảnh bìa, thông tin mô tả, ISBD, MARC, tải biểu ghi (.mrc / MARCXML), bản sách ở các
 * kho và tình trạng, tài liệu liên quan.
 */
@Component({
  selector: 'opac-bib-detail',
  imports: [DatePipe, NgTemplateOutlet, RouterLink],
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
              @if (b.coverUrl && !coverBroken()) {
                <img [src]="b.coverUrl" [alt]="'Bìa ' + b.title" referrerpolicy="no-referrer" (error)="coverBroken.set(true)"
                     class="w-24 h-32 sm:w-32 sm:h-44 rounded-lg object-cover bg-slate-100 border border-slate-200 shrink-0" />
              } @else {
                <div class="w-24 h-32 rounded-lg bg-gradient-to-br from-blue-100 to-sky-50 text-blue-500 flex items-center justify-center shrink-0">
                  <span class="material-icons text-4xl">menu_book</span>
                </div>
              }
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

            <div class="mt-6 flex items-end gap-1 border-b border-slate-200 text-sm flex-wrap" role="tablist">
              @for (t of tabs; track t.key) {
                <button type="button" role="tab" [attr.aria-selected]="tab() === t.key" (click)="openTab(t.key)"
                        class="px-3 py-2 -mb-px border-b-2" [class]="tab() === t.key ? 'border-blue-600 text-blue-700 font-medium' : 'border-transparent text-slate-500 hover:text-slate-800'">
                  {{ t.label }}</button>
              }
              <span class="ml-auto pb-1.5 flex gap-3 text-xs">
                <a [href]="api.exportUrl(b.publicId, 'iso2709')" download class="text-blue-600 hover:underline inline-flex items-center gap-0.5" title="Tải biểu ghi ISO 2709">
                  <span class="material-icons text-[16px]">download</span>.mrc</a>
                <a [href]="api.exportUrl(b.publicId, 'marcxml')" download class="text-blue-600 hover:underline inline-flex items-center gap-0.5" title="Tải biểu ghi MARCXML">
                  <span class="material-icons text-[16px]">download</span>MARCXML</a>
              </span>
            </div>

            @switch (tab()) {
              @case ('info') {
                <dl class="mt-4 grid sm:grid-cols-[160px_1fr] gap-x-4 gap-y-2 text-sm">
                  @for (row of rows(); track row[0]) {
                    <dt class="text-slate-500">{{ row[0] }}</dt><dd class="text-slate-800 whitespace-pre-line">{{ row[1] }}</dd>
                  }
                </dl>
                @if (b.summary) {
                  <h2 class="mt-6 font-semibold text-slate-900">Tóm tắt</h2>
                  <p class="mt-2 text-sm text-slate-700 whitespace-pre-line">{{ b.summary }}</p>
                }
              }
              @case ('isbd') {
                @if (marc(); as m) {
                  <div class="mt-4 border border-slate-200 rounded-lg p-4 bg-amber-50/40 text-sm text-slate-800 font-serif space-y-2">
                    @for (p of m.isbd; track $index) { <p [class.indent-8]="$index === 0">{{ p }}</p> }
                    @if (!m.isbd.length) { <p class="text-slate-500">Biểu ghi chưa đủ thông tin để trình bày ISBD.</p> }
                  </div>
                } @else { <ng-container [ngTemplateOutlet]="marcState" /> }
              }
              @case ('marc') {
                @if (marc(); as m) {
                  <div class="mt-4 overflow-x-auto">
                    <table class="w-full text-xs font-mono min-w-[480px]">
                      <tbody>
                        <tr class="border-b border-slate-100"><td class="py-1 pr-3 text-slate-500 align-top">LDR</td><td></td><td class="py-1 whitespace-pre">{{ m.leader }}</td></tr>
                        @for (f of m.fields; track $index) {
                          <tr class="border-b border-slate-100">
                            <td class="py-1 pr-3 text-blue-700 align-top">{{ f.tag }}</td>
                            <td class="py-1 pr-3 text-slate-500 align-top whitespace-pre">{{ indicators(f) }}</td>
                            <td class="py-1 break-all">
                              @if (!f.subfields?.length) { <span class="whitespace-pre-wrap">{{ f.value }}</span> }
                              @for (s of f.subfields ?? []; track $index) { <span class="text-rose-600">&#36;{{ s.code }}</span>{{ s.value }} }
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                } @else { <ng-container [ngTemplateOutlet]="marcState" /> }
              }
            }
            <ng-template #marcState>
              @if (marcError(); as err) { <p class="mt-4 text-sm text-red-700">{{ err }}</p> }
              @else { <p class="mt-4 text-sm text-slate-500">Đang tải...</p> }
            </ng-template>

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
  protected readonly api = inject(SearchApi);
  private readonly title = inject(Title);
  protected readonly library = inject(Library);

  readonly publicId = input.required<string>();

  protected readonly bib = signal<Detail | null>(null);
  protected readonly similar = signal<Bib[]>([]);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly coverBroken = signal(false);
  protected readonly tabs: { key: Tab; label: string }[] = [
    { key: 'info', label: 'Thông tin' },
    { key: 'isbd', label: 'ISBD' },
    { key: 'marc', label: 'MARC' },
  ];
  protected readonly tab = signal<Tab>('info');
  protected readonly marc = signal<OpacMarc | null>(null);
  protected readonly marcError = signal<string | null>(null);

  constructor() {
    effect(() => void this.load(this.publicId()));
  }

  /** ISBD và MARC lấy từ catalog khi bạn đọc mở tab lần đầu. */
  protected openTab(tab: Tab): void {
    this.tab.set(tab);
    const b = this.bib();
    if (tab === 'info' || !b || this.marc()?.publicId === b.publicId) return;
    this.marcError.set(null);
    this.api.marc(b.publicId).then((m) => this.marc.set(m), (e) => this.marcError.set(searchErrorMessage(e)));
  }

  /** Chỉ thị trường dữ liệu, khoảng trắng hiện là # như phiếu MARC. */
  protected indicators(f: MarcField): string {
    return f.subfields?.length ? `${f.ind1 || ' '}${f.ind2 || ' '}`.replaceAll(' ', '#') : '';
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
    this.coverBroken.set(false);
    this.marc.set(null);
    this.tab.set('info');
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
