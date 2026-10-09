import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { ModuleService, ModuleSyncDiffResult } from '../../../services/system/module.service';
import { AppModule, AppModuleFlatNode } from '../../../models/system/module';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CanDirective } from '../../../directives/can.directive';

@Component({
  selector: 'app-module-page',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule, NgSelectModule],
  templateUrl: './module.html'
})
export class ModulePage implements OnInit, OnDestroy {
  private service  = inject(ModuleService);
  private translate = inject(TranslateService);
  private toastr   = inject(ToastrService);
  private destroy$ = new Subject<void>();

  loading           = signal<boolean>(false);
  saving            = signal<boolean>(false);
  showModal         = signal<boolean>(false);
  showDeleteConfirm = signal<boolean>(false);
  editMode          = signal<boolean>(false);
  showSyncModal     = signal<boolean>(false);
  syncLoading       = signal<boolean>(false);
  syncApplying      = signal<boolean>(false);
  syncResult        = signal<ModuleSyncDiffResult | null>(null);

  treeData:      AppModule[]         = [];
  flatTree:      AppModuleFlatNode[] = [];
  parentOptions: { id: number; label: string }[] = [];
  searchText     = '';
  editingId:        string | null = null;
  editingNumericId: number | null = null;
  deletingNode:     AppModuleFlatNode | null = null;

  dataForm = new FormGroup({
    parentId:   new FormControl<number | null>(null),
    name:       new FormControl('', [Validators.required, Validators.maxLength(500)]),
    moduleCode: new FormControl(''),
    icon:       new FormControl(''),
    link:       new FormControl(''),
    sortOrder:  new FormControl<number>(0),
    status:     new FormControl<number>(1)
  });

  ngOnInit(): void {
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.load());
    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  load(): void {
    this.loading.set(true);
    this.service.getTree()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
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

  flatten(nodes: AppModule[], depth = 0, parentVisible = true): AppModuleFlatNode[] {
    const result: AppModuleFlatNode[] = [];
    for (const node of nodes) {
      const isVisible = depth === 0 || parentVisible;
      const flat: AppModuleFlatNode = { ...node, depth, visible: isVisible };
      result.push(flat);
      if (node.children?.length) {
        const childVisible = isVisible && !!node.expanded;
        result.push(...this.flatten(node.children, depth + 1, childVisible));
      }
    }
    return result;
  }

  toggleNode(node: AppModuleFlatNode): void {
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
      this.flatTree.filter(n => n.name.toLowerCase().includes(q)).map(n => n.id)
    );
    const addAncestors = (parentId: number | null) => {
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
    this.editingId = null;
    this.editingNumericId = null;
    this.editMode.set(false);
    this.dataForm.reset({ status: 1, sortOrder: 0, parentId: null });
    this.showModal.set(true);
  }

  openCreateChild(node: AppModuleFlatNode): void {
    this.editingId = null;
    this.editingNumericId = null;
    this.editMode.set(false);
    this.dataForm.reset({ status: 1, sortOrder: 0, parentId: node.id });
    this.showModal.set(true);
  }

  openEdit(node: AppModuleFlatNode): void {
    this.editingId = node.publicId ?? null;
    this.editingNumericId = node.id;
    this.editMode.set(true);
    this.dataForm.patchValue({
      parentId:   node.parentId,
      name:       node.name,
      moduleCode: node.moduleCode || '',
      icon:       node.icon || '',
      link:       node.link || '',
      sortOrder:  node.sortOrder ?? 0,
      status:     this.resolveStatus(node)
    });
    this.showModal.set(true);
  }

  private resolveStatus(node: AppModule): number {
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
    if (this.dataForm.invalid) {
      this.dataForm.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const v = this.dataForm.value;
    const payload: any = {
      Name:       v.name,
      ParentId:   v.parentId || 0,
      ModuleCode: v.moduleCode || '',
      Icon:       v.icon || '',
      Link:       v.link || '',
      SortOrder:  v.sortOrder ?? 0,
      Status:     v.status === 1 ? 2 : 1
    };

    const req$ = this.editingId
      ? this.service.update(this.editingId, payload)
      : this.service.create(payload);

    req$.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeModal();
        this.load();
        this.toastr.success(this.translate.instant(
          this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'
        ));
      },
      error: () => {
        this.saving.set(false);
      }
    });
  }

  confirmDelete(node: AppModuleFlatNode): void {
    this.deletingNode = node;
    this.showDeleteConfirm.set(true);
  }

  doDelete(): void {
    if (!this.deletingNode?.publicId) return;
    this.saving.set(true);
    this.service.delete(this.deletingNode.publicId).subscribe({
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

  getNodeId(node: AppModule): any {
    return node.publicId ?? node.id;
  }

  // ─── Đồng bộ Module (so với menu.ts qua ModuleCatalog phía backend) ────────

  openSync(): void {
    this.syncResult.set(null);
    this.showSyncModal.set(true);
    this.syncLoading.set(true);
    this.service.syncDiff().subscribe({
      next: result => {
        this.syncResult.set(result);
        this.syncLoading.set(false);
      },
      error: () => {
        this.syncLoading.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.showSyncModal.set(false);
      }
    });
  }

  applySync(): void {
    this.syncApplying.set(true);
    this.service.syncApply().subscribe({
      next: result => {
        this.syncApplying.set(false);
        this.closeSyncModal();
        this.load();
        this.toastr.success(
          result.created > 0
            ? this.translate.instant('MODULE.SYNC_APPLY_SUCCESS', { count: result.created })
            : this.translate.instant('MODULE.SYNC_APPLY_NONE')
        );
      },
      error: () => {
        this.syncApplying.set(false);
      }
    });
  }

  closeSyncModal(): void {
    this.showSyncModal.set(false);
    this.syncResult.set(null);
  }
}
