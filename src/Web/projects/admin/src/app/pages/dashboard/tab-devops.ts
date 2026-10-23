import { Component, computed, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { PlatformHealth } from './dash-types';
import { Kpi, Panel, Source } from './dash-kit';

/**
 * Tab DevOps (chỉ host hệ thống): sức khoẻ từng service (gateway ping /ready) và độ sâu các hàng đợi lỗi RabbitMQ.
 * LRC còn hàng đợi Hangfire và cảnh báo bảo mật — K12 không có tương đương nên không vẽ.
 */
@Component({
  selector: 'app-tab-devops',
  imports: [Kpi, Panel, DecimalPipe, DatePipe, TranslatePipe],
  template: `
    <div class="grid grid-cols-1 sm:grid-cols-3 gap-5 mb-5">
      <app-kpi icon="dns" tint="bg-blue-50 text-blue-600" label="DASH.OPS.SERVICES" [value]="d()?.services?.length" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="error" tint="bg-red-50 text-red-600" label="DASH.OPS.DOWN" [value]="down()" [danger]="(down() ?? 0) > 0" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="mark_email_unread" tint="bg-amber-50 text-amber-600" label="DASH.OPS.ERROR_MESSAGES" sub="DASH.OPS.ERROR_MESSAGES_SUB" [value]="errorMessages()" [danger]="(errorMessages() ?? 0) > 0" [loading]="src.loading()" [error]="src.error()" />
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-5">
      <app-panel title="DASH.OPS.SERVICE_STATUS" icon="monitor_heart" iconColor="text-emerald-600" [loading]="src.loading()" [error]="src.error()" [empty]="!d()?.services?.length" [height]="160" (retry)="src.load()">
        <table class="w-full text-left text-sm">
          <thead><tr class="text-[11px] uppercase tracking-wider text-slate-400"><th class="py-2 pr-3">{{ 'DASH.OPS.SERVICE' | translate }}</th><th class="py-2 pr-3">{{ 'COMMON.STATUS' | translate }}</th><th class="py-2 text-right">{{ 'DASH.OPS.LATENCY' | translate }}</th></tr></thead>
          <tbody>
            @for (s of d()!.services; track s.name) {
              <tr class="border-t border-slate-100">
                <td class="py-2 pr-3 font-semibold text-slate-700">{{ s.name }}</td>
                <td class="py-2 pr-3"><span class="inline-flex items-center gap-1.5 text-xs font-bold" [class]="s.status === 'Healthy' ? 'text-emerald-600' : 'text-red-600'">
                  <span class="w-2 h-2 rounded-full" [class]="s.status === 'Healthy' ? 'bg-emerald-500' : 'bg-red-500'"></span>{{ statusKey[s.status] | translate }}</span>
                  @if (s.detail) { <span class="text-xs text-slate-400 ml-2">{{ s.detail }}</span> }</td>
                <td class="py-2 text-right text-slate-500">@if (s.latencyMs !== null) { {{ s.latencyMs | number }} ms }</td>
              </tr>
            }
          </tbody>
        </table>
        @if (d()?.checkedAt) { <p class="text-xs text-slate-400 mt-3">{{ 'DASH.OPS.CHECKED_AT' | translate }}: {{ d()!.checkedAt | date: 'dd/MM/yyyy HH:mm:ss' }}</p> }
      </app-panel>

      <app-panel title="DASH.OPS.QUEUES" sub="DASH.OPS.QUEUES_SUB" icon="forward_to_inbox" iconColor="text-amber-500" [loading]="src.loading()" [error]="src.error()" [empty]="false" [height]="160" (retry)="src.load()">
        @if (d()?.queues; as queues) {
          <table class="w-full text-left text-sm">
            <thead><tr class="text-[11px] uppercase tracking-wider text-slate-400"><th class="py-2 pr-3">{{ 'DASH.OPS.QUEUE' | translate }}</th><th class="py-2 text-right">{{ 'DASH.OPS.MESSAGES' | translate }}</th></tr></thead>
            <tbody>
              @for (q of queues; track q.name) {
                <tr class="border-t border-slate-100"><td class="py-2 pr-3 font-mono text-xs text-slate-700">{{ q.name }}</td><td class="py-2 text-right font-bold" [class.text-red-600]="q.messages > 0">{{ q.messages | number }}</td></tr>
              } @empty {
                <tr><td colspan="2" class="py-6 text-center text-xs text-slate-400">{{ 'DASH.OPS.NO_QUEUES' | translate }}</td></tr>
              }
            </tbody>
          </table>
        } @else {
          <p class="text-xs text-slate-400 py-6 text-center">{{ 'DASH.OPS.QUEUES_UNAVAILABLE' | translate }}@if (d()?.queuesDetail) { <br />{{ d()!.queuesDetail }} }</p>
        }
      </app-panel>
    </div>
  `,
})
export class TabDevops {
  private readonly http = inject(HttpClient);
  protected readonly src = new Source<PlatformHealth>(() => firstValueFrom(this.http.get<PlatformHealth>('/api/system/health')));
  protected readonly d = this.src.data;
  protected readonly statusKey: Record<string, string> = { Healthy: 'DASH.OPS.HEALTHY', Unhealthy: 'DASH.OPS.UNHEALTHY', Unreachable: 'DASH.OPS.UNREACHABLE' };
  protected readonly down = computed(() => this.d()?.services.filter((s) => s.status !== 'Healthy').length ?? null);
  protected readonly errorMessages = computed(() => { const q = this.d()?.queues; return q ? q.reduce((n, x) => n + x.messages, 0) : null; });
}
