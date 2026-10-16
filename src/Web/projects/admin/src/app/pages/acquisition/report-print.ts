import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Preferences } from '../../core/preferences';
import { Session } from '../../core/session';
import { HEADER_PREF } from '../holdings/labels';

/** sessionStorage: bản in báo cáo bổ sung — màn báo cáo ghi trước khi mở trang in. */
export const ACQ_PRINT_KEY = 'elib.print.acq-report';

/** Bản in một báo cáo: đã dựng sẵn chữ (cột, dòng, dòng tổng) nên trang in không gọi lại API. */
/** Trần số dòng một bản in (API trả tối đa 500 dòng mỗi trang). */
export const ACQ_PRINT_MAX_ROWS = 5000;

export interface AcqPrintJob {
  title: string; subtitle: string; headers: string[]; rows: string[][]; total: string[] | null; right: number[]; truncated: boolean;
}

interface Header { parent: string; library: string; }

/**
 * In báo cáo bổ sung theo mẫu giấy (ELIB-LRC: AcquisitionListPrint, StoreAllocationPrint, AccessionRegisterPrint, NewBookCatalog):
 * cơ quan chủ quản + tên thư viện, tiêu đề, khoảng ngày, bảng có STT, dòng tổng, ngày tháng và khối chữ ký. Cơ quan chủ quản / tên thư viện
 * nhớ theo tài khoản (cùng khoá với bản in nhãn).
 */
@Component({
  selector: 'app-acq-report-print',
  imports: [FormsModule, RouterLink, TranslatePipe],
  styles: `
    :host { display: block; background: #fff; }
    .sheet { max-width: 297mm; margin: 0 auto; padding: 12mm; color: #000; font-size: 13px; }
    .head { display: flex; justify-content: space-between; gap: 16px; }
    .head .org { text-align: center; text-transform: uppercase; }
    .head .parent { font-size: 12px; }
    .head .lib { font-weight: 700; }
    h1 { margin: 18px 0 2px; text-align: center; font-size: 18px; text-transform: uppercase; }
    .sub { text-align: center; font-style: italic; margin-bottom: 12px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { border: 1px solid #000; padding: 3px 6px; vertical-align: top; }
    th { background: #f1f1f1; text-align: center; }
    td.r { text-align: right; white-space: nowrap; }
    tr.total td { font-weight: 700; }
    .signs { display: flex; justify-content: space-between; margin-top: 24px; text-align: center; break-inside: avoid; }
    .signs .date { font-style: italic; margin-bottom: 2px; }
    .signs div div:first-child { font-weight: 700; }
    .signs .space { height: 70px; }
    @media print {
      @page { size: A4 landscape; margin: 8mm; }
      .no-print { display: none !important; }
      .sheet { padding: 0; max-width: none; }
      thead { display: table-header-group; }
      tr { break-inside: avoid; }
    }
  `,
  template: `
    <div class="no-print sticky top-0 z-10 bg-white border-b border-gray-200 px-4 py-3 flex items-center gap-3 flex-wrap">
      <a routerLink="/ab-reports" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">arrow_back</span> {{ 'ACQ_REPORT.TITLE' | translate }}</a>
      <input class="input !w-64" [ngModel]="header().parent" (ngModelChange)="setHeader({ parent: $event })" [placeholder]="'LABELS_PAGE.HEADER_PARENT' | translate" />
      <input class="input !w-64" [ngModel]="header().library" (ngModelChange)="setHeader({ library: $event })" [placeholder]="'LABELS_PAGE.HEADER_LIBRARY' | translate" />
      <span class="flex-1"></span>
      <button type="button" class="btn-primary" [disabled]="!job()" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'ACQ_REPORT.PRINT' | translate }}</button>
    </div>
    @if (job(); as j) {
      <div class="sheet">
        <div class="head">
          <div class="org">
            @if (header().parent) { <div class="parent">{{ header().parent }}</div> }
            <div class="lib">{{ header().library }}</div>
          </div>
          <div></div>
        </div>
        <h1>{{ j.title }}</h1>
        @if (j.subtitle) { <div class="sub">{{ j.subtitle }}</div> }
        <table>
          <thead><tr><th style="width:44px">{{ 'ACQ.NO' | translate }}</th>@for (h of j.headers; track $index) { <th>{{ h }}</th> }</tr></thead>
          <tbody>
            @for (r of j.rows; track $index) {
              <tr><td class="r">{{ $index + 1 }}</td>@for (c of r; track $index) { <td [class.r]="j.right.includes($index)">{{ c }}</td> }</tr>
            }
            @if (j.total; as t) {
              <tr class="total"><td></td>@for (c of t; track $index) { <td [class.r]="j.right.includes($index)">{{ c }}</td> }</tr>
            }
          </tbody>
        </table>
        @if (j.truncated) { <p class="no-print text-amber-700 mt-2">{{ 'ACQ_REPORT.PRINT_TRUNCATED' | translate: { max: maxRows } }}</p> }
        <div class="signs">
          <div><div class="space"></div></div>
          <div>
            <div class="date">{{ today }}</div>
            <div>{{ 'ACQ_REPORT.SIGN_MAKER' | translate }}</div>
            <div class="space"></div>
          </div>
          <div>
            <div class="date">&nbsp;</div>
            <div>{{ 'ACQ_REPORT.SIGN_HEAD' | translate }}</div>
            <div class="space"></div>
          </div>
        </div>
      </div>
    } @else {
      <p class="no-print text-center text-gray-500 py-24">{{ 'ACQ_REPORT.PRINT_EMPTY' | translate }}</p>
    }
  `,
})
export class AcquisitionReportPrint implements OnInit {
  private readonly prefs = inject(Preferences);
  private readonly session = inject(Session);

  protected readonly maxRows = ACQ_PRINT_MAX_ROWS;
  protected readonly job = signal<AcqPrintJob | null>(null);
  protected readonly header = signal<Header>({ parent: '', library: '' });
  protected readonly today = (() => {
    const d = new Date();
    return `Ngày ${d.getDate()} tháng ${d.getMonth() + 1} năm ${d.getFullYear()}`;
  })();

  ngOnInit(): void {
    this.header.set({ parent: '', library: this.session.features()?.name ?? '' });
    this.prefs.watch<Header>(HEADER_PREF, (h) => this.header.set({ parent: h?.parent ?? '', library: h?.library ?? '' }));
    try {
      const job = JSON.parse(sessionStorage.getItem(ACQ_PRINT_KEY) ?? 'null') as AcqPrintJob | null;
      if (job && Array.isArray(job.rows)) this.job.set(job);
    } catch {
      /* chưa có báo cáo để in — hiện trang trống */
    }
  }

  protected setHeader(change: Partial<Header>): void {
    this.header.update((h) => ({ ...h, ...change }));
    this.prefs.set(HEADER_PREF, this.header());
  }

  protected print(): void {
    window.print();
  }
}
