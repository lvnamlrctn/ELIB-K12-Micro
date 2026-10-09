import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ToastrService } from '../../../../services/shared/toastr.service';
import { RoomBookingAdminService } from '../../../../services/map/room-booking.service';
import { RoomBookingSettings } from '../../../../models/map/room-booking';
import { CanDirective } from '../../../../directives/can.directive';

/** Tab "Tham số" của màn đặt phòng: giờ mở cửa mặc định, giới hạn lượt đặt, khoá đặt phòng khi vắng mặt. */
@Component({
  selector: 'app-room-booking-settings-tab',
  standalone: true,
  imports: [FormsModule, MatIconModule, TranslateModule, CanDirective],
  templateUrl: './room-booking-settings-tab.html'
})
export class RoomBookingSettingsTab implements OnInit {
  private svc = inject(RoomBookingAdminService);
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);

  model = signal<RoomBookingSettings | null>(null);
  loading = signal(false);
  saving = signal(false);

  ngOnInit(): void {
    this.loading.set(true);
    this.svc.getSettings().subscribe({
      next: s => { this.model.set({ ...s }); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  save(): void {
    const m = this.model();
    if (!m) return;
    this.saving.set(true);
    this.svc.saveSettings(m).subscribe({
      next: s => { this.model.set({ ...s }); this.saving.set(false); this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); },
      error: () => this.saving.set(false)
    });
  }
}
