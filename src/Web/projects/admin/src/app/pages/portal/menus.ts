import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, STATUS_ACTIVE, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, StatusBadge } from '../../shared/ui';

interface MenuRow {
  id: number; publicId: string; name: string; menuTypeId: number; linkType: string; parentId: number | null; level: number; sortOrder: number; status: number;
  link: string | null; targetId: number | null; openInNewTab: boolean; requiresLogin: boolean; icon: string | null; language: string | null;
}
interface MenuForm {
  publicId: string | null; name: string; menuTypeId: number | null; linkType: string; parentId: number | null; sortOrder: number; status: number; link: string;
  targetId: number | null; openInNewTab: boolean; requiresLogin: boolean; icon: string; language: string;
}
type Named = { id: number; publicId: string; name: string };

/**
 * Menu cổng thông tin (LRC: Menu, quyền PORTAL_MENUS): cây theo loại menu. Đích chỉ có 4 kiểu — địa chỉ ngoài (http/https), đường dẫn nội bộ, trang tĩnh
 * hoặc chuyên mục tin của chính đơn vị (server kiểm lại).
 */
@Component({
  selector: 'app-portal-menus',
  imports: [FormsModule, Modal, ConfirmDelete, Loading, StatusBadge, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'PORTAL_MENUS_PAGE.TITLE' | translate }}</h4></div>
    <div class="panel flex gap-3 items-end justify-between flex-wrap">
      <div>
        <label class="field-label" for="mn-type">{{ 'PORTAL_MENUS_PAGE.TYPE' | translate }}</label>
        <select id="mn-type" class="input" [ngModel]="typeFilter()" (ngModelChange)="typeFilter.set($event)">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
        </select>
      </div>
      @if (can('add')) { <button (click)="openCreate()" class="btn-add"><span class="material-icons text-[18px]">add</span> {{ 'COMMON.ADD_BTN' | translate }}</button> }
    </div>

    <div class="panel !p-0 relative">
      @if (loading()) { <app-loading /> }
      <table class="w-full text-left border-collapse">
        <thead><tr><th class="th">{{ 'PORTAL_MENUS_PAGE.NAME' | translate }}</th><th class="th">{{ 'PORTAL_MENUS_PAGE.TYPE' | translate }}</th><th class="th">{{ 'PORTAL_MENUS_PAGE.TARGET' | translate }}</th>
          <th class="th !text-right">{{ 'PORTAL_MENUS_PAGE.ORDER' | translate }}</th><th class="th !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th !text-center">{{ 'COMMON.ACTIONS' | translate }}</th></tr></thead>
        <tbody>
          @for (m of visible(); track m.publicId) {
            <tr class="hover:bg-gray-50">
              <td class="td"><span [style.padding-left.px]="(m.level - 1) * 20" class="font-medium text-gray-800">{{ m.name }}</span></td>
              <td class="td">{{ typeName(m.menuTypeId) }}</td><td class="td text-sm text-gray-600">{{ targetText(m) }}</td>
              <td class="td text-right">{{ m.sortOrder }}</td><td class="td text-center"><app-status-badge [status]="m.status" /></td>
              <td class="td"><div class="flex items-center justify-center gap-1">
                @if (can('edit')) { <button (click)="openEdit(m)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate"><span class="material-icons text-[18px]">edit</span></button> }
                @if (can('delete')) { <button (click)="deleting.set(m)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">delete</span></button> }
              </div></td>
            </tr>
          } @empty { <tr><td colspan="6" class="td text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
        </tbody>
      </table>
    </div>

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'COMMON.EDIT' : 'COMMON.ADD_BTN'" widthClass="max-w-xl" (closed)="form.set(null)">
        <form id="menu-form" class="grid grid-cols-2 gap-4" (ngSubmit)="save()">
          <div class="col-span-2"><label class="form-label" for="m-name">{{ 'PORTAL_MENUS_PAGE.NAME' | translate }} *</label><input id="m-name" class="input" name="name" required maxlength="200" [(ngModel)]="f.name" /></div>
          <div><label class="form-label" for="m-type">{{ 'PORTAL_MENUS_PAGE.TYPE' | translate }} *</label>
            <select id="m-type" class="input" name="menuType" required [(ngModel)]="f.menuTypeId">@for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
          <div><label class="form-label" for="m-parent">{{ 'PORTAL_MENUS_PAGE.PARENT' | translate }}</label>
            <select id="m-parent" class="input" name="parent" [(ngModel)]="f.parentId"><option [ngValue]="null">—</option>
              @for (p of parentOptions(f); track p.publicId) { <option [ngValue]="p.id">{{ p.name }}</option> }</select></div>
          <div><label class="form-label" for="m-lt">{{ 'PORTAL_MENUS_PAGE.LINK_TYPE' | translate }}</label>
            <select id="m-lt" class="input" name="linkType" [(ngModel)]="f.linkType">
              <option value="path">{{ 'PORTAL_MENUS_PAGE.TYPE_PATH' | translate }}</option><option value="url">{{ 'PORTAL_MENUS_PAGE.TYPE_URL' | translate }}</option>
              <option value="page">{{ 'PORTAL_MENUS_PAGE.TYPE_PAGE' | translate }}</option><option value="category">{{ 'PORTAL_MENUS_PAGE.TYPE_CATEGORY' | translate }}</option></select></div>
          <div>
            @switch (f.linkType) {
              @case ('page') { <label class="form-label" for="m-tp">{{ 'PORTAL_MENUS_PAGE.TYPE_PAGE' | translate }}</label>
                <select id="m-tp" class="input" name="targetPage" [(ngModel)]="f.targetId">@for (p of pages(); track p.id) { <option [ngValue]="p.id">{{ p.name }}</option> }</select> }
              @case ('category') { <label class="form-label" for="m-tc">{{ 'PORTAL_MENUS_PAGE.TYPE_CATEGORY' | translate }}</label>
                <select id="m-tc" class="input" name="targetCategory" [(ngModel)]="f.targetId">@for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }</select> }
              @default { <label class="form-label" for="m-link">{{ 'PORTAL_MENUS_PAGE.LINK' | translate }}</label>
                <input id="m-link" class="input" name="link" maxlength="500" [(ngModel)]="f.link" [placeholder]="f.linkType === 'url' ? 'https://' : '/tin-tuc'" /> }
            }
          </div>
          <div><label class="form-label" for="m-order">{{ 'PORTAL_MENUS_PAGE.ORDER' | translate }}</label><input id="m-order" type="number" class="input" name="order" [(ngModel)]="f.sortOrder" /></div>
          <div><label class="form-label" for="m-icon">{{ 'PORTAL_MENUS_PAGE.ICON' | translate }}</label><input id="m-icon" class="input font-mono" name="icon" maxlength="40" [(ngModel)]="f.icon" placeholder="home" /></div>
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" name="newTab" [(ngModel)]="f.openInNewTab" /> {{ 'PORTAL_MENUS_PAGE.NEW_TAB' | translate }}</label>
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" name="login" [(ngModel)]="f.requiresLogin" /> {{ 'PORTAL_MENUS_PAGE.REQUIRES_LOGIN' | translate }}</label>
          <div><label class="form-label" for="m-lang">{{ 'PORTAL_NEWS_PAGE.LANGUAGE' | translate }}</label>
            <select id="m-lang" class="input" name="language" [(ngModel)]="f.language"><option value="">{{ 'PORTAL_NEWS_PAGE.ALL_LANGUAGES' | translate }}</option><option value="vi">{{ 'PORTAL_NEWS_PAGE.LANG_VI' | translate }}</option><option value="en">{{ 'PORTAL_NEWS_PAGE.LANG_EN' | translate }}</option></select></div>
          <div><label class="form-label" for="m-status">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="m-status" class="input" name="status" [(ngModel)]="f.status"><option [ngValue]="2">{{ 'COMMON.ACTIVE' | translate }}</option><option [ngValue]="1">{{ 'COMMON.STATUS_INACTIVE' | translate }}</option></select></div>
        </form>
        <ng-container footer>
          <button type="button" (click)="form.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="menu-form" [disabled]="saving()" class="btn-primary"><span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span> {{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (deleting(); as d) { <app-confirm-delete [busy]="saving()" message="COMMON.DELETE_CONFIRM_IRREVERSIBLE" (confirmed)="doDelete(d)" (cancelled)="deleting.set(null)" /> }
  `,
})
export class PortalMenus implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly client = this.api.crud<MenuRow>('menus', 'portal');

  protected readonly menus = signal<MenuRow[]>([]);
  protected readonly types = signal<(Named & { code: string })[]>([]);
  protected readonly pages = signal<Named[]>([]);
  protected readonly categories = signal<Named[]>([]);
  protected readonly typeFilter = signal<number | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly form = signal<MenuForm | null>(null);
  protected readonly deleting = signal<MenuRow | null>(null);

  /** Cây phẳng theo thứ tự cha → con (đã sắp theo loại menu, thứ tự). */
  protected readonly visible = computed(() => {
    const rows = this.menus().filter((m) => this.typeFilter() == null || m.menuTypeId === this.typeFilter());
    const byParent = new Map<number | null, MenuRow[]>();
    for (const r of rows) (byParent.get(r.parentId) ?? byParent.set(r.parentId, []).get(r.parentId)!).push(r);
    const out: MenuRow[] = [];
    const walk = (parent: number | null): void =>
      (byParent.get(parent) ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id).forEach((r) => {
        out.push(r);
        walk(r.id);
      });
    // Gốc của lọc có thể là mục có cha nằm ngoài lọc — coi mục không tìm thấy cha trong tập là gốc.
    const ids = new Set(rows.map((r) => r.id));
    for (const r of rows.filter((x) => x.parentId == null || !ids.has(x.parentId)).sort((a, b) => a.menuTypeId - b.menuTypeId || a.sortOrder - b.sortOrder || a.id - b.id)) {
      if (!out.includes(r)) {
        out.push(r);
        walk(r.id);
      }
    }
    return out;
  });

  ngOnInit(): void {
    void this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`PORTAL_MENUS:${action}`);
  }

  protected typeName(id: number): string {
    return this.types().find((t) => t.id === id)?.name ?? '';
  }

  protected targetText(m: MenuRow): string {
    if (m.linkType === 'page') return this.pages().find((p) => p.id === m.targetId)?.name ?? '';
    if (m.linkType === 'category') return this.categories().find((c) => c.id === m.targetId)?.name ?? '';
    return m.link ?? '';
  }

  protected parentOptions(f: MenuForm): MenuRow[] {
    return this.menus().filter((m) => m.menuTypeId === f.menuTypeId && m.publicId !== f.publicId);
  }

  protected openCreate(): void {
    this.form.set({ publicId: null, name: '', menuTypeId: this.typeFilter() ?? this.types()[0]?.id ?? null, linkType: 'path', parentId: null, sortOrder: 0, status: 2, link: '/', targetId: null, openInNewTab: false, requiresLogin: false, icon: '', language: '' });
  }

  protected openEdit(m: MenuRow): void {
    this.form.set({
      publicId: m.publicId, name: m.name, menuTypeId: m.menuTypeId, linkType: m.linkType, parentId: m.parentId, sortOrder: m.sortOrder, status: m.status, link: m.link ?? '',
      targetId: m.targetId, openInNewTab: m.openInNewTab, requiresLogin: m.requiresLogin, icon: m.icon ?? '', language: m.language ?? '',
    });
  }

  protected async save(): Promise<void> {
    const f = this.form();
    if (!f || f.menuTypeId == null) return;
    this.saving.set(true);
    try {
      const body = {
        name: f.name, menuTypeId: f.menuTypeId, linkType: f.linkType, parentId: f.parentId, sortOrder: f.sortOrder, status: f.status,
        link: f.linkType === 'url' || f.linkType === 'path' ? f.link : null, targetId: f.linkType === 'page' || f.linkType === 'category' ? f.targetId : null,
        openInNewTab: f.openInNewTab, requiresLogin: f.requiresLogin, icon: f.icon || null, language: f.language || null,
      };
      if (f.publicId) await this.client.update(f.publicId, body);
      else await this.client.add(body);
      this.toastr.success(f.publicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async doDelete(m: MenuRow): Promise<void> {
    this.saving.set(true);
    try {
      await this.client.delete(m.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [menus, types, pages, categories] = await Promise.all([
        this.client.searchAll(), this.api.crud<Named & { code: string }>('menu-types', 'portal').searchAll(),
        this.api.crud<{ id: number; publicId: string; title: string }>('pages', 'portal').searchAll(), this.api.crud<Named>('news-categories', 'portal').searchAll(),
      ]);
      this.menus.set(menus);
      this.types.set(types);
      this.pages.set(pages.map((p) => ({ id: p.id, publicId: p.publicId, name: p.title })));
      this.categories.set(categories);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
