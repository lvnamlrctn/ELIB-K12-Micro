import { NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Msg, SearchApi, Z3950Hit, Z3950Request, Z3950Result, Z3950Server, searchErrorMessage } from '../core/search';

const PAGE_SIZE = 10;

/**
 * Tra cứu liên thư viện (monolith: OPAC tìm nâng cao — tab Z39.50, /bookz3950/:id): tìm tài liệu ở thư viện khác qua Z39.50/SRU, các thư
 * viện do quản trị bật cho OPAC. Mỗi thư viện một danh sách và phân trang riêng; bấm một kết quả để xem đủ thông tin MARC.
 */
@Component({
  selector: 'opac-z3950',
  imports: [FormsModule, NgTemplateOutlet, RouterLink, TranslatePipe],
  template: `
    <section class="bg-white border-b border-slate-200">
      <div class="max-w-6xl mx-auto px-4 py-6">
        <a routerLink="/tim-kiem" class="text-sm text-blue-600 hover:underline inline-flex items-center gap-1">
          <span class="material-icons text-[18px]">arrow_back</span> {{ 'OPAC.Z3950.BACK' | translate }}</a>
        <h1 class="mt-3 text-xl font-bold text-slate-900">{{ 'OPAC.Z3950.TITLE' | translate }}</h1>
        <p class="text-sm text-slate-600">{{ 'OPAC.Z3950.SUBTITLE' | translate }}</p>

        @if (loaded() && !servers().length) {
          <p class="mt-4 text-sm text-slate-500">{{ 'OPAC.Z3950.NOT_ENABLED' | translate }}</p>
        } @else {
          <form (ngSubmit)="search()" class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            @for (f of fields; track f.key) {
              <div>
                <label class="block text-xs text-slate-500 mb-1" [for]="'z-' + f.key">{{ f.label | translate }}</label>
                <input [id]="'z-' + f.key" [name]="f.key" [(ngModel)]="query[f.key]" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" />
              </div>
            }
            <div class="sm:col-span-2 lg:col-span-4 flex flex-wrap items-center gap-x-5 gap-y-2">
              @for (s of servers(); track s.publicId) {
                <label class="flex items-center gap-2 text-sm text-slate-700">
                  <input type="checkbox" class="rounded border-slate-300" [checked]="chosen().has(s.publicId)" (change)="toggle(s.publicId)" /> {{ s.name }}
                </label>
              }
              <button type="submit" [disabled]="loading()" class="ml-auto px-5 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium disabled:opacity-50">
                {{ (loading() ? 'OPAC.SEARCH.SEARCHING' : 'OPAC.SEARCH.SEARCH_BTN') | translate }}</button>
            </div>
          </form>
        }
      </div>
    </section>

    <section class="max-w-6xl mx-auto px-4 py-6 space-y-5">
      @if (error(); as e) { <div class="bg-red-50 text-red-700 rounded-lg px-4 py-3 text-sm">{{ e.key | translate: e.params }}</div> }
      @for (r of results(); track r.serverId) {
        <div class="bg-white border border-slate-200 rounded-xl">
          <div class="px-4 py-3 border-b border-slate-100 flex items-center gap-3 flex-wrap">
            <div class="font-semibold text-slate-900">{{ r.serverName }}</div>
            @if (r.error) { <span class="text-sm text-red-600">{{ r.error }}</span> }
            @else { <span class="text-sm text-slate-500">{{ 'OPAC.Z3950.RESULTS_N' | translate: { count: r.total } }}</span> }
            @if (r.total > r.pageSize) {
              <span class="ml-auto flex items-center gap-2 text-sm">
                <button type="button" class="px-3 py-1 rounded-lg border border-slate-200 disabled:opacity-40" [disabled]="r.page <= 1 || paging() === r.serverId" (click)="go(r, r.page - 1)">{{ 'COMMON.PREV' | translate }}</button>
                <span class="text-slate-600">{{ r.page }} / {{ pages(r) }}</span>
                <button type="button" class="px-3 py-1 rounded-lg border border-slate-200 disabled:opacity-40" [disabled]="r.page >= pages(r) || paging() === r.serverId" (click)="go(r, r.page + 1)">{{ 'COMMON.NEXT' | translate }}</button>
              </span>
            }
          </div>
          <ul class="divide-y divide-slate-100">
            @for (h of r.hits; track h.position) {
              <li class="px-4 py-3">
                <button type="button" class="text-left w-full" (click)="toggleOpen(r.serverId + h.position)" [attr.aria-expanded]="open() === r.serverId + h.position">
                  <div class="font-medium text-slate-900 hover:text-blue-700">{{ h.title ?? ('OPAC.Z3950.NO_TITLE' | translate) }}</div>
                  <div class="text-sm text-slate-600">
                    {{ h.author ?? ('OPAC.COMMON.UNKNOWN_AUTHOR' | translate) }}@if (h.publisher) { · {{ h.publisher }} }@if (h.year) { · {{ h.year }} }@if (h.isbn) { · ISBN {{ h.isbn }} }
                  </div>
                </button>
                @if (open() === r.serverId + h.position) { <ng-container [ngTemplateOutlet]="marc" [ngTemplateOutletContext]="{ $implicit: h }" /> }
              </li>
            }
          </ul>
        </div>
      }
    </section>

    <ng-template #marc let-h>
      <div class="mt-3 overflow-x-auto">
        <table class="w-full text-xs font-mono min-w-[480px]">
          <tbody>
            @for (f of asHit(h).record.fields; track $index) {
              <tr class="border-b border-slate-100">
                <td class="py-1 pr-3 text-blue-700 align-top">{{ f.tag }}</td>
                <td class="py-1 break-all">
                  @if (!f.subfields?.length) { {{ f.value }} }
                  @for (s of f.subfields ?? []; track $index) { <span class="text-rose-600">&#36;{{ s.code }}</span>{{ s.value }} }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </ng-template>
  `,
})
export class Z3950 implements OnInit {
  private readonly api = inject(SearchApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly fields: { key: 'title' | 'author' | 'isbn' | 'keyword'; label: string }[] = [
    { key: 'title', label: 'OPAC.SEARCH.F_TITLE' },
    { key: 'author', label: 'OPAC.SEARCH.F_AUTHOR' },
    { key: 'isbn', label: 'OPAC.SEARCH.F_ISBN' },
    { key: 'keyword', label: 'OPAC.SEARCH.F_KEYWORD' },
  ];
  protected readonly servers = signal<Z3950Server[]>([]);
  protected readonly chosen = signal(new Set<string>());
  protected readonly loaded = signal(false);
  protected readonly loading = signal(false);
  protected readonly paging = signal<string | null>(null);
  protected readonly results = signal<Z3950Result[]>([]);
  protected readonly error = signal<Msg | null>(null);
  protected readonly open = signal<string | null>(null);
  protected query: Record<string, string> = {};
  private last: Omit<Z3950Request, 'page' | 'pageSize'> = {};

  async ngOnInit(): Promise<void> {
    this.query['title'] = this.route.snapshot.queryParamMap.get('q') ?? '';
    try {
      const servers = await this.api.z3950Servers();
      this.servers.set(servers);
      this.chosen.set(new Set(servers.map((s) => s.publicId)));
    } catch (e) {
      this.error.set(searchErrorMessage(e));
    } finally {
      this.loaded.set(true);
    }
    if (this.query['title'] && this.servers().length) await this.search();
  }

  protected asHit(h: unknown): Z3950Hit {
    return h as Z3950Hit;
  }

  protected toggle(id: string): void {
    const next = new Set(this.chosen());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.chosen.set(next);
  }

  protected toggleOpen(key: string): void {
    this.open.set(this.open() === key ? null : key);
  }

  protected pages(r: Z3950Result): number {
    return Math.max(1, Math.ceil(r.total / r.pageSize));
  }

  protected async search(): Promise<void> {
    const terms = Object.fromEntries(this.fields.map((f) => [f.key, this.query[f.key]?.trim() || null]));
    if (!Object.values(terms).some((v) => v)) {
      this.error.set({ key: 'OPAC.Z3950.NEED_TERM' });
      return;
    }
    if (!this.chosen().size) {
      this.error.set({ key: 'OPAC.Z3950.NEED_SERVER' });
      return;
    }
    this.last = { ...terms, serverIds: [...this.chosen()] };
    this.loading.set(true);
    this.error.set(null);
    try {
      this.results.set(await this.api.z3950Search({ ...this.last, page: 1, pageSize: PAGE_SIZE }));
    } catch (e) {
      this.error.set(searchErrorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected async go(r: Z3950Result, page: number): Promise<void> {
    this.paging.set(r.serverId);
    try {
      const [next] = await this.api.z3950Search({ ...this.last, serverIds: [r.serverId], page, pageSize: PAGE_SIZE });
      if (next) this.results.update((all) => all.map((x) => (x.serverId === r.serverId ? next : x)));
    } catch (e) {
      this.error.set(searchErrorMessage(e));
    } finally {
      this.paging.set(null);
    }
  }
}
