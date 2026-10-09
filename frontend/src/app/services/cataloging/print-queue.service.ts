import { Injectable, signal, computed } from '@angular/core';
import { Bib } from '../../models/cataloging/bib';

/** Hàng đợi in phích — cộng dồn biểu ghi qua nhiều lần tìm kiếm trên bib-list, tồn tại trong bộ nhớ phiên làm việc. */
@Injectable({ providedIn: 'root' })
export class PrintQueueService {
  queue = signal<Bib[]>([]);
  count = computed(() => this.queue().length);

  add(items: Bib[]): void {
    this.queue.update(list => {
      const existingIds = new Set(list.map(b => b.bibId));
      const toAdd = items.filter(b => !existingIds.has(b.bibId));
      return [...list, ...toAdd];
    });
  }

  remove(bibId: number): void {
    this.queue.update(list => list.filter(b => b.bibId !== bibId));
  }

  clear(): void {
    this.queue.set([]);
  }
}
