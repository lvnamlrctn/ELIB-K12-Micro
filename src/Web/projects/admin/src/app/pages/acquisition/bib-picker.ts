import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { AcqBib, Api, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';

/**
 * Chọn biểu ghi cho dòng đơn đặt / phiếu nhận: gõ MFN, nhan đề, tác giả hoặc ISBN rồi chọn trong danh sách (bản sao biểu ghi của phân hệ
 * bổ sung — MFN chưa có thì hỏi catalog).
 */
@Component({
  selector: 'app-acq-bib-picker',
  imports: [FormsModule, TranslatePipe],
  template: `
    @if (selected(); as b) {
      <div class="flex items-start gap-2 rounded-lg border border-blue-200 bg-blue-50/60 px-3 py-2">
        <div class="flex-1 text-sm">
          <div class="font-medium text-gray-800">{{ b.title }}</div>
          <div class="text-[12px] text-gray-500">MFN {{ b.mfn }}@if (b.author) { · {{ b.author }} }@if (b.publishYear) { · {{ b.publishYear }} }</div>
        </div>
        <button type="button" class="icon-btn text-gray-400 hover:text-red-600" [title]="'ACQ.CLEAR_BIB' | translate" (click)="clear()">
          <span class="material-icons text-[18px]">close</span></button>
      </div>
    } @else {
      <div class="flex gap-2">
        <input [id]="inputId()" class="input" [(ngModel)]="keyword" [placeholder]="'ACQ.BIB_SEARCH_PH' | translate" (keydown.enter)="$event.preventDefault(); find()" />
        <button type="button" class="btn-secondary" [disabled]="busy()" (click)="find()"><span class="material-icons text-[18px]">search</span></button>
      </div>
      @if (results(); as list) {
        <div class="mt-1 max-h-60 overflow-y-auto rounded-lg border border-gray-200 bg-white">
          @for (b of list; track b.bibPublicId) {
            <button type="button" class="block w-full text-left px-3 py-2 text-sm hover:bg-gray-50 border-b border-gray-100 last:border-0" (click)="pick(b)">
              <span class="font-mono text-[12px] text-gray-500">MFN {{ b.mfn }}</span> {{ b.title }}
              @if (b.author) { <span class="text-[12px] text-gray-500">— {{ b.author }}</span> }
              @if (b.isbns) { <span class="text-[11px] text-gray-400"> · ISBN {{ b.isbns }}</span> }
            </button>
          } @empty {
            <div class="px-3 py-3 text-sm text-gray-500">{{ 'ACQ.BIB_NONE' | translate }}</div>
          }
        </div>
      }
    }
  `,
})
export class BibPicker {
  readonly inputId = input('acq-bib');
  readonly selected = input<AcqBib | null>(null);
  readonly picked = output<AcqBib | null>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected keyword = '';
  protected readonly results = signal<AcqBib[] | null>(null);
  protected readonly busy = signal(false);

  protected async find(): Promise<void> {
    const keyword = this.keyword.trim();
    if (!keyword) return;
    this.busy.set(true);
    try {
      this.results.set(await this.api.lookupAcqBibs(keyword));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected pick(b: AcqBib): void {
    this.results.set(null);
    this.keyword = '';
    this.picked.emit(b);
  }

  protected clear(): void {
    this.picked.emit(null);
  }
}
