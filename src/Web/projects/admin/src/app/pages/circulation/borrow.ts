import { formatDate } from '@angular/common';
import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Router } from '@angular/router';
import { Api, CheckoutLine, CircPlace, Loan, ReaderPanel, ReturnResult, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe, Language } from '../../core/i18n';
import { Session } from '../../core/session';
import { CameraCapture, CapturedPhoto } from '../../shared/camera';
import { LOAN_SLIP_KEY, LoanSlip } from './loan-slip';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';

const PLACE_KEY = 'elib.circulation.place';

/** Số ĐKCB tối đa một lần mượn nhiều (khớp trần LoanResource.MaxCheckout). */
const MAX_BULK = 200;

type ActionKind = 'renew' | 'note' | 'lock';

/**
 * Quầy mượn trả (monolith: /admin/borrow, quyền BORROW), bố cục như ELIB-LRC: thanh điều khiển (điểm lưu thông, số thẻ, ô ĐKCB, nhận diện
 * khuôn mặt) → thẻ bạn đọc có ảnh, chỉ số, in phiếu → bảng lượt đang mượn có ô chọn và thanh công cụ (mượn thêm, trả, gia hạn, ghi chú, lịch
 * sử, tải lại, khoá thẻ, phiếu phạt) → hộp thoại xác nhận trả, mượn nhiều. Một ô ĐKCB: đã chọn bạn đọc thì mượn, chưa thì trả (bản của bất
 * kỳ bạn đọc nào). Giữ phần K12 hơn LRC: đặt mượn / sách giữ trên kệ, nợ phạt, lý do bắt buộc khi gia hạn / ghi chú.
 */
@Component({
  selector: 'app-borrow',
  imports: [FormsModule, LDatePipe, LNumberPipe, Modal, TranslatePipe, CameraCapture],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'BORROW_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-4 gap-3 items-start">
        <div>
          <label for="bw-place" class="form-label">{{ 'BORROW_PAGE.PLACE' | translate }}</label>
          <select id="bw-place" class="input" [ngModel]="placeId()" (ngModelChange)="choosePlace($event)">
            @for (p of places(); track p.id) { <option [ngValue]="p.id">{{ p.code }} — {{ p.name }}@if (p.kind === 2) { ({{ 'CIRC_PLACE_PAGE.KIND_READING' | translate }}) }</option> }
          </select>
        </div>
        <div>
          <label for="bw-card" class="form-label">{{ 'BORROW_PAGE.CARD' | translate }}</label>
          <div class="flex gap-2">
            <input id="bw-card" #cardInput class="input font-mono" [(ngModel)]="cardNo" (keydown.enter)="loadReader()" [placeholder]="'BORROW_PAGE.CARD_PH' | translate" autocomplete="off" />
            <button type="button" class="btn-primary !px-3" [disabled]="busy() || !cardNo.trim()" [title]="'BORROW_PAGE.VIEW_READER' | translate" (click)="loadReader()">
              <span class="material-icons text-[18px]">person_search</span></button>
          </div>
        </div>
        <div>
          <label for="bw-item" class="form-label">{{ 'BORROW_PAGE.BARCODE' | translate }}</label>
          <div class="flex gap-2">
            <input id="bw-item" #itemInput class="input font-mono" [(ngModel)]="barcode" (keydown.enter)="scan()" [placeholder]="'BORROW_PAGE.SCAN_PH' | translate" autocomplete="off" />
            <button type="button" class="btn-primary !px-3 !bg-green-600 hover:!bg-green-700" [disabled]="busy() || !barcode.trim()" [title]="'BORROW_PAGE.SCAN' | translate" (click)="scan()">
              <span class="material-icons text-[18px]">{{ lending() ? 'outbox' : 'assignment_return' }}</span></button>
            <button type="button" class="btn-primary !px-3 !bg-indigo-600 hover:!bg-indigo-700" [disabled]="busy() || !lending()" [title]="'BORROW_PAGE.BULK_BORROW' | translate" (click)="openBulk()">
              <span class="material-icons text-[18px]">playlist_add</span></button>
          </div>
          @if (reader(); as r) {
            <p class="mt-1 text-xs font-medium text-blue-700 break-words">{{ 'BORROW_PAGE.LENDING_TO' | translate: { card: r.cardNo, name: r.fullName } }}</p>
          } @else {
            <p class="mt-1 text-xs text-gray-500">{{ 'BORROW_PAGE.RETURN_ONLY' | translate }}</p>
          }
        </div>
        <div>
          <span class="form-label">{{ 'BORROW_PAGE.FACE_LABEL' | translate }}</span>
          @if (faceEnabled()) {
            <button type="button" class="btn-secondary w-full justify-center" [disabled]="busy()" (click)="camera.set(true)">
              <span class="material-icons text-[18px]">face</span> {{ 'BORROW_PAGE.FACE_START' | translate }}</button>
          } @else {
            <p class="text-xs text-gray-500 pt-2">{{ 'BORROW_PAGE.FACE_OFF' | translate }}</p>
          }
        </div>
      </div>
      @if (lines().length) {
        <ul class="mt-3 space-y-1 text-sm">
          @for (l of lines(); track $index) {
            <li [class]="l.success ? 'text-green-700' : 'text-red-600'"><span class="font-mono">{{ l.barcode }}</span>: {{ l.message }}</li>
          }
        </ul>
      }
    </div>

    @if (reader(); as r) {
      <!-- Thẻ bạn đọc -->
      <div class="panel">
        <div class="flex flex-col md:flex-row md:items-center gap-5">
          <div class="flex items-center gap-4 flex-1 min-w-0">
            @if (photoUrl()) { <img [src]="photoUrl()" [alt]="'BORROW_PAGE.PHOTO_ALT' | translate" class="w-16 h-16 rounded-full object-cover border border-gray-200 shrink-0" /> }
            @else { <div class="w-16 h-16 rounded-full bg-blue-100 flex items-center justify-center text-blue-600 text-2xl font-bold shrink-0">{{ (r.fullName || '?').charAt(0) }}</div> }
            <div class="flex-1 min-w-0 grid grid-cols-2 md:grid-cols-3 gap-x-4 gap-y-1 text-sm">
              <div><span class="text-gray-400">{{ 'BORROW_PAGE.CARD' | translate }}:</span> <span class="font-semibold font-mono">{{ r.cardNo }}</span></div>
              <div class="md:col-span-2 flex items-center gap-2 flex-wrap">
                <span class="font-semibold">{{ r.fullName }}</span>
                <span [class]="blocked() ? 'badge-danger' : 'badge-on'">{{ (blocked() ? 'BORROW_PAGE.LOCKED' : 'BORROW_PAGE.NORMAL') | translate }}</span>
                @if (r.isExpired) { <span class="badge-warn">{{ 'BORROW_PAGE.EXPIRED' | translate }}</span> }
              </div>
              <div><span class="text-gray-400">{{ 'BORROW_PAGE.READER_TYPE' | translate }}:</span> {{ r.readerTypeName ?? '—' }}</div>
              <div><span class="text-gray-400">{{ 'BORROW_PAGE.CLASS_LABEL' | translate }}:</span> {{ r.className ?? '—' }}</div>
              <div><span class="text-gray-400">{{ 'BORROW_PAGE.COURSE_LABEL' | translate }}:</span> {{ r.courseName ?? '—' }}</div>
              <div class="col-span-2 md:col-span-3"><span class="text-gray-400">{{ 'BORROW_PAGE.CARD_EXPIRY_LABEL' | translate }}:</span> {{ r.expireDate ? (r.expireDate | ldate: 'dd/MM/yyyy') : '—' }}
                <span class="text-xs text-gray-500"> · {{ policy(r) }}</span></div>
            </div>
          </div>
          <button type="button" class="btn-secondary shrink-0" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'BORROW_PAGE.PRINT' | translate }}</button>
        </div>
        @if (r.blockReason) { <div class="alert-error !block mt-3">{{ r.blockReason }}</div> }
        @if (r.unpaidFines > 0) { <div class="mt-3"><span class="badge-danger">{{ 'BORROW_PAGE.UNPAID' | translate: { amount: (r.unpaidFines | lnumber: '1.0-0') } }}</span></div> }

        <div class="flex flex-wrap gap-5 mt-4 pt-4 border-t border-gray-100">
          <div class="flex items-center gap-2"><div class="w-9 h-9 rounded-full bg-green-500 text-white flex items-center justify-center font-bold">{{ r.currentLoans.length }}</div>
            <span class="text-xs text-gray-500">{{ 'BORROW_PAGE.STAT_BORROWING' | translate }}</span></div>
          <div class="flex items-center gap-2"><div class="w-9 h-9 rounded-full bg-orange-500 text-white flex items-center justify-center font-bold">{{ overdueCount() }}</div>
            <span class="text-xs text-gray-500">{{ 'BORROW_PAGE.STAT_OVERDUE' | translate }}</span></div>
          <div class="flex items-center gap-2"><div class="w-9 h-9 rounded-full bg-blue-500 text-white flex items-center justify-center font-bold">{{ renewedCount() }}</div>
            <span class="text-xs text-gray-500">{{ 'BORROW_PAGE.STAT_RENEWED' | translate }}</span></div>
          <div class="flex items-center gap-2"><div class="w-9 h-9 rounded-full bg-red-500 text-white flex items-center justify-center font-bold">{{ blocked() ? 1 : 0 }}</div>
            <span class="text-xs text-gray-500">{{ 'BORROW_PAGE.STAT_BLOCKED' | translate }}</span></div>
        </div>
      </div>

      @if (r.holds?.length) {
        <div class="panel">
          <div class="font-semibold text-gray-800 mb-2">{{ 'BORROW_PAGE.HOLDS' | translate: { count: r.holds!.length } }}</div>
          <ul class="space-y-1.5 text-sm">
            @for (h of r.holds!; track h.publicId) {
              <li class="flex items-center gap-2 flex-wrap">
                <span [class]="h.status === 2 ? 'badge-info' : 'badge-warn'">{{ (h.status === 2 ? 'BORROW_PAGE.HOLD_HELD' : 'BORROW_PAGE.HOLD_WAITING') | translate }}</span>
                <span>{{ h.title ?? 'MFN ' + h.mfn }}</span>
                @if (h.status === 2) { <span class="text-gray-600" [innerHTML]="'BORROW_PAGE.HOLD_COPY' | translate: { barcode: h.barcode, date: (h.expiresAt | ldate: 'dd/MM/yyyy') }"></span> }
                @else if (h.queuePosition) { <span class="text-gray-500">{{ 'BORROW_PAGE.HOLD_QUEUE' | translate: { n: h.queuePosition } }}</span> }
              </li>
            }
          </ul>
        </div>
      }

      <!-- Lượt đang mượn -->
      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden mb-5">
        <div class="px-5 py-3 border-b border-gray-100 font-semibold text-gray-800">{{ 'BORROW_PAGE.ON_LOAN' | translate: { count: r.currentLoans.length } }}</div>
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse min-w-[760px]">
            <thead><tr>
              <th class="th w-10"><input type="checkbox" class="w-4 h-4" [checked]="allSelected()" [disabled]="!r.currentLoans.length" [attr.aria-label]="'BORROW_PAGE.SELECT_ALL' | translate" (change)="toggleAll()" /></th>
              <th class="th w-32">{{ 'BORROW_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'BORROW_PAGE.TITLE_COL' | translate }}</th>
              <th class="th w-28">{{ 'BORROW_PAGE.LOANED' | translate }}</th><th class="th w-28">{{ 'BORROW_PAGE.DUE' | translate }}</th>
            </tr></thead>
            <tbody>
              @for (l of r.currentLoans; track l.publicId) {
                <tr class="hover:bg-gray-50/50" [class.bg-red-50]="overdue(l)">
                  <td class="td"><input type="checkbox" class="w-4 h-4" [checked]="selected().has(l.publicId)" (change)="toggle(l.publicId)" /></td>
                  <td class="td font-mono text-[13px]">{{ l.barcode }}</td>
                  <td class="td">
                    <div class="font-medium text-gray-800">{{ l.title ?? '—' }}</div>
                    <div class="text-xs text-gray-500 flex flex-wrap gap-x-3">
                      @if (l.storeName) { <span>{{ 'BORROW_PAGE.LOCATION' | translate }}: {{ l.storeName }}</span> }
                      <span>{{ 'BORROW_PAGE.RENEWALS' | translate }}: {{ l.renewCount }}</span>
                      <span [class.text-red-600]="overdue(l)" [class.font-semibold]="overdue(l)">{{ remaining(l) }}</span>
                      @if (l.note) { <span class="text-amber-700">{{ l.note }}</span> }
                    </div>
                  </td>
                  <td class="td text-[13px]">{{ l.loanedAt | ldate: 'dd/MM/yyyy' }}</td>
                  <td class="td text-[13px]" [class.text-red-600]="overdue(l)" [class.font-semibold]="overdue(l)">{{ l.dueAt | ldate: 'dd/MM/yyyy' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="5" class="py-8 text-center text-gray-500">{{ 'BORROW_PAGE.NO_LOANS' | translate }}</td></tr>
              }
            </tbody>
          </table>
        </div>
        <div class="flex items-center gap-1 px-3 py-2 border-t border-gray-100 bg-gray-50 flex-wrap">
          <button type="button" class="tool-btn" [title]="'BORROW_PAGE.TOOLBAR_ADD' | translate" (click)="focusItem()"><span class="material-icons text-[20px]">add</span></button>
          @if (can('edit')) {
            <button type="button" class="tool-btn" [disabled]="busy()" [title]="'BORROW_PAGE.TOOLBAR_RETURN' | translate" (click)="returnSelected()"><span class="material-icons text-[20px]">assignment_return</span></button>
            <button type="button" class="tool-btn !text-white !bg-amber-500 hover:!bg-amber-600" [disabled]="busy()" [title]="'BORROW_PAGE.TOOLBAR_RENEW' | translate" (click)="openAction('renew')"><span class="material-icons text-[20px]">autorenew</span></button>
            <button type="button" class="tool-btn" [disabled]="busy()" [title]="'BORROW_PAGE.TOOLBAR_NOTE' | translate" (click)="openAction('note')"><span class="material-icons text-[20px]">edit_note</span></button>
          }
          @if (session.can('LOAN_HISTORY:view')) {
            <button type="button" class="tool-btn !text-blue-700" [title]="'BORROW_PAGE.TOOLBAR_HISTORY' | translate" (click)="history()"><span class="material-icons text-[20px]">history</span></button>
          }
          <button type="button" class="tool-btn" [disabled]="busy()" [title]="'BORROW_PAGE.TOOLBAR_REFRESH' | translate" (click)="reload()"><span class="material-icons text-[20px]">refresh</span></button>
          @if (session.can('READERS:edit') && !r.isLocked) {
            <button type="button" class="tool-btn" [disabled]="busy()" [title]="'BORROW_PAGE.TOOLBAR_LOCK' | translate" (click)="openAction('lock')"><span class="material-icons text-[20px]">lock</span></button>
          }
          @if (canFine()) {
            <button type="button" class="tool-btn !text-white !bg-red-500 hover:!bg-red-600" [disabled]="busy() || (!r.hasOverdue && !selected().size)" [title]="'BORROW_PAGE.TOOLBAR_FINE' | translate" (click)="fine([...selected()])"><span class="material-icons text-[20px]">gavel</span></button>
          }
        </div>
      </div>
    } @else {
      <div class="panel text-center text-gray-400 py-12">
        <span class="material-icons text-5xl mb-2 text-gray-300">badge</span>
        <p>{{ 'BORROW_PAGE.SCAN_CARD' | translate }}</p>
      </div>
    }

    @if (returns().length) {
      <div class="panel">
        <div class="font-semibold text-gray-800 mb-2">{{ 'BORROW_PAGE.RECENT_RETURNS' | translate }}</div>
        <ul class="space-y-2">
          @for (r of returns(); track r.loan.publicId) {
            <li class="text-sm border border-gray-100 rounded-lg p-2.5" [class.border-red-200]="r.overdueDays > 0" [class.bg-red-50]="r.overdueDays > 0">
              <div><span class="font-mono font-medium">{{ r.loan.barcode }}</span> — {{ r.loan.title ?? '' }}</div>
              <div class="text-xs text-gray-600">{{ 'BORROW_PAGE.RETURNED_LINE' | translate: { name: r.loan.readerName ?? '', card: r.loan.cardNo, date: (r.loan.loanedAt | ldate: 'dd/MM/yyyy') } }}</div>
              @if (r.overdueDays > 0) { <div class="text-xs font-semibold text-red-600">{{ 'BORROW_PAGE.OVERDUE_DAYS' | translate: { days: r.overdueDays } }}</div> }
              @if (r.holdFor; as h) {
                <div class="text-xs font-semibold text-blue-700 mt-1">{{ 'BORROW_PAGE.HOLD_SHELF' | translate: { name: h.readerName ?? '', card: h.cardNo, date: (h.expiresAt | ldate: 'dd/MM/yyyy') } }}</div>
              }
            </li>
          }
        </ul>
      </div>
    }

    @if (camera()) {
      <app-camera-capture title="BORROW_PAGE.CAMERA_TITLE" hint="BORROW_PAGE.CAMERA_HINT" captureLabel="BORROW_PAGE.CAMERA_CAPTURE" [maxSize]="640"
                          (captured)="identify($event)" (cancelled)="camera.set(false)" />
    }

    @if (action(); as a) {
      <app-modal [title]="(a.kind === 'renew' ? 'BORROW_PAGE.RENEW_TITLE_N' : a.kind === 'note' ? 'BORROW_PAGE.NOTE_TITLE_N' : 'BORROW_PAGE.LOCK_TITLE') | translate: { count: a.loans.length, card: reader()?.cardNo ?? '' }"
                 widthClass="max-w-md" (closed)="action.set(null)">
        <div class="space-y-4">
          @if (a.kind === 'note') {
            <div>
              <label for="bw-note" class="form-label">{{ 'BORROW_PAGE.NOTE' | translate }}</label>
              <input id="bw-note" class="input" maxlength="1000" [(ngModel)]="noteText" />
            </div>
          } @else if (a.kind === 'renew') {
            <p class="text-sm text-gray-600">{{ 'BORROW_PAGE.RENEW_APPLY' | translate: { count: a.loans.length } }}</p>
          }
          <div>
            <label for="bw-reason" class="form-label">{{ 'BORROW_PAGE.REASON' | translate }}</label>
            <input id="bw-reason" class="input" maxlength="500" [(ngModel)]="reason" [placeholder]="'BORROW_PAGE.REQUIRED' | translate" />
          </div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="action.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy() || !reason.trim()" (click)="runAction(a)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (confirmReturn(); as o) {
      <app-modal [title]="'BORROW_PAGE.RETURN_CONFIRM_TITLE' | translate" widthClass="max-w-lg" (closed)="confirmReturn.set(null)">
        <div class="space-y-3 text-sm text-gray-700">
          <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 bg-gray-50 rounded-lg p-4">
            <dt class="text-gray-500">{{ 'BORROW_PAGE.BARCODE' | translate }}</dt><dd class="font-semibold font-mono">{{ o.barcode }}</dd>
            <dt class="text-gray-500">{{ 'BORROW_PAGE.TITLE_COL' | translate }}</dt><dd>{{ o.title ?? '—' }}</dd>
            <dt class="text-gray-500">{{ 'BORROW_PAGE.RETURN_READER' | translate }}</dt><dd><span class="font-semibold">{{ o.cardNo }}</span> – {{ o.readerName ?? '—' }}</dd>
            <dt class="text-gray-500">{{ 'BORROW_PAGE.LOANED' | translate }}</dt><dd>{{ o.loanedAt | ldate: 'dd/MM/yyyy' }}</dd>
            <dt class="text-gray-500">{{ 'BORROW_PAGE.DUE' | translate }}</dt><dd>{{ o.dueAt | ldate: 'dd/MM/yyyy' }}</dd>
          </dl>
          @if (reader() && reader()!.readerPublicId !== o.readerPublicId) {
            <p class="text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">{{ 'BORROW_PAGE.RETURN_OTHER_READER' | translate }}</p>
          }
          @if (overdueDays(o) > 0) {
            <p class="font-semibold text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2"
               [innerHTML]="'BORROW_PAGE.OVERDUE_CONFIRM' | translate: { barcode: o.barcode, name: o.readerName ?? '', card: o.cardNo, date: (o.dueAt | ldate: 'dd/MM/yyyy'), days: overdueDays(o) }"></p>
            <p class="text-xs text-gray-500">{{ 'BORROW_PAGE.OVERDUE_CONFIRM_HINT' | translate }}</p>
          }
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" [disabled]="busy()" (click)="confirmReturn.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          @if (overdueDays(o) > 0 && canFine()) {
            <button type="button" class="btn-danger" [disabled]="busy()" (click)="returnWithFine(o)">
              <span class="material-icons text-[18px]">gavel</span> {{ 'BORROW_PAGE.RETURN_WITH_FINE' | translate }}</button>
          }
          <button type="button" #confirmBtn class="btn-primary" [disabled]="busy()" (click)="returnNoFine(o)">
            {{ (overdueDays(o) > 0 ? 'BORROW_PAGE.RETURN_NO_FINE' : 'BORROW_PAGE.RETURN') | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (bulk()) {
      <app-modal [title]="'BORROW_PAGE.BULK_TITLE' | translate: { card: reader()?.cardNo ?? '' }" widthClass="max-w-lg" (closed)="bulk.set(false)">
        <div class="space-y-3">
          <div class="grid grid-cols-3 gap-3">
            <div><label for="bk-prefix" class="form-label">{{ 'BORROW_PAGE.BULK_PREFIX' | translate }}</label><input id="bk-prefix" class="input font-mono" [(ngModel)]="bulkPrefix" /></div>
            <div><label for="bk-from" class="form-label">{{ 'BORROW_PAGE.BULK_FROM' | translate }}</label><input id="bk-from" class="input font-mono" inputmode="numeric" [(ngModel)]="bulkFrom" /></div>
            <div><label for="bk-to" class="form-label">{{ 'BORROW_PAGE.BULK_TO' | translate }}</label><input id="bk-to" class="input font-mono" inputmode="numeric" [(ngModel)]="bulkTo" /></div>
          </div>
          <div>
            <label for="bk-manual" class="form-label">{{ 'BORROW_PAGE.BULK_MANUAL' | translate }} <span class="text-gray-400 font-normal">({{ 'BORROW_PAGE.BULK_MANUAL_HINT' | translate }})</span></label>
            <textarea id="bk-manual" rows="3" class="input font-mono resize-none" [(ngModel)]="bulkManual"></textarea>
          </div>
          <p class="text-xs text-gray-500">{{ 'BORROW_PAGE.BULK_HINT' | translate: { max: maxBulk } }}</p>
          @if (bulkResults().length) {
            <div class="max-h-48 overflow-y-auto border border-gray-100 rounded-lg">
              @for (l of bulkResults(); track $index) {
                <div class="flex items-center justify-between gap-3 px-3 py-1.5 text-sm border-b border-gray-50 last:border-0" [class.bg-red-50]="!l.success" [class.bg-green-50]="l.success">
                  <span class="font-mono">{{ l.barcode }}</span><span [class]="l.success ? 'text-green-700' : 'text-red-600'">{{ l.message }}</span>
                </div>
              }
            </div>
          }
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="bulk.set(false)">{{ 'BORROW_PAGE.BULK_CLOSE' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="submitBulk()">{{ 'BORROW_PAGE.CHECKOUT' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
  styles: `
    .tool-btn { width: 2.25rem; height: 2.25rem; border-radius: 0.5rem; display: inline-flex; align-items: center; justify-content: center; color: #4b5563; }
    .tool-btn:hover:not(:disabled) { background: #e5e7eb; }
    .tool-btn:disabled { opacity: 0.4; cursor: not-allowed; }
  `,
})
export class Borrow implements OnInit {
  private readonly api = inject(Api);
  protected readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly language = inject(Language);
  private readonly router = inject(Router);
  private readonly itemInput = viewChild<ElementRef<HTMLInputElement>>('itemInput');
  private readonly confirmBtn = viewChild<ElementRef<HTMLButtonElement>>('confirmBtn');

  protected readonly maxBulk = MAX_BULK;
  protected readonly places = signal<CircPlace[]>([]);
  protected readonly placeId = signal<number | null>(null);
  protected readonly reader = signal<ReaderPanel | null>(null);
  protected readonly photoUrl = signal<string | null>(null);
  protected readonly lines = signal<CheckoutLine[]>([]);
  protected readonly returns = signal<ReturnResult[]>([]);
  protected readonly busy = signal(false);
  protected readonly faceEnabled = signal(false);
  protected readonly camera = signal(false);
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly action = signal<{ loans: Loan[]; kind: ActionKind } | null>(null);
  /** Lượt đang chờ cán bộ xác nhận trả (hiện chi tiết; quá hạn thì chọn Huỷ / Lập phiếu phạt / Trả không phạt). */
  protected readonly confirmReturn = signal<Loan | null>(null);
  protected readonly bulk = signal(false);
  protected readonly bulkResults = signal<CheckoutLine[]>([]);
  protected cardNo = '';
  protected barcode = '';
  protected reason = '';
  protected noteText = '';
  protected bulkPrefix = '';
  protected bulkFrom = '';
  protected bulkTo = '';
  protected bulkManual = '';

  /** Đã chọn bạn đọc được phép mượn → ô ĐKCB là mượn; ngược lại là trả. */
  protected readonly lending = computed(() => {
    const r = this.reader();
    return !!r && r.canBorrow && this.session.can('BORROW:add');
  });
  protected readonly blocked = computed(() => !!this.reader()?.isLocked);
  protected readonly overdueCount = computed(() => this.reader()?.currentLoans.filter((l) => this.overdue(l)).length ?? 0);
  protected readonly renewedCount = computed(() => this.reader()?.currentLoans.filter((l) => l.renewCount > 0).length ?? 0);
  protected readonly allSelected = computed(() => {
    const loans = this.reader()?.currentLoans ?? [];
    return loans.length > 0 && loans.every((l) => this.selected().has(l.publicId));
  });

  async ngOnInit(): Promise<void> {
    this.api.faceStatus().then((s) => this.faceEnabled.set(s.enabled), () => this.faceEnabled.set(false));
    try {
      const places = await this.api.crud<CircPlace>('circ-places', 'circulation').searchAll();
      this.places.set(places);
      const saved = Number(readSaved());
      this.placeId.set(places.find((p) => p.id === saved)?.id ?? places[0]?.id ?? null);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  /** "Chính sách: mượn 14 ngày, tối đa 5 tài liệu, gia hạn tối đa 2 lần." */
  protected policy(r: ReaderPanel): string {
    let text = this.language.t('BORROW_PAGE.POLICY', { days: r.loanDays });
    if (r.maxLoans != null) text += this.language.t('BORROW_PAGE.POLICY_MAX', { count: r.maxLoans });
    if (r.maxRenewals != null) text += this.language.t('BORROW_PAGE.POLICY_RENEW', { count: r.maxRenewals });
    return text + '.';
  }

  protected can(action: string): boolean {
    return this.session.can(`BORROW:${action}`);
  }

  protected canFine(): boolean {
    return this.session.can('FINES:add');
  }

  protected overdue(l: Loan): boolean {
    return new Date(l.dueAt).getTime() < Date.now();
  }

  /** Số ngày quá hạn theo ngày lịch (trả trong ngày hết hạn = không quá hạn) — như Loan.OverdueDays của circulation. */
  protected overdueDays(l: Loan): number {
    const day = (d: Date) => Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()) / 86_400_000;
    return Math.max(0, day(new Date()) - day(new Date(l.dueAt)));
  }

  /** "Còn 3 ngày" / "Quá hạn 2 ngày". */
  protected remaining(l: Loan): string {
    const days = this.overdueDays(l);
    if (days > 0) return this.language.t('BORROW_PAGE.OVERDUE_DAYS', { days });
    const day = (d: Date) => Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()) / 86_400_000;
    return this.language.t('BORROW_PAGE.DAYS_LEFT', { days: Math.max(0, day(new Date(l.dueAt)) - day(new Date())) });
  }

  protected choosePlace(id: number | null): void {
    this.placeId.set(id);
    try {
      localStorage.setItem(PLACE_KEY, String(id ?? ''));
    } catch {
      /* trình duyệt chặn lưu — lần sau chọn lại */
    }
  }

  protected focusItem(): void {
    this.itemInput()?.nativeElement.focus();
  }

  /** Phiếu mượn khổ nhỏ ở trang in riêng (/print/loan-slip) — bản in chỉ có nội dung phiếu, không kèm khung quản trị. */
  protected print(): void {
    const r = this.reader();
    if (!r) return;
    const day = (iso: string) => formatDate(iso, 'dd/MM/yyyy', this.language.locale());
    const slip: LoanSlip = {
      at: formatDate(new Date(), 'dd/MM/yyyy HH:mm', this.language.locale()), cardNo: r.cardNo, fullName: r.fullName, className: r.className,
      loans: r.currentLoans.map((l) => ({ title: l.title ?? '', barcode: l.barcode, loanedOn: day(l.loanedAt), dueOn: day(l.dueAt), note: l.note })),
    };
    try {
      sessionStorage.setItem(LOAN_SLIP_KEY, JSON.stringify(slip));
    } catch {
      this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
      return;
    }
    void this.router.navigateByUrl('/print/loan-slip');
  }

  protected history(): void {
    void this.router.navigate(['/loan-history']);
  }

  // ── Chọn lượt mượn ──
  protected toggle(id: string): void {
    this.selected.update((s) => {
      const next = new Set(s);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }

  protected toggleAll(): void {
    const loans = this.reader()?.currentLoans ?? [];
    this.selected.set(this.allSelected() ? new Set() : new Set(loans.map((l) => l.publicId)));
  }

  private chosenLoans(): Loan[] {
    const ids = this.selected();
    return (this.reader()?.currentLoans ?? []).filter((l) => ids.has(l.publicId));
  }

  // ── Nhận diện khuôn mặt ──
  protected async identify(photo: CapturedPhoto): Promise<void> {
    this.camera.set(false);
    await this.run(async () => {
      const match = await this.api.faceIdentify(photo.dataUrl);
      if (!match) {
        this.toastr.warning('BORROW_PAGE.FACE_FAIL');
        return;
      }
      this.toastr.success('BORROW_PAGE.FACE_FOUND', { name: match.fullName, card: match.cardNo });
      this.cardNo = match.cardNo;
      await this.loadReaderCore();
    });
  }

  // ── Bạn đọc ──
  protected async loadReader(): Promise<void> {
    if (!this.cardNo.trim()) return;
    await this.run(() => this.loadReaderCore());
  }

  private async loadReaderCore(): Promise<void> {
    const panel = await this.api.loanReader(this.cardNo.trim(), this.placeId());
    this.reader.set(panel);
    this.lines.set([]);
    this.selected.set(new Set());
    this.photoUrl.set(null);
    if (panel.photoId) this.api.fileUrl(panel.photoId).then((u) => this.photoUrl.set(u), () => this.photoUrl.set(null));
    setTimeout(() => this.itemInput()?.nativeElement.focus());
  }

  protected async reload(): Promise<void> {
    await this.run(() => this.refresh());
  }

  private async refresh(): Promise<void> {
    const r = this.reader();
    if (!r) return;
    const panel = await this.api.loanReader(r.cardNo, this.placeId());
    this.reader.set(panel);
    const open = new Set(panel.currentLoans.map((l) => l.publicId));
    this.selected.update((s) => new Set([...s].filter((id) => open.has(id))));
  }

  // ── Mượn / trả ──
  /**
   * Một ô ĐKCB, như ELIB-LRC: tài liệu đang được mượn (của bạn đọc đang xem hay của bất kỳ ai) → hộp thoại xác nhận trả; không thì,
   * nếu đã chọn bạn đọc được mượn → mượn; còn lại báo tài liệu không đang được mượn.
   */
  protected async scan(): Promise<void> {
    const code = this.barcode.trim();
    if (!code) return;
    const key = code.toUpperCase();
    const own = this.reader()?.currentLoans.find((l) => l.barcode.toUpperCase() === key);
    if (own) {
      this.askReturn(own);
      return;
    }
    await this.run(async () => {
      // Lượt đang mở của bất kỳ bạn đọc nào; không tra được (thiếu quyền xem lịch sử…) thì coi như chưa biết.
      const lookup = this.can('edit') ? await this.api.searchLoans({ barcode: code, state: 'open', pageIndex: 1, pageSize: 1 }).then((p) => ({ ok: true, loan: p.items[0] ?? null }), () => ({ ok: false, loan: null })) : { ok: true, loan: null };
      if (lookup.loan) {
        this.askReturn(lookup.loan);
      } else if (this.lending()) {
        await this.checkout(code);
      } else if (this.reader() == null && lookup.ok === false) {
        // Không tra được lượt mượn: thử trả thẳng theo ĐKCB (như cũ).
        this.afterReturn(await this.api.returnLoan({ barcode: code, circPlaceId: this.placeId() }));
      } else {
        this.toastr.warning('BORROW_PAGE.NO_ACTIVE_LOAN', { barcode: code });
        setTimeout(() => this.itemInput()?.nativeElement.select());
      }
    });
  }

  private askReturn(loan: Loan): void {
    if (!this.can('edit')) {
      this.toastr.error('BORROW_PAGE.NO_RETURN_RIGHT');
      return;
    }
    this.confirmReturn.set(loan);
    setTimeout(() => this.confirmBtn()?.nativeElement.focus());
  }

  private async checkout(code: string): Promise<void> {
    const r = this.reader();
    const place = this.placeId();
    if (!r) return;
    if (place == null) {
      this.toastr.error('BORROW_PAGE.CHOOSE_PLACE');
      return;
    }
    const result = await this.api.checkout(r.cardNo, [code], place);
    this.lines.update((all) => [...result.lines, ...all].slice(0, 20));
    this.barcode = '';
    await this.refresh();
  }

  /** Trả các lượt đã chọn trong bảng; lượt quá hạn thì dừng để hỏi (huỷ / phiếu phạt / không phạt) trước, còn lại trả luôn. */
  protected async returnSelected(): Promise<void> {
    const loans = this.chosenLoans();
    if (!loans.length) {
      this.toastr.warning('BORROW_PAGE.SELECT_FIRST');
      return;
    }
    const late = loans.find((l) => this.overdueDays(l) > 0);
    if (late) {
      this.confirmReturn.set(late);
      return;
    }
    await this.run(async () => {
      for (const l of loans) await this.doReturn(l);
    });
  }

  protected async returnNoFine(l: Loan): Promise<void> {
    await this.run(async () => {
      await this.doReturn(l);
      this.confirmReturn.set(null);
    });
  }

  /** Lập phiếu phạt TRƯỚC khi trả (phiếu gom lượt đang mượn quá hạn, tính số ngày đến hôm nay), rồi trả và mở phiếu để thu tiền. */
  protected async returnWithFine(l: Loan): Promise<void> {
    await this.run(async () => {
      const detail = await this.api.buildFineTicket(l.cardNo, [], this.placeId());
      await this.doReturn(l);
      this.confirmReturn.set(null);
      await this.router.navigate(['/fine-ticket', detail.ticket.publicId]);
    });
  }

  private async doReturn(l: Loan): Promise<void> {
    this.afterReturn(await this.api.returnLoan({ loanId: l.publicId, circPlaceId: this.placeId() }));
  }

  private afterReturn(result: ReturnResult): void {
    this.returns.update((all) => [result, ...all].slice(0, 20));
    if (this.barcode.trim() && result.loan.barcode.toUpperCase() === this.barcode.trim().toUpperCase()) this.barcode = '';
    if (this.reader()?.readerPublicId === result.loan.readerPublicId) void this.refresh();
  }

  /** Gom tài liệu quá hạn (+ lượt chọn — báo mất) vào phiếu phạt đang mở của bạn đọc, mở phiếu để thu tiền. */
  protected async fine(loanIds: string[]): Promise<void> {
    const r = this.reader();
    if (!r) return;
    await this.run(async () => {
      const detail = await this.api.buildFineTicket(r.cardNo, loanIds);
      await this.router.navigate(['/fine-ticket', detail.ticket.publicId]);
    });
  }

  // ── Gia hạn / ghi chú / khoá thẻ ──
  protected openAction(kind: ActionKind): void {
    const loans = this.chosenLoans();
    if (kind !== 'lock' && !loans.length) {
      this.toastr.warning('BORROW_PAGE.SELECT_FIRST');
      return;
    }
    this.reason = '';
    this.noteText = kind === 'note' && loans.length === 1 ? (loans[0].note ?? '') : '';
    this.action.set({ loans, kind });
  }

  protected async runAction(a: { loans: Loan[]; kind: ActionKind }): Promise<void> {
    await this.run(async () => {
      const reason = this.reason.trim();
      if (a.kind === 'lock') {
        const r = this.reader();
        if (r) await this.api.lockReader(r.readerPublicId, reason);
        this.toastr.success('BORROW_PAGE.LOCK_DONE');
      } else if (a.kind === 'renew') {
        let done = 0;
        for (const l of a.loans) {
          try {
            const renewed = await this.api.renewLoan(l.publicId, reason);
            done++;
            if (a.loans.length === 1) this.toastr.success('BORROW_PAGE.RENEWED', { barcode: renewed.barcode, date: formatDate(renewed.dueAt, 'dd/MM/yyyy', this.language.locale()) });
          } catch (e) {
            this.toastr.error(`${l.barcode}: ${errorMessage(e)}`);
          }
        }
        if (a.loans.length > 1) this.toastr.success('BORROW_PAGE.RENEWED_N', { done, total: a.loans.length });
      } else {
        for (const l of a.loans) await this.api.noteLoan(l.publicId, this.noteText, reason);
        this.toastr.success('BORROW_PAGE.NOTE_SAVED');
      }
      this.action.set(null);
      await this.refresh();
    });
  }

  // ── Mượn nhiều ──
  protected openBulk(): void {
    this.bulkResults.set([]);
    this.bulk.set(true);
  }

  /** ĐKCB cần mượn: khoảng (tiền tố + số từ…đến, giữ số 0 đầu theo độ dài ô "từ") và danh sách nhập tay; bỏ trùng, giữ thứ tự. */
  private bulkCodes(): string[] {
    const codes: string[] = [];
    const from = this.bulkFrom.trim();
    const to = this.bulkTo.trim();
    if (/^\d+$/.test(from) && /^\d+$/.test(to)) {
      const a = Number(from);
      const b = Number(to);
      if (b - a >= MAX_BULK) throw new Error(this.language.t('BORROW_PAGE.BULK_TOO_MANY', { max: MAX_BULK }));
      for (let n = a; n <= b; n++) codes.push(this.bulkPrefix.trim() + String(n).padStart(from.length, '0'));
    }
    codes.push(...this.bulkManual.split(/[\s,;]+/).map((c) => c.trim()).filter(Boolean));
    return [...new Set(codes)];
  }

  protected async submitBulk(): Promise<void> {
    const r = this.reader();
    const place = this.placeId();
    if (!r) return;
    if (place == null) {
      this.toastr.error('BORROW_PAGE.CHOOSE_PLACE');
      return;
    }
    let codes: string[];
    try {
      codes = this.bulkCodes();
    } catch (e) {
      this.toastr.error((e as Error).message);
      return;
    }
    if (!codes.length) {
      this.toastr.warning('BORROW_PAGE.BULK_EMPTY');
      return;
    }
    if (codes.length > MAX_BULK) {
      this.toastr.error('BORROW_PAGE.BULK_TOO_MANY', { max: MAX_BULK });
      return;
    }
    await this.run(async () => {
      const result = await this.api.checkout(r.cardNo, codes, place);
      this.bulkResults.set(result.lines);
      await this.refresh();
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

function readSaved(): string | null {
  try {
    return localStorage.getItem(PLACE_KEY);
  } catch {
    return null;
  }
}
