import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { CanDirective } from '../../../directives/can.directive';
import { ToastrService } from '../../../services/shared/toastr.service';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Z3950ServerService, ZebraExportStatus } from '../../../services/cataloging/z3950-server.service';

/**
 * Trang theo dõi server Z39.50 (Zebra). Chỉ hiển thị + nút xuất lại dữ liệu; KHÔNG cấu hình được Zebra
 * từ đây (host/cổng/tên database do biến môi trường của container quyết định — xem docs/Z3950-SERVER.md).
 */
@Component({
  selector: 'app-z3950-server',
  standalone: true,
  imports: [CanDirective, CommonModule, TranslateModule, MatIconModule, AppDatePipe],
  templateUrl: './z3950-server.html'
})
export class Z3950ServerPage implements OnInit, OnDestroy {
  private service   = inject(Z3950ServerService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  status      = signal<ZebraExportStatus | null>(null);
  isLoading   = signal(false);
  isRebuilding = signal(false);

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  load(): void {
    this.isLoading.set(true);
    this.service.getStatus().pipe(takeUntil(this.destroy$)).subscribe({
      next: s => { this.status.set(s); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); }
    });
  }

  rebuild(): void {
    this.isRebuilding.set(true);
    this.service.rebuild().pipe(takeUntil(this.destroy$)).subscribe({
      next: jobId => {
        this.isRebuilding.set(false);
        if (jobId == null) { this.toastr.error(this.translate.instant('COMMON.SAVE_ERROR')); return; }
        this.toastr.success(this.translate.instant('Z3950_SERVER.REBUILD_QUEUED'));
      },
      error: () => { this.isRebuilding.set(false); }
    });
  }

  /** Chuỗi kết nối mẫu để đưa cho thư viện bạn, VD "203.0.113.5:2100/ELIB_PDP". */
  connectionString(database: string | null): string {
    const s = this.status();
    if (!s || !database) return '—';
    return `${s.host}:${s.port}/${database}`;
  }
}
