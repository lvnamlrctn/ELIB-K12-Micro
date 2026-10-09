import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from '../../services/shared/toastr.service';
import { MapFloorUtilityService } from '../../services/map/map-floor-utility.service';
import { MapFloorUtility } from '../../models/map/map-floor-utility';

@Component({
  selector: 'app-map-floor-utility-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule],
  templateUrl: './map-floor-utility-modal.html'
})
export class MapFloorUtilityModalComponent implements OnChanges {
  private utilityService = inject(MapFloorUtilityService);
  private toastr         = inject(ToastrService);
  public  translate      = inject(TranslateService);

  @Input() open = false;
  @Input() floorId: number | null = null;
  @Input() floorName = '';
  @Output() closed = new EventEmitter<void>();

  utilitiesList = signal<MapFloorUtility[]>([]);
  newUtility = { name: '', description: '', quantity: 1, conditionStatus: 'HOAT_DONG', iconName: '' };

  ngOnChanges(): void {
    if (this.open && this.floorId) {
      this.newUtility = { name: '', description: '', quantity: 1, conditionStatus: 'HOAT_DONG', iconName: '' };
      this.loadUtilities();
    }
  }

  loadUtilities(): void {
    if (!this.floorId) return;
    this.utilityService.searchAllByFloor(this.floorId).subscribe(list => this.utilitiesList.set(list));
  }

  async addUtility(): Promise<void> {
    const floorId = this.floorId;
    if (!floorId || !this.newUtility.name.trim()) return;
    const payload: Partial<MapFloorUtility> = { floorId, ...this.newUtility };
    try {
      await firstValueFrom(this.utilityService.create(payload));
      this.newUtility = { name: '', description: '', quantity: 1, conditionStatus: 'HOAT_DONG', iconName: '' };
      this.loadUtilities();
    } catch { this.toastr.error(this.translate.instant('COMMON.ADD_ERROR')); }
  }

  async deleteUtility(item: MapFloorUtility): Promise<void> {
    if (!item.publicId) return;
    try {
      await firstValueFrom(this.utilityService.delete(item.publicId));
      this.loadUtilities();
    } catch { this.toastr.error(this.translate.instant('COMMON.DELETE_ERROR')); }
  }

  close(): void { this.closed.emit(); }
}
