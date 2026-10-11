import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, Z3950Hit, Z3950SearchRequest, Z3950ServerOption, Z3950ServerResult, errorMessage } from '../../core/api';
import { MarcDrafts } from '../../core/marc-draft';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';

const PAGE_SIZE = 10;

/**
 * Tra cứu Z39.50 / SRU (monolith: /admin/z3950-search, "Thêm sách từ Z3950"): tra song song các thư viện đã cấu hình, xem MARC, nạp bản ghi
 * vào màn biên mục mới để kiểm tra rồi lưu (001/003/005 của thư viện nguồn bỏ đi — hệ thống tự sinh).
 */
@Component({
  selector: 'app-z3950-search',
  imports: [FormsModule, Modal, RouterLink],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">Tra cứu Z39.50</h4>
      @if (session.can('Z3950_CONFIGS:view')) {
        <a routerLink="/z3950-configs" class="btn-secondary !py-1.5 ml-auto"><span class="material-icons text-[18px]">dns</span> Máy chủ Z39.50</a>
      }
    </div>

    <form class="panel" (ngSubmit)="search()">
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        @for (f of fields; track f.key) {
          <div>
            <label class="form-label" [for]="'z-' + f.key">{{ f.label }}</label>
            <input class="input" [id]="'z-' + f.key" [name]="f.key" [(ngModel)]="query[f.key]" [placeholder]="f.placeholder" />
          </div>
        }
      </div>
      <div class="mt-4">
        <div class="form-label">Thư viện</div>
        @if (servers().length) {
          <div class="flex flex-wrap gap-x-5 gap-y-1.5">
            @for (s of servers(); track s.publicId) {
              <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
                <input type="checkbox" class="rounded border-gray-300" [checked]="chosen().has(s.publicId)" (change)="toggleServer(s.publicId)" />
                {{ s.name }}@if (s.groupName) { <span class="text-xs text-gray-400">({{ s.groupName }})</span> }
              </label>
            }
          </div>
        } @else {
          <p class="text-sm text-gray-500">Chưa có máy chủ Z39.50 nào đang hoạt động — thêm ở màn "Máy chủ Z39.50".</p>
        }
      </div>
      <div class="flex justify-end mt-4">
        <button type="submit" class="btn-primary" [disabled]="loading() || !servers().length">
          <span class="material-icons text-[18px]" [class.animate-spin]="loading()">{{ loading() ? 'autorenew' : 'travel_explore' }}</span> Tìm
        </button>
      </div>
    </form>

    @for (r of results(); track r.serverId) {
      <div class="panel">
        <div class="flex items-center gap-3 flex-wrap mb-2">
          <div class="font-medium text-gray-800">{{ r.serverName }}</div>
          @if (r.error) { <span class="badge-danger">{{ r.error }}</span> }
          @else { <span class="text-sm text-gray-500">{{ r.total }} kết quả</span> }
          @if (r.total > r.pageSize) {
            <div class="ml-auto flex items-center gap-2 text-sm">
              <button type="button" class="btn-secondary !py-1 !px-2" [disabled]="r.page <= 1 || paging() === r.serverId" (click)="page(r, r.page - 1)">Trước</button>
              <span>Trang {{ r.page }} / {{ pages(r) }}</span>
              <button type="button" class="btn-secondary !py-1 !px-2" [disabled]="r.page >= pages(r) || paging() === r.serverId" (click)="page(r, r.page + 1)">Sau</button>
            </div>
          }
        </div>
        @if (r.hits.length) {
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse min-w-[720px]">
              <thead><tr><th class="th w-12">#</th><th class="th">Nhan đề</th><th class="th">Tác giả</th><th class="th">Xuất bản</th><th class="th">ISBN</th><th class="th w-28"></th></tr></thead>
              <tbody>
                @for (h of r.hits; track h.position) {
                  <tr class="hover:bg-gray-50/50">
                    <td class="td text-gray-500">{{ h.position }}</td>
                    <td class="td font-medium">{{ h.title ?? '(không nhan đề)' }}</td>
                    <td class="td">{{ h.author ?? '' }}</td>
                    <td class="td">{{ h.publisher ?? '' }}@if (h.year) { , {{ h.year }} }</td>
                    <td class="td font-mono text-[13px]">{{ h.isbn ?? '' }}</td>
                    <td class="td">
                      <div class="flex gap-2 justify-end">
                        <button type="button" class="icon-btn bg-gray-50 text-gray-600 hover:bg-gray-100" title="Xem MARC" (click)="viewing.set({ hit: h, server: r })">
                          <span class="material-icons text-[18px]">visibility</span></button>
                        @if (session.can('CATALOG_BIBS:add')) {
                          <button type="button" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" title="Biên mục từ bản ghi này" (click)="catalogue(h, r)">
                            <span class="material-icons text-[18px]">playlist_add</span></button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    }

    @if (viewing(); as v) {
      <app-modal [title]="v.hit.title ?? 'Bản ghi MARC'" widthClass="max-w-3xl" (closed)="viewing.set(null)">
        <div class="text-xs text-gray-500 mb-2">{{ v.server.serverName }} · {{ v.server.recordSyntax }} · bản ghi {{ v.hit.position }}</div>
        <div class="overflow-x-auto max-h-[60vh]">
          <table class="w-full text-xs font-mono">
            <tbody>
              <tr class="border-b border-gray-100"><td class="py-1 pr-3 text-gray-500">LDR</td><td></td><td class="py-1 whitespace-pre">{{ v.hit.record.leader }}</td></tr>
              @for (f of v.hit.record.fields; track $index) {
                <tr class="border-b border-gray-100">
                  <td class="py-1 pr-3 text-blue-700 align-top">{{ f.tag }}</td>
                  <td class="py-1 pr-3 text-gray-500 align-top whitespace-pre">{{ f.subfields?.length ? ((f.ind1 || '#') + (f.ind2 || '#')).replaceAll(' ', '#') : '' }}</td>
                  <td class="py-1 break-all">
                    @if (!f.subfields?.length) { {{ f.value }} }
                    @for (s of f.subfields ?? []; track $index) { <span class="text-rose-600">&#36;{{ s.code }}</span>{{ s.value }} }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="viewing.set(null)">Đóng</button>
          @if (session.can('CATALOG_BIBS:add')) {
            <button type="button" class="btn-primary" (click)="catalogue(v.hit, v.server)"><span class="material-icons text-[18px]">playlist_add</span> Biên mục từ bản ghi này</button>
          }
        </ng-container>
      </app-modal>
    }
  `,
})
export class Z3950Search implements OnInit {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly drafts = inject(MarcDrafts);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected readonly fields: { key: keyof Z3950SearchRequest; label: string; placeholder: string }[] = [
    { key: 'title', label: 'Nhan đề', placeholder: 'VD: Harry Potter' },
    { key: 'author', label: 'Tác giả', placeholder: 'VD: Rowling' },
    { key: 'isbn', label: 'ISBN', placeholder: 'VD: 9780747532699' },
    { key: 'publisher', label: 'Nhà xuất bản', placeholder: '' },
    { key: 'keyword', label: 'Từ khoá', placeholder: '' },
    { key: 'subject', label: 'Chủ đề', placeholder: '' },
  ];
  protected readonly servers = signal<Z3950ServerOption[]>([]);
  protected readonly chosen = signal(new Set<string>());
  protected readonly results = signal<Z3950ServerResult[]>([]);
  protected readonly loading = signal(false);
  protected readonly paging = signal<string | null>(null);
  protected readonly viewing = signal<{ hit: Z3950Hit; server: Z3950ServerResult } | null>(null);
  protected query: Record<string, string> = {};
  private lastQuery: Z3950SearchRequest = {};

  async ngOnInit(): Promise<void> {
    try {
      const servers = await this.api.z3950Servers();
      this.servers.set(servers);
      this.chosen.set(new Set(servers.map((s) => s.publicId)));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected toggleServer(id: string): void {
    const next = new Set(this.chosen());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.chosen.set(next);
  }

  protected pages(r: Z3950ServerResult): number {
    return Math.max(1, Math.ceil(r.total / r.pageSize));
  }

  protected async search(): Promise<void> {
    const terms = Object.fromEntries(this.fields.map((f) => [f.key, this.query[f.key]?.trim() || null]));
    if (!Object.values(terms).some((v) => v)) {
      this.toastr.warning('Nhập ít nhất một điều kiện tìm.');
      return;
    }
    if (!this.chosen().size) {
      this.toastr.warning('Chọn ít nhất một thư viện.');
      return;
    }
    this.lastQuery = terms;
    this.loading.set(true);
    try {
      this.results.set(await this.api.z3950Search({ ...terms, serverIds: [...this.chosen()], page: 1, pageSize: PAGE_SIZE }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  /** Mỗi thư viện phân trang riêng — chỉ tra lại thư viện đó. */
  protected async page(r: Z3950ServerResult, page: number): Promise<void> {
    this.paging.set(r.serverId);
    try {
      const [next] = await this.api.z3950Search({ ...this.lastQuery, serverIds: [r.serverId], page, pageSize: PAGE_SIZE });
      if (next) this.results.update((all) => all.map((x) => (x.serverId === r.serverId ? next : x)));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.paging.set(null);
    }
  }

  /** Nạp bản ghi vào màn biên mục mới: bỏ 001/003/005 của thư viện nguồn (hệ thống tự sinh MFN, mã đơn vị, thời điểm). */
  protected catalogue(hit: Z3950Hit, server: Z3950ServerResult): void {
    if (server.recordSyntax === 'UNIMARC' && !confirm('Bản ghi UNIMARC sẽ không tự chuyển sang MARC21 — vẫn nạp để biên mục tay?')) return;
    const fields = hit.record.fields.filter((f) => !['001', '003', '005'].includes(f.tag)).map((f) => structuredClone(f));
    this.drafts.set({ leader: hit.record.leader, fields, source: server.serverName });
    this.viewing.set(null);
    void this.router.navigate(['/catalog-bibs', 'new']);
  }
}
