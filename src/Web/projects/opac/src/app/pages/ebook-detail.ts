import { Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  DigitalApi, DigitalItemDetail, DigitalLoan, DigitalLoanStatus, DigitalReservation, DigitalReservationStatus, DigitalReview,
} from '../core/digital';
import { Language } from '../core/i18n';
import { Library } from '../core/library';
import { ReaderAuth } from '../core/reader-auth';

/**
 * Chi tiết tài liệu số (OPAC) — thông tin mô tả, nút mượn/trả/đặt trước/huỷ, link đọc/tải file có hạn (mở tab mới — chưa có khuôn
 * trình xem nhúng trong hệ thống nên không dùng iframe ở MVP này), link "Xem bản in" nếu có liên kết sách in (một chiều).
 */
@Component({
  selector: 'opac-ebook-detail',
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <div class="max-w-5xl mx-auto px-4 py-6">
      <a routerLink="/thu-vien-so" class="text-sm text-blue-600 hover:underline inline-flex items-center gap-1">
        <span class="material-icons text-[18px]">arrow_back</span> {{ 'OPAC.DIGITAL.BACK_TO_SEARCH' | translate }}</a>

      @if (loading()) {
        <div class="flex justify-center py-24"><span class="material-icons animate-spin text-blue-600 text-4xl">autorenew</span></div>
      } @else if (notFound()) {
        <div class="text-center py-24 text-slate-600">
          <span class="material-icons text-6xl text-slate-300">find_in_page</span>
          <p class="mt-3">{{ 'OPAC.DIGITAL.NOT_FOUND' | translate }}</p>
        </div>
      } @else if (item(); as it) {
        <div class="mt-4 grid gap-6 lg:grid-cols-[1fr_320px]">
          <article class="bg-white border border-slate-200 rounded-xl p-6">
            <div class="flex gap-5">
              @if (it.coverUrl && !coverBroken()) {
                <img [src]="it.coverUrl" alt="" referrerpolicy="no-referrer" (error)="coverBroken.set(true)"
                     class="w-24 h-32 sm:w-32 sm:h-44 rounded-lg object-cover bg-slate-100 border border-slate-200 shrink-0" />
              } @else {
                <div class="w-24 h-32 rounded-lg bg-gradient-to-br from-blue-100 to-sky-50 text-blue-500 flex items-center justify-center shrink-0">
                  <span class="material-icons text-4xl">menu_book</span>
                </div>
              }
              <div class="min-w-0">
                <div class="flex items-start justify-between gap-2">
                  <h1 class="text-xl md:text-2xl font-bold text-slate-900">{{ it.title }}</h1>
                  @if (library.has('DIGITAL') && auth.loggedIn()) {
                    <button type="button" [disabled]="favoriteBusy()" (click)="toggleFavorite(it)" [title]="(favorite() ? 'OPAC.DIGITAL.UNFAVORITE' : 'OPAC.DIGITAL.FAVORITE') | translate"
                            class="shrink-0 text-2xl disabled:opacity-50" [class.text-rose-600]="favorite()" [class.text-slate-300]="!favorite()">
                      <span class="material-icons">{{ favorite() ? 'favorite' : 'favorite_border' }}</span>
                    </button>
                  }
                </div>
                @if (it.author) { <div class="mt-1 text-slate-700">{{ it.author }}</div> }
                <div class="mt-3 flex gap-2 flex-wrap text-xs">
                  @if (it.collectionName) { <span class="px-2 py-0.5 rounded bg-slate-100 text-slate-600">{{ it.collectionName }}</span> }
                  @if (it.free) { <span class="px-2 py-0.5 rounded bg-green-50 text-green-700">{{ 'OPAC.DIGITAL.FREE' | translate }}</span> }
                  @else { <span class="px-2 py-0.5 rounded bg-amber-50 text-amber-700">{{ 'OPAC.DIGITAL.NEEDS_LOGIN' | translate }}</span> }
                </div>
                @if (it.linkedBibPublicId) {
                  <a [routerLink]="['/tai-lieu', it.linkedBibPublicId]" class="mt-3 inline-flex items-center gap-1 text-sm text-blue-600 hover:underline">
                    <span class="material-icons text-[16px]">menu_book</span>{{ 'OPAC.DIGITAL.VIEW_PRINT_BOOK' | translate }}</a>
                }
              </div>
            </div>

            <dl class="mt-6 grid sm:grid-cols-[160px_1fr] gap-x-4 gap-y-2 text-sm">
              @for (row of rows(it); track row[0]) { <dt class="text-slate-500">{{ row[0] | translate }}</dt><dd class="text-slate-800 whitespace-pre-line">{{ row[1] }}</dd> }
            </dl>

            <h2 class="mt-8 font-semibold text-slate-900">{{ 'OPAC.DIGITAL.FILES' | translate }}</h2>
            @if (it.files.length) {
              <ul class="mt-3 divide-y divide-slate-100">
                @for (f of it.files; track f.publicId) {
                  <li class="py-2.5 flex items-center justify-between gap-3">
                    <span class="flex items-center gap-2 text-sm text-slate-800"><span class="material-icons text-slate-400 text-[20px]">picture_as_pdf</span>{{ f.description || f.fileExt || 'PDF' }}</span>
                    <div class="flex items-center gap-2 shrink-0">
                      <button type="button" class="px-3 py-1.5 rounded-lg border border-blue-300 text-blue-700 text-sm hover:bg-blue-50 disabled:opacity-50"
                              [disabled]="fileBusy() === f.publicId" (click)="openFile(it, f.publicId, false)">{{ 'OPAC.DIGITAL.READ_ONLINE' | translate }}</button>
                      @if (it.allowDownload) {
                        <button type="button" class="px-3 py-1.5 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-50 disabled:opacity-50"
                                [disabled]="fileBusy() === f.publicId" (click)="openFile(it, f.publicId, true)">{{ 'OPAC.DIGITAL.DOWNLOAD' | translate }}</button>
                      }
                    </div>
                  </li>
                }
              </ul>
              @if (fileError(); as err) { <p class="text-red-600 text-sm mt-2">{{ err }}</p> }
            } @else {
              <p class="mt-2 text-sm text-slate-500">{{ 'OPAC.DIGITAL.NO_FILES' | translate }}</p>
            }

            <h2 class="mt-8 font-semibold text-slate-900">{{ 'OPAC.DIGITAL.REVIEWS' | translate }}</h2>
            @if (reviews().length) {
              <ul class="mt-3 divide-y divide-slate-100">
                @for (r of reviews(); track r.publicId) {
                  <li class="py-3">
                    <div class="flex items-center gap-2 text-sm">
                      <span class="font-medium text-slate-900">{{ r.displayName || ('OPAC.DIGITAL.ANONYMOUS' | translate) }}</span>
                      @if (r.rating) {
                        <span class="text-amber-500 text-xs">{{ '★'.repeat(r.rating) }}{{ '☆'.repeat(5 - r.rating) }}</span>
                      }
                    </div>
                    @if (r.content) { <p class="mt-1 text-sm text-slate-700 whitespace-pre-line">{{ r.content }}</p> }
                  </li>
                }
              </ul>
            } @else {
              <p class="mt-2 text-sm text-slate-500">{{ 'OPAC.DIGITAL.NO_REVIEWS' | translate }}</p>
            }

            <form class="mt-4 border border-slate-200 rounded-lg p-4 space-y-2" (ngSubmit)="submitReview(it)">
              <div class="font-medium text-slate-900 text-sm">{{ 'OPAC.DIGITAL.WRITE_REVIEW' | translate }}</div>
              <div class="flex gap-2 flex-wrap">
                <input type="text" [(ngModel)]="reviewDisplayName" name="displayName" [placeholder]="'OPAC.DIGITAL.YOUR_NAME' | translate"
                       class="flex-1 min-w-[160px] px-3 py-1.5 border border-slate-300 rounded-lg text-sm" />
                <select [(ngModel)]="reviewRating" name="rating" class="px-3 py-1.5 border border-slate-300 rounded-lg text-sm">
                  <option [ngValue]="null">{{ 'OPAC.DIGITAL.NO_RATING' | translate }}</option>
                  @for (n of [1, 2, 3, 4, 5]; track n) { <option [ngValue]="n">{{ n }} ★</option> }
                </select>
              </div>
              <textarea [(ngModel)]="reviewContent" name="content" rows="2" [placeholder]="'OPAC.DIGITAL.YOUR_REVIEW' | translate"
                        class="w-full px-3 py-1.5 border border-slate-300 rounded-lg text-sm"></textarea>
              @if (reviewSubmitted()) { <p class="text-green-700 text-sm">{{ 'OPAC.DIGITAL.REVIEW_SUBMITTED' | translate }}</p> }
              @if (reviewError(); as err) { <p class="text-red-600 text-sm">{{ err }}</p> }
              <button type="submit" [disabled]="reviewBusy()" class="px-3 py-1.5 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50">
                {{ 'OPAC.DIGITAL.SUBMIT_REVIEW' | translate }}</button>
            </form>
          </article>

          <aside class="space-y-4">
            @if (library.has('DIGITAL')) {
              <div class="bg-white border border-slate-200 rounded-xl p-4 text-sm text-slate-600">
                <div class="font-medium text-slate-900 mb-1">{{ 'OPAC.DIGITAL.BORROW_TITLE' | translate }}</div>
                @if (!auth.loggedIn()) {
                  <p>{{ 'OPAC.DIGITAL.LOGIN_TO_BORROW' | translate }}</p>
                  <button type="button" class="mt-3 inline-flex items-center gap-1.5 px-3 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700"
                          (click)="auth.login(router.url)">
                    <span class="material-icons text-[18px]">login</span>{{ 'OPAC.NAVBAR.LOGIN' | translate }}</button>
                } @else if (myLoan(); as loan) {
                  <p class="text-green-700">{{ 'OPAC.DIGITAL.LOAN_ACTIVE' | translate: { date: loan.expiresAt ? (loan.expiresAt.slice(0, 10)) : ('OPAC.DIGITAL.NO_LIMIT' | translate) } }}</p>
                  @if (borrowError(); as err) { <p class="text-red-600 mt-2">{{ err }}</p> }
                  <button type="button" [disabled]="borrowBusy()" (click)="returnLoan(loan)"
                          class="mt-3 px-3 py-2 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-50 disabled:opacity-50">
                    {{ 'OPAC.DIGITAL.RETURN' | translate }}</button>
                } @else if (myReservation(); as resv) {
                  <p [class.text-green-700]="resv.status === ReservationStatus.Ready">
                    {{ (resv.status === ReservationStatus.Ready ? 'OPAC.DIGITAL.RESERVATION_READY' : 'OPAC.DIGITAL.RESERVATION_WAITING')
                      | translate: { date: resv.readyExpiresAt?.slice(0, 10), position: resv.queuePosition } }}
                  </p>
                  @if (borrowError(); as err) { <p class="text-red-600 mt-2">{{ err }}</p> }
                  <button type="button" [disabled]="borrowBusy()" (click)="cancelReservation(resv)"
                          class="mt-3 px-3 py-2 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-50 disabled:opacity-50">
                    {{ 'OPAC.DIGITAL.CANCEL_RESERVATION' | translate }}</button>
                } @else {
                  <p>{{ 'OPAC.DIGITAL.BORROW_HINT' | translate }}</p>
                  @if (borrowError(); as err) { <p class="text-red-600 mt-2">{{ err }}</p> }
                  <div class="mt-3 flex gap-2 flex-wrap">
                    <button type="button" [disabled]="borrowBusy()" (click)="borrow(it)"
                            class="px-3 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50">
                      {{ 'OPAC.DIGITAL.BORROW' | translate }}</button>
                    @if (showReserve()) {
                      <button type="button" [disabled]="borrowBusy()" (click)="reserve(it)"
                              class="px-3 py-2 rounded-lg border border-blue-300 text-blue-700 text-sm hover:bg-blue-50 disabled:opacity-50">
                        {{ 'OPAC.DIGITAL.RESERVE' | translate }}</button>
                    }
                  </div>
                }
              </div>
            }
          </aside>
        </div>
      }
    </div>
  `,
})
export class EbookDetail {
  private readonly api = inject(DigitalApi);
  private readonly title = inject(Title);
  protected readonly library = inject(Library);
  protected readonly auth = inject(ReaderAuth);
  protected readonly router = inject(Router);
  private readonly language = inject(Language);

  readonly publicId = input.required<string>();

  protected readonly item = signal<DigitalItemDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly coverBroken = signal(false);

  protected readonly ReservationStatus = DigitalReservationStatus;
  protected readonly myLoan = signal<DigitalLoan | null>(null);
  protected readonly myReservation = signal<DigitalReservation | null>(null);
  protected readonly borrowBusy = signal(false);
  protected readonly borrowError = signal<string | null>(null);
  protected readonly showReserve = signal(false);

  protected readonly fileBusy = signal<string | null>(null);
  protected readonly fileError = signal<string | null>(null);

  protected readonly reviews = signal<DigitalReview[]>([]);
  protected reviewDisplayName = '';
  protected reviewRating: number | null = null;
  protected reviewContent = '';
  protected readonly reviewBusy = signal(false);
  protected readonly reviewError = signal<string | null>(null);
  protected readonly reviewSubmitted = signal(false);

  protected readonly favorite = signal(false);
  protected readonly favoriteBusy = signal(false);

  constructor() {
    effect(() => void this.load(this.publicId()));
    effect(() => {
      const it = this.item();
      const loggedIn = this.auth.loggedIn();
      if (it && loggedIn && this.library.has('DIGITAL')) {
        void this.refreshMine(it.publicId);
        void this.refreshFavorite(it.publicId);
      } else {
        this.myLoan.set(null);
        this.myReservation.set(null);
        this.favorite.set(false);
      }
    });
  }

  protected rows(it: DigitalItemDetail): [string, string][] {
    const all: [string, string | null][] = [
      ['OPAC.DIGITAL.PUBLISHER', [it.publisher, it.publishDate].filter(Boolean).join(' — ') || null],
      ['OPAC.DIGITAL.SUBJECT', it.subjectName],
      ['OPAC.DIGITAL.TOPIC', it.topicName],
      ['OPAC.DIGITAL.TYPE', it.typeName],
      ['OPAC.DIGITAL.PAGE', it.page],
      ['OPAC.DIGITAL.OTHER_TITLE', it.otherTitle],
      ['OPAC.SEARCH.F_KEYWORD', it.keyword],
    ];
    return all.filter((r): r is [string, string] => !!r[1]);
  }

  protected async borrow(it: DigitalItemDetail): Promise<void> {
    this.borrowBusy.set(true);
    this.borrowError.set(null);
    try {
      this.myLoan.set(await this.api.borrow(it.publicId));
      this.showReserve.set(false);
    } catch (e) {
      this.borrowError.set(this.language.apiError(e));
      this.showReserve.set(true);
    } finally {
      this.borrowBusy.set(false);
    }
  }

  protected async returnLoan(loan: DigitalLoan): Promise<void> {
    this.borrowBusy.set(true);
    this.borrowError.set(null);
    try {
      await this.api.returnLoan(loan.publicId);
      this.myLoan.set(null);
    } catch (e) {
      this.borrowError.set(this.language.apiError(e));
    } finally {
      this.borrowBusy.set(false);
    }
  }

  protected async reserve(it: DigitalItemDetail): Promise<void> {
    this.borrowBusy.set(true);
    this.borrowError.set(null);
    try {
      this.myReservation.set(await this.api.reserve(it.publicId));
    } catch (e) {
      this.borrowError.set(this.language.apiError(e));
    } finally {
      this.borrowBusy.set(false);
    }
  }

  protected async cancelReservation(resv: DigitalReservation): Promise<void> {
    this.borrowBusy.set(true);
    this.borrowError.set(null);
    try {
      await this.api.cancelReservation(resv.publicId);
      this.myReservation.set(null);
    } catch (e) {
      this.borrowError.set(this.language.apiError(e));
    } finally {
      this.borrowBusy.set(false);
    }
  }

  protected async openFile(it: DigitalItemDetail, filePublicId: string, download: boolean): Promise<void> {
    this.fileBusy.set(filePublicId);
    this.fileError.set(null);
    try {
      const url = await this.api.fileUrl(it.publicId, filePublicId, download);
      window.open(url.url, '_blank', 'noopener');
    } catch (e) {
      this.fileError.set(this.language.apiError(e));
    } finally {
      this.fileBusy.set(null);
    }
  }

  protected async submitReview(it: DigitalItemDetail): Promise<void> {
    this.reviewBusy.set(true);
    this.reviewError.set(null);
    try {
      await this.api.submitReview(it.publicId, {
        rating: this.reviewRating, displayName: this.reviewDisplayName || null, content: this.reviewContent || null,
      });
      this.reviewSubmitted.set(true);
      this.reviewDisplayName = '';
      this.reviewRating = null;
      this.reviewContent = '';
    } catch (e) {
      this.reviewError.set(this.language.apiError(e));
    } finally {
      this.reviewBusy.set(false);
    }
  }

  protected async toggleFavorite(it: DigitalItemDetail): Promise<void> {
    this.favoriteBusy.set(true);
    try {
      if (this.favorite()) {
        await this.api.removeFavorite(it.publicId);
        this.favorite.set(false);
      } else {
        await this.api.addFavorite(it.publicId);
        this.favorite.set(true);
      }
    } finally {
      this.favoriteBusy.set(false);
    }
  }

  private async refreshFavorite(itemPublicId: string): Promise<void> {
    try {
      const favorites = await this.api.myFavorites();
      this.favorite.set(favorites.some((f) => f.ebookItemPublicId === itemPublicId));
    } catch {
      this.favorite.set(false);
    }
  }

  private async refreshMine(itemPublicId: string): Promise<void> {
    try {
      const loans = await this.api.myLoans();
      this.myLoan.set(loans.find((l) => l.ebookItemPublicId === itemPublicId && l.status === DigitalLoanStatus.Active) ?? null);
    } catch {
      this.myLoan.set(null);
    }
    try {
      const reservations = await this.api.myReservations();
      this.myReservation.set(reservations.find((r) => r.ebookItemPublicId === itemPublicId) ?? null);
    } catch {
      this.myReservation.set(null);
    }
  }

  private async load(publicId: string): Promise<void> {
    this.loading.set(true);
    this.notFound.set(false);
    this.coverBroken.set(false);
    this.borrowError.set(null);
    this.showReserve.set(false);
    this.reviewSubmitted.set(false);
    try {
      const item = await this.api.detail(publicId);
      this.item.set(item);
      this.title.setTitle(`${item.title} — ${this.library.name()}`);
      try {
        this.reviews.set(await this.api.publishedReviews(publicId));
      } catch {
        this.reviews.set([]);
      }
    } catch {
      this.item.set(null);
      this.notFound.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
