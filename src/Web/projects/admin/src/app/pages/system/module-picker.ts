import { Component, computed, input, model } from '@angular/core';
import { ModuleDef } from '../../core/api';

const PACKAGE_NAMES: Record<string, string> = {
  PRINT: 'Gói Sách in',
  DIGITAL: 'Gói Thư viện số',
  SEARCH: 'Gói Tra cứu',
  EXTENSION: 'Mở rộng',
  K12: 'Riêng K12',
};

/** Chọn module bán, nhóm theo gói. Chọn một module thì tự chọn các module nó phụ thuộc; bỏ chọn thì bỏ luôn module phụ thuộc vào nó. */
@Component({
  selector: 'app-module-picker',
  template: `
    <div class="space-y-4">
      @for (group of groups(); track group.name) {
        <div>
          <div class="text-xs font-bold text-gray-500 uppercase tracking-wide mb-2">{{ group.label }}</div>
          <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-2">
            @for (m of group.modules; track m.code) {
              <label class="flex items-start gap-2 p-3 rounded-lg border cursor-pointer transition-colors"
                     [class]="selected().includes(m.code) ? 'border-blue-300 bg-blue-50/60' : 'border-gray-200 hover:bg-gray-50'"
                     [title]="m.dependsOn.length ? 'Cần: ' + m.dependsOn.join(', ') : ''">
                <input type="checkbox" class="rounded border-gray-300 mt-0.5" [checked]="selected().includes(m.code)" (change)="toggle(m.code, $any($event.target).checked)" />
                <span>
                  <span class="block text-sm font-medium text-gray-800">{{ m.name }}</span>
                  <span class="block text-xs text-gray-500 font-mono">{{ m.code }}@if (m.dependsOn.length) { · cần {{ m.dependsOn.join(', ') }} }</span>
                </span>
              </label>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class ModulePicker {
  readonly modules = input.required<ModuleDef[]>();
  readonly selected = model<string[]>([]);

  protected readonly groups = computed(() => {
    const map = new Map<string, ModuleDef[]>();
    for (const m of this.modules()) map.set(m.package, [...(map.get(m.package) ?? []), m]);
    return [...map.entries()].map(([name, modules]) => ({ name, label: PACKAGE_NAMES[name] ?? name, modules }));
  });

  protected toggle(code: string, checked: boolean): void {
    const byCode = new Map(this.modules().map((m) => [m.code, m]));
    const set = new Set(this.selected());
    if (checked) {
      const add = (c: string): void => {
        if (set.has(c)) return;
        set.add(c);
        byCode.get(c)?.dependsOn.forEach(add);
      };
      add(code);
    } else {
      const remove = (c: string): void => {
        if (!set.delete(c)) return;
        this.modules().filter((m) => m.dependsOn.includes(c)).forEach((m) => remove(m.code));
      };
      remove(code);
    }
    this.selected.set([...set]);
  }
}
