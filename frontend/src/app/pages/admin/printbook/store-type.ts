import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { StoreTypeService } from '../../../services/printbook/store-type.service';
import { StoreType, StoreTypeFlatNode } from '../../../models/printbook/store-type';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';

@Component({
  selector: 'app-store-type',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule, TenantFilterSelectComponent],
  templateUrl: './store-type.html'
})
export class StoreTypePage implements OnInit, OnDestroy {
  private service   = inject(StoreTypeService);
  private translate = inject(TranslateService);
  private toastr    = inject(ToastrService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  loading           = signal(false);
  saving            = signal(false);
  showModal         = signal(false);
  showDeleteConfirm = signal(false);
  editMode          = signal(false);

  treeData:        StoreType[]             = [];
  flatTree:        StoreTypeFlatNode[]     = [];
  parentOptions:   { id: number; label: string }[] = [];
  searchText       = '';
  editingId:       number | null = null;
  editingPublicId: string | null = null;
  deletingNode:    StoreTypeFlatNode | null = null;

  dataForm = new FormGroup({
    parentId: new FormControl<number | null>(null),
    name:     new FormControl('', [Validators.required, Validators.maxLength(500)]),
  });

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onTenantChange(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getTree(this.tenantId).pipe(takeUntil(this.destroy$)).subscribe({
      next: tree => {
        this.treeData      = tree;
        this.flatTree      = this.flatten(tree);
        this.parentOptions = this.service.flattenForSelect(tree);
        this.loading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.loading.set(false);
      }
    });
  }

  flatten(nodes: StoreType[], depth = 0, parentVisible = true): StoreTypeFlatNode[] {
    const result: StoreTypeFlatNode[] = [];
    for (const node of nodes) {
      const isVisible = depth === 0 || parentVisible;
      const flat: StoreTypeFlatNode = { ...node, depth, visible: isVisible };
      result.push(flat);
      if (node.children?.length) {
        const childVisible = isVisible && !!node.expanded;
        result.push(...this.flatten(node.children, depth + 1, childVisible));
      }
    }
    return result;
  }

  toggleNode(node: StoreTypeFlatNode): void {
    node.expanded = !node.expanded;
    this.updateVisibility();
  }

  updateVisibility(): void {
    const isAncestorExpanded = (parentId: number | null | undefined): boolean => {
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
    this.flatTree.forEach(n => {
      n.expanded = false;
      n.visible = n.depth === 0;
    });
    this.flatTree = [...this.flatTree];
  }

  onSearch(): void {
    const q = this.searchText.toLowerCase().trim();
    if (!q) {
      this.flatTree = this.flatten(this.treeData);
      return;
    }
    const matchIds = new Set(
      this.flatTree.filter(n => (n.name ?? '').toLowerCase().includes(q)).map(n => n.id)
    );
    const addAncestors = (parentId: number | null | undefined) => {
      if (!parentId) return;
      matchIds.add(parentId);
      const parent = this.flatTree.find(n => n.id === parentId);
      if (parent) addAncestors(parent.parentId);
    };
    this.flatTree.filter(n => matchIds.has(n.id)).forEach(n => addAncestors(n.parentId));
    this.flatTree.forEach(n => {
      n.visible = matchIds.has(n.id);
      if (n.visible) n.expanded = true;
    });
    this.flatTree = [...this.flatTree];
  }

  openCreate(): void {
    this.editingId       = null;
    this.editingPublicId = null;
    this.editMode.set(false);
    this.dataForm.reset({ parentId: null });
    this.showModal.set(true);
  }

  openCreateChild(node: StoreTypeFlatNode): void {
    this.editingId       = null;
    this.editingPublicId = null;
    this.editMode.set(false);
    this.dataForm.reset({ parentId: node.id });
    this.showModal.set(true);
  }

  openEdit(node: StoreTypeFlatNode): void {
    this.editingId       = node.id;
    this.editingPublicId = node.publicId ?? null;
    this.editMode.set(true);
    this.dataForm.patchValue({
      parentId: node.parentId ?? null,
      name:     node.name ?? '',
    });
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    this.saving.set(true);
    const v = this.dataForm.value;
    const payload: Partial<StoreType> = {
      name:     v.name ?? '',
      parentId: v.parentId || null,
    };

    const req$ = this.editingPublicId !== null
      ? this.service.update(this.editingPublicId, payload)
      : this.service.create(payload);

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

  confirmDelete(node: StoreTypeFlatNode): void {
    this.deletingNode = node;
    this.showDeleteConfirm.set(true);
  }

  doDelete(): void {
    if (!this.deletingNode?.publicId) return;
    this.saving.set(true);
    this.service.delete(this.deletingNode.publicId).pipe(takeUntil(this.destroy$)).subscribe({
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
}
