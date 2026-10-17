import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import {
  AcqAccessionRow, AcqLabelQuantityRow, AcqListPage, AcqLookupRow, AcqNewBookRow, AcqReportName, AcqReportRequest, Api, ItemLabel, saveFile,
} from '../../core/api';
import { Preferences } from '../../core/preferences';
import { ToastrService } from '../../shared/toastr';
import { HEADER_PREF, LABEL_BARCODES_KEY, LabelJob, PRINT_KEY as LABEL_PRINT_KEY } from '../holdings/labels';
import { ACQ_PRINT_KEY, ACQ_PRINT_MAX_ROWS, AcqPrintJob, AcqPrintSection } from './report-print';

export type AcqReportKind = 'list' | 'lookup' | 'allocation' | 'register' | 'newbooks';

/** Các báo cáo bổ sung: khoá dịch tiêu đề (cũng là tiêu đề bản in), API, tên file Excel. */
export const ACQ_REPORTS: { kind: AcqReportKind; label: string; report: AcqReportName; file: string }[] = [
  { kind: 'list', label: 'ACQ_REPORT.TAB_LIST', report: 'AcquisitionList', file: 'danh-sach-bo-sung.xlsx' },
  { kind: 'lookup', label: 'ACQ_REPORT.TAB_LOOKUP', report: 'LookupLines', file: 'tra-cuu-don-nhan.xlsx' },
  { kind: 'allocation', label: 'ACQ_REPORT.TAB_ALLOCATION', report: 'StoreAllocation', file: 'phan-bo-kho.xlsx' },
  { kind: 'register', label: 'ACQ_REPORT.TAB_REGISTER', report: 'AccessionRegister', file: 'so-dang-ky-ca-biet.xlsx' },
  { kind: 'newbooks', label: 'ACQ_REPORT.TAB_NEWBOOKS', report: 'NewBooks', file: 'thu-muc-sach-moi.xlsx' },
];

type Paged<R> = { items: R[]; total?: number; totalCount?: number };

/** Trần số nhãn môn loại in theo số lượng (như Holdings.ItemResource.MaxLabels cho nhãn theo ĐKCB). */
const MAX_CLASS_LABELS = 2000;

/**
 * Xuất Excel, in theo mẫu giấy (report-print) và in nhãn theo bộ lọc báo cáo bổ sung — dùng chung cho trang "Báo cáo bổ sung" và trang
 * "Tra cứu đơn nhận" (nút "Báo cáo" + "Loại báo cáo" như ELIB-LRC). Lỗi ném lên nơi gọi (toast ở đó).
 */
@Injectable({ providedIn: 'root' })
export class AcqReportActions {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly i18n = inject(TranslateService);
  private readonly prefs = inject(Preferences);

  async export(kind: AcqReportKind, body: AcqReportRequest): Promise<void> {
    const r = ACQ_REPORTS.find((x) => x.kind === kind)!;
    saveFile(await this.api.exportAcqReport(r.report, body), r.file);
  }

  /** Lấy hết các trang (trần ACQ_PRINT_MAX_ROWS dòng) rồi mở trang in theo mẫu giấy. storeName: tên kho của dòng tra cứu. */
  async print(kind: AcqReportKind, body: AcqReportRequest, storeName: (r: AcqLookupRow) => string): Promise<void> {
    const job = await this.buildPrintJob(kind, body, storeName);
    if (!job.rows.length && !job.sections?.length) {
      this.toastr.warning('ACQ_REPORT.NOTHING_TO_PRINT');
      return;
    }
    try {
      sessionStorage.setItem(ACQ_PRINT_KEY, JSON.stringify(job));
    } catch {
      this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
      return;
    }
    void this.router.navigateByUrl('/print/acq-report');
  }

  /** In nhãn cho các ĐKCB của sổ đăng ký cá biệt theo bộ lọc (tối đa ACQ_PRINT_MAX_ROWS). */
  async printLabels(body: AcqReportRequest): Promise<void> {
    const all = await this.allPages<AcqAccessionRow>('AccessionRegister', body, (p) => p.totalCount ?? 0);
    if (all.truncated) this.toastr.warning('ACQ_REPORT.LABELS_TRUNCATED', { max: ACQ_PRINT_MAX_ROWS });
    const barcodes = all.rows.map((x) => x.barcode);
    if (!barcodes.length) {
      this.toastr.warning('ACQ_REPORT.NOTHING_TO_PRINT');
      return;
    }
    try {
      sessionStorage.setItem(LABEL_BARCODES_KEY, JSON.stringify(barcodes));
    } catch {
      this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
      return;
    }
    void this.router.navigateByUrl('/print-labels');
  }

  /**
   * In nhãn gáy/môn loại theo SỐ LƯỢNG mỗi biểu ghi khớp bộ lọc (monolith: LookupLinesForLabel) — dùng khi sách chưa được đăng ký ĐKCB
   * cụ thể nên chưa có mã vạch thật; mỗi biểu ghi in đúng số lượng đã đặt/nhận. Dùng lại trang in nhãn chung (kind "spine").
   */
  async printClassLabels(body: AcqReportRequest): Promise<void> {
    const rows = await this.api.acqReport<AcqLabelQuantityRow[]>('LabelQuantities', body);
    const labels: ItemLabel[] = [];
    rows.forEach((r, bi) => {
      for (let i = 0; i < r.quantity && labels.length < MAX_CLASS_LABELS; i++)
        labels.push({ barcode: `mfn-${r.mfn}-${bi}-${i}`, mfn: r.mfn, title: r.title, author: r.author, publishYear: null, ddc: r.ddc, cutter: r.cutter, storeCode: null, storeName: null });
    });
    if (!labels.length) {
      this.toastr.warning('ACQ_REPORT.NOTHING_TO_PRINT');
      return;
    }
    if (labels.length >= MAX_CLASS_LABELS) this.toastr.warning('ACQ_REPORT.LABELS_TRUNCATED', { max: MAX_CLASS_LABELS });
    let header = { parent: '', library: '' };
    this.prefs.watch<{ parent: string; library: string }>(HEADER_PREF, (h) => { header = { parent: h?.parent ?? '', library: h?.library ?? '' }; });
    const job: LabelJob = { kind: 'spine', labels, header, skip: 0 };
    try {
      sessionStorage.setItem(LABEL_PRINT_KEY, JSON.stringify(job));
    } catch {
      this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
      return;
    }
    void this.router.navigateByUrl('/print/labels');
  }

  private async allPages<R>(name: AcqReportName, body: AcqReportRequest, total: (p: Paged<R>) => number): Promise<{ rows: R[]; truncated: boolean; first: Paged<R> }> {
    const size = 500;
    const rows: R[] = [];
    let first: Paged<R> | null = null;
    for (let page = 1; ; page++) {
      const p = await this.api.acqReport<Paged<R>>(name, { ...body, pageIndex: page, pageSize: size });
      first ??= p;
      rows.push(...p.items);
      if (!p.items.length || rows.length >= total(p) || rows.length >= ACQ_PRINT_MAX_ROWS) break;
    }
    return { rows: rows.slice(0, ACQ_PRINT_MAX_ROWS), truncated: total(first!) > ACQ_PRINT_MAX_ROWS, first: first! };
  }

  private async buildPrintJob(kind: AcqReportKind, body: AcqReportRequest, storeName: (r: AcqLookupRow) => string): Promise<AcqPrintJob> {
    const t = ACQ_REPORTS.find((x) => x.kind === kind)!;
    const tr = (k: string) => this.i18n.instant(k) as string;
    const num = (v: number, digits = 0) => v.toLocaleString('vi-VN', { maximumFractionDigits: digits });
    const date = (v: string | null) => (v ? v.split('-').reverse().join('/') : '');
    const range = (a: string, b: string) => (a && b ? `Từ ngày ${date(a)} đến ngày ${date(b)}` : a ? `Từ ngày ${date(a)}` : b ? `Đến ngày ${date(b)}` : '');
    const base = { title: tr(t.label), subtitle: range(body.from ?? '', body.to ?? ''), total: null as string[] | null, truncated: false };
    switch (kind) {
      case 'list': {
        const all = await this.allPages<AcqListPage['items'][number]>(t.report, body, (p) => p.total ?? 0);
        const page = all.first as unknown as AcqListPage;
        return {
          ...base, truncated: all.truncated,
          headers: [tr('ACQ_REPORT.RECEIPT'), tr('ACQ_RECEIPT.RECEIVED_ON'), tr('ITEMS_PAGE.TITLE_COL'), tr('ACQ.QUANTITY'), tr('ACQ.PRICE'), tr('ACQ.AMOUNT')],
          rows: all.rows.map((x) => ['#' + x.receiptId, date(x.receivedOn), `${x.title} (MFN ${x.mfn})`, num(x.quantity), x.price === null ? '' : num(x.price, 2) + ' ' + (x.currency ?? ''), num(x.total)]),
          total: [tr('STORE_STATS.TOTAL'), '', '', num(page.quantity), '', num(page.amount)], right: [3, 4, 5],
        };
      }
      case 'lookup': {
        const all = await this.allPages<AcqLookupRow>(t.report, body, (p) => p.totalCount ?? 0);
        return {
          ...base, truncated: all.truncated, right: [],
          headers: ['MFN', tr('ACQ_REPORT.RECEIPT'), tr('ACQ_REPORT.DOCUMENT'), tr('ACQ_RECEIPT.RECEIVED_ON'), tr('ACQ.SOURCE'), tr('ACQ_RECEIPT.STORE')],
          rows: all.rows.map((x) => [String(x.mfn), '#' + x.receiptId, x.isbd, date(x.receivedOn), x.sourceName ?? '', storeName(x)]),
        };
      }
      case 'allocation': {
        // Bản in theo mẫu giấy như ELIB-LRC: từng kho → từng biểu ghi (NXB, ký hiệu DDC, số lượng) — lấy lại từ Sổ ĐKCB (đã có đủ chi
        // tiết theo bản sách) thay vì báo cáo tổng StoreAllocation (chỉ có tổng số mỗi kho).
        const all = await this.allPages<AcqAccessionRow>('AccessionRegister', body, (p) => p.totalCount ?? 0);
        const byStore = new Map<string, AcqAccessionRow[]>();
        for (const row of all.rows) {
          const key = row.storeName ?? tr('ACQ_REPORT.NO_STORE');
          const group = byStore.get(key);
          if (group) group.push(row); else byStore.set(key, [row]);
        }
        const headers = [tr('ITEMS_PAGE.TITLE_COL'), tr('ITEMS_PAGE.AUTHOR'), tr('ACQ.PUBLISHER'), tr('ACQ_REPORT.CLASS'), tr('ACQ_REPORT.COPIES')];
        const sections: AcqPrintSection[] = [...byStore.entries()].sort((a, b) => a[0].localeCompare(b[0], 'vi')).map(([store, storeRows]) => {
          const byBib = new Map<number, { row: AcqAccessionRow; count: number }>();
          for (const row of storeRows) {
            const entry = byBib.get(row.mfn);
            if (entry) entry.count++; else byBib.set(row.mfn, { row, count: 1 });
          }
          const rows = [...byBib.values()].map(({ row, count }) => [`${row.title ?? ''} (MFN ${row.mfn})`, row.author ?? '', row.publisher ?? '', row.ddc ?? '', num(count)]);
          return { heading: `${store} (${num(storeRows.length)})`, rows, total: [tr('STORE_STATS.TOTAL'), '', '', '', num(storeRows.length)] };
        });
        return {
          ...base, truncated: all.truncated, right: [4], headers, rows: [], sections,
          total: [tr('STORE_STATS.TOTAL'), '', '', '', num(all.rows.length)],
        };
      }
      case 'register': {
        // "Mặc định lọc theo năm hiện tại" khi chưa chọn bộ lọc nào (như ELIB-LRC) — tránh in cả sổ ĐKCB nhiều năm; chỉ áp dụng khi in,
        // không đổi bộ lọc trên màn hình.
        const hasOtherFilter = !!(body.receiptCodeFrom || body.receiptCodeTo || body.storeId || body.supplierId || body.receiptName
          || body.completed !== null || body.sourceId || body.fundId || body.createdBy || body.createdFrom || body.createdTo
          || body.mfnFrom || body.mfnTo || body.title || body.author || body.publisher || body.publishYear);
        const printBody = !body.from && !body.to && !hasOtherFilter
          ? { ...body, from: `${new Date().getFullYear()}-01-01`, to: `${new Date().getFullYear()}-12-31` }
          : body;
        const all = await this.allPages<AcqAccessionRow>(t.report, printBody, (p) => p.totalCount ?? 0);
        return {
          ...base, subtitle: range(printBody.from ?? '', printBody.to ?? ''), truncated: all.truncated, right: [8],
          headers: [
            tr('ITEMS_PAGE.BARCODE'), tr('ITEMS_PAGE.TITLE_COL'), tr('ITEMS_PAGE.AUTHOR'), tr('ACQ.PUBLISHER'), tr('ACQ.YEAR'), tr('ACQ_REPORT.CLASS'),
            tr('ITEMS_PAGE.STORE'), tr('ACQ_RECEIPT.RECEIVED_ON'), tr('ACQ.PRICE'),
          ],
          rows: all.rows.map((x) => [
            x.barcode, `${x.title ?? ''} (MFN ${x.mfn})`, x.author ?? '', x.publisher ?? '', x.publishYear ?? '', x.ddc?.charAt(0) ?? '',
            x.storeName ?? '', date(x.date), x.price === null ? '' : num(x.price, 2) + ' ' + (x.currency ?? ''),
          ]),
        };
      }
      case 'newbooks': {
        const all = await this.allPages<AcqNewBookRow>(t.report, body, (p) => p.totalCount ?? 0);
        return {
          ...base, truncated: all.truncated, right: [],
          headers: ['MFN', tr('ITEMS_PAGE.TITLE_COL'), tr('ITEMS_PAGE.AUTHOR'), tr('ACQ.PUBLISHER'), tr('ACQ.YEAR'), tr('ACQ_REPORT.CATALOGED_ON')],
          rows: all.rows.map((x) => [String(x.mfn), x.title, x.author ?? '', x.publisher ?? '', x.publishYear ?? '', date(x.catalogedOn)]),
        };
      }
    }
  }
}
