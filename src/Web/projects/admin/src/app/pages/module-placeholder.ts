import { TranslatePipe } from '@ngx-translate/core';
import { Component, computed, inject, input } from '@angular/core';
import { MODULE_NAMES, Session } from '../core/session';

/** Chỗ cho các phân hệ GĐ1+ — hiện ra trong menu theo license nhưng chưa có màn hình. */
@Component({
  imports: [TranslatePipe],
  selector: 'app-module-placeholder',
  template: `
    <div class="mb-5"><h4 class="page-title">{{ name() | translate }}</h4></div>
    <div class="bg-white rounded-xl shadow-sm border border-gray-100 p-10 text-center">
      @if (licensed()) {
        <span class="material-icons text-5xl text-blue-300 mb-3">construction</span>
        <p class="text-gray-700 font-medium">{{ 'MODULE_PLACEHOLDER.BUILDING' | translate }}</p>
        <p class="text-sm text-gray-500 mt-1">{{ 'MODULE_PLACEHOLDER.COMING' | translate }}</p>
      } @else {
        <span class="material-icons text-5xl text-red-300 mb-3">lock</span>
        <p class="text-gray-700 font-medium">{{ 'MODULE_PLACEHOLDER.NOT_LICENSED' | translate }}</p>
      }
    </div>
  `,
})
export class ModulePlaceholder {
  private readonly session = inject(Session);
  readonly code = input.required<string>();
  protected readonly name = computed(() => MODULE_NAMES[this.code()] ?? this.code());
  protected readonly licensed = computed(() => this.session.features()?.modules.includes(this.code()) ?? false);
}
