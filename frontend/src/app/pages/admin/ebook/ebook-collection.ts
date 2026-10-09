import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { EbookCollectionService } from '../../../services/ebook/collection.service';
import { EbookCollection, EbookCollectionFlatNode } from '../../../models/ebook/ebook-collection';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CollectionPermissionService, ReaderType, CollectionPermissionItem } from '../../../services/ebook/collection-permission.service';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

interface PermissionRow {
  readerTypeId: number;
  name: string;
  depth: number;
  read: boolean;
  download: boolean;
  maxdocument: number;
  offlineDays: number | null;
}

@Component({
  selector: 'app-ebook-collection',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule, NgSelectModule],
  templateUrl: './ebook-collection.html'
})
export class EbookCollectionPage implements OnInit, OnDestroy {
  private service     = inject(EbookCollectionService);
  private permService = inject(CollectionPermissionService);
  private translate   = inject(TranslateService);
  private toastr      = inject(ToastrService);
  private auth        = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$    = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  loading             = signal<boolean>(false);
  saving              = signal<boolean>(false);
  showModal           = signal<boolean>(false);
  showDeleteConfirm   = signal<boolean>(false);
  editMode            = signal<boolean>(false);
  showPermissionModal = signal<boolean>(false);
  loadingPermission   = signal<boolean>(false);
  savingPermission    = signal<boolean>(false);

  treeData:      EbookCollection[]         = [];
  flatTree:      EbookCollectionFlatNode[] = [];
  parentOptions: { id: number; label: string }[] = [];
  searchText     = '';
  editingId:        string | null = null;
  editingNumericId: number | null = null;
  deletingNode:     EbookCollectionFlatNode | null = null;
  permissionNode:   EbookCollectionFlatNode | null = null;
  permissionRows = signal<PermissionRow[]>([]);

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

  flatten(nodes: EbookCollection[], depth = 0, parentVisible = true): EbookCollectionFlatNode[] {
    const result: EbookCollectionFlatNode[] = [];
    for (const node of nodes) {
      const isVisible = depth === 0 || parentVisible;
      const flat: EbookCollectionFlatNode = { ...node, depth, visible: isVisible };
      result.push(flat);
      if (node.children?.length) {
        const childVisible = isVisible && !!node.expanded;
        result.push(...this.flatten(node.children, depth + 1, childVisible));
      }
    }
    return result;
  }

  toggleNode(node: EbookCollectionFlatNode): void {
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

  openCreateChild(node: EbookCollectionFlatNode): void {
    this.editingId = null;
    this.editingNumericId = null;
    this.editMode.set(false);
    this.dataForm.reset({ status: 1, order: 0, parentId: node.id, language: 'vi' });
    this.showModal.set(true);
  }

  openEdit(node: EbookCollectionFlatNode): void {
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

  private resolveStatus(node: EbookCollection): number {
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

  confirmDelete(node: EbookCollectionFlatNode): void {
    this.deletingNode = node;
    this.showDeleteConfirm.set(true);
  }

  hasBlockingChildren(): boolean {
    return !!(this.deletingNode?.hasChildren || this.deletingNode?.children?.length);
  }

  doDelete(): void {
    if (!this.deletingNode || this.hasBlockingChildren()) return;
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
      error: (err: any) => {
        this.saving.set(false);
        this.closeConfirm();
        this.toastr.error(err?.error?.message || this.translate.instant('COMMON.DELETE_ERROR'));
      }
    });
  }

  closeConfirm(): void {
    this.showDeleteConfirm.set(false);
    this.deletingNode = null;
  }

  getNodeId(node: EbookCollection): any {
    return node.publicId ?? node.id;
  }

  openPermission(node: EbookCollectionFlatNode): void {
    if (!node.publicId) return;
    this.permissionNode = node;
    this.permissionRows.set([]);
    this.showPermissionModal.set(true);
    this.loadingPermission.set(true);

    forkJoin({
      readerTypes: this.permService.getReaderTypes(),
      permissions: this.permService.getPermissions(node.publicId)
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: ({ readerTypes, permissions }) => {
        const rows = this.flattenReaderTypes(readerTypes);
        for (const row of rows) {
          const existing = permissions.find((p: CollectionPermissionItem) => p.readerTypeId === row.readerTypeId);
          if (existing) {
            row.read        = existing.read === 2;
            row.download    = existing.download === 2;
            row.maxdocument = existing.maxdocument ?? 0;
            row.offlineDays = existing.offlineDays ?? null;
          }
        }
        this.permissionRows.set(rows);
        this.loadingPermission.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COLLECTION_PERMISSION.LOAD_ERROR'));
        this.loadingPermission.set(false);
      }
    });
  }

  private flattenReaderTypes(types: ReaderType[], depth = 0): PermissionRow[] {
    const result: PermissionRow[] = [];
    for (const t of types) {
      result.push({ readerTypeId: t.id, name: t.name, depth, read: false, download: false, maxdocument: 0, offlineDays: null });
      if (t.children?.length) result.push(...this.flattenReaderTypes(t.children, depth + 1));
    }
    return result;
  }

  setPermRead(index: number, checked: boolean): void {
    this.permissionRows.update(rows => {
      const updated = [...rows];
      updated[index] = { ...updated[index], read: checked, download: checked ? updated[index].download : false };
      return updated;
    });
  }

  setPermDownload(index: number, checked: boolean): void {
    this.permissionRows.update(rows => {
      const updated = [...rows];
      updated[index] = { ...updated[index], download: checked, read: checked ? true : updated[index].read };
      return updated;
    });
  }

  setPermMaxdocument(index: number, value: string): void {
    const num = isNaN(Number(value)) ? 0 : Number(value);
    this.permissionRows.update(rows => {
      const updated = [...rows];
      updated[index] = { ...updated[index], maxdocument: num };
      return updated;
    });
  }

  setPermOfflineDays(index: number, value: string): void {
    const num = value.trim() === '' || isNaN(Number(value)) ? null : Number(value);
    this.permissionRows.update(rows => {
      const updated = [...rows];
      updated[index] = { ...updated[index], offlineDays: num };
      return updated;
    });
  }

  closePermission(): void {
    this.showPermissionModal.set(false);
    this.permissionNode = null;
    this.permissionRows.set([]);
  }

  savePermissions(): void {
    if (!this.permissionNode?.publicId) return;
    this.savingPermission.set(true);
    const payload: CollectionPermissionItem[] = this.permissionRows().map(row => ({
      readerTypeId: row.readerTypeId,
      read:         row.read ? 2 : 1,
      download:     row.download ? 2 : 1,
      maxdocument:  isNaN(Number(row.maxdocument)) ? 0 : Number(row.maxdocument),
      offlineDays:  row.offlineDays || null
    }));
    this.permService.savePermissions(this.permissionNode.publicId, payload)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.savingPermission.set(false);
          this.closePermission();
          this.toastr.success(this.translate.instant('COLLECTION_PERMISSION.SAVE_SUCCESS'));
        },
        error: () => {
          this.savingPermission.set(false);
        }
      });
  }
}
