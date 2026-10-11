import { Injectable, WritableSignal, computed, inject, signal } from '@angular/core';
import { Api, NamedItem, Org } from '../../core/api';

/** Một lựa chọn đơn vị trong ô chọn dạng cây (thụt lề theo cấp). */
export interface OrgOption { id: number; name: string; depth: number; }

/**
 * Danh mục dùng ở màn bạn đọc: loại/lớp/khoá/nhóm (patron), đơn vị (cây), dân tộc, trình độ, học hàm (tenant). Thiếu quyền xem danh mục nào
 * thì danh mục đó để trống — không chặn màn bạn đọc. Cung cấp ở cấp component (mỗi lần mở màn tải lại).
 */
@Injectable()
export class ReaderCatalogs {
  private readonly api = inject(Api);

  readonly types = signal<NamedItem[]>([]);
  readonly classes = signal<NamedItem[]>([]);
  readonly courses = signal<NamedItem[]>([]);
  readonly groups = signal<NamedItem[]>([]);
  readonly orgs = signal<Org[]>([]);
  readonly ethnicities = signal<NamedItem[]>([]);
  readonly degrees = signal<NamedItem[]>([]);
  readonly academicTitles = signal<NamedItem[]>([]);

  /** Đơn vị theo thứ tự cây (cha trước con, cùng cấp theo thứ tự rồi tên). */
  readonly orgOptions = computed<OrgOption[]>(() => {
    const all = this.orgs();
    const byParent = new Map<number | null, Org[]>();
    const ids = new Set(all.map((o) => o.id));
    for (const o of all) {
      const parent = o.parentId !== null && ids.has(o.parentId) ? o.parentId : null;
      byParent.set(parent, [...(byParent.get(parent) ?? []), o]);
    }
    const out: OrgOption[] = [];
    const walk = (parent: number | null, depth: number) => {
      for (const o of (byParent.get(parent) ?? []).sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))) {
        out.push({ id: o.id, name: o.name, depth });
        if (depth < 20) walk(o.id, depth + 1);
      }
    };
    walk(null, 0);
    return out;
  });

  async load(): Promise<void> {
    await Promise.all([
      this.fill(this.types, () => this.api.crud<NamedItem>('reader-types', 'patron').searchAll()),
      this.fill(this.classes, () => this.api.crud<NamedItem>('classes', 'patron').searchAll()),
      this.fill(this.courses, () => this.api.crud<NamedItem>('courses', 'patron').searchAll()),
      this.fill(this.groups, () => this.api.crud<NamedItem>('reader-groups', 'patron').searchAll()),
      this.fill(this.orgs, () => this.api.crud<Org>('orgs').searchAll()),
      this.fill(this.ethnicities, () => this.api.crud<NamedItem>('ethnicities').searchAll()),
      this.fill(this.degrees, () => this.api.crud<NamedItem>('degrees').searchAll()),
      this.fill(this.academicTitles, () => this.api.crud<NamedItem>('academic-titles').searchAll()),
    ]);
  }

  private async fill<T>(target: WritableSignal<T[]>, fetch: () => Promise<T[]>): Promise<void> {
    try {
      target.set(await fetch());
    } catch {
      /* thiếu quyền xem danh mục — ô chọn để trống */
    }
  }

  /** Đơn vị và mọi đơn vị con (lọc "thuộc đơn vị" như ELIB-LRC). */
  orgWithDescendants(id: number): number[] {
    const children = new Map<number, number[]>();
    for (const o of this.orgs()) if (o.parentId !== null) children.set(o.parentId, [...(children.get(o.parentId) ?? []), o.id]);
    const out: number[] = [];
    const stack = [id];
    while (stack.length && out.length < 500) {
      const next = stack.pop()!;
      if (out.includes(next)) continue;
      out.push(next);
      stack.push(...(children.get(next) ?? []));
    }
    return out;
  }

  name(items: { id: number; name: string }[], id: number | null | undefined): string {
    return id == null ? '' : (items.find((i) => i.id === id)?.name ?? '');
  }
}
