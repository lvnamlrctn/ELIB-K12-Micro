import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { Z3950SearchService } from '../../../services/cataloging/z3950-search.service';
import { Z3950GroupService } from '../../../services/acquisition/z3950-group.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { WorkSheetService } from '../../../services/cataloging/worksheet.service';
import { Z3950ResultItem } from '../../../models/cataloging/z3950-result';
import { CanDirective } from '../../../directives/can.directive';

@Component({
  selector: 'app-z3950-search',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule],
  templateUrl: './z3950-search.html'
})
export class Z3950SearchPage implements OnInit, OnDestroy {
  private service    = inject(Z3950SearchService);
  private groupSvc   = inject(Z3950GroupService);
  private bibTypeSvc = inject(BibTypeService);
  private worksheetSvc = inject(WorkSheetService);
  private router     = inject(Router);
  private toastr     = inject(ToastrService);
  public  translate  = inject(TranslateService);
  private destroy$   = new Subject<void>();

  displayedColumns = ['stt', 'title', 'author', 'publisher', 'year', 'source', 'actions'];
  dataSource: Z3950ResultItem[] = [];
  groups     = signal<{ id: number; name: string }[]>([]);
  bibTypes   = signal<{ id: number; name: string }[]>([]);
  worksheets = signal<{ id: number; name: string }[]>([]);
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);
  hasSearched = signal(false);
  // Server cấu hình kết nối/tìm kiếm thất bại — backend trả sẵn theo từng server, trước đây bị bỏ qua (chỉ thấy "0 kết quả").
  searchErrors = signal<{ configName: string; error: string }[]>([]);

  // mục tiêu nhập biểu ghi
  importBibTypeId = signal<number | null>(null);
  importWorksheetId = signal<number | null>(null);
  importingId = signal<string | null>(null);

  // xem MARC
  showMarc = signal(false);
  marcText = signal<string>('');
  marcLoading = signal(false);

  searchForm = new FormGroup({
    title:     new FormControl<string>('', { nonNullable: true }),
    author:    new FormControl<string>('', { nonNullable: true }),
    isbn:      new FormControl<string>('', { nonNullable: true }),
    issn:      new FormControl<string>('', { nonNullable: true }),
    publisher: new FormControl<string>('', { nonNullable: true }),
    keyword:   new FormControl<string>('', { nonNullable: true }),
    groupId:   new FormControl<number | null>(null),
  });

  ngOnInit(): void { this.loadDropdowns(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.runSearch(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadDropdowns(): void {
    this.groupSvc.getAll({ draw: 0, start: 0, length: 1000, search: { value: '' } }).pipe(takeUntil(this.destroy$)).subscribe({ next: res => this.groups.set((res.data || []).map(g => ({ id: Number(g.id), name: g.name }))), error: () => {} });
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => { const bt = (list || []).map(t => ({ id: Number(t.id), name: t.name || '' })); this.bibTypes.set(bt); if (bt.length && this.importBibTypeId() == null) this.onBibTypeChange(bt[0].id); }, error: () => {} });
  }
  onBibTypeChange(id: number | null): void {
    this.importBibTypeId.set(id); this.importWorksheetId.set(null); this.worksheets.set([]);
    if (!id) return;
    this.worksheetSvc.search({ bibTypeId: id, pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { const ws = (res.data || []).map(w => ({ id: Number(w.id), name: w.name || '' })); this.worksheets.set(ws); if (ws.length) this.importWorksheetId.set(ws[0].id); }, error: () => {}
    });
  }

  hasQuery(): boolean { const v = this.searchForm.getRawValue(); return !!(v.title || v.author || v.isbn || v.issn || v.publisher || v.keyword); }

  runSearch(): void {
    if (!this.hasQuery()) { this.toastr.error(this.translate.instant('Z3950_SEARCH.NEED_QUERY')); return; }
    const v = this.searchForm.getRawValue();
    this.isLoading.set(true); this.hasSearched.set(true); this.searchErrors.set([]);
    this.service.search({ title: v.title || null, author: v.author || null, isbn: v.isbn || null, issn: v.issn || null, publisher: v.publisher || null, keyword: v.keyword || null, groupId: v.groupId, pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.searchErrors.set(res.errors ?? []); this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('Z3950_SEARCH.SEARCH_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.runSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  viewMarc(row: Z3950ResultItem): void {
    this.showMarc.set(true); this.marcLoading.set(true); this.marcText.set('');
    this.service.getMarc(row.id, row.configId).pipe(takeUntil(this.destroy$)).subscribe({
      next: marc => { this.marcText.set(marc || this.translate.instant('Z3950_SEARCH.NO_MARC')); this.marcLoading.set(false); },
      error: () => { this.marcText.set(this.translate.instant('Z3950_SEARCH.NO_MARC')); this.marcLoading.set(false); }
    });
  }
  closeMarc(): void { this.showMarc.set(false); }

  importRow(row: Z3950ResultItem): void {
    const bibTypeId = this.importBibTypeId();
    if (!bibTypeId) { this.toastr.error(this.translate.instant('Z3950_SEARCH.NEED_BIB_TYPE')); return; }
    this.importingId.set(row.id);
    this.service.import({ resultId: row.id, configId: row.configId, bibTypeId, worksheetId: this.importWorksheetId() }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.importingId.set(null);
        this.toastr.success(this.translate.instant('Z3950_SEARCH.IMPORT_OK'));
        if (res.mfn != null) this.router.navigate(['/admin/catalog-bibs/edit', res.mfn]);
      },
      error: () => { this.importingId.set(null); this.toastr.error(this.translate.instant('Z3950_SEARCH.IMPORT_ERROR')); }
    });
  }
}
