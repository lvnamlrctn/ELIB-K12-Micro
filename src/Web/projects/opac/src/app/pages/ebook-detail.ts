import { Component, effect, inject, input, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { DigitalApi, DigitalItemDetail, DigitalLoan, DigitalLoanStatus, DigitalReservation, DigitalReservationStatus } from '../core/digital';
import { Language } from '../core/i18n';
import { Library } from '../core/library';
import { ReaderAuth } from '../core/reader-auth';

/**
 * Chi tiết tài liệu số (OPAC) — thông tin mô tả, nút mượn/trả/đặt trước/huỷ, link đọc/tải file có hạn (mở tab mới — chưa có khuôn
 * trình xem nhúng trong hệ thống nên không dùng iframe ở MVP này), link "Xem bản in" nếu có liên kết sách in (một chiều).
 */
@Component({
  selector: 'opac-ebook-detail',
  imports: [RouterLink, TranslatePipe],
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
                <h1 class="text-xl md:text-2xl font-bold text-slate-900">{{ it.title }}</h1>
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

  constructor() {
    effect(() => void this.load(this.publicId()));
    effect(() => {
      const it = this.item();
      const loggedIn = this.auth.loggedIn();
      if (it && loggedIn && this.library.has('DIGITAL')) void this.refreshMine(it.publicId);
      else {
        this.myLoan.set(null);
        this.myReservation.set(null);
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
    try {
      const item = await this.api.detail(publicId);
      this.item.set(item);
      this.title.setTitle(`${item.title} — ${this.library.name()}`);
    } catch {
      this.item.set(null);
      this.notFound.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
