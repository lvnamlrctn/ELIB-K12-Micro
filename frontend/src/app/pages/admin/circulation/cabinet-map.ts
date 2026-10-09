import { Component, inject, OnInit, OnDestroy, signal, computed, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatIconModule } from '@angular/material/icon';
import { DragDropModule, CdkDragEnd } from '@angular/cdk/drag-drop';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { Router } from '@angular/router';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CabinetService } from '../../../services/printbook/cabinet.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { Cabinet } from '../../../models/printbook/cabinet';
import { CircPlace } from '../../../models/printbook/circ-place';

@Component({
  selector: 'app-cabinet-map',
  standalone: true,
  imports: [CommonModule, FormsModule, NgSelectModule, TranslateModule, MatIconModule, DragDropModule],
  templateUrl: './cabinet-map.html'
})
export class CabinetMapPage implements OnInit, OnDestroy {
  private service      = inject(CabinetService);
  private circPlaceSvc = inject(CircPlaceService);
  private toastr       = inject(ToastrService);
  public  translate    = inject(TranslateService);
  private router       = inject(Router);
  private destroy$     = new Subject<void>();

  @ViewChild('canvas') canvasRef!: ElementRef<HTMLDivElement>;

  circPlaces  = signal<CircPlace[]>([]);
  circPlaceId = signal<number | null>(null);
  cabinets    = signal<Cabinet[]>([]);
  isLoading   = signal(false);

  placedCabinets   = computed(() => this.cabinets().filter(c => c.positionX != null && c.positionY != null));
  unplacedCabinets = computed(() => this.cabinets().filter(c => c.positionX == null || c.positionY == null));

  ngOnInit(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.circPlaces.set(data);
      if (data.length && this.circPlaceId() == null) this.circPlaceId.set(Math.min(...data.map(x => x.id)));
      this.loadCabinets();
    });
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  onCircPlaceChange(): void { this.loadCabinets(); }

  loadCabinets(): void {
    const circPlaceId = this.circPlaceId();
    if (circPlaceId == null) return;
    this.isLoading.set(true);
    this.service.search({ pageIndex: 1, pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.cabinets.set((res.data || []).filter(c => c.circPlaceId === circPlaceId));
        this.isLoading.set(false);
      },
      error: () => { this.isLoading.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  private justDragged = false;

  onDragEnded(cabinet: Cabinet, event: CdkDragEnd): void {
    const distance = Math.hypot(event.distance.x, event.distance.y);
    if (distance > 3) this.justDragged = true;

    const canvas = this.canvasRef?.nativeElement;
    if (!canvas) return;
    const canvasRect = canvas.getBoundingClientRect();
    const dragRect = event.source.element.nativeElement.getBoundingClientRect();
    const x = ((dragRect.left + dragRect.width / 2 - canvasRect.left) / canvasRect.width) * 100;
    const y = ((dragRect.top + dragRect.height / 2 - canvasRect.top) / canvasRect.height) * 100;
    const positionX = Math.min(98, Math.max(2, x));
    const positionY = Math.min(96, Math.max(2, y));

    event.source.reset();
    this.cabinets.update(list => list.map(c => c.id === cabinet.id ? { ...c, positionX, positionY } : c));

    if (!cabinet.publicId) return;
    this.service.update(cabinet.publicId, {
      name: cabinet.name, code: cabinet.code, circPlaceId: cabinet.circPlaceId, note: cabinet.note, status: cabinet.status,
      rows: cabinet.rows, cols: cabinet.cols,
      positionX, positionY
    }).pipe(takeUntil(this.destroy$)).subscribe({
      error: () => {}
    });
  }

  openCompartments(cabinet: Cabinet): void {
    if (this.justDragged) { this.justDragged = false; return; }
    this.router.navigate(['/admin/cabinets'], { queryParams: { openCompartments: cabinet.publicId } });
  }
}
