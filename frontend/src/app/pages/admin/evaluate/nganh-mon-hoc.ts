import { Component, inject, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { SelectionModel } from '@angular/cdk/collections';
import { forkJoin } from 'rxjs';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { ToastrService } from '../../../services/shared/toastr.service';
import { NganhMonHocService, NganhMonHoc } from '../../../services/evaluate/nganh-mon-hoc.service';
import { NganhHocService, NganhHoc } from '../../../services/evaluate/nganh-hoc.service';
import { MonHocService, MonHoc } from '../../../services/evaluate/mon-hoc.service';
import { EvaluateDegreeService } from '../../../services/evaluate/evaluate-degree.service';
import { KnowledgeService } from '../../../services/evaluate/knowledge.service';
import { CourseOptionService } from '../../../services/evaluate/course-option.service';
import { BaseEntity } from '../../../models/shared/base-entity';
import { CanDirective } from '../../../directives/can.directive';

interface SubjectRow {
  link: NganhMonHoc;
  subject: MonHoc | undefined;
}

@Component({
  selector: 'app-nganh-mon-hoc',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule, CanDirective, NgSelectModule],
  templateUrl: './nganh-mon-hoc.html'
})
export class NganhMonHocPage implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private linkService = inject(NganhMonHocService);
  private majorService = inject(NganhHocService);
  private subjectService = inject(MonHocService);
  private degreeService = inject(EvaluateDegreeService);
  private knowledgeService = inject(KnowledgeService);
  private optionService = inject(CourseOptionService);
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);

  majorId = this.route.snapshot.paramMap.get('majorId') || '';
  major = signal<NganhHoc | null>(null);
  /** Id số nguyên thật của ngành (khác `majorId` — route param là publicId GUID) — dùng làm khóa ngoại khi thêm/tìm NganhMonHoc. */
  majorNumericId = signal<number | null>(null);
  isLoading = signal<boolean>(false);
  rows = signal<SubjectRow[]>([]);
  allSubjects = signal<MonHoc[]>([]);

  degreeOptions: BaseEntity[] = [];
  knowledgeOptions: BaseEntity[] = [];
  courseOptionOptions: BaseEntity[] = [];

  // ── Modal "Thêm môn học" ─────────────────────────────────────────────────
  showAddModal = signal<boolean>(false);
  addSelection = new SelectionModel<MonHoc>(true, []);
  filterKnowledgeId: number | null = null;
  filterDegreeId: number | null = null;
  filterOptionId: number | null = null;
  filterMaMon = '';
  filterTenMon = '';
  addPageIndex = signal<number>(0);
  addPageSize = 10;

  ngOnInit() {
    if (!this.majorId) { this.router.navigate(['/admin/subject-majors']); return; }
    this.majorService.getById(this.majorId).subscribe(m => {
      this.major.set(m);
      this.majorNumericId.set(Number(m.id));
      this.loadAll();
    });
    this.degreeService.searchAll().subscribe(list => this.degreeOptions = list);
    this.knowledgeService.searchAll().subscribe(list => this.knowledgeOptions = list);
    this.optionService.searchAll().subscribe(list => this.courseOptionOptions = list);
  }

  loadAll() {
    this.isLoading.set(true);
    forkJoin({
      links: this.linkService.listByMajor(this.majorNumericId() ?? 0),
      subjects: this.subjectService.searchAll()
    }).subscribe(({ links, subjects }) => {
      this.allSubjects.set(subjects);
      const bySubjectId = new Map(subjects.map(s => [Number(s.id), s]));
      this.rows.set(links.map(link => ({ link, subject: bySubjectId.get(Number(link.monHocId)) })));
      this.isLoading.set(false);
    });
  }

  removeSubject(row: SubjectRow) {
    const publicId = row.link.publicId || row.link.id;
    if (!publicId) return;
    this.linkService.unlink(publicId).subscribe(ok => {
      if (ok) {
        this.loadAll();
        this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
      }
    });
  }

  goBack() {
    this.router.navigate(['/admin/subject-majors']);
  }

  // ── Modal "Thêm môn học" ─────────────────────────────────────────────────
  openAddModal() {
    this.addSelection.clear();
    this.filterKnowledgeId = null;
    this.filterDegreeId = null;
    this.filterOptionId = null;
    this.filterMaMon = '';
    this.filterTenMon = '';
    this.addPageIndex.set(0);
    this.showAddModal.set(true);
  }

  closeAddModal() {
    this.showAddModal.set(false);
  }

  triggerAddSearch() {
    this.addPageIndex.set(0);
  }

  filteredCandidates = computed<MonHoc[]>(() => {
    const assignedIds = new Set(this.rows().map(r => Number(r.link.monHocId)));
    const kw = this.filterMaMon.trim().toLowerCase();
    const tw = this.filterTenMon.trim().toLowerCase();
    return this.allSubjects().filter(s => {
      if (assignedIds.has(Number(s.id))) return false;
      if (this.filterKnowledgeId != null && Number(s.knowledgeId) !== Number(this.filterKnowledgeId)) return false;
      if (this.filterDegreeId != null && Number(s.degreeId) !== Number(this.filterDegreeId)) return false;
      if (this.filterOptionId != null && Number(s.optionId) !== Number(this.filterOptionId)) return false;
      if (kw && !(s.maMon || '').toLowerCase().includes(kw)) return false;
      if (tw && !(s.tenMon || '').toLowerCase().includes(tw)) return false;
      return true;
    });
  });

  totalAddPages = computed<number>(() => Math.max(1, Math.ceil(this.filteredCandidates().length / this.addPageSize)));

  pagedCandidates = computed<MonHoc[]>(() => {
    const effectiveIndex = Math.min(this.addPageIndex(), this.totalAddPages() - 1);
    const start = effectiveIndex * this.addPageSize;
    return this.filteredCandidates().slice(start, start + this.addPageSize);
  });

  isAllAddSelected(): boolean {
    const page = this.pagedCandidates();
    return page.length > 0 && page.every(s => this.addSelection.isSelected(s));
  }

  toggleAllAddRows() {
    const page = this.pagedCandidates();
    if (this.isAllAddSelected()) { page.forEach(s => this.addSelection.deselect(s)); return; }
    page.forEach(s => this.addSelection.select(s));
  }

  goFirstAddPage() { this.addPageIndex.set(0); }
  goPrevAddPage() { this.addPageIndex.update(i => Math.max(0, i - 1)); }
  goNextAddPage() { this.addPageIndex.update(i => Math.min(this.totalAddPages() - 1, i + 1)); }
  goLastAddPage() { this.addPageIndex.set(this.totalAddPages() - 1); }

  addSelectedSubjects(closeAfter: boolean) {
    const selected = this.addSelection.selected;
    if (!selected.length) {
      if (closeAfter) this.closeAddModal();
      return;
    }
    const requests = selected.map(s => this.linkService.link(this.majorNumericId() ?? 0, s.id!));
    forkJoin(requests).subscribe(() => {
      this.addSelection.clear();
      this.loadAll();
      this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
      if (closeAfter) this.closeAddModal();
    });
  }
}
