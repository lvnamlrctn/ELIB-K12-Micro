import { Component, EventEmitter, Input, Output, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { Auth } from '../../../services/auth';
import { TenantOptionsService, TenantOption } from '../../../services/shared/tenant-options.service';

/** Đợt 24 — ô lọc "Đơn vị" dùng chung cho các trang admin. Tự ẩn hoàn toàn nếu tài khoản hiện tại
 * không đặc quyền (Auth.isPrivileged() === false), tự tải danh sách đơn vị qua TenantOptionsService
 * (cache sẵn, không gọi lại API mỗi trang). Trang cha chỉ cần:
 *   <app-tenant-filter-select [(tenantId)]="tenantId" (tenantIdChange)="onTenantChange()" />
 * thay cho toàn bộ khối "isPrivileged/tenantOptions/loadTenants" từng phải tự chép ở mỗi trang
 * (xem menu-type.ts/.html — bản viết tay đầy đủ nhất trước Đợt 24). */
@Component({
  selector: 'app-tenant-filter-select',
  standalone: true,
  imports: [CommonModule, FormsModule, NgSelectModule, TranslateModule],
  templateUrl: './tenant-filter-select.html',
  // display:contents — không tạo thêm thẻ bao ngoài, để <ng-select> bên trong tự làm 1 ô lưới CSS Grid
  // của trang cha (mỗi trang có số cột grid khác nhau, component không nên can thiệp vào layout đó).
  host: { style: 'display: contents' }
})
export class TenantFilterSelectComponent implements OnInit, OnDestroy {
  private auth = inject(Auth);
  private tenantOptionsService = inject(TenantOptionsService);
  private destroy$ = new Subject<void>();

  @Input() tenantId: string | null = null;
  @Output() tenantIdChange = new EventEmitter<string | null>();

  isPrivileged = this.auth.isPrivileged();
  tenantOptions: TenantOption[] = [];

  ngOnInit(): void {
    if (!this.isPrivileged) return;
    this.tenantOptionsService.getAll().pipe(takeUntil(this.destroy$)).subscribe(opts => this.tenantOptions = opts);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onChange(): void {
    this.tenantIdChange.emit(this.tenantId);
  }
}
