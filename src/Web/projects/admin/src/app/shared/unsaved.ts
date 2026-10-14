import { Component, input, signal } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Modal } from './ui';

/** Màn có form dài (biên mục...) — rời trang khi còn thay đổi chưa lưu thì hỏi lại (ELIB-LRC: cảnh báo chưa lưu). */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
  /** Hỏi người dùng có bỏ thay đổi không — thường là <code>LeaveConfirm.ask()</code>. */
  confirmLeave(): Promise<boolean>;
}

export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) =>
  !component.hasUnsavedChanges() || component.confirmLeave();

/** Trạng thái hộp thoại "Chưa lưu thay đổi" của một màn — <code>ask()</code> chờ người dùng chọn ở lại / rời trang. */
export class LeaveConfirm {
  private resolve: ((leave: boolean) => void) | null = null;
  readonly open = signal(false);

  ask(): Promise<boolean> {
    this.resolve?.(false);
    this.open.set(true);
    return new Promise<boolean>((resolve) => (this.resolve = resolve));
  }

  answer(leave: boolean): void {
    this.open.set(false);
    this.resolve?.(leave);
    this.resolve = null;
  }
}

/** Hộp thoại "Chưa lưu thay đổi": <code>&lt;app-unsaved-confirm [state]="leave" /&gt;</code>. */
@Component({
  selector: 'app-unsaved-confirm',
  imports: [Modal, TranslatePipe],
  template: `
    @if (state().open()) {
      <app-modal [title]="'COMMON.UNSAVED_TITLE' | translate" widthClass="max-w-md" (closed)="state().answer(false)">
        <p class="text-sm text-gray-700">{{ 'COMMON.UNSAVED_MESSAGE' | translate }}</p>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="state().answer(false)">{{ 'COMMON.UNSAVED_STAY' | translate }}</button>
          <button type="button" class="btn-danger" (click)="state().answer(true)">{{ 'COMMON.UNSAVED_LEAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class UnsavedConfirm {
  readonly state = input.required<LeaveConfirm>();
}
