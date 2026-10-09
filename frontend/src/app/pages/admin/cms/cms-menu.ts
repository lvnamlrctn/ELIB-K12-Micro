import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { CmsMenuService } from '../../../services/cms/cms-menu.service';
import { CategoryService } from '../../../services/cms/category.service';
import { EbookCollectionService } from '../../../services/ebook/collection.service';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CmsMenu, CmsMenuFlatNode } from '../../../models/cms/cms-menu';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-cms-menu',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule, NgSelectModule],
  templateUrl: './cms-menu.html'
})
export class CmsMenuPage implements OnInit, OnDestroy {
  private service    = inject(CmsMenuService);
  private catSvc     = inject(CategoryService);
  private collSvc    = inject(EbookCollectionService);
  private translate  = inject(TranslateService);
  private toastr     = inject(ToastrService);
  private route      = inject(ActivatedRoute);
  private router     = inject(Router);
  private auth       = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$   = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  loading           = signal<boolean>(false);
  saving            = signal<boolean>(false);
  showModal         = signal<boolean>(false);
  showDeleteConfirm = signal<boolean>(false);
  editMode          = signal<boolean>(false);

  currentMenuTypePublicId = '';
  currentMenuTypeId       = 0;
  currentMenuTypeName     = '';

  treeData:      CmsMenu[]         = [];
  flatTree:      CmsMenuFlatNode[] = [];
  parentOptions: { id: number; label: string }[] = [];

  menuTypeOptions:  { id: number; publicId: string; name: string; code: string }[] = [];
  categoryOptions:  { id: string; label: string }[] = [];
  collectionOptions:{ id: string; label: string }[] = [];
  pageOptions:      { id: string; name: string }[]  = [];

  searchText       = '';
  editingId:        string | null = null;
  editingNumericId: number | null = null;
  deletingNode:     CmsMenuFlatNode | null = null;

  dataForm = new FormGroup({
    parentId:  new FormControl<number | null>(null),
    name:      new FormControl('', [Validators.required]),
    menuType:  new FormControl<number | null>(null),
    linkType:  new FormControl<string>('external'),
    subId:     new FormControl<string | null>(null),
    link:      new FormControl(''),
    friendUrl: new FormControl(''),
    sortOrder: new FormControl<number>(0),
    status:    new FormControl<boolean>(true),
    openType:  new FormControl<string>('_self'),
    isLogIn:   new FormControl<boolean>(false),
    icon:      new FormControl('')
  });

  get linkType(): string {
    return this.dataForm.get('linkType')?.value || 'external';
  }

  ngOnInit(): void {
    this.currentMenuTypePublicId = this.route.snapshot.paramMap.get('menuTypeId') || '';

    this.service.getMenuTypes().pipe(takeUntil(this.destroy$)).subscribe(types => {
      this.menuTypeOptions = types;
      const found = types.find(t => t.publicId === this.currentMenuTypePublicId);
      this.currentMenuTypeName = found?.name || '';
      this.currentMenuTypeId   = found?.id ?? 0;
    });

    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => this.load());
    if (this.isPrivileged) {
      this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
    }
    this.loadOptions();
    this.load();
  }

  onTenantChange(): void {
    this.load();
  }

  goBack(): void {
    this.router.navigate(['/admin/menu-types']);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadOptions(): void {
    // Category/Collection: SubId cần publicId (backend MenuRequest chỉ có SubId) → flatten theo publicId.
    this.catSvc.getTree().pipe(takeUntil(this.destroy$)).subscribe(tree => {
      this.categoryOptions = this.flattenPublic(tree);
    });
    this.collSvc.getTree().pipe(takeUntil(this.destroy$)).subscribe(tree => {
      this.collectionOptions = this.flattenPublic(tree);
    });
    this.service.getPages().pipe(takeUntil(this.destroy$)).subscribe(p => this.pageOptions = p);
  }

  // Flatten cây (Category/Collection) thành option dùng publicId làm value cho SubId.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private flattenPublic(nodes: any[], depth = 0): { id: string; label: string }[] {
    const out: { id: string; label: string }[] = [];
    for (const n of nodes) {
      out.push({ id: String(n.publicId ?? n.id), label: '——'.repeat(depth) + (n.name || '') });
      if (n.children?.length) out.push(...this.flattenPublic(n.children, depth + 1));
    }
    return out;
  }

  load(): void {
    this.loading.set(true);
    this.service.getTree(this.currentMenuTypePublicId, this.tenantId).pipe(takeUntil(this.destroy$)).subscribe({
      next: tree => {
        this.treeData     = tree;
        this.flatTree     = this.flatten(tree);
        this.parentOptions = this.service.flattenForSelect(tree);
        this.loading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.loading.set(false);
      }
    });
  }

  flatten(nodes: CmsMenu[], depth = 0, parentVisible = true): CmsMenuFlatNode[] {
    const result: CmsMenuFlatNode[] = [];
    for (const node of nodes) {
      const isVisible = depth === 0 || parentVisible;
      const flat: CmsMenuFlatNode = { ...node, depth, visible: isVisible };
      result.push(flat);
      if (node.children?.length) {
        result.push(...this.flatten(node.children, depth + 1, isVisible && !!node.expanded));
      }
    }
    return result;
  }

  toggleNode(node: CmsMenuFlatNode): void {
    node.expanded = !node.expanded;
    this.updateVisibility();
  }

  updateVisibility(): void {
    const expanded = (parentId: number | null | undefined): boolean => {
      if (!parentId) return true;
      const p = this.flatTree.find(n => n.id === parentId);
      return !!p?.expanded && expanded(p.parentId);
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
    const matchIds = new Set(
      this.flatTree.filter(n => (n.name || '').toLowerCase().includes(q)).map(n => n.id)
    );
    const addAncestors = (pid: number | null | undefined) => {
      if (!pid) return;
      matchIds.add(pid);
      addAncestors(this.flatTree.find(n => n.id === pid)?.parentId);
    };
    this.flatTree.filter(n => matchIds.has(n.id)).forEach(n => addAncestors(n.parentId));
    this.flatTree.forEach(n => { n.visible = matchIds.has(n.id); if (n.visible) n.expanded = true; });
    this.flatTree = [...this.flatTree];
  }

  openCreate(): void {
    this.editingId = null;
    this.editingNumericId = null;
    this.editMode.set(false);
    this.dataForm.reset({ status: true, sortOrder: 0, parentId: null, linkType: 'external', openType: '_self', isLogIn: false, menuType: this.currentMenuTypeId || null });
    this.showModal.set(true);
  }

  openCreateChild(node: CmsMenuFlatNode): void {
    this.editingId = null;
    this.editingNumericId = null;
    this.editMode.set(false);
    this.dataForm.reset({ status: true, sortOrder: 0, parentId: node.id, linkType: 'external', openType: '_self', isLogIn: false, menuType: this.currentMenuTypeId || null });
    this.showModal.set(true);
  }

  openEdit(node: CmsMenuFlatNode): void {
    this.editingId       = node.publicId ?? null;
    this.editingNumericId = node.id;
    this.editMode.set(true);
    this.dataForm.patchValue({
      parentId:  node.parentId ?? null,
      name:      node.name || '',
      menuType:  node.menuType ?? null,
      linkType:  node.linkType || 'external',
      subId:     node.subId || null,
      link:      node.link || '',
      friendUrl: node.friendUrl || '',
      sortOrder: node.sortOrder ?? 0,
      status:    (node.status ?? 1) === 2,
      openType:  node.openType || '_self',
      isLogIn:   (node.isLogIn ?? 0) === 1,
      icon:      node.icon || ''
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
    const v = this.dataForm.getRawValue();
    const payload: any = {
      Name:      v.name,
      ParentId:  v.parentId ?? 0,
      MenuType:  v.menuType ?? 0,
      LinkType:  v.linkType || 'external',
      SubId:     v.subId ?? '',
      Link:      v.link || '',
      FriendUrl: v.friendUrl || '',
      SortOrder: v.sortOrder ?? 0,
      Status:    v.status ? 2 : 1,
      OpenType:  v.openType || '_self',
      IsLogIn:   v.isLogIn ? 1 : 0,
      Icon:      v.icon || ''
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

  confirmDelete(node: CmsMenuFlatNode): void {
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

  getMenuTypeName(id: number | null | undefined): string {
    if (!id) return '—';
    return this.menuTypeOptions.find(t => t.id === id)?.name || String(id);
  }

  getNodeId(node: CmsMenu): any {
    return node.publicId ?? node.id;
  }
}
