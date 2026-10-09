import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environment } from '../../../../environments/environment';
import { ReaderImportMapping } from '../../../models/reader/reader-import-mapping';
import { Auth } from '../../../services/auth';

type Template = { name: string; headerRow: number; columns: Record<string, string | null> };

/** Ghép cột Excel ↔ trường bạn đọc khi nhập (port ELIB-LRC 09-15, Đợt 20). Mẫu ghép lưu trên trình duyệt, khoá theo
 *  đơn vị đang đăng nhập để mẫu của đơn vị này không lẫn sang đơn vị khác dùng chung máy. */
@Component({
  selector: 'app-reader-import-mapping', standalone: true, imports: [FormsModule],
  template: `
  <section class="border-2 border-dashed border-gray-300 rounded-xl bg-gray-50 p-3 space-y-3">
    <h3 class="font-semibold text-gray-800">Ghép cột Excel với thông tin bạn đọc</h3>
    <label class="flex items-center gap-2 text-sm text-gray-600">Dòng tiêu đề
      <input class="w-16 px-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500" type="number" min="1" max="20" [(ngModel)]="headerRow" (ngModelChange)="inspect()">
    </label>
    @if (busy()) { <p class="flex items-center gap-2 text-sm text-gray-500"><span class="w-3.5 h-3.5 border-2 border-gray-300 border-t-blue-500 rounded-full animate-spin"></span> Đang đọc tên cột…</p> }
    @if (error()) { <p class="text-sm text-red-700 bg-red-50 border border-red-100 rounded-lg px-3 py-2" role="alert">{{ error() }}</p> }
    @if (headers().length) {
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 max-h-64 overflow-auto p-0.5">
        @for (field of fields(); track field.key) {
          <label class="block text-xs font-medium text-gray-500">{{ field.label }} {{ field.key === 'Cardno' ? '(bắt buộc)' : '' }}
            <select class="mt-1 w-full px-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white" [ngModel]="columns[field.key] ?? null" (ngModelChange)="columns[field.key]=$event; changed()">
              <option [ngValue]="null">Không nhập trường này</option>
              @for (column of headers(); track column.index) { <option [ngValue]="column.index">{{ column.index + 1 }}. {{ column.name }}</option> }
            </select>
          </label>
        }
      </div>
      <div class="flex flex-wrap gap-2 items-center">
        <input class="flex-1 min-w-32 px-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500" [(ngModel)]="templateName" placeholder="Tên mẫu nhập" aria-label="Tên mẫu nhập">
        <button type="button" class="px-3 py-1.5 text-sm font-medium text-gray-600 border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors" (click)="save()">Lưu mẫu trên trình duyệt</button>
        <select class="px-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white" [ngModel]="''" (ngModelChange)="apply($event)" aria-label="Chọn mẫu nhập">
          <option value="">Chọn mẫu đã lưu</option>
          @for (template of templates(); track template.name) { <option [value]="template.name">{{ template.name }}</option> }
        </select>
      </div>
    }
    <button type="button" class="text-sm font-medium text-blue-700 hover:text-blue-800 hover:underline transition-colors" (click)="legacy()">Dùng bố cục 16 cột cũ (tiêu đề dòng 1)</button>
    <p class="text-xs text-gray-500">Số thẻ nên lưu dạng văn bản để giữ số 0 đầu. Mật khẩu không xuất vào tệp báo lỗi. Có thể ánh xạ Họ và Tên riêng, hoặc chỉ ánh xạ cột "Họ và Tên (một cột, tự tách Họ/Tên)" rồi để trống hai cột Họ/Tên.</p>
  </section>`
})
export class ReaderImportMappingComponent implements OnChanges {
  @Input() file: File | null = null;
  @Output() mappingChange = new EventEmitter<ReaderImportMapping | undefined>();
  @Output() validChange = new EventEmitter<boolean>();
  private http = inject(HttpClient); private destroyRef = inject(DestroyRef); private auth = inject(Auth); private revision = 0;
  private get storageKey(): string { return `reader-import-column-templates-v1:${this.auth.getTenantId() ?? 'system'}`; }
  headerRow = 1; columns: Record<string, number | null> = {}; templateName = '';
  headers = signal<{ index: number; name: string }[]>([]); fields = signal<{ key: string; label: string }[]>([]);
  error = signal(''); busy = signal(false); templates = signal<Template[]>([]);
  constructor() { try { const saved = JSON.parse(localStorage.getItem(this.storageKey) ?? '[]'); if (Array.isArray(saved)) this.templates.set(saved.slice(0,20)); } catch { } }
  ngOnChanges() { this.headerRow = 1; this.inspect(); }
  inspect() {
    const revision = ++this.revision; this.validChange.emit(false); this.mappingChange.emit(undefined); this.error.set(''); this.headers.set([]);
    if (!this.file) return;
    this.busy.set(true); const form = new FormData(); form.append('file', this.file); form.append('headerRow', String(this.headerRow));
    this.http.post<any>(environment.baseApiUrl + '/api/Circulation/Reader/Import/Columns', form).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: response => { if (revision !== this.revision) return; const data = response.data; this.headers.set(data.headers); this.fields.set(data.fields); this.columns = data.suggested; this.busy.set(false); this.changed(); },
      error: e => { if (revision !== this.revision) return; this.busy.set(false); this.error.set(e?.error?.message || 'Không đọc được tên cột. Có thể chọn bố cục cũ nếu tệp đúng mẫu.'); }
    });
  }
  changed() {
    const used = Object.values(this.columns).filter(x => x !== null);
    const valid = this.columns['Cardno'] != null && new Set(used).size === used.length;
    this.validChange.emit(valid); this.mappingChange.emit({ headerRow: this.headerRow, columns: { ...this.columns } });
    this.error.set(valid ? '' : 'Chọn cột Số thẻ và tránh ghép một cột vào nhiều trường.'); return valid;
  }
  legacy() { this.revision++; this.busy.set(false); this.error.set('Đang dùng bố cục cũ, tiêu đề dòng 1.'); this.mappingChange.emit(undefined); this.validChange.emit(true); }
  save() {
    const name = this.templateName.trim(); if (!name || !this.changed()) return;
    const template = { name, headerRow: this.headerRow, columns: Object.fromEntries(Object.entries(this.columns).map(([key, index]) => [key, this.headers().find(h => h.index === index)?.name ?? null])) };
    const templates = [...this.templates().filter(t => t.name !== name), template].slice(-20);
    try { localStorage.setItem(this.storageKey, JSON.stringify(templates)); this.templates.set(templates); } catch { this.error.set('Không lưu được mẫu trên trình duyệt.'); }
  }
  apply(name: string) {
    const template = this.templates().find(t => t.name === name); if (!template) return;
    if (template.headerRow !== this.headerRow) { this.error.set('Chọn dòng tiêu đề ' + template.headerRow + ' rồi áp dụng mẫu lại.'); return; }
    this.columns = Object.fromEntries(this.fields().map(f => { const matches = this.headers().filter(h => h.name === template.columns[f.key]); return [f.key, matches.length === 1 ? matches[0].index : null]; }));
    this.changed();
  }
}
