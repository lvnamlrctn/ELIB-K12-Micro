import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, EntityHistory, errorMessage } from '../../core/api';
import { LDatePipe, Language } from '../../core/i18n';
import { Loading, Modal } from '../../shared/ui';
import { ReaderCatalogs } from './reader-catalogs';

/** Trường trong diff (tên thuộc tính của ReaderDto) → khoá dịch nhãn. */
const FIELD_LABELS: Record<string, string> = {
  cardNo: 'READER.CARD_CODE', lastName: 'READER.FIRST_NAME', firstName: 'READER.LAST_NAME', cardUid: 'READER.CARD_UID', email: 'READER.EMAIL',
  phone: 'READER.PHONE', address: 'READER.ADDRESS', birthDate: 'READER.BIRTH_DATE', sex: 'READER.GENDER', readerTypeId: 'READER.READER_TYPE',
  classId: 'READER.ACADEMIC_CLASS', courseId: 'READER.COURSE', orgId: 'READER.ORG', degreeId: 'READER.DEGREE', ethnicityId: 'READER.ETHNIC',
  academicTitleId: 'READER.PROF', issueDate: 'READER.ISSUED_DATE', expireDate: 'READER.EXPIRED_DATE', status: 'READER.STATUS',
  lockReason: 'READER.LOCK_REASON', photoId: 'READER.AVATAR', groupIds: 'READER_HISTORY.GROUPS',
};

interface Row { entry: EntityHistory; reason: string | null; changes: { field: string; from: string; to: string }[]; }

/** Lịch sử thay đổi của một bạn đọc (service audit): thời điểm, người sửa, thao tác, trường cũ → mới, lý do sửa. */
@Component({
  selector: 'app-reader-history',
  imports: [LDatePipe, Loading, Modal, TranslatePipe],
  template: `
    <app-modal [title]="'READER_HISTORY.TITLE' | translate: { card: cardNo() }" widthClass="max-w-3xl" (closed)="closed.emit()">
      <div class="relative min-h-24">
        @if (loading()) { <app-loading /> }
        @if (error()) { <div class="alert-error">{{ error() }}</div> }
        @if (!loading() && !error() && !rows().length) { <p class="text-sm text-gray-500">{{ 'READER_HISTORY.EMPTY' | translate }}</p> }
        <ol class="space-y-3">
          @for (r of rows(); track r.entry.publicId) {
            <li class="border border-gray-200 rounded-lg p-3">
              <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                <span class="font-mono text-gray-500">{{ r.entry.occurredAt | ldate: 'dd/MM/yyyy HH:mm:ss' }}</span>
                <span class="px-2 py-0.5 rounded text-xs font-medium" [class]="actionCss(r.entry.action)">{{ ('READER_HISTORY.ACTION_' + r.entry.action) | translate }}</span>
                <span class="text-gray-700">{{ r.entry.actorName || ('READER_HISTORY.SYSTEM' | translate) }}</span>
              </div>
              @if (r.reason) { <div class="text-sm mt-1"><span class="text-gray-500">{{ 'READER_HISTORY.REASON' | translate }}:</span> {{ r.reason }}</div> }
              @if (r.changes.length) {
                <table class="w-full text-xs mt-2">
                  <thead><tr class="text-left text-gray-500"><th class="py-1 pr-2 w-1/4">{{ 'READER_HISTORY.FIELD' | translate }}</th>
                    <th class="py-1 pr-2">{{ 'READER_HISTORY.OLD' | translate }}</th><th class="py-1">{{ 'READER_HISTORY.NEW' | translate }}</th></tr></thead>
                  <tbody>
                    @for (ch of r.changes; track ch.field) {
                      <tr class="border-t border-gray-100">
                        <td class="py-1 pr-2 text-gray-600">{{ label(ch.field) | translate }}</td>
                        <td class="py-1 pr-2 text-red-700 line-through decoration-red-300">{{ ch.from }}</td>
                        <td class="py-1 text-green-700">{{ ch.to }}</td>
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
export class ReaderHistory implements OnInit {
  readonly publicId = input.required<string>();
  readonly cardNo = input.required<string>();
  readonly closed = output<void>();

  private readonly api = inject(Api);
  private readonly c = inject(ReaderCatalogs);
  private readonly language = inject(Language);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly rows = signal<Row[]>([]);

  async ngOnInit(): Promise<void> {
    try {
      this.rows.set((await this.api.entityHistory('Reader', this.publicId())).map((e) => this.toRow(e)));
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected label(field: string): string {
    return FIELD_LABELS[field] ?? field;
  }

  protected actionCss(action: string): string {
    return action === 'DELETE' ? 'bg-red-50 text-red-700' : action === 'ADD' || action === 'IMPORT' ? 'bg-green-50 text-green-700' : 'bg-blue-50 text-blue-700';
  }

  private toRow(entry: EntityHistory): Row {
    let parsed: Record<string, unknown> = {};
    try {
      parsed = entry.changes ? (JSON.parse(entry.changes) as Record<string, unknown>) : {};
    } catch {
      parsed = {};
    }
    const reason = typeof parsed['_reason'] === 'string' ? parsed['_reason'] : null;
    const changes = Object.entries(parsed)
      .filter(([k, v]) => k !== '_reason' && Array.isArray(v) && v.length === 2)
      .map(([field, v]) => ({ field, from: this.format(field, (v as unknown[])[0]), to: this.format(field, (v as unknown[])[1]) }));
    return { entry, reason, changes };
  }

  /** Id danh mục → tên; ngày → dd/MM/yyyy; giới tính, trạng thái → nhãn. */
  private format(field: string, value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    const t = (key: string) => this.language.t(key);
    const name = (list: { id: number; name: string }[], id: unknown) => list.find((x) => x.id === id)?.name ?? `#${String(id)}`;
    switch (field) {
      case 'readerTypeId': return name(this.c.types(), value);
      case 'classId': return name(this.c.classes(), value);
      case 'courseId': return name(this.c.courses(), value);
      case 'orgId': return name(this.c.orgs(), value);
      case 'ethnicityId': return name(this.c.ethnicities(), value);
      case 'degreeId': return name(this.c.degrees(), value);
      case 'academicTitleId': return name(this.c.academicTitles(), value);
      case 'groupIds': return Array.isArray(value) && value.length ? value.map((id) => name(this.c.groups(), id)).join(', ') : '—';
      case 'sex': return value === 1 ? t('READER.GENDER_MALE') : value === 0 ? t('READER.GENDER_FEMALE') : String(value);
      case 'status': return value === 2 ? t('COMMON.ACTIVE') : value === 1 ? t('READER.LOCKED') : String(value);
      case 'photoId': return t('READER_HISTORY.PHOTO');
      case 'birthDate': case 'issueDate': case 'expireDate':
        return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value.split('-').reverse().join('/') : String(value);
      default: return typeof value === 'object' ? JSON.stringify(value) : String(value);
    }
  }
}
