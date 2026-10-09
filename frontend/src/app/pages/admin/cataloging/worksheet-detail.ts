import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin, of } from 'rxjs';
import { switchMap, map } from 'rxjs/operators';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { WorkSheetService } from '../../../services/cataloging/worksheet.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { MarcFieldService } from '../../../services/cataloging/marc-field.service';
import { MarcIndicatorService } from '../../../services/cataloging/marc-indicator.service';
import { MarcSubFieldService } from '../../../services/cataloging/marc-subfield.service';
import { WorksheetFieldService } from '../../../services/cataloging/worksheet-field.service';
import { WorksheetSubfieldService } from '../../../services/cataloging/worksheet-subfield.service';
import { WorkSheet } from '../../../models/cataloging/worksheet';
import { BibType } from '../../../models/cataloging/bib-type';
import { MarcFieldDic } from '../../../models/cataloging/marc-field';
import { MarcIndicatorDic } from '../../../models/cataloging/marc-indicator';
import { MarcSubFieldDic } from '../../../models/cataloging/marc-subfield';

interface SubfieldRow {
  publicId?: string;
  dbId?:     number;
  subfield:  string;
  value:     string;
}
interface FieldGroup {
  publicId?: string;
  dbId?:     number;
  field:     string;
  l1:        string;
  l2:        string;
  subfields: SubfieldRow[];
}

@Component({
  selector: 'app-worksheet-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule, NgSelectModule],
  templateUrl: './worksheet-detail.html'
})
export class WorksheetDetailPage implements OnInit, OnDestroy {
  private route         = inject(ActivatedRoute);
  private router        = inject(Router);
  private worksheetSvc  = inject(WorkSheetService);
  private bibTypeSvc    = inject(BibTypeService);
  private marcFieldSvc  = inject(MarcFieldService);
  private marcIndSvc    = inject(MarcIndicatorService);
  private marcSubSvc    = inject(MarcSubFieldService);
  private wsFieldSvc    = inject(WorksheetFieldService);
  private wsSubSvc      = inject(WorksheetSubfieldService);
  private toastr        = inject(ToastrService);
  public  translate     = inject(TranslateService);
  private destroy$      = new Subject<void>();

  publicId = '';
  worksheet = signal<WorkSheet | null>(null);
  bibTypes  = signal<BibType[]>([]);
  marcFields    = signal<MarcFieldDic[]>([]);
  marcIndicators = signal<MarcIndicatorDic[]>([]);
  marcSubFields  = signal<MarcSubFieldDic[]>([]);

  groups   = signal<FieldGroup[]>([]);
  initialGroups: FieldGroup[] = [];
  expanded = signal<Set<string>>(new Set());

  isLoading = signal(false);
  isSaving  = signal(false);
  confirmRemoveField = signal<string | null>(null);

  bibTypeName = computed(() => {
    const id = this.worksheet()?.bib_Type_Id;
    if (!id) return '—';
    return this.bibTypes().find(t => t.id === id)?.name || '—';
  });

  ngOnInit(): void {
    this.publicId = this.route.snapshot.paramMap.get('publicId') || '';
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.bibTypes.set(l), error: () => {} });
    this.marcFieldSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.marcFields.set(l), error: () => {} });
    this.marcIndSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.marcIndicators.set(l), error: () => {} });
    this.marcSubSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.marcSubFields.set(l), error: () => {} });
    this.load();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  load(): void {
    if (!this.publicId) return;
    this.isLoading.set(true);
    this.worksheetSvc.getById(this.publicId).pipe(
      takeUntil(this.destroy$),
      switchMap(ws => {
        this.worksheet.set(ws);
        if (!ws.id) return of({ fields: [], subfields: [] as any[] });
        return this.wsFieldSvc.getByWorksheet(ws.id).pipe(
          switchMap(fields => {
            const fieldIds = fields.map(f => f.id).filter((id): id is number => !!id);
            return this.wsSubSvc.getByFields(fieldIds).pipe(map(subfields => ({ fields, subfields })));
          })
        );
      })
    ).subscribe({
      next: ({ fields, subfields }) => {
        const groups: FieldGroup[] = fields.map(f => ({
          publicId: f.publicId, dbId: f.id, field: f.field || '', l1: f.l1 || '', l2: f.l2 || '',
          subfields: subfields.filter(s => s.worksheet_Field_Id === f.id).map(s => ({
            publicId: s.publicId, dbId: s.id, subfield: s.subfield || '', value: s.value || ''
          }))
        }));
        this.groups.set(groups);
        this.initialGroups = JSON.parse(JSON.stringify(groups));
        this.isLoading.set(false);
      },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }

  fieldDesc(tag: string): string {
    const f = this.marcFields().find(m => m.field === tag);
    return f?.vndescription || f?.description || '';
  }
  subfieldDesc(tag: string, code: string): string {
    const s = this.marcSubFields().find(m => m.field === tag && m.subfield === code);
    return s?.vndescription || s?.description || '';
  }
  subfieldsFor(tag: string): MarcSubFieldDic[] {
    return this.marcSubFields().filter(s => s.field === tag);
  }
  indicatorsFor(tag: string, position: '1' | '2'): MarcIndicatorDic[] {
    return this.marcIndicators().filter(i => i.field_Id === tag && i.indicator === position);
  }

  isChecked(tag: string): boolean { return this.groups().some(g => g.field === tag); }
  isSubChecked(tag: string, code: string): boolean {
    return this.groups().find(g => g.field === tag)?.subfields.some(s => s.subfield === code) ?? false;
  }
  toggleExpand(tag: string): void {
    const set = new Set(this.expanded());
    set.has(tag) ? set.delete(tag) : set.add(tag);
    this.expanded.set(set);
  }
  isExpanded(tag: string): boolean { return this.expanded().has(tag); }

  // Ghi chú: 1 mã trường MARC (vd "653", "700") có thể lặp lại nhiều lần trong cùng 1 worksheet
  // (trường lặp — repeatable). Cây bên phải chỉ thao tác ở mức "mã trường" (thêm/xoá gộp tất cả
  // các dòng cùng mã), còn bảng bên trái sửa từng dòng cụ thể theo đúng tham chiếu đối tượng —
  // không dùng lại mã trường/mã trường con làm khoá để tránh nhầm lẫn giữa các dòng trùng mã.
  toggleField(tag: string): void {
    const has = this.groups().some(g => g.field === tag);
    if (has) {
      const anyWithSub = this.groups().some(g => g.field === tag && g.subfields.length > 0);
      if (anyWithSub) { this.confirmRemoveField.set(tag); return; }
      this.groups.set(this.groups().filter(g => g.field !== tag));
    } else {
      this.groups.set([...this.groups(), { field: tag, l1: '', l2: '', subfields: [] }]);
    }
  }
  confirmRemoveFieldExecute(): void {
    const tag = this.confirmRemoveField();
    if (tag) this.groups.set(this.groups().filter(g => g.field !== tag));
    this.confirmRemoveField.set(null);
  }
  cancelRemoveField(): void { this.confirmRemoveField.set(null); }

  toggleSubfield(tag: string, code: string): void {
    let group = this.groups().find(g => g.field === tag);
    if (!group) { group = { field: tag, l1: '', l2: '', subfields: [] }; this.groups.set([...this.groups(), group]); }
    const target = group;
    const has = target.subfields.some(s => s.subfield === code);
    const updated: FieldGroup = has
      ? { ...target, subfields: target.subfields.filter(s => s.subfield !== code) }
      : { ...target, subfields: [...target.subfields, { subfield: code, value: '' }] };
    this.groups.set(this.groups().map(g => g === target ? updated : g));
  }

  setIndicator(g: FieldGroup, pos: 'l1' | 'l2', value: string): void {
    this.groups.set(this.groups().map(x => x === g ? { ...x, [pos]: value } : x));
  }
  setSubfieldValue(g: FieldGroup, s: SubfieldRow, value: string): void {
    this.groups.set(this.groups().map(x => x !== g ? x : {
      ...x, subfields: x.subfields.map(sf => sf === s ? { ...sf, value } : sf)
    }));
  }

  back(): void { this.router.navigate(['/admin/worksheets']); }

  save(): void {
    const ws = this.worksheet();
    if (!ws?.id) return;
    this.isSaving.set(true);

    const current = this.groups();
    const initial = this.initialGroups;

    // Ghép cặp theo dbId (không theo mã trường/mã trường con) — vì 1 mã trường MARC lặp
    // (vd "653", "700") có thể có nhiều dòng worksheet_field cùng mã trong 1 worksheet.
    const removedFields = initial.filter(ig => ig.dbId != null && !current.some(g => g.dbId === ig.dbId));
    const addedFields    = current.filter(g => g.dbId == null);
    const keptFields      = current.filter(g => g.dbId != null && initial.some(ig => ig.dbId === g.dbId));

    // 1) Xoá field không còn chọn (xoá subfield con đã lưu trước, rồi xoá field)
    const removeFieldSubDeletes = removedFields.flatMap(f => f.subfields.filter(s => s.publicId).map(s => this.wsSubSvc.delete(s.publicId!)));
    const removeFieldDeletes    = removedFields.filter(f => f.publicId).map(f => this.wsFieldSvc.delete(f.publicId!));

    // 2) Cập nhật chỉ thị cho field đã có nếu thay đổi + diff subfield con (ghép theo dbId)
    const keptFieldUpdates: any[] = [];
    const keptSubDeletes: any[] = [];
    const keptSubAdds: any[] = [];
    const keptSubUpdates: any[] = [];
    for (const g of keptFields) {
      const ig = initial.find(i => i.dbId === g.dbId)!;
      if (g.publicId && (g.l1 !== ig.l1 || g.l2 !== ig.l2)) {
        keptFieldUpdates.push(this.wsFieldSvc.update(g.publicId, { bib_Worksheet_Id: ws.id, field: g.field, l1: g.l1, l2: g.l2 }));
      }
      const removedSub = ig.subfields.filter(s => s.dbId != null && !g.subfields.some(s2 => s2.dbId === s.dbId));
      const addedSub   = g.subfields.filter(s => s.dbId == null);
      const keptSub     = g.subfields.filter(s => s.dbId != null && ig.subfields.some(s2 => s2.dbId === s.dbId));
      removedSub.filter(s => s.publicId).forEach(s => keptSubDeletes.push(this.wsSubSvc.delete(s.publicId!)));
      addedSub.forEach(s => keptSubAdds.push(this.wsSubSvc.create({ worksheet_Field_Id: g.dbId, subfield: s.subfield, value: s.value })));
      for (const s of keptSub) {
        const is = ig.subfields.find(s2 => s2.dbId === s.dbId)!;
        if (s.publicId && s.value !== is.value) keptSubUpdates.push(this.wsSubSvc.update(s.publicId, { worksheet_Field_Id: g.dbId, subfield: s.subfield, value: s.value }));
      }
    }

    const updates = [...removeFieldSubDeletes, ...removeFieldDeletes, ...keptFieldUpdates, ...keptSubDeletes, ...keptSubAdds, ...keptSubUpdates];
    if (!updates.length) { this.saveAddedFields(ws.id!, addedFields); return; }
    forkJoin(updates)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => this.saveAddedFields(ws.id!, addedFields),
        error: () => { this.isSaving.set(false); }
      });
  }

  private saveAddedFields(worksheetId: number, addedFields: FieldGroup[]): void {
    if (!addedFields.length) { this.finishSave(); return; }
    const creates = addedFields.map(g => this.wsFieldSvc.create({ bib_Worksheet_Id: worksheetId, field: g.field, l1: g.l1, l2: g.l2 }));
    forkJoin(creates).pipe(takeUntil(this.destroy$)).subscribe({
      next: (created: any[]) => {
        const subAdds = addedFields.flatMap((g, i) => {
          const newId = created[i]?.id ?? created[i]?.data?.id;
          return g.subfields.map(s => this.wsSubSvc.create({ worksheet_Field_Id: newId, subfield: s.subfield, value: s.value }));
        });
        if (!subAdds.length) { this.finishSave(); return; }
        forkJoin(subAdds).pipe(takeUntil(this.destroy$)).subscribe({
          next: () => this.finishSave(),
          error: () => { this.isSaving.set(false); }
        });
      },
      error: () => { this.isSaving.set(false); }
    });
  }

  private finishSave(): void {
    this.isSaving.set(false);
    this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS'));
    this.load();
  }
}
