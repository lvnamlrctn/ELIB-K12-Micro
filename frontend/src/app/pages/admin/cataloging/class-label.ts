import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { ClassLabelService, ClassLabelItem } from '../../../services/cataloging/class-label.service';

@Component({
  selector: 'app-class-label',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatIconModule],
  templateUrl: './class-label.html'
})
export class ClassLabelPage {
  private service   = inject(ClassLabelService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);

  isLoading = signal(false);
  labels = signal<ClassLabelItem[]>([]);

  searchForm = new FormGroup({
    receiptCode: new FormControl<string>('', { nonNullable: true }),
    barcodeFrom: new FormControl<string>('', { nonNullable: true }),
    barcodeTo:   new FormControl<string>('', { nonNullable: true }),
  });

  search(): void {
    const v = this.searchForm.getRawValue();
    this.isLoading.set(true);
    this.service.search({ receiptCode: v.receiptCode || null, barcodeFrom: v.barcodeFrom || null, barcodeTo: v.barcodeTo || null }).subscribe({
      next: list => { this.labels.set(list); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }
  clear(): void { this.labels.set([]); }
  print(): void { if (typeof window !== 'undefined') window.print(); }
}
