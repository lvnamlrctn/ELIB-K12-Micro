import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Preferences } from '../../core/preferences';
import { ReportMeta, vnDateLine } from '../../core/report-meta';
import { Session } from '../../core/session';
import { HEADER_PREF } from '../holdings/labels';

/** sessionStorage: bản in báo cáo — màn báo cáo ghi trước khi mở trang in (dùng chung mọi báo cáo, không riêng bổ sung). */
export const ACQ_PRINT_KEY = 'elib.print.acq-report';

/** Bản in một báo cáo: đã dựng sẵn chữ (cột, dòng, dòng tổng) nên trang in không gọi lại API. */
/** Trần số dòng một bản in (API trả tối đa 500 dòng mỗi trang). */
export const ACQ_PRINT_MAX_ROWS = 5000;

export interface AcqPrintJob {
  title: string; subtitle: string; headers: string[]; rows: string[][]; total: string[] | null; right: number[]; truncated: boolean;
  /** Phân bổ kho: in theo từng kho (một bảng + dòng tổng riêng) thay vì một bảng chung — bỏ qua `rows` khi có. */
  sections?: AcqPrintSection[];
  /** Nút quay lại của trang in (mặc định: báo cáo bổ sung). */
  backUrl?: string; backLabel?: string;
}

/** Một bảng con; `headers` riêng khi các bảng khác cột nhau (mặc định dùng cột chung của job). */
export interface AcqPrintSection { heading: string; rows: string[][]; total: string[] | null; headers?: string[]; right?: number[]; }

/** Ghi bản in vào sessionStorage rồi mở trang in chung. false = trình duyệt chặn sessionStorage. */
export function openReportPrint(router: Router, job: AcqPrintJob): boolean {
  try {
    sessionStorage.setItem(ACQ_PRINT_KEY, JSON.stringify(job));
  } catch {
    return false;
  }
  void router.navigateByUrl('/print/report');
  return true;
}

interface Header { parent: string; library: string; }

/**
 * In báo cáo bổ sung theo mẫu giấy (ELIB-LRC: AcquisitionListPrint, StoreAllocationPrint, AccessionRegisterPrint, NewBookCatalog):
 * cơ quan chủ quản + tên thư viện, tiêu đề, khoảng ngày, bảng có STT, dòng tổng, "Địa danh, ngày … tháng … năm …" căn phải và khối chữ ký
 * (người lập báo cáo kèm họ tên / thủ trưởng đơn vị — mẫu ReportSignoff của ELIB-LRC). Dùng chung cho mọi báo cáo in; cơ quan chủ quản /
 * tên thư viện / địa danh nhớ theo tài khoản.
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
    h2.section { font-size: 14px; margin: 14px 0 4px; break-after: avoid; }
    table + h2.section { margin-top: 18px; }
    .signoff { margin-top: 24px; break-inside: avoid; }
    .signoff .date { text-align: right; margin-bottom: 14px; }
    .signoff .signs { display: flex; justify-content: space-around; text-align: center; }
    .signoff .role { font-weight: 700; text-transform: uppercase; }
    .signoff .hint { font-style: italic; }
    .signoff .space { height: 64px; }
    .signoff .who { font-weight: 600; }
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
      <a [routerLink]="job()?.backUrl ?? '/ab-reports'" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">arrow_back</span> {{ job()?.backLabel ?? ('ACQ_REPORT.TITLE' | translate) }}</a>
      <input class="input !w-64" [ngModel]="header().parent" (ngModelChange)="setHeader({ parent: $event })" [placeholder]="'LABELS_PAGE.HEADER_PARENT' | translate" />
      <input class="input !w-64" [ngModel]="header().library" (ngModelChange)="setHeader({ library: $event })" [placeholder]="'LABELS_PAGE.HEADER_LIBRARY' | translate" />
      <input class="input !w-44" [ngModel]="meta.place()" (ngModelChange)="meta.setPlace($event)" [placeholder]="'REPORT_PRINT.PLACE' | translate" [attr.aria-label]="'REPORT_PRINT.PLACE' | translate" />
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
        @if (j.sections; as secs) {
          @for (s of secs; track s.heading) {
            <h2 class="section">{{ s.heading }}</h2>
            <table>
              <thead><tr><th style="width:44px">{{ 'ACQ.NO' | translate }}</th>@for (h of s.headers ?? j.headers; track $index) { <th>{{ h }}</th> }</tr></thead>
              <tbody>
                @for (r of s.rows; track $index) {
                  <tr><td class="r">{{ $index + 1 }}</td>@for (c of r; track $index) { <td [class.r]="(s.right ?? j.right).includes($index)">{{ c }}</td> }</tr>
                }
                @if (s.total; as st) {
                  <tr class="total"><td></td>@for (c of st; track $index) { <td [class.r]="(s.right ?? j.right).includes($index)">{{ c }}</td> }</tr>
                }
              </tbody>
            </table>
          }
          @if (j.total; as t) {
            <table><tbody><tr class="total"><td></td>@for (c of t; track $index) { <td [class.r]="j.right.includes($index)">{{ c }}</td> }</tr></tbody></table>
          }
        } @else {
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
        }
        @if (j.truncated) { <p class="no-print text-amber-700 mt-2">{{ 'ACQ_REPORT.PRINT_TRUNCATED' | translate: { max: maxRows } }}</p> }
        <div class="signoff">
          <div class="date">@if (meta.place().trim()) { {{ meta.place().trim() }}, } {{ dateLine }}</div>
          <div class="signs">
            <div>
              <div class="role">{{ 'ACQ_REPORT.SIGN_MAKER' | translate }}</div>
              <div class="hint">({{ 'REPORT_PRINT.SIGN_HINT' | translate }})</div>
              <div class="space"></div>
              <div class="who">{{ meta.maker() }}</div>
            </div>
            <div>
              <div class="role">{{ 'ACQ_REPORT.SIGN_HEAD' | translate }}</div>
              <div class="hint">({{ 'REPORT_PRINT.SIGN_HINT' | translate }})</div>
            </div>
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
  protected readonly meta = inject(ReportMeta);
  protected readonly dateLine = vnDateLine();

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
    this.meta.parent.set(this.header().parent); // Excel xuất sau đó gửi đúng cơ quan chủ quản vừa sửa
  }

  protected print(): void {
    window.print();
  }
}

/** Các cột toàn số (kể cả 1.234,5 và %) — in căn phải. */
export function numericColumns(rows: string[][]): number[] {
  const out: number[] = [];
  const width = rows.reduce((m, r) => Math.max(m, r.length), 0);
  for (let c = 0; c < width; c++) {
    const cells = rows.map((r) => r[c]).filter((v) => v !== undefined && v !== '');
    if (cells.length && cells.every((v) => /^-?[\d.,]+\s*%?$/.test(v))) out.push(c);
  }
  return out;
}

/** "Từ ngày dd/MM/yyyy đến ngày dd/MM/yyyy" (yyyy-MM-dd vào); không lọc ngày → rỗng. */
export function periodLine(from: string | null | undefined, to: string | null | undefined): string {
  const date = (v: string) => v.split('-').reverse().join('/');
  if (from && to) return `Từ ngày ${date(from)} đến ngày ${date(to)}`; // i18n-data: dòng ngày in trên báo cáo (mẫu văn bản hành chính)
  return from ? `Từ ngày ${date(from)}` : to ? `Đến ngày ${date(to)}` : ''; // i18n-data: dòng ngày in trên báo cáo (mẫu văn bản hành chính)
}
