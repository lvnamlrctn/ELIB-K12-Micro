import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, EntityHistory, MarcFieldDef, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Loading, Modal } from '../../shared/ui';

interface Row { entry: EntityHistory; reason: string | null; changes: { field: string; from: string; to: string }[]; }

/** Nhãn ngoài trường MARC trong diff của biểu ghi (BibResource.Diff). */
const OTHER_LABELS: Record<string, string> = { bibTypeId: 'BIB_EDITOR.RECORD_TYPE', status: 'BIB_EDITOR.OPAC_VISIBLE', leader: 'Leader' };

/**
 * Lịch sử biên mục của một biểu ghi (service audit, monolith: LogBienMuc): thời điểm, người sửa, thao tác, từng trường MARC cũ → mới
 * (dạng "chỉ thị $a giá trị …"), lý do sửa.
 */
@Component({
  selector: 'app-bib-history',
  imports: [LDatePipe, Loading, Modal, TranslatePipe],
  template: `
    <app-modal [title]="'BIB_EDITOR.HISTORY' | translate" widthClass="max-w-4xl" (closed)="closed.emit()">
      <div class="relative min-h-24">
        @if (loading()) { <app-loading /> }
        @if (error()) { <div class="alert-error">{{ error() }}</div> }
        @if (!loading() && !error() && !rows().length) { <p class="text-sm text-gray-500">{{ 'READER_HISTORY.EMPTY' | translate }}</p> }
        <ol class="space-y-3">
          @for (r of rows(); track r.entry.publicId) {
            <li class="border border-gray-200 rounded-lg p-3">
              <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                <span class="font-mono text-gray-500">{{ r.entry.occurredAt | ldate: 'dd/MM/yyyy HH:mm:ss' }}</span>
                <span class="px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-700">{{ ('READER_HISTORY.ACTION_' + r.entry.action) | translate }}</span>
                <span class="text-gray-700">{{ r.entry.actorName || ('READER_HISTORY.SYSTEM' | translate) }}</span>
              </div>
              @if (r.reason) { <div class="text-sm mt-1"><span class="text-gray-500">{{ 'BIB_EDITOR.REASON' | translate }}:</span> {{ r.reason }}</div> }
              @if (r.changes.length) {
                <table class="w-full text-xs mt-2">
                  <thead><tr class="text-left text-gray-500"><th class="py-1 pr-2 w-1/5">{{ 'READER_HISTORY.FIELD' | translate }}</th>
                    <th class="py-1 pr-2">{{ 'READER_HISTORY.OLD' | translate }}</th><th class="py-1">{{ 'READER_HISTORY.NEW' | translate }}</th></tr></thead>
                  <tbody>
                    @for (ch of r.changes; track ch.field) {
                      <tr class="border-t border-gray-100 align-top">
                        <td class="py-1 pr-2 text-gray-600"><span class="font-mono font-bold">{{ tagOf(ch.field) }}</span> {{ label(ch.field) | translate }}</td>
                        <td class="py-1 pr-2 font-mono text-red-700 line-through decoration-red-300 break-words">{{ ch.from }}</td>
                        <td class="py-1 font-mono text-green-700 break-words">{{ ch.to }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              } @else if (r.entry.summary) { <div class="text-xs text-gray-500 mt-1">{{ r.entry.summary }}</div> }
            </li>
          }
        </ol>
      </div>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CLOSE' | translate }}</button>
      </ng-container>
    </app-modal>
  `,
})
export class BibHistory implements OnInit {
  readonly publicId = input.required<string>();
  readonly defs = input<MarcFieldDef[]>([]);
  readonly closed = output<void>();

  private readonly api = inject(Api);
  protected readonly rows = signal<Row[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      const entries = await this.api.entityHistory('Bib', this.publicId());
      this.rows.set(entries.map((entry) => {
        const changes: Row['changes'] = [];
        let reason: string | null = null;
        if (entry.changes) {
          try {
            for (const [field, value] of Object.entries(JSON.parse(entry.changes) as Record<string, unknown>)) {
              if (field === '_reason') { reason = String(value); continue; }
              const [from, to] = Array.isArray(value) ? value : ['…', '…'];
              changes.push({ field, from: from == null ? '—' : String(from), to: to == null ? '—' : String(to) });
            }
          } catch { /* nhật ký cũ không có JSON hợp lệ */ }
        }
        return { entry, reason, changes };
      }));
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected tagOf(field: string): string {
    return /^\d{3}$/.test(field) ? field : '';
  }

  /** Nhãn MARC21 (từ điển trường của catalog) hoặc khoá dịch của thuộc tính khác. */
  protected label(field: string): string {
    return /^\d{3}$/.test(field) ? (this.defs().find((d) => d.tag === field)?.name ?? '') : (OTHER_LABELS[field] ?? field);
  }
}
