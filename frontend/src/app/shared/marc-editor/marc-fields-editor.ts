import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { MarcField } from '../../models/cataloging/bib';
import { MarcFieldDic } from '../../models/cataloging/marc-field';
import { MarcSubFieldDic } from '../../models/cataloging/marc-subfield';

@Component({
  selector: 'app-marc-fields-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule],
  templateUrl: './marc-fields-editor.html'
})
export class MarcFieldsEditorComponent {
  @Input() fields: MarcField[] = [];
  @Input() marcFieldDics: MarcFieldDic[] = [];
  @Input() marcSubFieldDics: MarcSubFieldDic[] = [];
  @Output() fieldsChange = new EventEmitter<MarcField[]>();

  isControlField(tag: string): boolean {
    return !!tag && tag.length === 3 && tag < '010';
  }

  // Field điều khiển 001-008 do người dùng tự nhập tay, không tự sinh — không cho xoá.
  isProtectedControlField(tag: string): boolean {
    return ['001', '002', '003', '004', '005', '006', '007', '008'].includes(tag);
  }

  // 001 (Số kiểm soát/MFN) và 005 (thời điểm giao dịch gần nhất) do hệ thống tự quản lý — không cho sửa tay.
  isLockedControlField(tag: string): boolean {
    return tag === '001' || tag === '005';
  }

  fieldName(tag: string): string {
    const f = this.marcFieldDics.find(m => m.field === tag);
    return f?.vndescription || f?.description || '';
  }
  subFieldName(tag: string, code: string): string {
    const s = this.marcSubFieldDics.find(m => m.field === tag && m.subfield === code);
    return s?.vndescription || s?.description || '';
  }

  removeField(i: number): void {
    this.fieldsChange.emit(this.fields.filter((_, idx) => idx !== i));
  }
  removeSubField(field: MarcField, j: number): void {
    field.subFields = (field.subFields || []).filter((_, idx) => idx !== j);
    this.fieldsChange.emit([...this.fields]);
  }

  // Thêm tự do (không qua danh mục) — escape hatch khi trường/trường con không có trong Marc_Field/Marc_Sub_Field
  addFieldManual(): void {
    this.fieldsChange.emit([...this.fields, { tag: '', ind1: ' ', ind2: ' ', subFields: [{ code: 'a', value: '' }] }]);
    this.showFieldPicker.set(false);
  }
  addSubFieldManual(field: MarcField): void {
    field.subFields = [...(field.subFields || []), { code: '', value: '' }];
    this.fieldsChange.emit([...this.fields]);
    this.showSubfieldPicker.set(false);
  }

  // ── Hộp thoại chọn trường (Marc_Field) ──────────────────────────────────────
  showFieldPicker = signal(false);
  fieldFilter = signal('');

  openFieldPicker(): void {
    this.fieldFilter.set('');
    this.showFieldPicker.set(true);
  }
  closeFieldPicker(): void { this.showFieldPicker.set(false); }
  filteredFieldDics(): MarcFieldDic[] {
    const q = this.fieldFilter().trim().toLowerCase();
    const list = q
      ? this.marcFieldDics.filter(d =>
          (d.field || '').toLowerCase().includes(q) ||
          (d.vndescription || '').toLowerCase().includes(q) ||
          (d.description || '').toLowerCase().includes(q))
      : this.marcFieldDics;
    return [...list].sort((a, b) => (a.field || '').localeCompare(b.field || ''));
  }
  pickField(dic: MarcFieldDic): void {
    this.fieldsChange.emit([...this.fields, { tag: dic.field ?? '', ind1: ' ', ind2: ' ', subFields: [] }]);
    this.showFieldPicker.set(false);
  }

  // ── Hộp thoại chọn trường con (Marc_Sub_Field) ──────────────────────────────
  showSubfieldPicker = signal(false);
  subfieldFilter = signal('');
  subfieldTarget = signal<MarcField | null>(null);

  openSubfieldPicker(field: MarcField): void {
    this.subfieldTarget.set(field);
    this.subfieldFilter.set('');
    this.showSubfieldPicker.set(true);
  }
  closeSubfieldPicker(): void { this.showSubfieldPicker.set(false); }
  filteredSubfieldDics(): MarcSubFieldDic[] {
    const target = this.subfieldTarget();
    if (!target) return [];
    const existingCodes = new Set((target.subFields || []).map(sf => sf.code));
    const q = this.subfieldFilter().trim().toLowerCase();
    return this.marcSubFieldDics
      .filter(d => d.field === target.tag)
      .filter(d => !existingCodes.has(d.subfield || '') || d.repeatable === 1)
      .filter(d => !q ||
        (d.subfield || '').toLowerCase().includes(q) ||
        (d.vndescription || '').toLowerCase().includes(q) ||
        (d.description || '').toLowerCase().includes(q))
      .sort((a, b) => (a.subfield || '').localeCompare(b.subfield || ''));
  }
  pickSubfield(dic: MarcSubFieldDic): void {
    const target = this.subfieldTarget();
    if (!target) return;
    target.subFields = [...(target.subFields || []), { code: dic.subfield ?? '', value: '' }];
    this.fieldsChange.emit([...this.fields]);
    this.showSubfieldPicker.set(false);
  }
}
