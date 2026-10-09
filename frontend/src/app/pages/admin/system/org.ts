import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { OrgService } from '../../../services/system/org.service';
import { OrgNode, OrgFlatNode } from '../../../models/system/org';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-org',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule, NgSelectModule],
  templateUrl: './org.html'
})
export class OrgPage implements OnInit, OnDestroy {
  private svc       = inject(OrgService);
  private translate = inject(TranslateService);
  private toastr    = inject(ToastrService);
  private auth      = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$  = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  loading           = signal(false);
  saving            = signal(false);
  showModal         = signal(false);
  showDeleteConfirm = signal(false);
  editMode          = signal(false);

  treeData:      OrgNode[]     = [];
  flatTree:      OrgFlatNode[] = [];
  parentOptions: { id: number; label: string }[] = [];
  searchText     = '';
  editingId:        string | number | null = null;
  editingNumericId: number | null = null;
  deletingNode:     OrgFlatNode | null = null;

  dataForm = new FormGroup({
    parentId:    new FormControl<number | null>(null),
    name:        new FormControl('', [Validators.required, Validators.maxLength(500)]),
    code:        new FormControl(''),
    order:       new FormControl<number>(0),
    status:      new FormControl<number>(1),
    description: new FormControl(''),
  });

  ngOnInit(): void {
    if (this.isPrivileged) {
      this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
    }
    this.load();
  }

  onTenantChange(): void { this.load(); }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  load(): void {
    this.loading.set(true);
    this.svc.getTree(this.searchText, this.tenantId).pipe(takeUntil(this.destroy$)).subscribe({
      next: tree => {
        this.treeData      = tree;
        this.flatTree      = this.flatten(tree);
        this.parentOptions = this.svc.flattenForSelect(tree);
        this.loading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.loading.set(false);
      }
    });
  }

  flatten(nodes: OrgNode[], depth = 0, parentVisible = true): OrgFlatNode[] {
    const result: OrgFlatNode[] = [];
    for (const node of nodes) {
      const isVisible = depth === 0 || parentVisible;
      result.push({ ...node, depth, visible: isVisible });
      if (node.children?.length) {
        result.push(...this.flatten(node.children, depth + 1, isVisible && !!node.expanded));
      }
    }
    return result;
  }

  toggleNode(node: OrgFlatNode): void {
    node.expanded = !node.expanded;
    this.updateVisibility();
  }

  updateVisibility(): void {
    const expanded = (parentId: number | null): boolean => {
      if (!parentId) return true;
      const p = this.flatTree.find(n => n.id === parentId);
      return !!p && !!p.expanded && expanded(p.parentId);
    };
    this.flatTree.forEach(n => { n.visible = n.depth === 0 || expanded(n.parentId); });
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
    const matchIds = new Set(this.flatTree.filter(n => n.name.toLowerCase().includes(q)).map(n => n.id));
    const addAncestors = (parentId: number | null) => {
      if (!parentId) return;
      matchIds.add(parentId);
      const p = this.flatTree.find(n => n.id === parentId);
      if (p) addAncestors(p.parentId);
    };
    this.flatTree.filter(n => matchIds.has(n.id)).forEach(n => addAncestors(n.parentId));
    this.flatTree.forEach(n => { n.visible = matchIds.has(n.id); if (n.visible) n.expanded = true; });
    this.flatTree = [...this.flatTree];
  }

  openCreate(): void {
    this.editingId = null;
    this.editingNumericId = null;
    this.editMode.set(false);
    this.dataForm.reset({ status: 1, order: 0, parentId: null });
    this.showModal.set(true);
  }

  openCreateChild(node: OrgFlatNode): void {
    this.editingId = null;
    this.editingNumericId = null;
    this.editMode.set(false);
    this.dataForm.reset({ status: 1, order: 0, parentId: node.id });
    this.showModal.set(true);
  }

  openEdit(node: OrgFlatNode): void {
    this.editingId        = node.publicId ?? node.id;
    this.editingNumericId = node.id;
    this.editMode.set(true);
    this.dataForm.patchValue({
      parentId:    node.parentId,
      name:        node.name,
      code:        node.code || '',
      order:       node.order ?? 0,
      status:      node.status ?? 1,
      description: node.description || '',
    });
    this.showModal.set(true);
  }

  closeModal(): void { this.showModal.set(false); this.dataForm.reset(); }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    this.saving.set(true);
    const v = this.dataForm.value;
    const payload: any = {
      Name:        v.name,
      ParentId:    v.parentId ?? 0,
      Code:        v.code || '',
      Order:       v.order ?? 0,
      Status:      v.status ?? 1,
      Description: v.description || '',
    };

    const req$ = this.editingId !== null
      ? this.svc.update(this.editingId, payload)
      : this.svc.create(payload);

    req$.pipe(takeUntil(this.destroy$)).subscribe({
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

  confirmDelete(node: OrgFlatNode): void {
    this.deletingNode = node;
    this.showDeleteConfirm.set(true);
  }

  doDelete(): void {
    if (!this.deletingNode) return;
    const id = this.deletingNode.publicId ?? this.deletingNode.id;
    this.saving.set(true);
    this.svc.delete(id).pipe(takeUntil(this.destroy$)).subscribe({
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

  closeConfirm(): void { this.showDeleteConfirm.set(false); this.deletingNode = null; }

  getNodeId(node: OrgNode): any { return node.publicId ?? node.id; }
}
