import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { EbookSubjectService } from '../../../services/ebook/subject.service';
import { EbookSubject, EbookSubjectFlatNode } from '../../../models/ebook/ebook-subject';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-ebook-subject',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule, NgSelectModule],
  templateUrl: './ebook-subject.html'
})
export class EbookSubjectPage implements OnInit, OnDestroy {
  private service = inject(EbookSubjectService);
  private translate = inject(TranslateService);
  private toastr = inject(ToastrService);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$ = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  loading           = signal<boolean>(false);
  saving            = signal<boolean>(false);
  showModal         = signal<boolean>(false);
  showDeleteConfirm = signal<boolean>(false);
  editMode          = signal<boolean>(false);

  treeData:      EbookSubject[]         = [];
  flatTree:      EbookSubjectFlatNode[] = [];
  parentOptions: { id: number; label: string }[] = [];
  searchText     = '';
  editingId:        string | null = null;
  editingNumericId: number | null = null;
  deletingNode:     EbookSubjectFlatNode | null = null;

  dataForm = new FormGroup({
    parentId:        new FormControl<number | null>(null),
    name:            new FormControl('', [Validators.required, Validators.maxLength(500)]),
    link:            new FormControl(''),
    order:           new FormControl<number>(0),
    status:          new FormControl<number>(1),
    description:     new FormControl(''),
    pageTitle:       new FormControl(''),
    metaDescription: new FormControl(''),
    keyword:         new FormControl(''),
    language:        new FormControl('')
  });

  ngOnInit(): void {
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => this.load());
    if (this.isPrivileged) {
      this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
    }
    this.load();
  }

  onTenantChange(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  load(): void {
    this.loading.set(true);
    this.service.getTree('', '', this.tenantId).pipe(takeUntil(this.destroy$)).subscribe({
      next: tree => {
        this.treeData = tree;
        this.flatTree = this.flatten(tree);
        this.parentOptions = this.service.flattenForSelect(tree);
        this.loading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.loading.set(false);
      }
    });
  }

  flatten(nodes: EbookSubject[], depth = 0, parentVisible = true): EbookSubjectFlatNode[] {
    const result: EbookSubjectFlatNode[] = [];
    for (const node of nodes) {
      const isVisible = depth === 0 || parentVisible;
      const flat: EbookSubjectFlatNode = { ...node, depth, visible: isVisible };
      result.push(flat);
      if (node.children?.length) {
        const childVisible = isVisible && !!node.expanded;
        result.push(...this.flatten(node.children, depth + 1, childVisible));
      }
    }
    return result;
  }

  toggleNode(node: EbookSubjectFlatNode): void {
    node.expanded = !node.expanded;
    this.updateVisibility();
  }

  updateVisibility(): void {
    const isAncestorExpanded = (parentId: number | null): boolean => {
      if (!parentId) return true;
      const parent = this.flatTree.find(n => n.id === parentId);
      if (!parent) return false;
      return !!parent.expanded && isAncestorExpanded(parent.parentId);
    };
    this.flatTree.forEach(node => {
      node.visible = node.depth === 0 || isAncestorExpanded(node.parentId);
    });
    this.flatTree = [...this.flatTree];
  }

  expandAll(): void {
    this.flatTree.forEach(n => { n.expanded = true; n.visible = true; });
    this.flatTree = [...this.flatTree];
  }

  collapseAll(): void {
    this.flatTree.forEach(n => { n.expanded = false; n.visible = n.depth === 0; });
    this.flatTree = [...this.flatTree];
  }

  onSearch(): void {
    const q = this.searchText.toLowerCase().trim();
    if (!q) { this.flatTree = this.flatten(this.treeData); return; }

    const matchIds = new Set(
      this.flatTree.filter(n => n.name.toLowerCase().includes(q)).map(n => n.id)
    );
    const addAncestors = (parentId: number | null) => {
      if (!parentId) return;
      matchIds.add(parentId);
      const parent = this.flatTree.find(n => n.id === parentId);
      if (parent) addAncestors(parent.parentId);
    };
    this.flatTree.filter(n => matchIds.has(n.id)).forEach(n => addAncestors(n.parentId));
    this.flatTree.forEach(n => { n.visible = matchIds.has(n.id); if (n.visible) n.expanded = true; });
    this.flatTree = [...this.flatTree];
  }

  openCreate(): void {
    this.editingId = null;
    this.editingNumericId = null;
    this.editMode.set(false);
    this.dataForm.reset({ status: 1, order: 0, parentId: null, language: 'vi' });
    this.showModal.set(true);
  }

  openCreateChild(node: EbookSubjectFlatNode): void {
    this.editingId = null;
    this.editingNumericId = null;
    this.editMode.set(false);
    this.dataForm.reset({ status: 1, order: 0, parentId: node.id, language: 'vi' });
    this.showModal.set(true);
  }

  openEdit(node: EbookSubjectFlatNode): void {
    this.editingId = node.publicId ?? null;
    this.editingNumericId = node.id;
    this.editMode.set(true);
    this.dataForm.patchValue({
      parentId:        node.parentId,
      name:            node.name,
      link:            node.link || '',
      order:           node.order ?? 0,
      status:          this.resolveStatus(node),
      description:     node.description || '',
      pageTitle:       node.pageTitle || '',
      metaDescription: node.metaDescription || '',
      keyword:         node.keyword || '',
      language:        node.language || ''
    });
    this.showModal.set(true);
  }

  private resolveStatus(node: EbookSubject): number {
    const s = node.status;
    if (s === true || s === 2) return 1;
    if (s === false || s === 0) return 0;
    return Number(s) || 1;
  }

  closeModal(): void {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    this.saving.set(true);
    const v = this.dataForm.value;
    const payload: any = {
      Name:            v.name,
      ParentId:        v.parentId || 0,
      Status:          v.status === 1 ? 2 : 1,
      Link:            v.link,
      Order:           v.order,
      Description:     v.description,
      PageTitle:       v.pageTitle,
      MetaDescription: v.metaDescription,
      Keyword:         v.keyword,
      Language:        v.language
    };

    const req$ = this.editingId
      ? this.service.update(this.editingId, payload)
      : this.service.create(payload);

    req$.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeModal();
        this.load();
        this.toastr.success(this.translate.instant(this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      },
      error: () => {
        this.saving.set(false);
      }
    });
  }

  confirmDelete(node: EbookSubjectFlatNode): void {
    this.deletingNode = node;
    this.showDeleteConfirm.set(true);
  }

  doDelete(): void {
    if (!this.deletingNode) return;
    const publicId = this.deletingNode.publicId;
    if (!publicId) return;
    this.saving.set(true);
    this.service.delete(publicId).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeConfirm();
        this.load();
        this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
      },
      error: () => {
        this.saving.set(false);
      }
    });
  }

  closeConfirm(): void {
    this.showDeleteConfirm.set(false);
    this.deletingNode = null;
  }

  getNodeId(node: EbookSubject): any {
    return node.publicId ?? node.id;
  }
}
