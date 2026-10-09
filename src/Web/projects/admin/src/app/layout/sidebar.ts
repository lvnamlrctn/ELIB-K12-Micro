import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Session } from '../core/session';
import { LayoutService } from './layout.service';
import { Menu, MenuItem } from './menu';

/** Sidebar tối, kéo giãn được, 3 cấp — giao diện như components/sidebar của frontend monolith. */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  host: { class: 'block z-30 h-full relative' },
  styles: `
    aside { width: var(--sbw, 260px); }
    @media (min-width: 768px) {
      aside.md-collapsed { margin-left: calc(var(--sbw, 260px) * -1); }
    }
    .resize-handle { position: absolute; top: 0; right: -2px; width: 4px; height: 100%; cursor: col-resize; z-index: 10; }
    .resize-handle:hover, .resize-handle.resizing { background: rgba(59, 130, 246, 0.5); }
  `,
  template: `
    @if (layout.mobileMenuOpen()) {
      <div class="fixed inset-0 bg-black/50 z-40 md:hidden" (click)="layout.mobileMenuOpen.set(false)"></div>
    }

    <aside
      class="fixed inset-y-0 left-0 bg-[#1a1d20] z-50 transition-transform duration-300 ease-in-out md:static md:translate-x-0 flex flex-col h-full"
      [class.-translate-x-full]="!layout.mobileMenuOpen()"
      [class.md-collapsed]="!layout.desktopMenuOpen()"
      [style.--sbw]="layout.sidebarWidth() + 'px'">
      <div class="resize-handle hidden md:block" [class.resizing]="resizing()" (mousedown)="startResize($event)"></div>

      <div class="p-4 border-b border-gray-700 flex items-center justify-between shrink-0">
        <div class="flex items-center text-blue-400 font-bold text-lg min-w-0">
          @if (logo(); as url) {
            <img [src]="url" alt="" class="w-8 h-8 object-contain rounded bg-white/90 p-0.5 mr-2 shrink-0" />
          } @else {
            <span class="material-icons mr-2">layers</span>
          }
          <span class="truncate">{{ session.features()?.logoText || 'ELIB-K12' }}</span>
        </div>
        <button class="md:hidden text-white hover:text-gray-300" (click)="layout.mobileMenuOpen.set(false)">
          <span class="material-icons">close</span>
        </button>
      </div>
      @if (unitName(); as name) {
        <div class="px-4 py-2.5 border-b border-gray-700/60 text-xs text-gray-400 truncate" [title]="name">{{ name }}</div>
      }

      <nav class="flex-1 overflow-y-auto py-4 px-3 custom-scrollbar">
        <ul class="space-y-1.5">
          @for (item of menu.items(); track item.title) {
            <li>
              @if (!item.children) {
                <a [routerLink]="item.link" routerLinkActive="bg-blue-600 text-white" [routerLinkActiveOptions]="{ exact: item.link === '/' }"
                   class="flex items-center px-3 py-2.5 text-gray-400 hover:text-white hover:bg-white/5 rounded-lg transition-colors group">
                  <span class="material-icons text-[20px] mr-3 group-hover:text-white transition-colors">{{ item.icon }}</span>
                  <span class="font-medium text-sm">{{ item.title }}</span>
                </a>
              } @else {
                <button (click)="toggle(item)"
                        class="w-full flex items-center justify-between px-3 py-2.5 text-gray-400 hover:text-white hover:bg-white/5 rounded-lg transition-colors">
                  <div class="flex items-center">
                    <span class="material-icons text-[20px] mr-3">{{ item.icon }}</span>
                    <span class="font-medium text-sm text-left">{{ item.title }}</span>
                  </div>
                  <span class="material-icons text-[18px] transition-transform duration-300" [class.rotate-180]="isOpen(item)">expand_more</span>
                </button>

                @if (isOpen(item)) {
                  <ul class="mt-1 space-y-1 pl-9 border-l border-gray-700/50 ml-[21px] pb-2">
                    @for (child of item.children; track child.title) {
                      <li>
                        @if (!child.children) {
                          <a [routerLink]="child.link" routerLinkActive="!text-white font-medium bg-white/10"
                             class="relative flex items-center py-2 pl-3 pr-2 text-sm text-gray-400 hover:text-white hover:bg-white/5 rounded-lg transition-colors">
                            @if (child.icon) { <span class="material-icons text-[18px] mr-2">{{ child.icon }}</span> }
                            {{ child.title }}
                          </a>
                        } @else {
                          <button (click)="toggle(child)"
                                  class="w-full flex items-center justify-between py-2 pl-3 pr-2 text-sm text-gray-400 hover:text-white hover:bg-white/5 rounded-lg transition-colors">
                            <div class="flex items-center">
                              @if (child.icon) { <span class="material-icons text-[18px] mr-2">{{ child.icon }}</span> }
                              <span class="whitespace-nowrap">{{ child.title }}</span>
                            </div>
                            <span class="material-icons text-[16px] transition-transform duration-300" [class.rotate-180]="isOpen(child)">expand_more</span>
                          </button>
                          @if (isOpen(child)) {
                            <ul class="pl-3 mt-1 space-y-1">
                              @for (sub of child.children; track sub.title) {
                                <li>
                                  <a [routerLink]="sub.link" routerLinkActive="!text-white bg-white/10"
                                     class="flex items-center py-1.5 px-3 text-xs text-gray-400 hover:text-white hover:bg-white/5 rounded-md transition-colors">
                                    @if (sub.icon) { <span class="material-icons text-[16px] mr-2">{{ sub.icon }}</span> }
                                    {{ sub.title }}
                                  </a>
                                </li>
                              }
                            </ul>
                          }
                        }
                      </li>
                    }
                  </ul>
                }
              }
            </li>
          }
        </ul>
      </nav>
    </aside>
  `,
})
export class Sidebar {
  protected readonly layout = inject(LayoutService);
  protected readonly menu = inject(Menu);
  protected readonly session = inject(Session);
  private readonly router = inject(Router);

  protected readonly resizing = signal(false);
  /** Nhóm đang mở (theo tiêu đề). null = chưa bấm lần nào: tự mở nhóm chứa trang hiện tại. */
  private readonly opened = signal<Set<string> | null>(null);

  protected logo(): string | null {
    return this.session.features()?.logoUrl ?? null;
  }

  protected unitName(): string | null {
    return this.session.isSystem() ? 'Quản trị nền tảng' : (this.session.features()?.name ?? null);
  }

  protected isOpen(item: MenuItem): boolean {
    const opened = this.opened();
    return opened ? opened.has(item.title) : this.containsActive(item);
  }

  protected toggle(item: MenuItem): void {
    const current = this.opened() ?? new Set(this.allGroups(this.menu.items()).filter((g) => this.containsActive(g)).map((g) => g.title));
    const next = new Set(current);
    if (next.has(item.title)) next.delete(item.title);
    else next.add(item.title);
    this.opened.set(next);
  }

  protected startResize(event: MouseEvent): void {
    event.preventDefault();
    this.resizing.set(true);
    document.body.style.userSelect = 'none';
    const move = (e: MouseEvent) => this.layout.setSidebarWidth(e.clientX);
    const end = () => {
      this.resizing.set(false);
      document.body.style.userSelect = '';
      document.removeEventListener('mousemove', move);
      document.removeEventListener('mouseup', end);
    };
    document.addEventListener('mousemove', move);
    document.addEventListener('mouseup', end);
  }

  private containsActive(item: MenuItem): boolean {
    const url = this.router.url;
    return (item.children ?? []).some((c) => (c.link && c.link !== '/' && url.startsWith(c.link)) || this.containsActive(c));
  }

  private allGroups(items: MenuItem[]): MenuItem[] {
    return items.flatMap((i) => (i.children ? [i, ...this.allGroups(i.children)] : []));
  }
}
