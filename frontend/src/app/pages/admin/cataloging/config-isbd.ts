import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { ConfigIsbdService, IsbdFieldService, IsbdSubfieldService } from '../../../services/cataloging/config-isbd.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { ConfigIsbd, IsbdFieldRule, IsbdSubfieldRule } from '../../../models/cataloging/config-isbd';
import { BibType } from '../../../models/cataloging/bib-type';
import { CanDirective } from '../../../directives/can.directive';

type ModalKind = 'config' | 'field' | 'subfield';

@Component({
  selector: 'app-config-isbd',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatIconModule, NgSelectModule],
  templateUrl: './config-isbd.html'
})
export class ConfigIsbdPage implements OnInit, OnDestroy {
  private configSvc   = inject(ConfigIsbdService);
  private fieldSvc    = inject(IsbdFieldService);
  private subfieldSvc = inject(IsbdSubfieldService);
  private bibTypeSvc  = inject(BibTypeService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private destroy$    = new Subject<void>();

  isLoading = signal(false);
  isSaving  = signal(false);

  configs   = signal<ConfigIsbd[]>([]);
  bibTypes  = signal<BibType[]>([]);
  fieldRules    = signal<IsbdFieldRule[]>([]);
  subfieldRules = signal<IsbdSubfieldRule[]>([]);

  selectedConfigId = signal<number | null>(null);
  expandedFields    = signal<Set<number>>(new Set());

  selectedConfig = computed(() => this.configs().find(c => c.id === this.selectedConfigId()) ?? null);
  fieldsOfSelectedConfig = computed(() =>
    this.fieldRules().filter(f => f.config_Id === this.selectedConfigId())
      .sort((a, b) => (a.fieldindex ?? 0) - (b.fieldindex ?? 0)));

  showModal = signal(false);
  modalKind = signal<ModalKind>('field');
  editMode  = signal(false);
  currentPublicId = signal<string | null>(null);
  parentFieldTag  = signal<string>('');

  showConfirmDelete = signal(false);
  deleteTarget: { kind: ModalKind; publicId: string } | null = null;

  configForm = new FormGroup({
    BibTypeId: new FormControl<number | null>(null, { validators: [Validators.required] }),
  });
  fieldForm = new FormGroup({
    Field:      new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Starttp:    new FormControl<string>('', { nonNullable: true }),
    Stoptp:     new FormControl<string>('', { nonNullable: true }),
    Fieldindex: new FormControl<number>(0, { nonNullable: true }),
  });
  subfieldForm = new FormGroup({
    Subfield:      new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Starttp:       new FormControl<string>('', { nonNullable: true }),
    Stoptp:        new FormControl<string>('', { nonNullable: true }),
    Nexttp:        new FormControl<string>('', { nonNullable: true }),
    Subfieldindex: new FormControl<number>(0, { nonNullable: true }),
  });

  ngOnInit(): void {
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.bibTypes.set(list), error: () => {} });
    this.load();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  load(): void {
    this.isLoading.set(true);
    this.configSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => {
        this.configs.set(list);
        if (this.selectedConfigId() !== null && !list.some(c => c.id === this.selectedConfigId())) this.selectedConfigId.set(null);
        this.isLoading.set(false);
      },
      error: () => { this.isLoading.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
    this.fieldSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.fieldRules.set(list), error: () => {} });
    this.subfieldSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.subfieldRules.set(list), error: () => {} });
  }

  bibTypeName(id: number | null | undefined): string {
    if (!id) return '—';
    return this.bibTypes().find(t => t.id === id)?.name || String(id);
  }
  unconfiguredBibTypes(): BibType[] {
    const usedIds = new Set(this.configs().map(c => c.bib_Type_Id));
    return this.bibTypes().filter(t => !usedIds.has(t.id));
  }

  selectConfig(id: number): void { this.selectedConfigId.set(id); this.expandedFields.set(new Set()); }

  subfieldsOf(tag: string | undefined): IsbdSubfieldRule[] {
    return this.subfieldRules()
      .filter(s => s.config_Id === this.selectedConfigId() && s.field === tag)
      .sort((a, b) => (a.subfieldindex ?? 0) - (b.subfieldindex ?? 0));
  }

  isExpanded(id: number): boolean { return this.expandedFields().has(id); }
  toggleExpand(id: number): void {
    this.expandedFields.update(set => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  // ── Config ─────────────────────────────────────────────────────────────
  openAddConfig(): void {
    this.modalKind.set('config'); this.editMode.set(false); this.currentPublicId.set(null);
    this.configForm.reset({ BibTypeId: null });
    this.showModal.set(true);
  }
  handleDeleteConfig(c: ConfigIsbd): void { if (!c.publicId) return; this.handleDelete('config', c.publicId); }

  // ── Field ──────────────────────────────────────────────────────────────
  openAddField(): void {
    this.modalKind.set('field'); this.editMode.set(false); this.currentPublicId.set(null);
    this.fieldForm.reset({ Field: '', Starttp: '', Stoptp: '', Fieldindex: (this.fieldsOfSelectedConfig().length + 1) * 10 });
    this.showModal.set(true);
  }
  openEditField(f: IsbdFieldRule): void {
    if (!f.publicId) return;
    this.modalKind.set('field'); this.editMode.set(true); this.currentPublicId.set(f.publicId);
    this.fieldForm.patchValue({ Field: f.field || '', Starttp: f.starttp || '', Stoptp: f.stoptp || '', Fieldindex: f.fieldindex ?? 0 });
    this.showModal.set(true);
  }

  // ── Subfield ───────────────────────────────────────────────────────────
  openAddSubfield(field: IsbdFieldRule): void {
    if (!field.field) return;
    this.modalKind.set('subfield'); this.editMode.set(false); this.currentPublicId.set(null); this.parentFieldTag.set(field.field);
    this.subfieldForm.reset({ Subfield: '', Starttp: '', Stoptp: '', Nexttp: '', Subfieldindex: (this.subfieldsOf(field.field).length + 1) * 10 });
    this.showModal.set(true);
  }
  openEditSubfield(s: IsbdSubfieldRule): void {
    if (!s.publicId) return;
    this.modalKind.set('subfield'); this.editMode.set(true); this.currentPublicId.set(s.publicId); this.parentFieldTag.set(s.field || '');
    this.subfieldForm.patchValue({
      Subfield: s.subfield || '', Starttp: s.starttp || '', Stoptp: s.stoptp || '', Nexttp: s.nexttp || '', Subfieldindex: s.subfieldindex ?? 0,
    });
    this.showModal.set(true);
  }

  closeModal(): void { this.showModal.set(false); }

  onSubmit(): void {
    const kind = this.modalKind();
    if (kind === 'config') {
      if (this.configForm.invalid) { this.configForm.markAllAsTouched(); return; }
      this.isSaving.set(true);
      const v = this.configForm.getRawValue();
      const configPayload: Partial<ConfigIsbd> = { bib_Type_Id: v.BibTypeId ?? undefined };
      this.configSvc.create(configPayload).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.isSaving.set(false); this.closeModal(); this.load(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
        error: () => { this.isSaving.set(false); }
      });
      return;
    }

    const configId = this.selectedConfigId();
    if (!configId) return;

    if (kind === 'field') {
      if (this.fieldForm.invalid) { this.fieldForm.markAllAsTouched(); return; }
      this.isSaving.set(true);
      const v = this.fieldForm.getRawValue();
      const payload: Partial<IsbdFieldRule> = { config_Id: configId, field: v.Field, starttp: v.Starttp || undefined, stoptp: v.Stoptp || undefined, fieldindex: v.Fieldindex };
      const pid = this.currentPublicId();
      const req$ = this.editMode() && pid ? this.fieldSvc.update(pid, payload) : this.fieldSvc.create(payload);
      req$.pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.isSaving.set(false); this.closeModal(); this.load(); this.toastr.success(this.translate.instant(this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
        error: () => { this.isSaving.set(false); }
      });
      return;
    }

    if (this.subfieldForm.invalid) { this.subfieldForm.markAllAsTouched(); return; }
    this.isSaving.set(true);
    const v = this.subfieldForm.getRawValue();
    const payload: Partial<IsbdSubfieldRule> = {
      config_Id: configId, field: this.parentFieldTag(), subfield: v.Subfield,
      starttp: v.Starttp || undefined, stoptp: v.Stoptp || undefined, nexttp: v.Nexttp || undefined, subfieldindex: v.Subfieldindex,
    };
    const pid = this.currentPublicId();
    const req$ = this.editMode() && pid ? this.subfieldSvc.update(pid, payload) : this.subfieldSvc.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); this.load(); this.toastr.success(this.translate.instant(this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  handleDelete(kind: ModalKind, publicId: string | undefined): void {
    if (!publicId) return;
    this.deleteTarget = { kind, publicId };
    this.showConfirmDelete.set(true);
  }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.deleteTarget = null; }
  confirmActionExecute(): void {
    const target = this.deleteTarget; if (!target) return;
    const svc = target.kind === 'config' ? this.configSvc : target.kind === 'field' ? this.fieldSvc : this.subfieldSvc;
    svc.delete(target.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.load(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
