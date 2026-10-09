import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { ToastrService } from '../../../services/shared/toastr.service';
import { NganhHocService, NganhHoc } from '../../../services/evaluate/nganh-hoc.service';
import { NganhHocReportService, NganhHocReportData } from '../../../services/evaluate/nganh-hoc-report.service';

@Component({
  selector: 'app-nganh-hoc-report',
  standalone: true,
  imports: [CommonModule, TranslateModule, MatIconModule],
  templateUrl: './nganh-hoc-report.html'
})
export class NganhHocReportPage implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private majorService = inject(NganhHocService);
  private reportService = inject(NganhHocReportService);
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);

  majorId = this.route.snapshot.paramMap.get('majorId') || '';
  major = signal<NganhHoc | null>(null);
  majorNumericId = signal<number | null>(null);
  report = signal<NganhHocReportData | null>(null);
  isLoading = signal<boolean>(false);
  isExporting = signal<string | null>(null);

  ngOnInit() {
    if (!this.majorId) { this.router.navigate(['/admin/subject-majors']); return; }
    this.majorService.getById(this.majorId).subscribe(m => {
      this.major.set(m);
      this.majorNumericId.set(Number(m.id));
      this.loadReport();
    });
  }

  loadReport() {
    const id = this.majorNumericId();
    if (id == null) return;
    this.isLoading.set(true);
    this.reportService.getSummary(id).subscribe(data => {
      this.report.set(data);
      this.isLoading.set(false);
      if (!data) this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
    });
  }

  rate(bucket: { total: number; available: number } | undefined): number {
    if (!bucket || bucket.total === 0) return 0;
    return Math.round((bucket.available / bucket.total) * 100);
  }

  goBack() {
    this.router.navigate(['/admin/subject-majors']);
  }

  private downloadBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  private doExport(kind: string, obs: import('rxjs').Observable<Blob>, filename: string) {
    const id = this.majorNumericId();
    if (id == null) return;
    this.isExporting.set(kind);
    obs.subscribe({
      next: blob => {
        this.isExporting.set(null);
        if (!blob || blob.size === 0) { this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR')); return; }
        this.downloadBlob(blob, filename);
      },
      error: () => { this.isExporting.set(null); this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR')); }
    });
  }

  exportByCourse() {
    const id = this.majorNumericId();
    if (id == null) return;
    this.doExport('by-course', this.reportService.exportByCourse(id), 'danh-sach-tai-lieu-theo-mon.xlsx');
  }

  exportAvailable() {
    const id = this.majorNumericId();
    if (id == null) return;
    this.doExport('available', this.reportService.exportAvailable(id), 'danh-sach-tai-lieu-da-lien-ket.xlsx');
  }

  exportNotAvailable() {
    const id = this.majorNumericId();
    if (id == null) return;
    this.doExport('not-available', this.reportService.exportNotAvailable(id), 'danh-sach-tai-lieu-chua-lien-ket.xlsx');
  }

  exportCourseList() {
    const id = this.majorNumericId();
    if (id == null) return;
    this.doExport('course-list', this.reportService.exportCourseList(id), 'danh-sach-mon-hoc.xlsx');
  }
}
