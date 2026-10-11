import { NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudClient, CrudPage, Reader, ReaderAccount, ReaderSearch, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ImportDialog } from '../../shared/import-dialog';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator, StatusBadge } from '../../shared/ui';
import { ReaderCatalogs } from './reader-catalogs';
import { ReaderFormDialog } from './reader-form';
import { ReaderPassword, accountLabel } from './reader-password';
import { ReaderExport, ReaderPhotos } from './reader-tools';

/** Cột của bảng bạn đọc — người dùng chọn hiện/ẩn (nhớ trên trình duyệt), mặc định như ELIB-LRC. */
type ColumnKey = 'cardNo' | 'fullName' | 'sex' | 'birthDate' | 'readerType' | 'class' | 'course' | 'org' | 'ethnicity' | 'degree'
  | 'citizenId' | 'cardUid' | 'email' | 'phone' | 'address' | 'issueDate' | 'expireDate' | 'status' | 'opac';
const COLUMNS: { key: ColumnKey; label: string; shown: boolean }[] = [
  { key: 'cardNo', label: 'READER.CARD_CODE', shown: true },
  { key: 'fullName', label: 'READER.FULL_NAME', shown: true },
  { key: 'sex', label: 'READER.GENDER', shown: false },
  { key: 'birthDate', label: 'READER.BIRTH_DATE', shown: false },
  { key: 'readerType', label: 'READER.READER_TYPE', shown: true },
  { key: 'class', label: 'READER.ACADEMIC_CLASS', shown: true },
  { key: 'course', label: 'READER.COURSE', shown: false },
  { key: 'org', label: 'READER.ORG', shown: false },
  { key: 'ethnicity', label: 'READER.ETHNIC', shown: false },
  { key: 'degree', label: 'READER.DEGREE', shown: false },
  { key: 'citizenId', label: 'READER.CITIZEN_ID', shown: false },
  { key: 'cardUid', label: 'READER.CARD_UID', shown: false },
  { key: 'email', label: 'READER.EMAIL', shown: false },
  { key: 'phone', label: 'READER.PHONE', shown: false },
  { key: 'address', label: 'READER.ADDRESS', shown: false },
  { key: 'issueDate', label: 'READER.ISSUED_DATE', shown: true },
  { key: 'expireDate', label: 'READER.EXPIRED_DATE', shown: true },
  { key: 'status', label: 'READER.STATUS', shown: true },
  { key: 'opac', label: 'READER.OPAC_LOGIN', shown: true },
];
const COLUMNS_KEY = 'elib.readers.columns';
const VIEW_KEY = 'elib.readers.view';

/**
 * Bạn đọc như ELIB-LRC (pages/admin/reader): bộ lọc họ/tên/mã thẻ/loại/lớp/khoá/đơn vị/trạng thái/ngày cấp/hạn thẻ, bảng có chọn cột
 * hoặc dạng thẻ, thêm/sửa đủ trường, ảnh thẻ, khoá/mở thẻ, sửa hàng loạt, mật khẩu OPAC, nhập/xuất Excel, tải ảnh hàng loạt.
 */
@Component({
  selector: 'app-readers',
  imports: [FormsModule, NgTemplateOutlet, LDatePipe, TranslatePipe, Loading, Modal, Paginator, StatusBadge, ConfirmDelete, ImportDialog,
    ReaderExport, ReaderPhotos, ReaderPassword, ReaderFormDialog],
  providers: [ReaderCatalogs],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'MENU.READERS' | translate }}</h4></div>

    <div class="panel">
      <form (ngSubmit)="find()" class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-6 gap-4">
        <div class="xl:col-span-2">
          <label for="kw" class="field-label">{{ 'READER.KEYWORD_LABEL' | translate }}</label>
          <input id="kw" name="kw" class="input" [(ngModel)]="search.keyword" [placeholder]="'READER.KEYWORD_PH' | translate" />
        </div>
        <div><label for="f-card" class="field-label">{{ 'READER.CARD_CODE' | translate }}</label>
          <input id="f-card" name="card" class="input font-mono" [(ngModel)]="search.cardNoContains" /></div>
        <div><label for="f-type" class="field-label">{{ 'READER.READER_TYPE' | translate }}</label>
          <select id="f-type" name="type" class="input" [(ngModel)]="search.readerTypeId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (t of c.types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
          </select></div>
        <div><label for="f-class" class="field-label">{{ 'READER.ACADEMIC_CLASS' | translate }}</label>
          <select id="f-class" name="class" class="input" [(ngModel)]="search.classId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (o of c.classes(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }
          </select></div>
        <div><label for="f-status" class="field-label">{{ 'READER.STATUS' | translate }}</label>
          <select id="f-status" name="status" class="input" [(ngModel)]="search.status" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option [ngValue]="2">{{ 'COMMON.ACTIVE' | translate }}</option>
            <option [ngValue]="1">{{ 'READER.LOCKED' | translate }}</option>
          </select></div>
        @if (moreFilters()) {
          <div><label for="f-last" class="field-label">{{ 'READER.FIRST_NAME' | translate }}</label>
            <input id="f-last" name="last" class="input" [(ngModel)]="search.lastName" /></div>
          <div><label for="f-first" class="field-label">{{ 'READER.LAST_NAME' | translate }}</label>
            <input id="f-first" name="first" class="input" [(ngModel)]="search.firstName" /></div>
          <div><label for="f-course" class="field-label">{{ 'READER.COURSE' | translate }}</label>
            <select id="f-course" name="course" class="input" [(ngModel)]="search.courseId" (change)="find()">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              @for (o of c.courses(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }
            </select></div>
          <div class="xl:col-span-2"><label for="f-org" class="field-label">{{ 'READER.ORG' | translate }}</label>
            <select id="f-org" name="org" class="input" [(ngModel)]="orgFilter" (change)="find()">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              @for (o of c.orgOptions(); track o.id) { <option [ngValue]="o.id">{{ indent(o.depth) }}{{ o.name }}</option> }
            </select></div>
          <div><label for="f-exp" class="field-label">{{ 'READER.EXPIRY_FILTER' | translate }}</label>
            <select id="f-exp" name="expired" class="input" [(ngModel)]="search.expired" (change)="find()">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              <option [ngValue]="false">{{ 'READER.NOT_EXPIRED' | translate }}</option>
              <option [ngValue]="true">{{ 'READER.EXPIRED' | translate }}</option>
            </select></div>
          <div><label for="f-if" class="field-label">{{ 'READER.ISSUED_FROM' | translate }}</label>
            <input id="f-if" name="if" type="date" class="input" [(ngModel)]="search.issuedFrom" /></div>
          <div><label for="f-it" class="field-label">{{ 'READER.ISSUED_TO' | translate }}</label>
            <input id="f-it" name="it" type="date" class="input" [(ngModel)]="search.issuedTo" /></div>
          <div><label for="f-ef" class="field-label">{{ 'READER.EXPIRED_FROM' | translate }}</label>
            <input id="f-ef" name="ef" type="date" class="input" [(ngModel)]="search.expiredFrom" /></div>
          <div><label for="f-et" class="field-label">{{ 'READER.EXPIRED_TO' | translate }}</label>
            <input id="f-et" name="et" type="date" class="input" [(ngModel)]="search.expiredTo" /></div>
        }
        <div class="sm:col-span-2 xl:col-span-6 flex justify-between items-center gap-2 flex-wrap">
          <button type="button" class="text-sm text-blue-600 hover:underline inline-flex items-center gap-1" (click)="moreFilters.set(!moreFilters())">
            <span class="material-icons text-[18px]">{{ moreFilters() ? 'expand_less' : 'tune' }}</span>
            {{ (moreFilters() ? 'READER.LESS_FILTERS' : 'READER.MORE_FILTERS') | translate }}</button>
          <div class="flex gap-2">
            <button type="button" class="btn-secondary" (click)="reset()">{{ 'COMMON.RESET' | translate }}</button>
            <button type="submit" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
          </div>
        </div>
      </form>
    </div>

    <div class="panel flex gap-2 items-center justify-between flex-wrap">
      <div class="flex gap-2 flex-wrap">
        @if (can('add')) {
          <button (click)="editing.set({ reader: null })" class="btn-add"><span class="material-icons text-[18px]">add</span> {{ 'READER.ADD_TITLE' | translate }}</button>
          <button (click)="importing.set(true)" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">upload_file</span> {{ 'COMMON.IMPORT_EXCEL' | translate }}</button>
        }
        <button (click)="exporting.set(true)" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">download</span> {{ 'READER.EXPORT_CONFIRM' | translate }}</button>
        @if (can('edit')) {
          <button (click)="uploadingPhotos.set(true)" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">photo_library</span> {{ 'READER.UPLOAD_PHOTOS' | translate }}</button>
          <button (click)="openBulk()" [disabled]="selected().size === 0" class="btn-secondary !py-1.5 !px-3">
            <span class="material-icons text-[18px]">tune</span> {{ 'READER.BULK_EDIT' | translate: { count: selected().size } }}</button>
          <button (click)="passwordFor.set({ readers: selectedReaders(), bulk: true })" [disabled]="selected().size === 0" class="btn-secondary !py-1.5 !px-3">
            <span class="material-icons text-[18px]">key</span> {{ 'READER.SET_OPAC_PASSWORD' | translate: { count: selected().size } }}</button>
        }
      </div>
      <div class="flex items-center gap-2">
        <span class="text-sm text-gray-500">{{ 'READER.COUNT' | translate: { count: page()?.totalCount ?? 0 } }}</span>
        <div class="relative">
          <button type="button" class="btn-secondary !py-1.5 !px-2" [title]="'READER.COLUMNS' | translate" (click)="columnsOpen.set(!columnsOpen())">
            <span class="material-icons text-[18px]">view_column</span></button>
          @if (columnsOpen()) {
            <div class="absolute right-0 mt-1 w-56 bg-white border border-gray-200 rounded-lg shadow-lg p-2 z-30">
              <div class="text-xs font-semibold text-gray-500 px-1 pb-1">{{ 'READER.COLUMNS' | translate }}</div>
              @for (col of columns; track col.key) {
                <label class="flex items-center gap-2 px-1 py-0.5 text-sm text-gray-700 cursor-pointer select-none">
                  <input type="checkbox" class="rounded border-gray-300" [checked]="shown().has(col.key)" (change)="toggleColumn(col.key)" /> {{ col.label | translate }}
                </label>
              }
            </div>
          }
        </div>
        <div class="flex rounded-lg border border-gray-200 overflow-hidden" role="group">
          <button type="button" class="px-2 py-1.5" [class]="view() === 'table' ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-100'"
                  [title]="'READER.VIEW_TABLE' | translate" (click)="setView('table')"><span class="material-icons text-[18px]">table_rows</span></button>
          <button type="button" class="px-2 py-1.5" [class]="view() === 'card' ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-100'"
                  [title]="'READER.VIEW_CARD' | translate" (click)="setView('card')"><span class="material-icons text-[18px]">badge</span></button>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      @if (view() === 'table') {
        <div class="overflow-x-auto w-full">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr>
                <th class="th w-10 text-center"><input type="checkbox" class="rounded border-gray-300" [checked]="allSelected()" (change)="toggleAll()" /></th>
                <th class="th w-12 !text-center">#</th>
                <th class="th w-12"></th>
                @for (col of visibleColumns(); track col.key) { <th class="th whitespace-nowrap">{{ col.label | translate }}</th> }
                <th class="th w-36 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (r of page()?.items ?? []; track r.publicId; let i = $index) {
                <tr class="hover:bg-gray-50/50">
                  <td class="td text-center"><input type="checkbox" class="rounded border-gray-300" [checked]="selected().has(r.publicId)" (change)="toggle(r)" /></td>
                  <td class="td text-center text-gray-500">{{ (search.pageIndex! - 1) * search.pageSize! + i + 1 }}</td>
                  <td class="td">
                    @if (r.photoId && photos()[r.photoId]; as url) { <img [src]="url" alt="" class="w-8 h-10 rounded object-cover border border-gray-200" /> }
                    @else { <span class="w-8 h-10 rounded bg-gray-100 text-gray-300 flex items-center justify-center"><span class="material-icons text-[20px]">person</span></span> }
                  </td>
                  @for (col of visibleColumns(); track col.key) {
                    @switch (col.key) {
                      @case ('cardNo') { <td class="td font-mono text-[13px] whitespace-nowrap">{{ r.cardNo }}</td> }
                      @case ('fullName') {
                        <td class="td font-medium text-gray-800 min-w-[160px]">{{ r.fullName }}
                          @if (r.lockReason) { <div class="text-[11px] text-red-600">{{ 'READER.LOCK_PREFIX' | translate: { reason: r.lockReason } }}</div> }
                        </td>
                      }
                      @case ('sex') { <td class="td">{{ (r.sex === 1 ? 'READER.GENDER_MALE' : r.sex === 0 ? 'READER.GENDER_FEMALE' : '—') | translate }}</td> }
                      @case ('birthDate') { <td class="td whitespace-nowrap">{{ (r.birthDate | ldate) ?? '—' }}</td> }
                      @case ('readerType') { <td class="td">{{ c.name(c.types(), r.readerTypeId) || '—' }}</td> }
                      @case ('class') { <td class="td">{{ c.name(c.classes(), r.classId) || '—' }}</td> }
                      @case ('course') { <td class="td">{{ c.name(c.courses(), r.courseId) || '—' }}</td> }
                      @case ('org') { <td class="td">{{ c.name(c.orgs(), r.orgId) || '—' }}</td> }
                      @case ('ethnicity') { <td class="td">{{ c.name(c.ethnicities(), r.ethnicityId) || '—' }}</td> }
                      @case ('degree') { <td class="td">{{ c.name(c.degrees(), r.degreeId) || '—' }}</td> }
                      @case ('citizenId') { <td class="td font-mono text-[13px]">{{ r.citizenId ?? '—' }}</td> }
                      @case ('cardUid') { <td class="td font-mono text-[13px]">{{ r.cardUid ?? '—' }}</td> }
                      @case ('email') { <td class="td">{{ r.email ?? '—' }}</td> }
                      @case ('phone') { <td class="td whitespace-nowrap">{{ r.phone ?? '—' }}</td> }
                      @case ('address') { <td class="td">{{ r.address ?? '—' }}</td> }
                      @case ('issueDate') { <td class="td whitespace-nowrap">{{ (r.issueDate | ldate) ?? '—' }}</td> }
                      @case ('expireDate') {
                        <td class="td whitespace-nowrap" [class.text-red-600]="isExpired(r)" [title]="isExpired(r) ? ('READER.CARD_EXPIRED_TIP' | translate) : ''">
                          {{ r.expireDate ? (r.expireDate | ldate) : ('READER.NO_EXPIRY' | translate) }}</td>
                      }
                      @case ('status') { <td class="td text-center"><app-status-badge [status]="r.status" inactiveText="READER.LOCKED" /></td> }
                      @case ('opac') {
                        <td class="td text-center">
                          @let acc = accountLabel(accounts()[r.publicId]);
                          <span class="inline-block rounded-full px-2 py-0.5 text-xs whitespace-nowrap {{ acc.css }}">{{ acc.text | translate }}</span>
                        </td>
                      }
                    }
                  }
                  <td class="td"><ng-container *ngTemplateOutlet="actions; context: { $implicit: r }" /></td>
                </tr>
              } @empty {
                <tr><td [attr.colspan]="visibleColumns().length + 4" class="py-8 px-4 text-center text-gray-500">{{ loading() ? '' : ('READER.NO_READERS' | translate) }}</td></tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <div class="grid gap-4 p-4 sm:grid-cols-2 xl:grid-cols-3">
          @for (r of page()?.items ?? []; track r.publicId) {
            <div class="border rounded-xl p-4 flex gap-4" [class]="selected().has(r.publicId) ? 'border-blue-400 bg-blue-50/40' : 'border-gray-200'">
              <div class="shrink-0 flex flex-col items-center gap-2">
                @if (r.photoId && photos()[r.photoId]; as url) { <img [src]="url" alt="" class="w-20 h-24 rounded-lg object-cover border border-gray-200" /> }
                @else { <span class="w-20 h-24 rounded-lg bg-gray-100 text-gray-300 flex items-center justify-center"><span class="material-icons text-[40px]">person</span></span> }
                <input type="checkbox" class="rounded border-gray-300" [checked]="selected().has(r.publicId)" (change)="toggle(r)" [attr.aria-label]="r.cardNo" />
              </div>
              <div class="min-w-0 flex-1 text-sm">
                <div class="font-semibold text-gray-800 truncate">{{ r.fullName }}</div>
                <div class="font-mono text-[13px] text-gray-600">{{ r.cardNo }}</div>
                <div class="text-gray-600 mt-1">{{ c.name(c.types(), r.readerTypeId) }} @if (r.classId) { · {{ c.name(c.classes(), r.classId) }} }</div>
                <div class="text-xs mt-1" [class.text-red-600]="isExpired(r)" [class.text-gray-500]="!isExpired(r)">
                  {{ 'READER.EXPIRED_DATE' | translate }}: {{ r.expireDate ? (r.expireDate | ldate) : ('READER.NO_EXPIRY' | translate) }}</div>
                <div class="mt-2 flex items-center justify-between gap-2 flex-wrap">
                  <app-status-badge [status]="r.status" inactiveText="READER.LOCKED" />
                  <ng-container *ngTemplateOutlet="actions; context: { $implicit: r }" />
                </div>
              </div>
            </div>
          } @empty {
            <p class="py-8 text-center text-gray-500 sm:col-span-2 xl:col-span-3">{{ loading() ? '' : ('READER.NO_READERS' | translate) }}</p>
          }
        </div>
      }
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>

    <ng-template #actions let-r>
      <div class="flex items-center justify-center gap-1.5">
        @if (can('edit')) {
          <button (click)="editing.set({ reader: r })" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate"><span class="material-icons text-[18px]">edit</span></button>
          <button (click)="passwordFor.set({ readers: [r], bulk: false })" class="icon-btn bg-violet-50 text-violet-600 hover:bg-violet-100" [title]="'READER.OPAC_PASSWORD' | translate"><span class="material-icons text-[18px]">key</span></button>
          @if (r.status === 2) {
            <button (click)="locking.set(r); lockReason = ''" class="icon-btn bg-amber-50 text-amber-600 hover:bg-amber-100" [title]="'READER.LOCK_CARD' | translate"><span class="material-icons text-[18px]">lock</span></button>
          } @else {
            <button (click)="unlock(r)" class="icon-btn bg-green-50 text-green-600 hover:bg-green-100" [title]="'READER.UNLOCK_CARD' | translate"><span class="material-icons text-[18px]">lock_open</span></button>
          }
        }
        @if (can('delete')) {
          <button (click)="deleting.set(r)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">delete</span></button>
        }
      </div>
    </ng-template>

    @if (editing(); as e) {
      <app-reader-form [reader]="e.reader" (closed)="editing.set(null)" (saved)="editing.set(null); load()" (changed)="load()" />
    }

    @if (locking(); as r) {
      <app-modal title="READER.LOCK_TITLE" (closed)="locking.set(null)">
        <p class="text-sm text-gray-600 mb-3">{{ 'READER.LOCK_TEXT' | translate: { card: r.cardNo, name: r.fullName } }}</p>
        <label class="form-label" for="lock-reason">{{ 'READER.LOCK_REASON' | translate }}</label>
        <textarea id="lock-reason" rows="3" class="input" [(ngModel)]="lockReason" [placeholder]="'READER.LOCK_REASON_PLACEHOLDER' | translate"></textarea>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="locking.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-danger" [disabled]="saving()" (click)="lock(r)"><span class="material-icons text-[18px]">lock</span>{{ 'READER.LOCK_CARD' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (bulk(); as b) {
      <app-modal [title]="'READER.BULK_TITLE' | translate: { count: selected().size }" (closed)="bulk.set(null)">
        <p class="text-xs text-gray-500 mb-3">{{ 'READER.BULK_HINT' | translate }}</p>
        <div class="grid grid-cols-2 gap-4">
          <div><label class="form-label" for="b-type">{{ 'READER.READER_TYPE' | translate }}</label>
            <select id="b-type" class="input" [(ngModel)]="b.readerTypeId"><option [ngValue]="null">{{ 'READER.KEEP' | translate }}</option>
              @for (t of c.types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
          <div><label class="form-label" for="b-class">{{ 'READER.ACADEMIC_CLASS' | translate }}</label>
            <select id="b-class" class="input" [(ngModel)]="b.classId"><option [ngValue]="null">{{ 'READER.KEEP' | translate }}</option>
              @for (o of c.classes(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="b-course">{{ 'READER.COURSE' | translate }}</label>
            <select id="b-course" class="input" [(ngModel)]="b.courseId"><option [ngValue]="null">{{ 'READER.KEEP' | translate }}</option>
              @for (o of c.courses(); track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label class="form-label" for="b-status">{{ 'READER.STATUS' | translate }}</label>
            <select id="b-status" class="input" [(ngModel)]="b.status"><option [ngValue]="null">{{ 'READER.KEEP' | translate }}</option>
              <option [ngValue]="2">{{ 'COMMON.ACTIVE' | translate }}</option><option [ngValue]="1">{{ 'READER.LOCKED' | translate }}</option></select></div>
          <div><label class="form-label" for="b-issue">{{ 'READER.ISSUED_DATE' | translate }}</label><input id="b-issue" type="date" class="input" [(ngModel)]="b.issueDate" /></div>
          <div><label class="form-label" for="b-exp">{{ 'READER.EXPIRED_DATE' | translate }}</label><input id="b-exp" type="date" class="input" [(ngModel)]="b.expireDate" /></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="bulk.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="saving()" (click)="applyBulk(b)"><span class="material-icons text-[18px]">done_all</span>{{ 'COMMON.UPDATE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (passwordFor(); as pw) {
      <app-reader-password [readers]="pw.readers" [bulk]="pw.bulk" [account]="pw.bulk ? undefined : accounts()[pw.readers[0].publicId]"
                           (closed)="passwordFor.set(null)" (saved)="passwordSaved(pw.bulk)" />
    }

    @if (deleting(); as r) {
      <app-confirm-delete [busy]="saving()" message="READER.DELETE_CONFIRM" [messageParams]="{ card: r.cardNo, name: r.fullName }"
                          (confirmed)="remove(r)" (cancelled)="deleting.set(null)" />
    }

    @if (exporting()) {
      <app-reader-export [search]="effectiveSearch()" [total]="page()?.totalCount ?? 0" (closed)="exporting.set(false)" />
    }

    @if (uploadingPhotos()) {
      <app-reader-photos (closed)="uploadingPhotos.set(false)" (uploaded)="load()" />
    }

    @if (importing()) {
      <app-import-dialog title="MENU.READERS" resource="readers" service="patron" (closed)="importing.set(false)" (imported)="find()" />
    }
  `,
})
export class Readers implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  protected readonly c = inject(ReaderCatalogs);
  private readonly client: CrudClient<Reader> = this.api.crud<Reader>('readers', 'patron');

  protected search: ReaderSearch = blankSearch();
  protected orgFilter: number | null = null;
  protected readonly columns = COLUMNS;
  protected readonly accountLabel = accountLabel;
  protected readonly page = signal<CrudPage<Reader> | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly moreFilters = signal(false);
  protected readonly columnsOpen = signal(false);
  protected readonly view = signal<'table' | 'card'>(readStored(VIEW_KEY) === 'card' ? 'card' : 'table');
  protected readonly shown = signal<Set<ColumnKey>>(storedColumns());
  protected readonly visibleColumns = computed(() => COLUMNS.filter((c) => this.shown().has(c.key)));
  protected readonly selected = signal<Set<string>>(new Set());
  protected readonly editing = signal<{ reader: Reader | null } | null>(null);
  protected readonly locking = signal<Reader | null>(null);
  protected readonly deleting = signal<Reader | null>(null);
  protected readonly importing = signal(false);
  protected readonly exporting = signal(false);
  protected readonly uploadingPhotos = signal(false);
  /** Ảnh nhỏ: id file → link ký (media, theo lô). */
  protected readonly photos = signal<Record<string, string>>({});
  /** Tình trạng đăng nhập OPAC (identity) của các bạn đọc trên trang. */
  protected readonly accounts = signal<Record<string, ReaderAccount>>({});
  protected readonly passwordFor = signal<{ readers: Pick<Reader, 'publicId' | 'cardNo' | 'fullName'>[]; bulk: boolean } | null>(null);
  protected readonly bulk = signal<{ readerTypeId: number | null; classId: number | null; courseId: number | null; status: number | null; issueDate: string | null; expireDate: string | null } | null>(null);
  protected lockReason = '';

  protected readonly allSelected = computed(() => {
    const items = this.page()?.items ?? [];
    return items.length > 0 && items.every((r) => this.selected().has(r.publicId));
  });

  ngOnInit(): void {
    void this.c.load();
    void this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`READERS:${action}`);
  }

  protected indent(depth: number): string {
    return '   '.repeat(depth);
  }

  protected isExpired(r: Reader): boolean {
    return !!r.expireDate && r.expireDate < new Date().toISOString().slice(0, 10);
  }

  /** Bộ lọc gửi lên API: đơn vị đã chọn kèm mọi đơn vị con; ô trống bỏ đi. */
  protected effectiveSearch(): ReaderSearch {
    const s = this.search;
    const text = (v: string | null | undefined) => v?.trim() || null;
    return {
      ...s, keyword: text(s.keyword) ?? undefined, cardNoContains: text(s.cardNoContains), lastName: text(s.lastName), firstName: text(s.firstName),
      issuedFrom: s.issuedFrom || null, issuedTo: s.issuedTo || null, expiredFrom: s.expiredFrom || null, expiredTo: s.expiredTo || null,
      orgIds: this.orgFilter === null ? null : this.c.orgWithDescendants(this.orgFilter),
    };
  }

  protected find(): void {
    this.search.pageIndex = 1;
    this.selected.set(new Set());
    void this.load();
  }

  protected reset(): void {
    this.search = blankSearch(this.search.pageSize);
    this.orgFilter = null;
    this.find();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.search.pageIndex = e.pageIndex;
    this.search.pageSize = e.pageSize;
    void this.load();
  }

  protected toggle(r: Reader): void {
    const next = new Set(this.selected());
    if (next.has(r.publicId)) next.delete(r.publicId);
    else next.add(r.publicId);
    this.selected.set(next);
  }

  protected toggleAll(): void {
    this.selected.set(this.allSelected() ? new Set() : new Set((this.page()?.items ?? []).map((r) => r.publicId)));
  }

  protected toggleColumn(key: ColumnKey): void {
    const next = new Set(this.shown());
    if (next.has(key)) next.delete(key);
    else next.add(key);
    if (!next.size) return;
    this.shown.set(next);
    writeStored(COLUMNS_KEY, JSON.stringify([...next]));
  }

  protected setView(view: 'table' | 'card'): void {
    this.view.set(view);
    writeStored(VIEW_KEY, view);
  }

  /** Bạn đọc đã chọn (có thể ở trang khác — chỉ cần publicId). */
  protected selectedReaders(): Pick<Reader, 'publicId' | 'cardNo' | 'fullName'>[] {
    return [...this.selected()].map((publicId) => ({ publicId, cardNo: '', fullName: '' }));
  }

  protected passwordSaved(bulk: boolean): void {
    this.passwordFor.set(null);
    if (bulk) this.selected.set(new Set());
    void this.loadAccounts();
  }

  protected lock(r: Reader): Promise<void> {
    return this.run(() => this.api.lockReader(r.publicId, this.lockReason.trim() || null), 'READER.LOCKED_DONE', () => this.locking.set(null));
  }

  protected unlock(r: Reader): Promise<void> {
    return this.run(() => this.api.unlockReader(r.publicId), 'READER.UNLOCKED_DONE');
  }

  protected remove(r: Reader): Promise<void> {
    return this.run(() => this.client.delete(r.publicId), 'READER.DELETED', () => this.deleting.set(null));
  }

  protected openBulk(): void {
    this.bulk.set({ readerTypeId: null, classId: null, courseId: null, status: null, issueDate: null, expireDate: null });
  }

  protected applyBulk(b: NonNullable<ReturnType<typeof this.bulk>>): Promise<void> {
    return this.run(async () => {
      const { updatedCount } = await this.api.bulkUpdateReaders({ publicIds: [...this.selected()], ...b, issueDate: b.issueDate || null, expireDate: b.expireDate || null });
      this.toastr.success('READER.BULK_DONE', { count: updatedCount });
      this.selected.set(new Set());
    }, null, () => this.bulk.set(null));
  }

  private async run(action: () => Promise<unknown>, success: string | null, after?: () => void): Promise<void> {
    this.saving.set(true);
    try {
      await action();
      if (success) this.toastr.success(success);
      after?.();
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.client.search(this.effectiveSearch()) as unknown as CrudPage<Reader>);
      void this.loadAccounts();
      void this.loadPhotos();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadAccounts(): Promise<void> {
    const ids = (this.page()?.items ?? []).map((r) => r.publicId);
    if (!ids.length) {
      this.accounts.set({});
      return;
    }
    try {
      this.accounts.set(Object.fromEntries((await this.api.readerAccounts(ids)).map((a) => [a.readerPublicId, a])));
    } catch {
      this.accounts.set({}); // identity không trả lời — danh sách bạn đọc vẫn dùng được
    }
  }

  private async loadPhotos(): Promise<void> {
    const ids = (this.page()?.items ?? []).map((r) => r.photoId).filter((id): id is string => !!id && !this.photos()[id]);
    if (!ids.length) return;
    try {
      const urls = await this.api.fileUrls(ids.slice(0, 100));
      this.photos.update((all) => ({ ...all, ...Object.fromEntries(urls.map((u) => [u.id, u.url])) }));
    } catch {
      /* không lấy được ảnh — hiện khung trống */
    }
  }
}

function blankSearch(pageSize = 10): ReaderSearch {
  return {
    keyword: '', cardNoContains: '', lastName: '', firstName: '', readerTypeId: null, classId: null, courseId: null, status: null, expired: null,
    issuedFrom: null, issuedTo: null, expiredFrom: null, expiredTo: null, pageIndex: 1, pageSize,
  };
}

function storedColumns(): Set<ColumnKey> {
  try {
    const keys = JSON.parse(readStored(COLUMNS_KEY) ?? 'null') as ColumnKey[] | null;
    const valid = (keys ?? []).filter((k) => COLUMNS.some((c) => c.key === k));
    if (valid.length) return new Set(valid);
  } catch {
    /* giá trị hỏng — dùng mặc định */
  }
  return new Set(COLUMNS.filter((c) => c.shown).map((c) => c.key));
}

function readStored(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStored(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* chế độ riêng tư — chỉ nhớ trong phiên */
  }
}
