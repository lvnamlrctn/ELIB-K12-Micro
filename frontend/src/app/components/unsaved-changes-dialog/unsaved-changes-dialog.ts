import { Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { UnsavedChangesDialogService } from '../../guards/unsaved-changes.guard';

/** Hộp thoại "có thay đổi chưa lưu" chung cho toàn app, gắn 1 lần ở app.html (Đợt 21, port từ ELIB-LRC). */
@Component({
  selector: 'app-unsaved-changes-dialog',
  standalone: true,
  imports: [MatIconModule, TranslateModule],
  template: `
    @if (dialog.visible()) {
      <div class="fixed inset-0 z-[200] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4"
           role="alertdialog" aria-modal="true" aria-labelledby="unsaved-title">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-sm overflow-hidden">
          <div class="p-6 text-center">
            <div class="w-16 h-16 bg-amber-50 text-amber-600 rounded-full flex items-center justify-center mx-auto mb-4">
              <mat-icon class="text-3xl">warning</mat-icon>
            </div>
            <h3 id="unsaved-title" class="text-lg font-bold text-gray-800 mb-2">{{ 'UNSAVED.TITLE' | translate }}</h3>
            <p class="text-gray-500 text-sm mb-6">{{ 'UNSAVED.MESSAGE' | translate }}</p>
            <div class="flex gap-3">
              <button type="button" (click)="dialog.respond(false)"
                class="flex-1 py-2 px-4 border border-gray-300 text-gray-700 font-medium rounded-lg hover:bg-gray-50 transition-colors text-sm">
                {{ 'UNSAVED.STAY' | translate }}
              </button>
              <button type="button" (click)="dialog.respond(true)"
                class="flex-1 py-2 px-4 bg-amber-600 hover:bg-amber-700 text-white font-medium rounded-lg transition-colors text-sm shadow-sm">
                {{ 'UNSAVED.LEAVE' | translate }}
              </button>
            </div>
          </div>
        </div>
      </div>
    }
  `
})
export class UnsavedChangesDialogComponent {
  dialog = inject(UnsavedChangesDialogService);
}
