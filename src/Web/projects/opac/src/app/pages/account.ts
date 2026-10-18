import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { CirculationApi, HoldDto, HoldStatus, LoanDto, ReaderPanel } from '../core/circulation';
import { DigitalApi, DigitalFavorite, DigitalLoan, DigitalReservation, DigitalReservationStatus } from '../core/digital';
import { LDatePipe, Language, LNumberPipe } from '../core/i18n';
import { Library } from '../core/library';
import { ReaderAuth, ReaderMe } from '../core/reader-auth';
import { ReaderCard } from './reader-card';

/** Tài khoản bạn đọc: chưa đăng nhập → nút đăng nhập; đã đăng nhập → thông tin thẻ, đổi mật khẩu, đăng xuất. */
@Component({
  selector: 'opac-account',
  imports: [FormsModule, LDatePipe, LNumberPipe, RouterLink, TranslatePipe, ReaderCard],
  template: `
    <div class="max-w-3xl mx-auto px-4 py-8">
      @if (!auth.loggedIn()) {
        <div class="bg-white rounded-2xl border border-slate-200 p-8 text-center">
          <span class="material-icons text-blue-600 text-5xl">account_circle</span>
          <h1 class="text-xl font-semibold text-slate-900 mt-3">{{ 'OPAC.MY_LIBRARY.ACCOUNT_TITLE' | translate }}</h1>
          <p class="text-slate-600 mt-2">{{ 'OPAC.MY_LIBRARY.LOGIN_HINT' | translate }}</p>
          @if (error()) { <p class="text-red-600 text-sm mt-3" role="alert">{{ error() }}</p> }
          <button type="button" class="mt-6 inline-flex items-center gap-2 px-5 py-2.5 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700"
                  (click)="auth.login('/tai-khoan')">
            <span class="material-icons text-[20px]">login</span>{{ 'OPAC.NAVBAR.LOGIN' | translate }}
          </button>
          <p class="text-xs text-slate-500 mt-4">{{ 'OPAC.MY_LIBRARY.NO_PASSWORD_HINT' | translate }}</p>
        </div>
      } @else {
        <div class="flex items-center justify-between gap-3 mb-6 flex-wrap">
          <h1 class="text-2xl font-semibold text-slate-900">{{ 'OPAC.NAVBAR.MY_ACCOUNT' | translate }}</h1>
          <button type="button" class="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-50"
                  (click)="auth.logout()"><span class="material-icons text-[18px]">logout</span>{{ 'OPAC.NAVBAR.LOGOUT' | translate }}</button>
        </div>

        @if (me(); as m) {
          <section class="bg-white rounded-2xl border border-slate-200 p-6">
            <div class="flex items-center gap-4">
              <span class="w-14 h-14 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
                <span class="material-icons text-3xl">badge</span></span>
              <div class="min-w-0">
                <div class="text-lg font-semibold text-slate-900">{{ m.fullName }}</div>
                <div class="text-slate-600 text-sm">{{ 'OPAC.MY_LIBRARY.CARD_NO' | translate }} <span class="font-mono">{{ m.cardNo }}</span></div>
              </div>
            </div>
            <dl class="grid grid-cols-1 sm:grid-cols-3 gap-4 mt-6 text-sm">
              <div><dt class="text-slate-500">Email</dt><dd class="text-slate-800 break-all">{{ m.email || '—' }}</dd></div>
              <div><dt class="text-slate-500">{{ 'OPAC.MY_LIBRARY.EXPIRE_DATE' | translate }}</dt>
                <dd [class.text-red-600]="expired(m)">{{ m.expireDate ? (m.expireDate | ldate) : ('OPAC.MY_LIBRARY.NO_EXPIRY' | translate) }}{{ expired(m) ? ' ' + ('OPAC.MY_LIBRARY.EXPIRED' | translate) : '' }}</dd></div>
              <div><dt class="text-slate-500">{{ 'OPAC.MY_LIBRARY.THIS_LOGIN' | translate }}</dt><dd class="text-slate-800">{{ m.lastLoginAt ? (m.lastLoginAt | ldate: 'dd/MM/yyyy HH:mm') : '—' }}</dd></div>
            </dl>
          </section>
        } @else if (error()) {
          <p class="text-red-600" role="alert">{{ error() }}</p>
        } @else {
          <div class="flex justify-center py-12"><span class="material-icons animate-spin text-blue-600 text-3xl">autorenew</span></div>
        }

        @if (me(); as m) {
          <section class="mt-6">
            <h2 class="font-semibold text-slate-900 mb-3">{{ 'OPAC.MY_LIBRARY.E_CARD' | translate }}</h2>
            <opac-reader-card [cardNo]="m.cardNo" [fullName]="m.fullName" [expireDate]="m.expireDate" [library]="library.name()" />
            <p class="text-xs text-slate-500 mt-2">{{ 'OPAC.MY_LIBRARY.E_CARD_HINT' | translate }}</p>
          </section>
        }

        <section class="bg-white rounded-2xl border border-slate-200 p-6 mt-6">
          <h2 class="font-semibold text-slate-900 mb-4">{{ 'OPAC.MY_LIBRARY.CHANGE_PASSWORD' | translate }}</h2>
          <form class="grid grid-cols-1 sm:grid-cols-3 gap-4 items-end" (ngSubmit)="changePassword()">
            <label class="block text-sm"><span class="text-slate-600">{{ 'OPAC.MY_LIBRARY.CURRENT_PASSWORD' | translate }}</span>
              <input type="password" name="current" autocomplete="current-password" [(ngModel)]="current" required
                     class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" /></label>
            <label class="block text-sm"><span class="text-slate-600">{{ 'OPAC.MY_LIBRARY.NEW_PASSWORD' | translate }}</span>
              <input type="password" name="next" autocomplete="new-password" [(ngModel)]="next" required minlength="6"
                     class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" /></label>
            <label class="block text-sm"><span class="text-slate-600">{{ 'OPAC.MY_LIBRARY.CONFIRM_PASSWORD' | translate }}</span>
              <input type="password" name="confirm" autocomplete="new-password" [(ngModel)]="confirm" required
                     class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" /></label>
            <div class="sm:col-span-3 flex items-center gap-3 flex-wrap">
              <button type="submit" [disabled]="busy()"
                      class="px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50">{{ 'OPAC.MY_LIBRARY.CHANGE_PASSWORD' | translate }}</button>
              @if (pwMessage(); as msg) {
                <span class="text-sm" [class.text-green-700]="msg.ok" [class.text-red-600]="!msg.ok" role="status">{{ msg.text | translate }}</span>
              }
            </div>
          </form>
        </section>

        @if (library.has('CIRCULATION')) {
          <section class="bg-white rounded-2xl border border-slate-200 p-6 mt-6">
            <h2 class="font-semibold text-slate-900 mb-4">{{ 'OPAC.MY_LIBRARY.MY_LOANS' | translate }}</h2>
            @if (panel(); as p) {
              @if (!p.canBorrow && p.blockReason) {
                <div class="mb-3 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ p.blockReason }}</div>
              }
              @if (p.unpaidFines > 0) {
                <div class="mb-3 rounded-lg bg-amber-50 border border-amber-200 text-amber-700 text-sm px-3 py-2">
                  {{ 'OPAC.MY_LIBRARY.UNPAID_FINES' | translate: { amount: (p.unpaidFines | lnumber: '1.0-0') } }}
                </div>
              }
              @if (p.currentLoans.length) {
                <ul class="divide-y divide-slate-100">
                  @for (loan of p.currentLoans; track loan.publicId) {
                    <li class="py-3 flex items-center justify-between gap-3 flex-wrap">
                      <div class="min-w-0">
                        <div class="font-medium text-slate-900 truncate">{{ loan.title || loan.barcode }}</div>
                        <div class="text-sm text-slate-500">
                          {{ 'OPAC.MY_LIBRARY.DUE_DATE' | translate }}
                          <span [class.text-red-600]="isOverdue(loan)">{{ loan.dueAt | ldate }}</span>
                          @if (loan.renewCount > 0) { · {{ 'OPAC.MY_LIBRARY.RENEWED_N' | translate: { count: loan.renewCount } }} }
                        </div>
                        @if (renewMessage(); as msg) {
                          @if (msg.loanId === loan.publicId) {
                            <div class="text-sm mt-1" [class.text-green-700]="msg.ok" [class.text-red-600]="!msg.ok" role="status">{{ msg.text }}</div>
                          }
                        }
                      </div>
                      <button type="button" [disabled]="renewingId() === loan.publicId"
                              class="px-3 py-1.5 rounded-lg border border-blue-300 text-blue-700 text-sm hover:bg-blue-50 disabled:opacity-50 shrink-0"
                              (click)="renew(loan)">{{ 'OPAC.MY_LIBRARY.RENEW' | translate }}</button>
                    </li>
                  }
                </ul>
              } @else {
                <p class="text-sm text-slate-500">{{ 'OPAC.MY_LIBRARY.NO_LOANS' | translate }}</p>
              }
            } @else if (loansError(); as err) {
              <p class="text-red-600 text-sm" role="alert">{{ err }}</p>
            } @else {
              <div class="flex justify-center py-6"><span class="material-icons animate-spin text-blue-600 text-2xl">autorenew</span></div>
            }
          </section>

          <section class="bg-white rounded-2xl border border-slate-200 p-6 mt-6">
            <h2 class="font-semibold text-slate-900 mb-4">{{ 'OPAC.MY_LIBRARY.MY_HOLDS' | translate }}</h2>
            @if (holdsError(); as err) { <p class="text-red-600 text-sm mb-3" role="alert">{{ err }}</p> }
            @if (holds().length) {
              <ul class="divide-y divide-slate-100">
                @for (hold of holds(); track hold.publicId) {
                  <li class="py-3 flex items-center justify-between gap-3 flex-wrap">
                    <div class="min-w-0">
                      <div class="font-medium text-slate-900 truncate">{{ hold.title || hold.mfn }}</div>
                      <div class="text-sm text-slate-500">
                        @if (hold.status === HoldStatus.Ready) {
                          <span class="text-green-700">{{ 'OPAC.MY_LIBRARY.HOLD_READY' | translate: { date: (hold.expiresAt | ldate) } }}</span>
                        } @else {
                          {{ 'OPAC.MY_LIBRARY.HOLD_WAITING' | translate: { position: hold.queuePosition } }}
                        }
                      </div>
                    </div>
                    <button type="button" [disabled]="cancelingId() === hold.publicId"
                            class="px-3 py-1.5 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-50 disabled:opacity-50 shrink-0"
                            (click)="cancelHold(hold)">{{ 'OPAC.MY_LIBRARY.CANCEL_HOLD' | translate }}</button>
                  </li>
                }
              </ul>
            } @else {
              <p class="text-sm text-slate-500">{{ 'OPAC.MY_LIBRARY.NO_HOLDS' | translate }}</p>
            }
          </section>

          <section class="bg-white rounded-2xl border border-slate-200 p-6 mt-6">
            <h2 class="font-semibold text-slate-900 mb-4">{{ 'OPAC.MY_LIBRARY.LOAN_HISTORY' | translate }}</h2>
            @if (historyLoading()) {
              <div class="flex justify-center py-6"><span class="material-icons animate-spin text-blue-600 text-2xl">autorenew</span></div>
            } @else if (history().length) {
              <ul class="divide-y divide-slate-100">
                @for (loan of history(); track loan.publicId) {
                  <li class="py-3">
                    <div class="font-medium text-slate-900 truncate">{{ loan.title || loan.barcode }}</div>
                    <div class="text-sm text-slate-500">
                      @if (loan.returnedAt) {
                        {{ 'OPAC.MY_LIBRARY.RETURNED_AT' | translate: { date: (loan.returnedAt | ldate) } }}
                      } @else {
                        <span [class.text-red-600]="isOverdue(loan)">{{ 'OPAC.MY_LIBRARY.STILL_BORROWING' | translate }} — {{ 'OPAC.MY_LIBRARY.DUE_DATE' | translate }} {{ loan.dueAt | ldate }}</span>
                      }
                    </div>
                  </li>
                }
              </ul>
            } @else {
              <p class="text-sm text-slate-500">{{ 'OPAC.MY_LIBRARY.NO_HISTORY' | translate }}</p>
            }
          </section>
        }

        @if (library.has('DIGITAL')) {
          <section class="bg-white rounded-2xl border border-slate-200 p-6 mt-6">
            <h2 class="font-semibold text-slate-900 mb-4">{{ 'OPAC.DIGITAL.MY_LOANS' | translate }}</h2>
            @if (digitalLoansError(); as err) { <p class="text-red-600 text-sm mb-3" role="alert">{{ err }}</p> }
            @if (digitalLoans().length) {
              <ul class="divide-y divide-slate-100">
                @for (loan of digitalLoans(); track loan.publicId) {
                  <li class="py-3 flex items-center justify-between gap-3 flex-wrap">
                    <div class="min-w-0">
                      <a [routerLink]="['/thu-vien-so', loan.ebookItemPublicId]" class="font-medium text-slate-900 hover:text-blue-700 truncate">{{ loan.title }}</a>
                      <div class="text-sm text-slate-500">{{ loan.expiresAt ? (('OPAC.DIGITAL.ACCESS_UNTIL' | translate) + ' ' + (loan.expiresAt | ldate)) : ('OPAC.DIGITAL.NO_LIMIT' | translate) }}</div>
                    </div>
                    <button type="button" [disabled]="digitalBusyId() === loan.publicId"
                            class="px-3 py-1.5 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-50 disabled:opacity-50 shrink-0"
                            (click)="returnDigitalLoan(loan)">{{ 'OPAC.DIGITAL.RETURN' | translate }}</button>
                  </li>
                }
              </ul>
            } @else {
              <p class="text-sm text-slate-500">{{ 'OPAC.DIGITAL.NO_LOANS' | translate }}</p>
            }
          </section>

          <section class="bg-white rounded-2xl border border-slate-200 p-6 mt-6">
            <h2 class="font-semibold text-slate-900 mb-4">{{ 'OPAC.DIGITAL.MY_RESERVATIONS' | translate }}</h2>
            @if (digitalReservationsError(); as err) { <p class="text-red-600 text-sm mb-3" role="alert">{{ err }}</p> }
            @if (digitalReservations().length) {
              <ul class="divide-y divide-slate-100">
                @for (resv of digitalReservations(); track resv.publicId) {
                  <li class="py-3 flex items-center justify-between gap-3 flex-wrap">
                    <div class="min-w-0">
                      <a [routerLink]="['/thu-vien-so', resv.ebookItemPublicId]" class="font-medium text-slate-900 hover:text-blue-700 truncate">{{ resv.title }}</a>
                      <div class="text-sm text-slate-500">
                        @if (resv.status === DigitalReservationStatus.Ready) {
                          <span class="text-green-700">{{ 'OPAC.DIGITAL.RESERVATION_READY' | translate: { date: (resv.readyExpiresAt | ldate) } }}</span>
                        } @else {
                          {{ 'OPAC.DIGITAL.RESERVATION_WAITING' | translate: { position: resv.queuePosition } }}
                        }
                      </div>
                    </div>
                    <button type="button" [disabled]="digitalBusyId() === resv.publicId"
                            class="px-3 py-1.5 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-50 disabled:opacity-50 shrink-0"
                            (click)="cancelDigitalReservation(resv)">{{ 'OPAC.DIGITAL.CANCEL_RESERVATION' | translate }}</button>
                  </li>
                }
              </ul>
            } @else {
              <p class="text-sm text-slate-500">{{ 'OPAC.DIGITAL.NO_RESERVATIONS' | translate }}</p>
            }
          </section>

          <section class="bg-white rounded-2xl border border-slate-200 p-6 mt-6">
            <h2 class="font-semibold text-slate-900 mb-4">{{ 'OPAC.DIGITAL.MY_FAVORITES' | translate }}</h2>
            @if (digitalFavoritesError(); as err) { <p class="text-red-600 text-sm mb-3" role="alert">{{ err }}</p> }
            @if (digitalFavorites().length) {
              <ul class="divide-y divide-slate-100">
                @for (fav of digitalFavorites(); track fav.ebookItemPublicId) {
                  <li class="py-3 flex items-center justify-between gap-3 flex-wrap">
                    <div class="min-w-0">
                      <a [routerLink]="['/thu-vien-so', fav.ebookItemPublicId]" class="font-medium text-slate-900 hover:text-blue-700 truncate">{{ fav.title }}</a>
                      @if (fav.author) { <div class="text-sm text-slate-500">{{ fav.author }}</div> }
                    </div>
                    <button type="button" [disabled]="digitalBusyId() === fav.ebookItemPublicId"
                            class="px-3 py-1.5 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-50 disabled:opacity-50 shrink-0"
                            (click)="removeDigitalFavorite(fav)">{{ 'OPAC.DIGITAL.UNFAVORITE' | translate }}</button>
                  </li>
                }
              </ul>
            } @else {
              <p class="text-sm text-slate-500">{{ 'OPAC.DIGITAL.NO_FAVORITES' | translate }}</p>
            }
          </section>
        }
      }
    </div>
  `,
})
export class Account implements OnInit {
  protected readonly auth = inject(ReaderAuth);
  protected readonly library = inject(Library);
  private readonly circulation = inject(CirculationApi);
  private readonly digital = inject(DigitalApi);
  private readonly language = inject(Language);
  protected readonly me = signal<ReaderMe | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly pwMessage = signal<{ ok: boolean; text: string } | null>(null);
  protected current = '';
  protected next = '';
  protected confirm = '';

  protected readonly HoldStatus = HoldStatus;
  protected readonly panel = signal<ReaderPanel | null>(null);
  protected readonly loansError = signal<string | null>(null);
  protected readonly renewingId = signal<string | null>(null);
  protected readonly renewMessage = signal<{ loanId: string; ok: boolean; text: string } | null>(null);
  protected readonly holds = signal<HoldDto[]>([]);
  protected readonly holdsError = signal<string | null>(null);
  protected readonly cancelingId = signal<string | null>(null);
  protected readonly history = signal<LoanDto[]>([]);
  protected readonly historyLoading = signal(false);

  protected readonly DigitalReservationStatus = DigitalReservationStatus;
  protected readonly digitalLoans = signal<DigitalLoan[]>([]);
  protected readonly digitalLoansError = signal<string | null>(null);
  protected readonly digitalReservations = signal<DigitalReservation[]>([]);
  protected readonly digitalReservationsError = signal<string | null>(null);
  protected readonly digitalFavorites = signal<DigitalFavorite[]>([]);
  protected readonly digitalFavoritesError = signal<string | null>(null);
  protected readonly digitalBusyId = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    if (!(await this.auth.restore())) return;
    try {
      this.me.set(await this.auth.me());
    } catch (e) {
      if (e instanceof HttpErrorResponse && (e.status === 401 || e.status === 403)) {
        // Token bị thu hồi (thẻ khoá, thư viện đặt lại mật khẩu…) — đăng nhập lại.
        await this.auth.forget();
      }
      this.error.set(this.language.apiError(e));
    }
    if (this.library.has('CIRCULATION')) {
      this.loadLoans();
      this.loadHolds();
      this.loadHistory();
    }
    if (this.library.has('DIGITAL')) {
      this.loadDigitalLoans();
      this.loadDigitalReservations();
      this.loadDigitalFavorites();
    }
  }

  protected expired(m: ReaderMe): boolean {
    return !!m.expireDate && m.expireDate < new Date().toISOString().slice(0, 10);
  }

  protected isOverdue(loan: LoanDto): boolean {
    return !loan.returnedAt && loan.dueAt < new Date().toISOString();
  }

  protected async renew(loan: LoanDto): Promise<void> {
    this.renewingId.set(loan.publicId);
    this.renewMessage.set(null);
    try {
      const updated = await this.circulation.renew(loan.publicId);
      this.panel.update((p) => (p ? { ...p, currentLoans: p.currentLoans.map((l) => (l.publicId === updated.publicId ? updated : l)) } : p));
      this.renewMessage.set({ loanId: loan.publicId, ok: true, text: this.language.t('OPAC.MY_LIBRARY.RENEW_DONE') });
    } catch (e) {
      this.renewMessage.set({ loanId: loan.publicId, ok: false, text: this.language.apiError(e) });
    } finally {
      this.renewingId.set(null);
    }
  }

  protected async cancelHold(hold: HoldDto): Promise<void> {
    this.cancelingId.set(hold.publicId);
    this.holdsError.set(null);
    try {
      await this.circulation.cancelHold(hold.publicId);
      this.holds.update((hs) => hs.filter((h) => h.publicId !== hold.publicId));
    } catch (e) {
      this.holdsError.set(this.language.apiError(e));
    } finally {
      this.cancelingId.set(null);
    }
  }

  private async loadLoans(): Promise<void> {
    try {
      this.panel.set(await this.circulation.myLoans());
    } catch (e) {
      this.loansError.set(this.language.apiError(e));
    }
  }

  private async loadHolds(): Promise<void> {
    try {
      this.holds.set(await this.circulation.myHolds());
    } catch (e) {
      this.holdsError.set(this.language.apiError(e));
    }
  }

  protected async returnDigitalLoan(loan: DigitalLoan): Promise<void> {
    this.digitalBusyId.set(loan.publicId);
    this.digitalLoansError.set(null);
    try {
      await this.digital.returnLoan(loan.publicId);
      this.digitalLoans.update((ls) => ls.filter((l) => l.publicId !== loan.publicId));
    } catch (e) {
      this.digitalLoansError.set(this.language.apiError(e));
    } finally {
      this.digitalBusyId.set(null);
    }
  }

  protected async cancelDigitalReservation(resv: DigitalReservation): Promise<void> {
    this.digitalBusyId.set(resv.publicId);
    this.digitalReservationsError.set(null);
    try {
      await this.digital.cancelReservation(resv.publicId);
      this.digitalReservations.update((rs) => rs.filter((r) => r.publicId !== resv.publicId));
    } catch (e) {
      this.digitalReservationsError.set(this.language.apiError(e));
    } finally {
      this.digitalBusyId.set(null);
    }
  }

  private async loadDigitalLoans(): Promise<void> {
    try {
      this.digitalLoans.set(await this.digital.myLoans());
    } catch (e) {
      this.digitalLoansError.set(this.language.apiError(e));
    }
  }

  private async loadDigitalReservations(): Promise<void> {
    try {
      this.digitalReservations.set(await this.digital.myReservations());
    } catch (e) {
      this.digitalReservationsError.set(this.language.apiError(e));
    }
  }

  protected async removeDigitalFavorite(fav: DigitalFavorite): Promise<void> {
    this.digitalBusyId.set(fav.ebookItemPublicId);
    this.digitalFavoritesError.set(null);
    try {
      await this.digital.removeFavorite(fav.ebookItemPublicId);
      this.digitalFavorites.update((fs) => fs.filter((f) => f.ebookItemPublicId !== fav.ebookItemPublicId));
    } catch (e) {
      this.digitalFavoritesError.set(this.language.apiError(e));
    } finally {
      this.digitalBusyId.set(null);
    }
  }

  private async loadDigitalFavorites(): Promise<void> {
    try {
      this.digitalFavorites.set(await this.digital.myFavorites());
    } catch (e) {
      this.digitalFavoritesError.set(this.language.apiError(e));
    }
  }

  private async loadHistory(): Promise<void> {
    this.historyLoading.set(true);
    try {
      this.history.set((await this.circulation.history({ pageSize: 20 })).items);
    } catch {
      // Lịch sử không quan trọng bằng sách đang mượn — lỗi thì để trống, không chặn phần còn lại của trang.
    } finally {
      this.historyLoading.set(false);
    }
  }

  protected async changePassword(): Promise<void> {
    if (this.next.length < 6) return this.pwMessage.set({ ok: false, text: 'OPAC.MY_LIBRARY.PASSWORD_MIN' });
    if (this.next !== this.confirm) return this.pwMessage.set({ ok: false, text: 'OPAC.MY_LIBRARY.PASSWORD_MISMATCH' });
    this.busy.set(true);
    try {
      await this.auth.changePassword(this.current, this.next);
      this.current = this.next = this.confirm = '';
      this.pwMessage.set({ ok: true, text: 'OPAC.MY_LIBRARY.PASSWORD_CHANGED' });
    } catch (e) {
      this.pwMessage.set({ ok: false, text: this.language.apiError(e) });
    } finally {
      this.busy.set(false);
    }
  }
}
