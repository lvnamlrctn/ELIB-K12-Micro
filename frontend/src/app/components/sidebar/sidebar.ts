import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Menu, MenuItem } from '../../services/menu';
import { LayoutService } from '../../services/layout';
import { PermissionService } from '../../services/system/permission.service';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, TranslateModule],
  templateUrl: './sidebar.html',
  host: {
    class: 'block z-30 h-full relative'
  },
  styles: [`
    aside { width: var(--sbw, 260px); }
    @media (min-width: 768px) {
      aside.md-collapsed { margin-left: calc(var(--sbw, 260px) * -1); }
    }
    .resize-handle { position: absolute; top: 0; right: -2px; width: 4px; height: 100%; cursor: col-resize; z-index: 10; }
    .resize-handle:hover, .resize-handle.resizing { background: rgba(59, 130, 246, 0.5); }
  `]
})
export class Sidebar {
  layout = inject(LayoutService);
  menuService = inject(Menu);
  private permission = inject(PermissionService);

  isResizing = signal(false);

  private onResizeMove = (e: MouseEvent): void => {
    this.layout.setSidebarWidth(e.clientX);
  };

  private onResizeEnd = (): void => {
    this.isResizing.set(false);
    document.removeEventListener('mousemove', this.onResizeMove);
    document.removeEventListener('mouseup', this.onResizeEnd);
    document.body.style.userSelect = '';
  };

  startResize(event: MouseEvent): void {
    event.preventDefault();
    this.isResizing.set(true);
    document.body.style.userSelect = 'none';
    document.addEventListener('mousemove', this.onResizeMove);
    document.addEventListener('mouseup', this.onResizeEnd);
  }

  private rawMenu = toSignal(this.menuService.getMenu(), { initialValue: [] as MenuItem[] });

  // Lọc menu theo quyền canView; tự cập nhật khi quyền (perms signal) tải xong.
  menuItems = computed(() => this.filterMenu(this.rawMenu()));

  private filterMenu(items: MenuItem[]): MenuItem[] {
    const out: MenuItem[] = [];
    for (const item of items) {
      if (item.children?.length) {
        const kids = this.filterMenu(item.children);
        if (kids.length) out.push({ ...item, children: kids });
      } else if (item.link) {
        const visible = item.perm
          ? this.permission.can(item.perm, 'view')
          : this.permission.canViewLink(item.permLink ?? item.link);
        if (visible) out.push(item);
      }
    }
    return out;
  }

  toggleSubmenu(item: MenuItem) {
    item.expanded = !item.expanded;
  }
}
