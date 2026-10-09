import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { MarcFieldService } from '../../../services/cataloging/marc-field.service';
import { MarcSubFieldService } from '../../../services/cataloging/marc-subfield.service';
import { MarcIndicatorService } from '../../../services/cataloging/marc-indicator.service';
import { MarcFieldDic } from '../../../models/cataloging/marc-field';
import { MarcSubFieldDic } from '../../../models/cataloging/marc-subfield';
import { MarcIndicatorDic } from '../../../models/cataloging/marc-indicator';
import { CanDirective } from '../../../directives/can.directive';

type ModalKind = 'field' | 'subfield' | 'indicator';

@Component({
  selector: 'app-marc-dictionary',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule],
  templateUrl: './marc-dictionary.html'
})
export class MarcDictionaryPage implements OnInit, OnDestroy {
  private fieldSvc     = inject(MarcFieldService);
  private subfieldSvc  = inject(MarcSubFieldService);
  private indicatorSvc = inject(MarcIndicatorService);
  private toastr       = inject(ToastrService);
  public  translate    = inject(TranslateService);
  private destroy$      = new Subject<void>();

  isLoading = signal(false);
  isSaving  = signal(false);

  fields     = signal<MarcFieldDic[]>([]);
  subfields  = signal<MarcSubFieldDic[]>([]);
  indicators = signal<MarcIndicatorDic[]>([]);

  expandedFields = signal<Set<number>>(new Set());
  keyword = signal('');

  showModal   = signal(false);
  modalKind   = signal<ModalKind>('field');
  editMode    = signal(false);
  currentPublicId = signal<string | null>(null);
  parentFieldTag  = signal<string>('');

  showConfirmDelete = signal(false);
  deleteTarget: { kind: ModalKind; publicId: string } | null = null;

  fieldForm = new FormGroup({
    Field:         new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Description:   new FormControl<string>('', { nonNullable: true }),
    Vndescription: new FormControl<string>('', { nonNullable: true }),
    Repeatable:    new FormControl<boolean>(false, { nonNullable: true }),
    MANDATORY:     new FormControl<boolean>(false, { nonNullable: true }),
  });
  subfieldForm = new FormGroup({
    Subfield:      new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Description:   new FormControl<string>('', { nonNullable: true }),
    Vndescription: new FormControl<string>('', { nonNullable: true }),
    Repeatable:    new FormControl<boolean>(false, { nonNullable: true }),
    MANDATORY:     new FormControl<boolean>(false, { nonNullable: true }),
  });
  indicatorForm = new FormGroup({
    INDICATOR:     new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Value:         new FormControl<string>('', { nonNullable: true }),
    Description:   new FormControl<string>('', { nonNullable: true }),
    VNDESCRIPTION: new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  load(): void {
    this.isLoading.set(true);
    this.fieldSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => { this.fields.set([...list].sort((a, b) => (a.field || '').localeCompare(b.field || ''))); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
    this.subfieldSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.subfields.set(list), error: () => {} });
    this.indicatorSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.indicators.set(list), error: () => {} });
  }

  filteredFields(): MarcFieldDic[] {
    const q = this.keyword().trim().toLowerCase();
    if (!q) return this.fields();
    return this.fields().filter(f =>
      (f.field || '').toLowerCase().includes(q) ||
      (f.vndescription || '').toLowerCase().includes(q) ||
      (f.description || '').toLowerCase().includes(q));
  }

  subfieldsOf(tag: string | undefined): MarcSubFieldDic[] {
    return this.subfields().filter(s => s.field === tag).sort((a, b) => (a.subfield || '').localeCompare(b.subfield || ''));
  }
  indicatorsOf(tag: string | undefined): MarcIndicatorDic[] {
    return this.indicators().filter(i => i.field_Id === tag).sort((a, b) => (a.indicator || '').localeCompare(b.indicator || ''));
  }

  isExpanded(id: number): boolean { return this.expandedFields().has(id); }
  toggleExpand(id: number): void {
    this.expandedFields.update(set => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  // ── Field ──────────────────────────────────────────────────────────────
  openAddField(): void {
    this.modalKind.set('field'); this.editMode.set(false); this.currentPublicId.set(null);
    this.fieldForm.reset({ Field: '', Description: '', Vndescription: '', Repeatable: false, MANDATORY: false });
    this.showModal.set(true);
  }
  openEditField(f: MarcFieldDic): void {
    if (!f.publicId) return;
    this.modalKind.set('field'); this.editMode.set(true); this.currentPublicId.set(f.publicId);
    this.fieldForm.patchValue({
      Field: f.field || '', Description: f.description || '', Vndescription: f.vndescription || '',
      Repeatable: (f.repeatable ?? 0) === 1, MANDATORY: (f.mandatory ?? 0) === 1,
    });
    this.showModal.set(true);
  }

  // ── Subfield ───────────────────────────────────────────────────────────
  openAddSubfield(field: MarcFieldDic): void {
    if (!field.field) return;
    this.modalKind.set('subfield'); this.editMode.set(false); this.currentPublicId.set(null); this.parentFieldTag.set(field.field);
    this.subfieldForm.reset({ Subfield: '', Description: '', Vndescription: '', Repeatable: false, MANDATORY: false });
    this.showModal.set(true);
  }
  openEditSubfield(s: MarcSubFieldDic): void {
    if (!s.publicId) return;
    this.modalKind.set('subfield'); this.editMode.set(true); this.currentPublicId.set(s.publicId); this.parentFieldTag.set(s.field || '');
    this.subfieldForm.patchValue({
      Subfield: s.subfield || '', Description: s.description || '', Vndescription: s.vndescription || '',
      Repeatable: (s.repeatable ?? 0) === 1, MANDATORY: (s.mandatory ?? 0) === 1,
    });
    this.showModal.set(true);
  }

  // ── Indicator ──────────────────────────────────────────────────────────
  openAddIndicator(field: MarcFieldDic): void {
    if (!field.field) return;
    this.modalKind.set('indicator'); this.editMode.set(false); this.currentPublicId.set(null); this.parentFieldTag.set(field.field);
    this.indicatorForm.reset({ INDICATOR: '', Value: '', Description: '', VNDESCRIPTION: '' });
    this.showModal.set(true);
  }
  openEditIndicator(i: MarcIndicatorDic): void {
    if (!i.publicId) return;
    this.modalKind.set('indicator'); this.editMode.set(true); this.currentPublicId.set(i.publicId); this.parentFieldTag.set(i.field_Id || '');
    this.indicatorForm.patchValue({
      INDICATOR: i.indicator || '', Value: i.value || '', Description: i.description || '', VNDESCRIPTION: i.vndescription || '',
    });
    this.showModal.set(true);
  }

  closeModal(): void { this.showModal.set(false); }

  onSubmit(): void {
    const kind = this.modalKind();
    const form = kind === 'field' ? this.fieldForm : kind === 'subfield' ? this.subfieldForm : this.indicatorForm;
    if (form.invalid) { form.markAllAsTouched(); return; }
    this.isSaving.set(true);

    let req$;
    if (kind === 'field') {
      const v = this.fieldForm.getRawValue();
      const payload: Partial<MarcFieldDic> = { field: v.Field, description: v.Description || undefined, vndescription: v.Vndescription || undefined, repeatable: v.Repeatable ? 1 : 0, mandatory: v.MANDATORY ? 1 : 0 };
      const pid = this.currentPublicId();
      req$ = this.editMode() && pid ? this.fieldSvc.update(pid, payload) : this.fieldSvc.create(payload);
    } else if (kind === 'subfield') {
      const v = this.subfieldForm.getRawValue();
      const payload: Partial<MarcSubFieldDic> = { field: this.parentFieldTag(), subfield: v.Subfield, description: v.Description || undefined, vndescription: v.Vndescription || undefined, repeatable: v.Repeatable ? 1 : 0, mandatory: v.MANDATORY ? 1 : 0 };
      const pid = this.currentPublicId();
      req$ = this.editMode() && pid ? this.subfieldSvc.update(pid, payload) : this.subfieldSvc.create(payload);
    } else {
      const v = this.indicatorForm.getRawValue();
      const payload: Partial<MarcIndicatorDic> = { field_Id: this.parentFieldTag(), indicator: v.INDICATOR, value: v.Value || undefined, description: v.Description || undefined, vndescription: v.VNDESCRIPTION || undefined };
      const pid = this.currentPublicId();
      req$ = this.editMode() && pid ? this.indicatorSvc.update(pid, payload) : this.indicatorSvc.create(payload);
    }

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
    const svc = target.kind === 'field' ? this.fieldSvc : target.kind === 'subfield' ? this.subfieldSvc : this.indicatorSvc;
    svc.delete(target.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.load(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
