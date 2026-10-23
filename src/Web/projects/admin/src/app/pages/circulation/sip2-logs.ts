import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage, Sip2Log, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { DateInput } from '../../shared/date-input';

/** Lệnh SIP2 hay gặp — hiện tên thay mã. */
const COMMANDS = ['93', '99', '63', '23', '17', '11', '09', '29', '65', '35', '37'] as const;

/** Nhật ký giao dịch của trạm tự phục vụ SIP2 (monolith: Sip2Log, quyền SIP2LOG) — mật khẩu trạm/bạn đọc đã che (CO***, AD***). */
@Component({
  selector: 'app-sip2-logs',
  imports: [DateInput, FormsModule, LDatePipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'SIP2.LOG_TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-3 xl:grid-cols-6 gap-4">
        <div class="xl:col-span-2">
          <label for="s2-kw" class="field-label">{{ 'SIP2.LOG_SEARCH' | translate }}</label>
          <input id="s2-kw" class="input" [(ngModel)]="keyword" (keydown.enter)="search()" [placeholder]="'SIP2.LOG_SEARCH_PH' | translate" />
        </div>
        <div>
          <label for="s2-cmd" class="field-label">{{ 'SIP2.COMMAND' | translate }}</label>
          <select id="s2-cmd" class="input" [(ngModel)]="command" (change)="search()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (c of commands; track c) { <option [ngValue]="c">{{ c }} — {{ ('SIP2.CMD_' + c) | translate }}</option> }
          </select>
        </div>
        <div>
          <label for="s2-ok" class="field-label">{{ 'SIP2.RESULT' | translate }}</label>
          <select id="s2-ok" class="input" [(ngModel)]="failed" (change)="search()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option [ngValue]="false">{{ 'SIP2.OK' | translate }}</option>
            <option [ngValue]="true">{{ 'SIP2.FAILED' | translate }}</option>
          </select>
        </div>
        <div>
          <label for="s2-from" class="field-label">{{ 'SIP2.FROM' | translate }}</label>
          <app-date-input fieldId="s2-from" [(ngModel)]="from" (ngModelChange)="search()"></app-date-input>
        </div>
        <div>
          <label for="s2-to" class="field-label">{{ 'SIP2.TO' | translate }}</label>
          <app-date-input fieldId="s2-to" [(ngModel)]="to" (ngModelChange)="search()"></app-date-input>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr>
              <th class="th w-40">{{ 'SIP2.TIME' | translate }}</th>
              <th class="th w-28">{{ 'SIP2.STATION' | translate }}</th>
              <th class="th w-40">{{ 'SIP2.COMMAND' | translate }}</th>
              <th class="th">{{ 'SIP2.MESSAGE' | translate }}</th>
              <th class="th w-24 !text-center">{{ 'SIP2.RESULT' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (row of page()?.items ?? []; track row.id) {
              <tr class="hover:bg-gray-50/50 align-top">
                <td class="td text-gray-500 whitespace-nowrap">{{ row.at | ldate: 'dd/MM/yyyy HH:mm:ss' }}
                  <div class="text-xs text-gray-400">{{ row.elapsedMs }} ms · {{ row.remoteIp ?? '—' }}</div></td>
                <td class="td font-mono text-[13px]">{{ row.stationCode ?? '—' }}</td>
                <td class="td"><span class="font-mono text-[13px]">{{ row.command }}</span> {{ ('SIP2.CMD_' + row.command) | translate }}</td>
                <td class="td">
                  <div class="font-mono text-xs break-all text-gray-700"><span class="text-gray-400">→</span> {{ row.request }}</div>
                  <div class="font-mono text-xs break-all text-gray-500 mt-1"><span class="text-gray-400">←</span> {{ row.response }}</div>
                </td>
                <td class="td text-center">
                  @if (row.ok) { <span class="badge-on">{{ 'SIP2.OK' | translate }}</span> } @else { <span class="badge-danger">{{ 'SIP2.FAILED' | translate }}</span> }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="py-8 px-4 text-center text-gray-500">{{ loading() ? '' : ('SIP2.LOG_EMPTY' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" />
      }
    </div>
  `,
})
export class Sip2Logs implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly commands = COMMANDS;
  protected keyword = '';
  protected command: string | null = null;
  protected failed: boolean | null = null;
  protected from = '';
  protected to = '';
  protected pageIndex = 1;
  protected pageSize = 20;
  protected readonly page = signal<CrudPage<Sip2Log> | null>(null);
  protected readonly loading = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  protected search(): void {
    this.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.api.sip2Logs({
        keyword: this.keyword.trim() || undefined, command: this.command, failed: this.failed, from: this.from || null, to: this.to || null,
        pageIndex: this.pageIndex, pageSize: this.pageSize,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
