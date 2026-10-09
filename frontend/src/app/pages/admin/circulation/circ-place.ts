import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { CanDirective } from '../../../directives/can.directive';
import { ToastrService } from '../../../services/shared/toastr.service';

import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { CircPlace } from '../../../models/printbook/circ-place';
import { StoreService } from '../../../services/printbook/store.service';
import { Store } from '../../../models/printbook/store';
import { ReaderTypeService } from '../../../services/circulation/reader-type.service';

import { PolicyCircService } from '../../../services/circulation/policy-circ.service';
import { PolicyCirc, PolicyCircTreeItem } from '../../../models/circulation/policy-circ';

import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { BibType } from '../../../models/cataloging/bib-type';
import { PolicyCircDocGroupService } from '../../../services/circulation/policy-circ-doc-group.service';
import { PolicyCircDocGroup } from '../../../models/circulation/policy-circ-doc-group';

import { CFineTypeService } from '../../../services/printbook/cfine-type.service';
import { CFineType } from '../../../models/printbook/cfine-type';
import { CFineMethodService } from '../../../services/printbook/cfine-method.service';
import { CFineMethod } from '../../../models/printbook/cfine-method';
import { PolicyCircFineService } from '../../../services/circulation/policy-circ-fine.service';
import { PolicyCircFine } from '../../../models/circulation/policy-circ-fine';

interface ReaderTypeOption { id: number; name: string; }

@Component({
  selector: 'app-circ-place',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, NgSelectModule, TranslateModule, MatIconModule],
  templateUrl: './circ-place.html'
})
export class CircPlacePage implements OnInit, OnDestroy {
  private circPlaceService  = inject(CircPlaceService);
  private storeService      = inject(StoreService);
  private readerTypeService = inject(ReaderTypeService);
  private policyService     = inject(PolicyCircService);
  // "Nhóm tài liệu" dùng chung danh mục BibType (loại tài liệu) thay vì bảng DocGroup riêng — DocGroup là
  // bảng thừa, không dùng ở đâu khác trong hệ thống.
  private bibTypeService    = inject(BibTypeService);
  private policyDocGroupService = inject(PolicyCircDocGroupService);
  private fineTypeService   = inject(CFineTypeService);
  private fineMethodService = inject(CFineMethodService);
  private policyFineService = inject(PolicyCircFineService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  // App chạy zoneless (không zone.js) — MỌI state đọc trong template mà bị gán bên trong
  // callback async (subscribe/promise) đều phải là signal, nếu không UI sẽ không tự cập nhật.

  // ===== Danh sách điểm lưu thông (cây 2 cấp: CircPlace -> PolicyCirc theo loại bạn đọc) =====
  loadingPlaces = signal(false);
  places = signal<CircPlace[]>([]);
  expandedIds = signal<Set<number>>(new Set());
  policiesByPlace = signal<Record<number, PolicyCircTreeItem[]>>({});
  loadingPolicyFor = signal<number | null>(null);

  selectedKind   = signal<'place' | 'policy' | null>(null);
  selectedPlace  = signal<CircPlace | null>(null);
  selectedPolicy = signal<PolicyCircTreeItem | null>(null);
  selectedPolicyFull: PolicyCirc | null = null;

  showDeletePlaceConfirm = signal(false);
  deletingPlace: CircPlace | null = null;

  // ===== Nghiệp vụ / Luồng công việc — chưa có bảng danh mục riêng, dùng enum cố định =====
  readonly cirTypeOptions       = [{ value: 1, key: 'CIRC_PLACE.CIR_TYPE_READ' }, { value: 2, key: 'CIRC_PLACE.CIR_TYPE_BORROW' }];
  readonly cirWorkFollowOptions = [{ value: 1, key: 'CIRC_PLACE.WORK_FOLLOW_BORROW_RETURN' }, { value: 2, key: 'CIRC_PLACE.WORK_FOLLOW_READ_ONLY' }];

  // ===== Panel: Thông tin điểm lưu thông =====
  savingPlace = signal(false);
  placeForm = new FormGroup({
    Code:                   new FormControl<string>('', { nonNullable: true }),
    Name:                   new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    CirType:                new FormControl<number | null>(null),
    CirWorkFollow:          new FormControl<number | null>(null),
    LimitBook:              new FormControl<number | null>(null),
    AccessRequestValidtime: new FormControl<number | null>(null),
    AutoAccept:             new FormControl<boolean>(false, { nonNullable: true }),
    WorkSession:            new FormControl<string>('', { nonNullable: true }),
  });

  allStores      = signal<Store[]>([]);
  allReaderTypes = signal<ReaderTypeOption[]>([]);
  selectedStoreIds      = signal<Set<number>>(new Set());
  selectedReaderTypeIds = signal<Set<number>>(new Set());

  // ===== Panel: Chính sách lưu thông cho loại bạn đọc =====
  savingPolicy = signal(false);
  policyForm = new FormGroup({
    NumberOfBook:         new FormControl<number | null>(null),
    NumberOfBookAccept:   new FormControl<number | null>(null),
    NumberOfDate:         new FormControl<number | null>(null),
    NumberOfRenewQty:     new FormControl<number | null>(null),
    NumberOfRenew:        new FormControl<number | null>(null),
    NumberOfRenewDays:    new FormControl<number | null>(null),
    NumberOfRequest:      new FormControl<number | null>(null),
    NumberOfRequestCount: new FormControl<number | null>(null),
    NumberOfRequestDays:  new FormControl<number | null>(null),
    AllowOpacRequest:     new FormControl<boolean>(false, { nonNullable: true }),
  });

  activeTab = signal<'docgroup' | 'fine'>('docgroup');

  allDocGroups  = signal<BibType[]>([]);
  docGroupRows  = signal<PolicyCircDocGroup[]>([]);
  showAddDocGroup = signal(false);
  newDocGroup: Partial<PolicyCircDocGroup> = {};

  allFineTypes   = signal<CFineType[]>([]);
  allFineMethods = signal<CFineMethod[]>([]);
  fineRows       = signal<PolicyCircFine[]>([]);
  showAddFine = signal(false);
  newFine: Partial<PolicyCircFine> = {};
  editingFinePublicId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadPlaces();
    this.storeService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.allStores.set(list));
    this.readerTypeService.getAll({ draw: 1, start: 0, length: 2000, search: { value: '' } } as any)
      .pipe(takeUntil(this.destroy$))
      .subscribe(res => this.allReaderTypes.set((res.data as any[]).map(x => ({ id: Number(x.id), name: x.name }))));
    this.bibTypeService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.allDocGroups.set(list));
    this.fineTypeService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.allFineTypes.set(list));
    this.fineMethodService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.allFineMethods.set(list));
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  // ===== Cây điểm lưu thông =====
  loadPlaces(): void {
    this.loadingPlaces.set(true);
    this.circPlaceService.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => { this.places.set(list); this.loadingPlaces.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.loadingPlaces.set(false); }
    });
  }

  toggleExpand(place: CircPlace): void {
    const current = this.expandedIds();
    if (current.has(place.id)) {
      const next = new Set(current);
      next.delete(place.id);
      this.expandedIds.set(next);
      return;
    }
    this.expandedIds.set(new Set(current).add(place.id));
    if (!this.policiesByPlace()[place.id] && place.publicId) {
      this.loadingPolicyFor.set(place.id);
      this.policyService.byCircPlace(place.publicId).pipe(takeUntil(this.destroy$)).subscribe(items => {
        this.policiesByPlace.update(m => ({ ...m, [place.id]: items }));
        this.loadingPolicyFor.set(null);
      });
    }
  }

  isExpanded(place: CircPlace): boolean { return this.expandedIds().has(place.id); }

  // ===== Chọn node =====
  selectNewPlace(): void {
    this.selectedKind.set('place');
    this.selectedPlace.set(null);
    this.selectedPolicy.set(null);
    this.placeForm.reset({ Code: '', Name: '', CirType: null, CirWorkFollow: null, LimitBook: null, AccessRequestValidtime: null, AutoAccept: false, WorkSession: '' });
    this.selectedStoreIds.set(new Set());
    this.selectedReaderTypeIds.set(new Set());
  }

  selectPlace(place: CircPlace): void {
    this.selectedKind.set('place');
    this.selectedPlace.set(place);
    this.selectedPolicy.set(null);
    this.placeForm.reset({
      Code:                   place.code ?? '',
      Name:                   place.name ?? '',
      CirType:                place.cirType ?? null,
      CirWorkFollow:          place.cirWorkFollow ?? null,
      LimitBook:              place.limitBook ?? null,
      AccessRequestValidtime: place.accessRequestValidtime ?? null,
      AutoAccept:             place.autoAccept === 1,
      WorkSession:            place.workSession ?? '',
    });
    this.selectedStoreIds.set(new Set());
    this.selectedReaderTypeIds.set(new Set());
    if (place.publicId) {
      this.circPlaceService.getMapping(place.publicId).pipe(takeUntil(this.destroy$)).subscribe(m => {
        this.selectedStoreIds.set(new Set(m.storeIds ?? []));
        this.selectedReaderTypeIds.set(new Set(m.readerTypeIds ?? []));
      });
    }
  }

  selectPolicy(place: CircPlace, policy: PolicyCircTreeItem): void {
    this.selectedKind.set('policy');
    this.selectedPlace.set(place);
    this.selectedPolicy.set(policy);
    this.selectedPolicyFull = null;
    this.activeTab.set('docgroup');
    this.policyForm.reset();
    this.policyService.getById(policy.publicId).pipe(takeUntil(this.destroy$)).subscribe((p: PolicyCirc) => {
      this.selectedPolicyFull = p;
      this.policyForm.patchValue({
        NumberOfBook:         p.numberOfBook ?? null,
        NumberOfBookAccept:   p.numberOfBookAccept ?? null,
        NumberOfDate:         p.numberOfDate ?? null,
        NumberOfRenewQty:     p.numberOfRenewQty ?? null,
        NumberOfRenew:        p.numberOfRenew ?? null,
        NumberOfRenewDays:    p.numberOfRenewDays ?? null,
        NumberOfRequest:      p.numberOfRequest ?? null,
        NumberOfRequestCount: p.numberOfRequestCount ?? null,
        NumberOfRequestDays:  p.numberOfRequestDays ?? null,
        AllowOpacRequest:     p.allowOpacRequest === 1,
      });
    });
    this.loadDocGroupRows(policy.id);
    this.loadFineRows(policy.id);
  }

  // ===== Lưu điểm lưu thông + 2 mapping =====
  savePlace(): void {
    if (this.placeForm.invalid) { this.placeForm.markAllAsTouched(); return; }
    const v = this.placeForm.getRawValue();
    const payload: Partial<CircPlace> = {
      code:                   v.Code || undefined,
      name:                   v.Name,
      cirType:                v.CirType ?? undefined,
      cirWorkFollow:          v.CirWorkFollow ?? undefined,
      limitBook:              v.LimitBook ?? undefined,
      accessRequestValidtime: v.AccessRequestValidtime ?? undefined,
      autoAccept:             v.AutoAccept ? 1 : 0,
      workSession:            v.WorkSession || undefined,
    };
    this.savingPlace.set(true);
    const current = this.selectedPlace();
    const req$ = current?.publicId
      ? this.circPlaceService.update(current.publicId, payload)
      : this.circPlaceService.create(payload);

    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: (saved) => {
        const publicId: string | undefined = current?.publicId ?? saved?.publicId ?? saved?.data?.publicId;
        if (!publicId) { this.savingPlace.set(false); return; }
        forkJoin([
          this.circPlaceService.replaceStoreMapping(publicId, [...this.selectedStoreIds()]),
          this.circPlaceService.replaceReaderTypeMapping(publicId, [...this.selectedReaderTypeIds()]),
        ]).pipe(takeUntil(this.destroy$)).subscribe(() => {
          this.savingPlace.set(false);
          this.toastr.success(this.translate.instant(current ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
          if (current?.id) this.policiesByPlace.update(m => ({ ...m, [current.id]: undefined as any }));
          this.loadPlaces();
        });
      },
      error: () => { this.savingPlace.set(false); }
    });
  }

  toggleStore(id: number): void {
    const next = new Set(this.selectedStoreIds());
    next.has(id) ? next.delete(id) : next.add(id);
    this.selectedStoreIds.set(next);
  }
  toggleReaderType(id: number): void {
    const next = new Set(this.selectedReaderTypeIds());
    next.has(id) ? next.delete(id) : next.add(id);
    this.selectedReaderTypeIds.set(next);
  }

  confirmDeletePlace(place: CircPlace): void {
    this.deletingPlace = place;
    this.showDeletePlaceConfirm.set(true);
  }
  closeDeletePlaceConfirm(): void { this.showDeletePlaceConfirm.set(false); this.deletingPlace = null; }
  doDeletePlace(): void {
    if (!this.deletingPlace?.publicId) return;
    this.circPlaceService.delete(this.deletingPlace.publicId).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
      if (this.selectedPlace()?.id === this.deletingPlace?.id) { this.selectedKind.set(null); this.selectedPlace.set(null); }
      this.closeDeletePlaceConfirm();
      this.loadPlaces();
    });
  }

  // ===== Lưu chính sách lưu thông =====
  savePolicy(): void {
    const policy = this.selectedPolicy();
    if (!policy) return;
    const v = this.policyForm.getRawValue();
    // Update là PUT toàn bộ entity (PropertyMapper ghi đè mọi field kể cả null) — phải giữ lại
    // readerType/circPlace/store/muontrung từ bản ghi gốc, không chỉ gửi các field form đang sửa.
    const payload: Partial<PolicyCirc> = {
      readerType:           this.selectedPolicyFull?.readerType ?? policy.readerTypeId,
      circPlace:            this.selectedPolicyFull?.circPlace ?? this.selectedPlace()?.id,
      store:                this.selectedPolicyFull?.store,
      muontrung:             this.selectedPolicyFull?.muontrung,
      numberOfBook:         v.NumberOfBook ?? undefined,
      numberOfBookAccept:   v.NumberOfBookAccept ?? undefined,
      numberOfDate:         v.NumberOfDate ?? undefined,
      numberOfRenewQty:     v.NumberOfRenewQty ?? undefined,
      numberOfRenew:        v.NumberOfRenew ?? undefined,
      numberOfRenewDays:    v.NumberOfRenewDays ?? undefined,
      numberOfRequest:      v.NumberOfRequest ?? undefined,
      numberOfRequestCount: v.NumberOfRequestCount ?? undefined,
      numberOfRequestDays:  v.NumberOfRequestDays ?? undefined,
      allowOpacRequest:     v.AllowOpacRequest ? 1 : 0,
    };
    this.savingPolicy.set(true);
    this.policyService.update(policy.publicId, payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.savingPolicy.set(false);
        this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
        const place = this.selectedPlace();
        if (place) { this.policiesByPlace.update(m => ({ ...m, [place.id]: undefined as any })); this.toggleExpand(place); this.toggleExpand(place); }
      },
      error: () => { this.savingPolicy.set(false); }
    });
  }

  // ===== Tab Nhóm tài liệu =====
  loadDocGroupRows(policyCircId: number): void {
    this.policyDocGroupService.searchByPolicy(policyCircId).pipe(takeUntil(this.destroy$)).subscribe(rows => {
      const groups = this.allDocGroups();
      this.docGroupRows.set(rows.map(r => ({ ...r, docGroupName: groups.find(g => g.id === r.docGroupId)?.name })));
    });
  }
  openAddDocGroup(): void {
    this.newDocGroup = { policyCircId: this.selectedPolicy()?.id };
    this.showAddDocGroup.set(true);
  }
  saveNewDocGroup(): void {
    const policy = this.selectedPolicy();
    if (!policy || !this.newDocGroup.docGroupId) return;
    this.policyDocGroupService.create({ ...this.newDocGroup, policyCircId: policy.id }).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.showAddDocGroup.set(false);
      this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
      this.loadDocGroupRows(policy.id);
    });
  }
  removeDocGroupRow(row: PolicyCircDocGroup): void {
    if (!row.publicId) return;
    this.policyDocGroupService.delete(row.publicId).pipe(takeUntil(this.destroy$)).subscribe(() => {
      const policy = this.selectedPolicy();
      if (policy) this.loadDocGroupRows(policy.id);
    });
  }

  // ===== Tab Cơ chế phạt =====
  loadFineRows(policyCircId: number): void {
    this.policyFineService.searchByPolicy(policyCircId).pipe(takeUntil(this.destroy$)).subscribe(rows => this.fineRows.set(rows));
  }
  openAddFine(): void {
    this.newFine = { policyCircId: this.selectedPolicy()?.id };
    this.editingFinePublicId.set(null);
    this.showAddFine.set(true);
  }
  editFineRow(row: PolicyCircFine): void {
    if (!row.publicId) return;
    this.newFine = {
      policyCircId: this.selectedPolicy()?.id,
      fineTypeId:   row.fineTypeId,
      fineMethodId: row.fineMethodId,
      holdCardDays: row.holdCardDays,
      fineAmount:   row.fineAmount,
    };
    this.editingFinePublicId.set(row.publicId);
    this.showAddFine.set(true);
  }
  cancelFineForm(): void {
    this.showAddFine.set(false);
    this.editingFinePublicId.set(null);
    this.newFine = {};
  }
  saveNewFine(): void {
    const policy = this.selectedPolicy();
    if (!policy || !this.newFine.fineTypeId) return;
    const editingPublicId = this.editingFinePublicId();
    const req$ = editingPublicId
      ? this.policyFineService.update(editingPublicId, { ...this.newFine, policyCircId: policy.id })
      : this.policyFineService.create({ ...this.newFine, policyCircId: policy.id });
    req$.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.showAddFine.set(false);
      this.editingFinePublicId.set(null);
      this.toastr.success(this.translate.instant(editingPublicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      this.loadFineRows(policy.id);
    });
  }
  removeFineRow(row: PolicyCircFine): void {
    if (!row.publicId) return;
    this.policyFineService.delete(row.publicId).pipe(takeUntil(this.destroy$)).subscribe(() => {
      const policy = this.selectedPolicy();
      if (policy) this.loadFineRows(policy.id);
    });
  }

  fineTypeName(id?: number): string { return this.allFineTypes().find(x => x.id === id)?.name ?? '—'; }
  fineMethodName(id?: number): string { return this.allFineMethods().find(x => x.id === id)?.name ?? '—'; }
}
