import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Session } from '../../core/session';
import { Home } from '../home';
import { TabAcquisition } from './tab-acquisition';
import { TabCataloging } from './tab-cataloging';
import { TabCirculation } from './tab-circulation';
import { TabDevops } from './tab-devops';
import { TabDigital } from './tab-digital';
import { TabOverview } from './tab-overview';
import { TabSpace } from './tab-space';

type TabId = 'overview' | 'circulation' | 'cataloging' | 'acquisition' | 'digital' | 'space' | 'devops';

interface TabDef { id: TabId; label: string; module?: string; perm?: string; }

/** Thứ tự và điều kiện hiện của các tab (quyền xem của chức năng nguồn + phân hệ đã được cấp license). Gateway/service vẫn tự chặn. */
const TENANT_TABS: TabDef[] = [
  { id: 'overview', label: 'DASH.TAB.OVERVIEW' },
  { id: 'circulation', label: 'DASH.TAB.CIRCULATION', module: 'CIRCULATION', perm: 'CIRC_REPORT:view' },
  { id: 'cataloging', label: 'DASH.TAB.CATALOGING', module: 'CATALOG', perm: 'CATALOG_BIBS:view' },
  { id: 'acquisition', label: 'DASH.TAB.ACQUISITION', module: 'ACQUISITION', perm: 'AB_RECEIPTS:view' },
  { id: 'digital', label: 'DASH.TAB.DIGITAL', module: 'DIGITAL', perm: 'DIGITAL_ITEMS:view' },
  { id: 'space', label: 'DASH.TAB.SPACE', module: 'SPACE', perm: 'SPACE_REPORT:view' },
];

/**
 * Bảng điều khiển (giống ELIB-LRC): mỗi tab một nhóm nghiệp vụ, nạp lười khi mở, mỗi nguồn số liệu có trạng thái riêng.
 * Host hệ thống: tổng quan nền tảng (danh sách đơn vị) + tab DevOps (sức khoẻ các service); host đơn vị: các tab theo quyền.
 */
@Component({
  selector: 'app-dashboard',
  imports: [Home, TabAcquisition, TabCataloging, TabCirculation, TabDevops, TabDigital, TabOverview, TabSpace, RouterLink, TranslatePipe],
  template: `
    <div class="mb-5 flex items-start justify-between gap-4">
      @if (!session.isSystem()) { <h4 class="page-title">{{ 'PAGE_TITLE.HOME' | translate }}</h4> } @else { <span></span> }
      @if (!session.isSystem() && session.can('SEARCH_STATS:view')) {
        <a routerLink="/search-stats" class="text-sm font-semibold text-indigo-700 underline">{{ 'DASH.SEARCH_QUALITY' | translate }}</a>
      }
    </div>

    <nav [attr.aria-label]="'DASH.TABS' | translate" class="flex flex-wrap gap-1 border-b border-gray-200 mb-5">
      @for (t of tabs(); track t.id) {
        <button type="button" (click)="select(t.id)" [attr.aria-pressed]="tab() === t.id"
          [class]="tab() === t.id ? 'border-blue-600 text-blue-700' : 'border-transparent text-gray-400 hover:text-gray-600'"
          class="px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors">{{ t.label | translate }}</button>
      }
    </nav>

    @switch (tab()) {
      @case ('overview') { @if (session.isSystem()) { <app-home /> } @else { <app-tab-overview /> } }
      @case ('circulation') { <app-tab-circulation /> }
      @case ('cataloging') { <app-tab-cataloging /> }
      @case ('acquisition') { <app-tab-acquisition /> }
      @case ('digital') { <app-tab-digital /> }
      @case ('space') { <app-tab-space /> }
      @case ('devops') { <app-tab-devops /> }
    }
  `,
})
export class Dashboard {
  protected readonly session = inject(Session);
  private readonly chosen = signal<TabId | null>(null);

  protected readonly tabs = computed<TabDef[]>(() => {
    if (this.session.isSystem()) return [{ id: 'overview', label: 'DASH.TAB.PLATFORM' }, { id: 'devops', label: 'DASH.TAB.DEVOPS' }];
    const modules = this.session.features()?.modules ?? [];
    return TENANT_TABS.filter((t) => (!t.module || modules.includes(t.module)) && (!t.perm || this.session.can(t.perm)));
  });

  protected readonly tab = computed<TabId>(() => {
    const tabs = this.tabs();
    const wanted = this.chosen();
    return tabs.find((t) => t.id === wanted)?.id ?? tabs[0]?.id ?? 'overview';
  });

  protected select(id: TabId): void { this.chosen.set(id); }
}
