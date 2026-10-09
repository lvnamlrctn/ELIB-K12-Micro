import { Component, inject } from '@angular/core';
import { ToastrService } from '../../services/shared/toastr.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-toastr',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="fixed top-4 right-4 z-[9999] flex flex-col gap-2 pointer-events-none">
      @for (toast of toastr.toasts(); track toast.id) {
        <div class="pointer-events-auto min-w-[300px] max-w-md p-4 rounded-lg shadow-lg border relative flex items-start gap-3 backdrop-blur-md animate-in slide-in-from-right-8 fade-in duration-300"
             [ngClass]="{
               'bg-emerald-50/90 text-emerald-800 border-emerald-200': toast.type === 'success',
               'bg-red-50/90 text-red-800 border-red-200': toast.type === 'error',
               'bg-blue-50/90 text-blue-800 border-blue-200': toast.type === 'info',
               'bg-amber-50/90 text-amber-800 border-amber-200': toast.type === 'warning'
             }">
          
          <div class="shrink-0 mt-0.5">
            @if (toast.type === 'success') {
              <span class="material-icons text-emerald-500">check_circle</span>
            }
            @if (toast.type === 'error') {
              <span class="material-icons text-red-500">error</span>
            }
            @if (toast.type === 'info') {
              <span class="material-icons text-blue-500">info</span>
            }
            @if (toast.type === 'warning') {
              <span class="material-icons text-amber-500">warning</span>
            }
          </div>

          <div class="flex-1 text-sm">
            @if (toast.title) {
              <h4 class="font-bold mb-1">{{ toast.title }}</h4>
            }
            <p [class.font-medium]="!toast.title">{{ toast.message }}</p>
          </div>

          <button (click)="toastr.remove(toast.id)" class="shrink-0 opacity-50 hover:opacity-100 transition-opacity">
            <span class="material-icons text-[18px]">close</span>
          </button>
        </div>
      }
    </div>
  `
})
export class ToastrComponent {
  toastr = inject(ToastrService);
}
