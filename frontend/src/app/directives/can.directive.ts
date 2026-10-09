import { Directive, Input, TemplateRef, ViewContainerRef, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { PermissionService } from '../services/system/permission.service';

export type CanAction = 'view' | 'add' | 'edit' | 'delete' | 'export' | 'import';

/**
 * *appCan="'add'" — chỉ render host nếu user có quyền tương ứng trên module của trang hiện tại.
 * export → quyền view, import → quyền add. Fail-open khi chưa có dữ liệu quyền.
 */
@Directive({ selector: '[appCan]', standalone: true })
export class CanDirective {
  private tpl = inject(TemplateRef<unknown>);
  private vcr = inject(ViewContainerRef);
  private perm = inject(PermissionService);
  private router = inject(Router);

  private action: CanAction = 'view';
  private rendered = false;

  @Input() set appCan(action: CanAction) {
    this.action = action || 'view';
    this.update();
  }

  /** Override URL dùng tra quyền — dùng cho trang con có route không khớp tiền tố module cha. */
  @Input() appCanUrl?: string;

  /** Chỉ định thẳng mã quyền (ModuleCode), bỏ qua việc suy từ URL. */
  @Input() appCanCode?: string;

  constructor() {
    // Tự đánh giá lại khi quyền được tải xong (perms signal đổi)
    effect(() => {
      this.perm.perms();
      this.update();
    });
  }

  private update(): void {
    const allowed = this.check();
    if (allowed && !this.rendered) {
      this.vcr.createEmbeddedView(this.tpl);
      this.rendered = true;
    } else if (!allowed && this.rendered) {
      this.vcr.clear();
      this.rendered = false;
    }
  }

  private check(): boolean {
    if (this.appCanCode) {
      const a = this.action === 'import' ? 'add' : this.action === 'export' ? 'view' : this.action;
      return this.perm.can(this.appCanCode, a);
    }
    const url = this.appCanUrl ?? this.router.url;
    switch (this.action) {
      case 'add':
      case 'import': return this.perm.canAdd(url);
      case 'edit':   return this.perm.canEdit(url);
      case 'delete': return this.perm.canDelete(url);
      case 'view':
      case 'export':
      default:       return this.perm.canView(url);
    }
  }
}
