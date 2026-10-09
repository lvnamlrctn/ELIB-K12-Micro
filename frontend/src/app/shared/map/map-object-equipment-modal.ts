import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from '../../services/shared/toastr.service';
import { MapEquipmentService } from '../../services/map/map-equipment.service';
import { MapEquipment } from '../../models/map/map-equipment';

@Component({
  selector: 'app-map-object-equipment-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule],
  templateUrl: './map-object-equipment-modal.html'
})
export class MapObjectEquipmentModalComponent implements OnChanges {
  private equipmentSvc = inject(MapEquipmentService);
  private toastr        = inject(ToastrService);
  public  translate     = inject(TranslateService);

  @Input() open = false;
  @Input() objectId: number | null = null;
  @Input() objectName = '';
  @Output() closed = new EventEmitter<void>();

  equipmentList = signal<MapEquipment[]>([]);
  newEquipment = { name: '', description: '', quantity: 1, conditionStatus: 'Sẵn sàng' };

  ngOnChanges(): void {
    if (this.open && this.objectId) {
      this.newEquipment = { name: '', description: '', quantity: 1, conditionStatus: 'Sẵn sàng' };
      this.loadEquipment();
    }
  }

  loadEquipment(): void {
    if (!this.objectId) return;
    this.equipmentSvc.searchAllByObject(this.objectId).subscribe(list => this.equipmentList.set(list));
  }

  async addEquipment(): Promise<void> {
    const objectId = this.objectId;
    if (!objectId || !this.newEquipment.name.trim()) return;
    const payload = { objectId, ...this.newEquipment };
    try {
      await firstValueFrom(this.equipmentSvc.create(payload));
      this.newEquipment = { name: '', description: '', quantity: 1, conditionStatus: 'Sẵn sàng' };
      this.loadEquipment();
    } catch { this.toastr.error(this.translate.instant('COMMON.ADD_ERROR')); }
  }

  async deleteEquipment(eq: MapEquipment): Promise<void> {
    if (!eq.publicId) return;
    try {
      await firstValueFrom(this.equipmentSvc.delete(eq.publicId));
      this.loadEquipment();
    } catch { this.toastr.error(this.translate.instant('COMMON.DELETE_ERROR')); }
  }

  close(): void { this.closed.emit(); }
}
