import { Component, Injectable, inject, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

/** message: khoá dịch (kèm params) hoặc câu đã dịch sẵn — pipe translate trả lại nguyên câu không phải khoá. */
export interface Toast {
  id: number;
  type: ToastType;
  message: string;
  params?: Record<string, unknown>;
}

/** Thông báo góc phải trên — như ToastrService/app-toastr của frontend monolith. */
@Injectable({ providedIn: 'root' })
export class ToastrService {
  readonly toasts = signal<Toast[]>([]);
  private nextId = 1;

  success(message: string, params?: Record<string, unknown>): void {
    this.show('success', message, params);
  }

  error(message: string, params?: Record<string, unknown>): void {
    this.show('error', message, params, 6000);
  }

  info(message: string, params?: Record<string, unknown>): void {
    this.show('info', message, params);
  }

  warning(message: string, params?: Record<string, unknown>): void {
    this.show('warning', message, params);
  }

  remove(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private show(type: ToastType, message: string, params: Record<string, unknown> | undefined, timeout = 3500): void {
    const id = this.nextId++;
    this.toasts.update((list) => [...list, { id, type, message, params }]);
    setTimeout(() => this.remove(id), timeout);
  }
}

@Component({
  selector: 'app-toastr',
  imports: [NgClass, TranslatePipe],
  template: `
    <div class="fixed top-4 right-4 z-[9999] flex flex-col gap-2 pointer-events-none">
      @for (toast of toastr.toasts(); track toast.id) {
        <div
          class="pointer-events-auto min-w-[300px] max-w-md p-4 rounded-lg shadow-lg border relative flex items-start gap-3 backdrop-blur-md"
          [ngClass]="{
            'bg-emerald-50/90 text-emerald-800 border-emerald-200': toast.type === 'success',
            'bg-red-50/90 text-red-800 border-red-200': toast.type === 'error',
            'bg-blue-50/90 text-blue-800 border-blue-200': toast.type === 'info',
            'bg-amber-50/90 text-amber-800 border-amber-200': toast.type === 'warning',
          }">
          <div class="shrink-0 mt-0.5">
            @switch (toast.type) {
              @case ('success') { <span class="material-icons text-emerald-500">check_circle</span> }
              @case ('error') { <span class="material-icons text-red-500">error</span> }
              @case ('info') { <span class="material-icons text-blue-500">info</span> }
              @case ('warning') { <span class="material-icons text-amber-500">warning</span> }
            }
          </div>
          <div class="flex-1 text-sm">
            <p class="font-medium">{{ toast.message | translate: toast.params }}</p>
          </div>
          <button (click)="toastr.remove(toast.id)" class="shrink-0 opacity-50 hover:opacity-100 transition-opacity">
            <span class="material-icons text-[18px]">close</span>
          </button>
        </div>
      }
    </div>
  `,
})
export class ToastrComponent {
  protected readonly toastr = inject(ToastrService);
}
