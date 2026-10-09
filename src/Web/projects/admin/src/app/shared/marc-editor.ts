import { Component, computed, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MarcField, MarcFieldDef, MarcSubfieldDef } from '../core/api';

/**
 * Soạn các trường MARC21 (monolith: shared/marc-editor) — mỗi trường một khối: nhãn, hai chỉ thị, các trường con $a, $b…
 * Tên trường/trường con lấy từ từ điển MARC21 của service catalog. <see cref="template"/>: soạn biểu mẫu biên mục
 * (giá trị là mặc định, được để trống, sắp được thứ tự trường).
 */
@Component({
  selector: 'app-marc-editor',
  imports: [FormsModule],
  template: `
    <div class="space-y-2.5">
      @for (f of fields(); track $index; let i = $index) {
        @let def = defOf(f.tag);
        <div class="border border-gray-200 rounded-lg p-3 bg-white" [class.border-amber-300]="repeatedWrongly(f, i)">
          <div class="flex items-center gap-2 flex-wrap">
            <input class="input !w-16 !py-1 font-mono font-bold text-center" maxlength="3" placeholder="Tag" [(ngModel)]="f.tag"
                   [disabled]="isSystem(f.tag)" (ngModelChange)="touch()" [attr.aria-label]="'Nhãn trường ' + (i + 1)" />
            @if (!isControl(f.tag)) {
              <input class="input !w-10 !py-1 font-mono text-center" maxlength="1" placeholder="#" [(ngModel)]="f.ind1"
                     [title]="def?.ind1 ?? 'Chỉ thị 1'" [attr.aria-label]="'Chỉ thị 1 trường ' + f.tag" />
              <input class="input !w-10 !py-1 font-mono text-center" maxlength="1" placeholder="#" [(ngModel)]="f.ind2"
                     [title]="def?.ind2 ?? 'Chỉ thị 2'" [attr.aria-label]="'Chỉ thị 2 trường ' + f.tag" />
            }
            <span class="text-sm font-semibold text-blue-700 truncate">{{ def?.name ?? (f.tag.length === 3 ? 'Trường ngoài từ điển' : '') }}</span>
            @if (repeatedWrongly(f, i)) { <span class="text-xs text-amber-600">Trường này không lặp</span> }
            <div class="ml-auto flex items-center gap-1">
              @if (template()) {
                <button type="button" class="icon-btn text-gray-500 hover:bg-gray-100" title="Lên" [disabled]="i === 0" (click)="move(i, -1)"><span class="material-icons text-[18px]">arrow_upward</span></button>
                <button type="button" class="icon-btn text-gray-500 hover:bg-gray-100" title="Xuống" [disabled]="i === fields().length - 1" (click)="move(i, 1)"><span class="material-icons text-[18px]">arrow_downward</span></button>
              }
              @if (!isControl(f.tag)) {
                <select #pick class="input !w-auto !py-1 text-xs" (change)="addSubfield(f, pick.value); pick.value = ''" [attr.aria-label]="'Thêm trường con cho ' + f.tag">
                  <option value="">+ trường con</option>
                  @for (s of def?.subfields ?? []; track s.code) { <option [value]="s.code">\${{ s.code }} {{ s.name }}</option> }
                  <option value="?">Mã khác…</option>
                </select>
              }
              @if (!isSystem(f.tag)) {
                <button type="button" class="icon-btn text-red-500 hover:bg-red-50" title="Xoá trường" (click)="remove(i)"><span class="material-icons text-[18px]">close</span></button>
              }
            </div>
          </div>

          @if (isControl(f.tag)) {
            <input class="input mt-2 font-mono text-[13px]" [(ngModel)]="f.value" [disabled]="isSystem(f.tag)"
                   [placeholder]="isSystem(f.tag) ? 'Hệ thống tự sinh' : (template() ? 'Giá trị mặc định' : 'Giá trị')" [attr.aria-label]="'Giá trị trường ' + f.tag" />
          } @else {
            <div class="mt-2 space-y-1.5 pl-2 sm:pl-6">
              @for (s of f.subfields ?? []; track $index; let j = $index) {
                <div class="flex items-center gap-2">
                  <span class="text-gray-400 font-mono text-sm">$</span>
                  <input class="input !w-10 !py-1 font-mono text-center" maxlength="1" [(ngModel)]="s.code" [attr.aria-label]="'Mã trường con ' + f.tag" />
                  <input class="input !py-1 flex-1" [(ngModel)]="s.value" [placeholder]="subName(def, s.code) ?? (template() ? 'Giá trị mặc định' : 'Giá trị')"
                         [title]="subName(def, s.code) ?? ''" [attr.aria-label]="f.tag + ' $' + s.code + ' ' + (subName(def, s.code) ?? '')" />
                  <button type="button" class="icon-btn text-red-400 hover:bg-red-50" title="Xoá trường con" (click)="removeSubfield(f, j)"><span class="material-icons text-[16px]">remove</span></button>
                </div>
              }
            </div>
          }
        </div>
      }

      @if (picking()) {
        <div class="border border-blue-200 rounded-lg p-3 bg-blue-50/40">
          <div class="flex gap-2">
            <input class="input" placeholder="Tìm theo nhãn hoặc tên trường (VD 650, chủ đề)..." [(ngModel)]="filter" aria-label="Tìm trường MARC" />
            <button type="button" class="btn-secondary !py-1.5" (click)="picking.set(false)">Đóng</button>
          </div>
          <div class="max-h-64 overflow-y-auto mt-2 grid grid-cols-1 md:grid-cols-2 gap-1">
            @for (d of filtered(); track d.tag) {
              <button type="button" class="text-left px-2 py-1.5 rounded hover:bg-blue-100 text-sm flex gap-2" (click)="addField(d)">
                <span class="font-mono font-bold text-blue-700">{{ d.tag }}</span><span class="text-gray-700 truncate">{{ d.name }}</span>
              </button>
            }
            <button type="button" class="text-left px-2 py-1.5 rounded hover:bg-blue-100 text-sm text-blue-600" (click)="addField(null)">Trường khác (tự nhập nhãn)…</button>
          </div>
        </div>
      } @else {
        <button type="button" (click)="picking.set(true); filter = ''"
                class="w-full py-2 border-2 border-dashed border-gray-300 rounded-lg text-sm text-gray-500 hover:border-blue-400 hover:text-blue-600 flex items-center justify-center gap-1">
          <span class="material-icons text-[18px]">add</span> Thêm trường
        </button>
      }
    </div>
  `,
})
export class MarcEditor {
  readonly fields = model.required<MarcField[]>();
  readonly defs = input<MarcFieldDef[]>([]);
  /** Soạn biểu mẫu: giữ thứ tự, cho sắp xếp, trường con được để trống. */
  readonly template = input(false);

  protected readonly picking = signal(false);
  protected filter = '';
  private readonly version = signal(0);

  private readonly byTag = computed(() => new Map(this.defs().map((d) => [d.tag, d])));

  protected filtered(): MarcFieldDef[] {
    const q = this.filter.trim().toLowerCase();
    return this.defs().filter((d) => !q || d.tag.includes(q) || d.name.toLowerCase().includes(q));
  }

  protected defOf(tag: string): MarcFieldDef | undefined {
    this.version();
    return this.byTag().get(tag);
  }

  protected subName(def: MarcFieldDef | undefined, code: string): string | null {
    return def?.subfields.find((s: MarcSubfieldDef) => s.code === code)?.name ?? null;
  }

  protected isControl(tag: string): boolean {
    return /^00\d$/.test(tag);
  }

  /** 001 (MFN) và 005 (thời điểm sửa) do hệ thống quản lý. */
  protected isSystem(tag: string): boolean {
    return tag === '001' || tag === '005';
  }

  protected repeatedWrongly(f: MarcField, index: number): boolean {
    const def = this.byTag().get(f.tag);
    return !!def && !def.repeatable && this.fields().findIndex((x) => x.tag === f.tag) !== index;
  }

  protected touch(): void {
    this.version.update((v) => v + 1);
  }

  protected addField(def: MarcFieldDef | null): void {
    const tag = def?.tag ?? '';
    const field: MarcField = this.isControl(tag)
      ? { tag, value: '' }
      : { tag, ind1: ' ', ind2: ' ', subfields: [{ code: def?.subfields[0]?.code ?? 'a', value: '' }] };
    const list = [...this.fields()];
    // Biểu ghi: chèn đúng chỗ theo nhãn (server cũng sắp như vậy); biểu mẫu: thêm cuối.
    const at = this.template() || !tag ? list.length : list.findIndex((x) => x.tag > tag);
    list.splice(at < 0 ? list.length : at, 0, field);
    this.fields.set(list);
    this.picking.set(false);
  }

  protected remove(index: number): void {
    this.fields.set(this.fields().filter((_, i) => i !== index));
  }

  protected move(index: number, delta: number): void {
    const list = [...this.fields()];
    const [item] = list.splice(index, 1);
    list.splice(index + delta, 0, item);
    this.fields.set(list);
  }

  protected addSubfield(f: MarcField, code: string): void {
    if (!code) return;
    f.subfields = [...(f.subfields ?? []), { code: code === '?' ? '' : code, value: '' }];
    this.fields.set([...this.fields()]);
  }

  protected removeSubfield(f: MarcField, index: number): void {
    f.subfields = (f.subfields ?? []).filter((_, i) => i !== index);
    this.fields.set([...this.fields()]);
  }
}
