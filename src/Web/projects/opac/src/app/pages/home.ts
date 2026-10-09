import { Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Library } from '../core/library';

interface Service { module: string; title: string; text: string; icon: string; link: string; }

/** Dịch vụ OPAC theo phân hệ đơn vị đã mua — phân hệ chưa mua không hiện (features của service tenant). */
const SERVICES: Service[] = [
  { module: 'SEARCH', title: 'Tra cứu tài liệu', text: 'Tìm sách, báo, tạp chí theo nhan đề, tác giả, chủ đề.', icon: 'search', link: '/tim-kiem' },
  { module: 'DIGITAL', title: 'Thư viện số', text: 'Đọc sách điện tử, bài giảng, tài liệu số của trường.', icon: 'menu_book', link: '/thu-vien-so' },
  { module: 'CIRCULATION', title: 'Mượn trả', text: 'Xem sách đang mượn, hạn trả, gia hạn trực tuyến.', icon: 'swap_horiz', link: '/tai-khoan' },
  { module: 'SPACE', title: 'Đặt phòng, chỗ ngồi', text: 'Đặt phòng đọc, phòng học nhóm.', icon: 'meeting_room', link: '/dat-phong' },
  { module: 'AI', title: 'Trợ lý AI', text: 'Hỏi đáp, gợi ý tài liệu phù hợp.', icon: 'smart_toy', link: '/tro-ly' },
  { module: 'PORTAL', title: 'Tin tức, sự kiện', text: 'Hoạt động và thông báo của thư viện.', icon: 'campaign', link: '/tin-tuc' },
];

const CONTACT_KEYS = ['LIBRARY_ADDR', 'OPENHOUR', 'LIBRARY_TEL', 'LIBRARY_EMAIL'];

@Component({
  selector: 'opac-home',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="bg-gradient-to-br from-blue-700 via-blue-600 to-sky-500 text-white">
      <div class="max-w-6xl mx-auto px-4 py-16 md:py-24">
        <h1 class="text-3xl md:text-4xl font-bold leading-tight max-w-3xl">{{ library.name() }}</h1>
        <p class="mt-3 text-blue-100 max-w-2xl">Tra cứu tài liệu, đọc sách số và sử dụng dịch vụ thư viện mọi lúc, mọi nơi.</p>
        @if (library.has('SEARCH')) {
          <form (ngSubmit)="search()" class="mt-8 flex max-w-2xl bg-white rounded-xl shadow-lg overflow-hidden">
            <span class="material-icons text-slate-400 self-center pl-4">search</span>
            <input name="q" [(ngModel)]="q" class="flex-1 px-3 py-4 text-slate-800 outline-none min-w-0"
                   placeholder="Nhập nhan đề, tác giả, chủ đề..." aria-label="Từ khoá tra cứu" />
            <button type="submit" class="px-6 bg-blue-600 hover:bg-blue-700 font-medium">Tìm</button>
          </form>
        }
      </div>
    </section>

    <section class="max-w-6xl mx-auto px-4 py-12">
      <h2 class="text-xl font-semibold text-slate-900 mb-6">Dịch vụ của thư viện</h2>
      @if (services().length) {
        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          @for (s of services(); track s.module) {
            <a [routerLink]="s.link" class="group bg-white rounded-xl border border-slate-200 p-5 hover:border-blue-300 hover:shadow-md transition">
              <span class="w-11 h-11 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center group-hover:bg-blue-600 group-hover:text-white transition">
                <span class="material-icons">{{ s.icon }}</span>
              </span>
              <div class="mt-4 font-semibold text-slate-900">{{ s.title }}</div>
              <p class="mt-1 text-sm text-slate-500">{{ s.text }}</p>
            </a>
          }
        </div>
      } @else {
        <p class="text-slate-500">Thư viện chưa mở dịch vụ trực tuyến nào.</p>
      }
    </section>

    @if (hasContact()) {
      <section class="max-w-6xl mx-auto px-4 pb-16">
        <div class="bg-white rounded-xl border border-slate-200 p-6 grid gap-4 md:grid-cols-3 text-sm">
          @if (p()['LIBRARY_ADDR']; as v) {
            <div><div class="text-slate-500 mb-1">Địa chỉ</div><div class="font-medium">{{ v }}</div></div>
          }
          @if (p()['OPENHOUR']; as v) {
            <div><div class="text-slate-500 mb-1">Giờ mở cửa</div><div class="font-medium whitespace-pre-line">{{ v }}</div></div>
          }
          @if (p()['LIBRARY_TEL'] || p()['LIBRARY_EMAIL']) {
            <div>
              <div class="text-slate-500 mb-1">Liên hệ</div>
              <div class="font-medium">{{ p()['LIBRARY_TEL'] }}</div>
              <div>{{ p()['LIBRARY_EMAIL'] }}</div>
            </div>
          }
        </div>
      </section>
    }
  `,
})
export class Home {
  protected readonly library = inject(Library);
  private readonly router = inject(Router);
  protected readonly p = this.library.parameters;
  protected q = '';

  protected readonly services = computed(() => SERVICES.filter((s) => this.library.has(s.module)));
  protected readonly hasContact = computed(() => CONTACT_KEYS.some((k) => this.p()[k]));

  protected search(): void {
    void this.router.navigate(['/tim-kiem'], { queryParams: { q: this.q.trim() || null } });
  }
}
