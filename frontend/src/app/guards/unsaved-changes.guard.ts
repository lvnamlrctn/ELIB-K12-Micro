import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { ToastrService } from '../services/shared/toastr.service';

/**
 * Cảnh báo "có thay đổi chưa lưu" (Đợt 21 — port từ ELIB-LRC). Khác LRC: chuyển route cũng dùng hộp thoại trong
 * app (CanDeactivate chấp nhận Promise) thay cho window.confirm; chỉ reload/đóng tab còn dùng hộp thoại của trình
 * duyệt vì `beforeunload` không cho tuỳ biến giao diện.
 */
export interface UnsavedChangesPage { canLeave(): boolean | Promise<boolean>; }
export const unsavedChangesGuard: CanDeactivateFn<UnsavedChangesPage> = page => page?.canLeave ? page.canLeave() : true;

/** So snapshot JSON của giá trị đang sửa với bản đã tải/lưu — sửa rồi khôi phục lại không bị coi là thay đổi. */
export class UnsavedChanges {
  private baseline = '';
  capture(value: unknown): void { this.baseline = JSON.stringify(value); }
  /** Cập nhật baseline của 1 trường (snapshot là object) — dùng khi trình soạn thảo tự chuẩn hoá HTML lúc sẵn sàng,
   *  để việc chuẩn hoá không bị coi là người dùng sửa, mà không xoá dấu thay đổi của các trường khác. */
  normalizeField(key: string, value: unknown): void {
    this.baseline = JSON.stringify({ ...JSON.parse(this.baseline || '{}'), [key]: value });
  }
  changed(value: unknown): boolean { return JSON.stringify(value) !== this.baseline; }
}

/** Hộp thoại dùng chung (UnsavedChangesDialogComponent gắn 1 lần ở app.html). */
@Injectable({ providedIn: 'root' })
export class UnsavedChangesDialogService {
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);
  visible = signal(false);
  private resolveFn?: (value: boolean) => void;

  confirmLeave(dirty: () => boolean, busy: () => boolean): Promise<boolean> {
    if (busy()) {
      this.toastr.warning(this.translate.instant('UNSAVED.BUSY'));
      return Promise.resolve(false);
    }
    if (!dirty()) return Promise.resolve(true);
    // Đã có hộp thoại đang mở (vd bấm đóng 2 lần) — hộp thoại cũ coi như "Ở lại".
    this.resolveFn?.(false);
    this.visible.set(true);
    return new Promise<boolean>(resolve => { this.resolveFn = resolve; });
  }

  respond(leave: boolean): void {
    this.visible.set(false);
    this.resolveFn?.(leave);
    this.resolveFn = undefined;
  }
}

/**
 * Gắn cảnh báo cho 1 trang: đăng ký `beforeunload` (reload/đóng tab) trong vòng đời component và trả về hàm hỏi
 * xác nhận dùng cho cả CanDeactivate lẫn đóng modal/mở form khác. Phải gọi trong injection context (khởi tạo field).
 */
export function watchUnsavedChanges(dirty: () => boolean, busy: () => boolean): () => Promise<boolean> {
  const destroy = inject(DestroyRef);
  const dialog = inject(UnsavedChangesDialogService);
  if (typeof window !== 'undefined') {
    const unload = (event: BeforeUnloadEvent) => {
      if (dirty() || busy()) { event.preventDefault(); event.returnValue = ''; }
    };
    window.addEventListener('beforeunload', unload);
    destroy.onDestroy(() => window.removeEventListener('beforeunload', unload));
  }
  return () => dialog.confirmLeave(dirty, busy);
}
