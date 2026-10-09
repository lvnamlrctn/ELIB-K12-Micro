import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  LibraryMapApiService, MapBuilding, MapFloorSummary, MapFloor, MapObjectInfo
} from '../../services/library-map-api.service';

/** Ánh xạ objectType (dùng chung với schema map.MapObject) sang icon Font Awesome — hệ icon OPAC dùng
 *  fas fa-*, khác với mat-icon ligature bên trang quản trị nên không tái dùng MAP_OBJECT_TYPES admin. */
const OBJECT_TYPE_ICONS: Record<string, string> = {
  ROOM: 'fas fa-door-open',
  SHELF: 'fas fa-book',
  PC: 'fas fa-desktop',
  STUDY_SPACE: 'fas fa-chair',
  COUNTER: 'fas fa-cash-register',
  SERVER_ROOM: 'fas fa-server',
  OFFICE: 'fas fa-briefcase',
  DOOR: 'fas fa-door-open',
  ELEVATOR: 'fas fa-arrows-up-down',
  RESTROOM: 'fas fa-restroom',
  OTHER: 'fas fa-shapes',
};

@Component({
  selector: 'app-library-map',
  imports: [RouterLink, FormsModule],
  templateUrl: './library-map.html'
})
export class LibraryMapComponent implements OnInit {
  private api = inject(LibraryMapApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  buildings = signal<MapBuilding[]>([]);
  floors = signal<MapFloorSummary[]>([]);
  currentFloor = signal<MapFloor | null>(null);
  selectedBuildingId = signal<number | null>(null);
  selectedFloorId = signal<number | null>(null);
  loading = signal(false);

  highlightObjectId = signal<number | null>(null);
  highlightRowId = signal<number | null>(null);
  entrance = signal<{ x: number; y: number } | null>(null);

  target = computed<MapObjectInfo | null>(() => {
    const id = this.highlightObjectId();
    if (!id) return null;
    return this.currentFloor()?.objects.find(o => o.id === id) ?? null;
  });

  routeSvgPath = computed<string | null>(() => {
    const start = this.entrance();
    const t = this.target();
    if (!start || !t || t.positionX == null || t.positionY == null) return null;
    const tx = t.positionX + (t.width ?? 0) / 2;
    const ty = t.positionY + (t.height ?? 0) / 2;
    return `M ${start.x} ${start.y} L ${tx} ${start.y} L ${tx} ${ty}`;
  });

  ngOnInit(): void {
    this.api.getBuildings().subscribe(list => this.buildings.set(list));

    const qp = this.route.snapshot.queryParamMap;
    const floorId = qp.get('floorId');
    const highlightObjectId = qp.get('highlightObjectId');
    const highlightRowId = qp.get('highlightRowId');
    const entranceX = qp.get('entranceX');
    const entranceY = qp.get('entranceY');

    if (highlightObjectId) this.highlightObjectId.set(Number(highlightObjectId));
    if (highlightRowId) this.highlightRowId.set(Number(highlightRowId));
    if (entranceX && entranceY) this.entrance.set({ x: Number(entranceX), y: Number(entranceY) });

    if (floorId) {
      this.loadFloor(Number(floorId), true);
    } else {
      this.api.getBuildings().subscribe(list => {
        if (list.length) this.onBuildingChange(list[0].id);
      });
    }
  }

  onBuildingChange(buildingId: number): void {
    this.selectedBuildingId.set(buildingId);
    this.api.getFloors(buildingId).subscribe(list => {
      this.floors.set(list);
      if (list.length) this.loadFloor(list[0].id);
      else this.currentFloor.set(null);
    });
  }

  onFloorChange(floorId: number): void {
    this.loadFloor(floorId);
  }

  private loadFloor(floorId: number, keepHighlight = false): void {
    if (!keepHighlight) {
      this.highlightObjectId.set(null);
      this.highlightRowId.set(null);
      this.entrance.set(null);
    }
    this.loading.set(true);
    this.api.getFloor(floorId).subscribe(floor => {
      this.currentFloor.set(floor);
      this.loading.set(false);
      if (!floor) return;
      this.selectedFloorId.set(floor.id);
      this.selectedBuildingId.set(floor.buildingId);
      this.api.getFloors(floor.buildingId).subscribe(list => this.floors.set(list));
    });
  }

  iconFor(objectType?: string): string {
    return (objectType && OBJECT_TYPE_ICONS[objectType]) || OBJECT_TYPE_ICONS['OTHER'];
  }

  /** Phòng học nhóm trên sơ đồ — bấm để sang trang đặt phòng, mở sẵn đúng phòng. */
  isRoom(obj: MapObjectInfo): boolean { return ['ROOM', 'STUDY_SPACE'].includes((obj.objectType ?? '').toUpperCase()); }
  openRoom(obj: MapObjectInfo): void { if (this.isRoom(obj)) this.router.navigate(['/dat-phong-hoc-nhom'], { queryParams: { room: obj.id } }); }

  geom(obj: MapObjectInfo): { x: number; y: number; w: number; h: number } {
    return { x: obj.positionX ?? 0, y: obj.positionY ?? 0, w: obj.width ?? 10, h: obj.height ?? 10 };
  }
}
