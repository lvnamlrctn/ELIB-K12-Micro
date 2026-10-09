import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, BibType, CrudClient, MarcField, MarcFieldDef, Worksheet, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { MarcEditor } from '../../shared/marc-editor';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal } from '../../shared/ui';

interface WorksheetForm { publicId: string | null; name: string; bibTypeId: number | null; fields: MarcField[]; }

/** Biểu mẫu biên mục (monolith: /admin/worksheets + worksheet-detail, quyền WORKSHEETS) — danh sách trường dựng sẵn cho màn biên mục. */
@Component({
  selector: 'app-worksheets',
  imports: [FormsModule, Loading, Modal, ConfirmDelete, MarcEditor],
  template: `
    <div class="mb-5"><h4 class="page-title">Biểu mẫu biên mục</h4></div>

    <div class="panel flex gap-3 items-end flex-wrap">
      <div class="w-64">
        <label for="ws-type" class="field-label">Loại biểu ghi</label>
        <select id="ws-type" class="input" [(ngModel)]="typeFilter" (change)="load()">
          <option [ngValue]="null">Tất cả</option>
          @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
        </select>
      </div>
      @if (can('add')) {
        <button class="btn-add ml-auto" (click)="open(null)"><span class="material-icons text-[18px]">add</span> Thêm biểu mẫu</button>
      }
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <table class="w-full text-left border-collapse">
        <thead><tr>
          <th class="th">Tên biểu mẫu</th><th class="th w-56">Loại biểu ghi</th><th class="th">Các trường</th><th class="th w-28 !text-center">Thao tác</th>
        </tr></thead>
        <tbody>
          @for (w of items(); track w.publicId) {
            <tr class="hover:bg-gray-50/50">
              <td class="td font-medium text-gray-800">{{ w.name }}</td>
              <td class="td">{{ typeName(w.bibTypeId) }}</td>
              <td class="td font-mono text-[12px] text-gray-500">{{ tags(w) }}</td>
              <td class="td">
                <div class="flex items-center justify-center gap-1.5">
                  @if (can('edit')) {
                    <button (click)="open(w)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" title="Sửa"><span class="material-icons text-[18px]">edit</span></button>
                  }
                  @if (can('delete')) {
                    <button (click)="deleting.set(w)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" title="Xoá"><span class="material-icons text-[18px]">delete</span></button>
                  }
                </div>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="4" class="py-8 text-center text-gray-500">{{ loading() ? '' : 'Chưa có biểu mẫu nào' }}</td></tr>
          }
        </tbody>
      </table>
    </div>

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'Sửa biểu mẫu' : 'Thêm biểu mẫu'" widthClass="max-w-4xl" (closed)="form.set(null)">
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
          <div><label class="form-label" for="ws-name">Tên biểu mẫu <span class="text-red-500">*</span></label>
            <input id="ws-name" class="input" [(ngModel)]="f.name" placeholder="VD: Sách tham khảo" /></div>
          <div><label class="form-label" for="ws-btype">Loại biểu ghi</label>
            <select id="ws-btype" class="input" [(ngModel)]="f.bibTypeId"><option [ngValue]="null">—</option>
              @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
        </div>
        <p class="text-xs text-gray-500 mb-3">Giá trị nhập ở đây là giá trị mặc định khi biên mục (VD 041$a = vie). Trường con để trống vẫn được giữ trong biểu mẫu.</p>
        <app-marc-editor [(fields)]="f.fields" [defs]="defs()" [template]="true" />
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="form.set(null)">Huỷ</button>
          <button type="button" class="btn-primary" [disabled]="saving()" (click)="save(f)"><span class="material-icons text-[16px]">save</span>{{ f.publicId ? 'Cập nhật' : 'Lưu' }}</button>
        </ng-container>
      </app-modal>
    }

    @if (deleting(); as w) {
      <app-confirm-delete [busy]="saving()" [message]="'Xoá biểu mẫu ' + w.name + '?'" (confirmed)="remove(w)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Worksheets implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly client: CrudClient<Worksheet> = this.api.crud<Worksheet>('worksheets', 'catalog');

  protected typeFilter: number | null = null;
  protected readonly items = signal<Worksheet[]>([]);
  protected readonly types = signal<BibType[]>([]);
  protected readonly defs = signal<MarcFieldDef[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly form = signal<WorksheetForm | null>(null);
  protected readonly deleting = signal<Worksheet | null>(null);

  async ngOnInit(): Promise<void> {
    void this.load();
    const [types, defs] = await Promise.allSettled([this.api.crud<BibType>('bib-types', 'catalog').searchAll(), this.api.marc21Fields()]);
    if (types.status === 'fulfilled') this.types.set(types.value);
    if (defs.status === 'fulfilled') this.defs.set(defs.value);
  }

  protected can(action: string): boolean {
    return this.session.can(`WORKSHEETS:${action}`);
  }

  protected typeName(id: number | null): string {
    return id == null ? '—' : (this.types().find((t) => t.id === id)?.name ?? '—');
  }

  protected tags(w: Worksheet): string {
    return w.fields.map((f) => f.tag).join(' · ');
  }

  protected open(w: Worksheet | null): void {
    this.form.set(w
      ? { publicId: w.publicId, name: w.name, bibTypeId: w.bibTypeId, fields: structuredClone(w.fields) }
      : { publicId: null, name: '', bibTypeId: this.typeFilter, fields: [{ tag: '245', ind1: '1', ind2: '0', subfields: [{ code: 'a', value: '' }, { code: 'c', value: '' }] }] });
  }

  protected async save(f: WorksheetForm): Promise<void> {
    if (!f.name.trim()) {
      this.toastr.warning('Vui lòng nhập tên biểu mẫu.');
      return;
    }
    this.saving.set(true);
    try {
      const body = { name: f.name, bibTypeId: f.bibTypeId, fields: f.fields };
      if (f.publicId) await this.client.update(f.publicId, body);
      else await this.client.add(body);
      this.toastr.success(f.publicId ? 'Đã cập nhật biểu mẫu.' : 'Đã thêm biểu mẫu.');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(w: Worksheet): Promise<void> {
    this.saving.set(true);
    try {
      await this.client.delete(w.publicId);
      this.toastr.success('Đã xoá biểu mẫu.');
      this.deleting.set(null);
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
      this.items.set(await this.client.searchAll({ bibTypeId: this.typeFilter } as never));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
