import { Injectable } from '@angular/core';

export interface ListPageState { pageIndex: number; pageSize: number; }

/**
 * Nhớ trang phân trang của từng danh sách trong phiên làm việc (port ELIB-LRC 09-24).
 * Danh sách điều hướng sang route chi tiết riêng bị Angular huỷ component khi rời đi → quay lại luôn về trang 1.
 * Mỗi trang danh sách gọi remember() mỗi lần tải dữ liệu và recall() trong ngOnInit trước lần tải đầu.
 * Chỉ giữ trong bộ nhớ (không localStorage) nên tự xoá khi tải lại trình duyệt/đăng xuất/đổi tài khoản
 * — không lẫn trang giữa các đơn vị.
 */
@Injectable({ providedIn: 'root' })
export class ListPageStateService {
  private readonly states = new Map<string, ListPageState>();

  remember(key: string, pageIndex: number, pageSize: number): void {
    this.states.set(key, { pageIndex, pageSize });
  }

  recall(key: string): ListPageState | undefined {
    return this.states.get(key);
  }

  clear(): void {
    this.states.clear();
  }
}
