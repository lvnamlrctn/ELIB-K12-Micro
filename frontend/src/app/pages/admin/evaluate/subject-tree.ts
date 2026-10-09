import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { SubjectTreeService } from '../../../services/evaluate/subject-tree.service';
import { SubjectTreeNode, SubjectTreeFlatNode } from '../../../models/evaluate/subject-tree';
import { ToastrService } from '../../../services/shared/toastr.service';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';

// Đợt 8 — Cây cơ sở môn học: Org (Đơn vị) → Chương trình → Ngành → Môn học → Tài liệu, đọc-only.
// Mọi thao tác thêm/sửa/xoá thực hiện ở màn hình CRUD tương ứng (nút "Mở" điều hướng sang đó).
const NODE_ICON: Record<string, string> = {
  donvi: 'corporate_fare', program: 'assignment', nganh: 'account_tree', monhoc: 'menu_book', tailieu: 'description'
};
const NODE_ROUTE: Record<string, string> = {
  donvi: '/admin/subject-units', program: '/admin/subject-programs', nganh: '/admin/subject-majors',
  monhoc: '/admin/subjects', tailieu: '/admin/subjects'
};

@Component({
  selector: 'app-subject-tree',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule, TenantFilterSelectComponent],
  templateUrl: './subject-tree.html'
})
export class SubjectTreePage implements OnInit, OnDestroy {
  private service  = inject(SubjectTreeService);
  private router   = inject(Router);
  public  translate = inject(TranslateService);
  private toastr   = inject(ToastrService);
  private auth     = inject(Auth);
  private destroy$ = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  loading = signal(false);
  treeData: SubjectTreeNode[] = [];
  flatTree: SubjectTreeFlatNode[] = [];
  searchText = '';

  ngOnInit(): void {
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {});
    this.load();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  onTenantChange(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getFullTree(this.tenantId).pipe(takeUntil(this.destroy$)).subscribe({
      next: tree => { this.treeData = tree; this.flatTree = this.flatten(tree); this.loading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.loading.set(false); }
    });
  }

  private flatten(nodes: SubjectTreeNode[], depth = 0, parentVisible = true, parentExpanded = true): SubjectTreeFlatNode[] {
    const result: SubjectTreeFlatNode[] = [];
    for (const node of nodes) {
      const isVisible = depth === 0 || (parentVisible && parentExpanded);
      const flat: SubjectTreeFlatNode = { ...node, depth, visible: isVisible, expanded: depth === 0 };
      result.push(flat);
      if (node.children?.length) result.push(...this.flatten(node.children, depth + 1, isVisible, flat.expanded));
    }
    return result;
  }

  toggleNode(node: SubjectTreeFlatNode): void {
    node.expanded = !node.expanded;
    this.updateVisibility();
  }

  private updateVisibility(): void {
    // Đơn giản hơn module.html (không cần tra ngược cha theo id) — dùng lại chính flatten() với trạng thái
    // expanded hiện có, vì cây gốc từ backend đã nested sẵn (children), không cần tái tạo từ danh sách phẳng.
    const expandedIds = new Set(this.flatTree.filter(n => n.expanded).map(n => `${n.nodeType}:${n.id}`));
    const rebuild = (nodes: SubjectTreeNode[], depth: number, parentVisible: boolean, parentExpanded: boolean): SubjectTreeFlatNode[] => {
      const result: SubjectTreeFlatNode[] = [];
      for (const node of nodes) {
        const isVisible = depth === 0 || (parentVisible && parentExpanded);
        const key = `${node.nodeType}:${node.id}`;
        const expanded = depth === 0 ? true : expandedIds.has(key);
        const flat: SubjectTreeFlatNode = { ...node, depth, visible: isVisible, expanded };
        result.push(flat);
        if (node.children?.length) result.push(...rebuild(node.children, depth + 1, isVisible, expanded));
      }
      return result;
    };
    this.flatTree = rebuild(this.treeData, 0, true, true);
  }

  expandAll(): void { this.flatTree.forEach(n => n.expanded = true); this.flatTree = [...this.flatTree]; this.flatTree.forEach(n => n.visible = true); }
  collapseAll(): void { this.flatTree = this.flatten(this.treeData); this.flatTree.forEach(n => { n.expanded = n.depth === 0; }); this.updateVisibility(); }

  onSearch(): void {
    const q = this.searchText.toLowerCase().trim();
    if (!q) { this.flatTree = this.flatten(this.treeData); return; }

    const matchKeys = new Set<string>();
    const walk = (nodes: SubjectTreeNode[], ancestors: string[]) => {
      for (const node of nodes) {
        const key = `${node.nodeType}:${node.id}`;
        if ((node.name || '').toLowerCase().includes(q)) { matchKeys.add(key); ancestors.forEach(a => matchKeys.add(a)); }
        if (node.children?.length) walk(node.children, [...ancestors, key]);
      }
    };
    walk(this.treeData, []);

    const expandedIds = matchKeys;
    const rebuild = (nodes: SubjectTreeNode[], depth: number): SubjectTreeFlatNode[] => {
      const result: SubjectTreeFlatNode[] = [];
      for (const node of nodes) {
        const key = `${node.nodeType}:${node.id}`;
        if (!matchKeys.has(key)) continue;
        const flat: SubjectTreeFlatNode = { ...node, depth, visible: true, expanded: expandedIds.has(key) };
        result.push(flat);
        if (node.children?.length) result.push(...rebuild(node.children, depth + 1));
      }
      return result;
    };
    this.flatTree = rebuild(this.treeData, 0);
  }

  nodeIcon(node: SubjectTreeFlatNode): string { return NODE_ICON[node.nodeType] || 'label'; }

  nodeTypeLabel(node: SubjectTreeFlatNode): string {
    return this.translate.instant('SUBJECT_TREE.NODE_TYPE_' + node.nodeType.toUpperCase());
  }

  openInCrud(node: SubjectTreeFlatNode): void {
    const route = NODE_ROUTE[node.nodeType];
    if (route) this.router.navigate([route]);
  }
}
