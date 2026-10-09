import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin, of } from 'rxjs';
import { switchMap, map as rxMap } from 'rxjs/operators';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { MapFloorService } from '../../../services/map/map-floor.service';
import { MapFloorUtilityService } from '../../../services/map/map-floor-utility.service';
import { MapObjectService } from '../../../services/map/map-object.service';
import { MapEquipmentService } from '../../../services/map/map-equipment.service';
import { MapFloor } from '../../../models/map/map-floor';
import { MapFloorUtility } from '../../../models/map/map-floor-utility';
import { MapObject, MAP_OBJECT_TYPES } from '../../../models/map/map-object';
import { MapEquipment } from '../../../models/map/map-equipment';
import { MapFloorUtilityModalComponent } from '../../../shared/map/map-floor-utility-modal';
import { MapObjectEquipmentModalComponent } from '../../../shared/map/map-object-equipment-modal';
import { statusColor, statusLabel } from '../../../shared/map/map-status.util';

interface ObjectWithEquipment { obj: MapObject; equipment: MapEquipment[]; }

@Component({
  selector: 'app-map-floor-overview',
  standalone: true,
  imports: [CommonModule, TranslateModule, MatIconModule, MapFloorUtilityModalComponent, MapObjectEquipmentModalComponent],
  templateUrl: './map-floor-overview.html'
})
export class MapFloorOverviewPage implements OnInit, OnDestroy {
  private floorService     = inject(MapFloorService);
  private utilityService   = inject(MapFloorUtilityService);
  private objectService    = inject(MapObjectService);
  private equipmentService = inject(MapEquipmentService);
  private route            = inject(ActivatedRoute);
  private router           = inject(Router);
  public  translate        = inject(TranslateService);
  private destroy$         = new Subject<void>();

  readonly objectTypes = MAP_OBJECT_TYPES;

  floorId              = signal<number | null>(null);
  floor                = signal<MapFloor | null>(null);
  utilities             = signal<MapFloorUtility[]>([]);
  objectsWithEquipment  = signal<ObjectWithEquipment[]>([]);
  isLoading             = signal(false);

  utilityModalOpen      = signal(false);
  equipmentModalOpen    = signal(false);
  equipmentModalObject  = signal<{ id: number; name: string } | null>(null);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.queryParamMap.get('floorId'));
    if (!id) { this.router.navigate(['/admin/map-floors']); return; }
    this.floorId.set(id);
    this.loadFloor();
    this.loadUtilities();
    this.loadObjectsWithEquipment();
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadFloor(): void {
    const id = this.floorId();
    if (!id) return;
    this.floorService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => {
      this.floor.set(list.find(f => f.id === id) || null);
    });
  }

  loadUtilities(): void {
    const id = this.floorId();
    if (!id) return;
    this.utilityService.searchAllByFloor(id).pipe(takeUntil(this.destroy$)).subscribe(list => this.utilities.set(list));
  }

  loadObjectsWithEquipment(): void {
    const id = this.floorId();
    if (!id) return;
    this.isLoading.set(true);
    this.objectService.searchAllByFloor(id).pipe(
      switchMap(objects => objects.length
        ? forkJoin(objects.map(obj => this.equipmentService.searchAllByObject(obj.id).pipe(rxMap(equipment => ({ obj, equipment })))))
        : of([] as ObjectWithEquipment[])),
      takeUntil(this.destroy$)
    ).subscribe({
      next: list => { this.objectsWithEquipment.set(list); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); }
    });
  }

  typeIcon(type?: string): string { return this.objectTypes.find(t => t.value === type)?.icon || 'category'; }
  typeLabel(type?: string): string { return this.objectTypes.find(t => t.value === type)?.label || type || '—'; }

  readonly statusColor = statusColor;
  readonly statusLabel = statusLabel;

  deleteUtility(item: MapFloorUtility): void {
    if (!item.publicId) return;
    this.utilityService.delete(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(() => this.loadUtilities());
  }

  deleteEquipment(eq: MapEquipment): void {
    if (!eq.publicId) return;
    this.equipmentService.delete(eq.publicId).pipe(takeUntil(this.destroy$)).subscribe(() => this.loadObjectsWithEquipment());
  }

  openUtilityModal(): void { this.utilityModalOpen.set(true); }
  onUtilityModalClosed(): void { this.utilityModalOpen.set(false); this.loadUtilities(); }

  openEquipmentModal(obj: MapObject): void {
    this.equipmentModalObject.set({ id: obj.id, name: obj.name || '' });
    this.equipmentModalOpen.set(true);
  }
  onEquipmentModalClosed(): void { this.equipmentModalOpen.set(false); this.loadObjectsWithEquipment(); }

  goBack(): void { this.router.navigate(['/admin/map-floors']); }
}
