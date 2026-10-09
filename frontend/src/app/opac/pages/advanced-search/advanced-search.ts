import { TranslateModule } from '@ngx-translate/core';
import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { SystemApiService } from '../../services/system-api.service';
import {
  Z3950SearchService, Z3950Field, Z3950BriefResult, Z3950DetailResult, Z3950Record
} from '../../services/z3950-search.service';

interface LibraryRow { id: string; name: string; selected: boolean; }
interface MergedRecord extends Z3950Record { libraryId: string; libraryName: string; systax: string; }

// Nhãn hiển thị → tên trường API (xem docs/FRONTEND_SEARCHZ3950.md)
const FIELD_MAP: Record<string, string> = {
  'Mọi trường':    'Keyword',
  'Nhan đề':       'Title',
  'Tác giả':       'Author',
  'Nhà xuất bản':  'Publisher',
  'Từ khóa':       'Keyword',
  'ISBN':          'ISBN',
};

@Component({
  selector: 'app-advanced-search',
  imports: [TranslateModule, CommonModule, FormsModule, RouterModule],
  templateUrl: './advanced-search.html',
  styleUrls: ['./advanced-search.css']
})
export class AdvancedSearchComponent implements OnInit {
  field1 = 'Mọi trường';
  query1 = '';

  bool2 = 'Và';
  field2 = 'Mọi trường';
  query2 = '';

  bool3 = 'Và';
  field3 = 'Mọi trường';
  query3 = '';

  libraries = signal<LibraryRow[]>([]);

  isSearching   = signal(false);
  searched      = signal(false);
  errorMsg      = signal('');
  briefResults  = signal<Z3950BriefResult[]>([]);
  detailResults = signal<Z3950DetailResult[]>([]);
  pageIndex     = signal(1);
  pageSize = 10;

  // Thu nhỏ/mở rộng các khối
  showLibraries = signal(true);
  showResults   = signal(true);
  // Dòng biểu ghi ngoại đang mở rộng (accordion)
  expandedIndex = signal<number | null>(null);

  mergedRecords = computed<MergedRecord[]>(() =>
    this.detailResults().flatMap(lib =>
      (lib.records ?? []).map(r => ({ ...r, libraryId: lib.libraryId, libraryName: lib.libraryName, systax: lib.systax }))
    )
  );
  // Tóm tắt chỉ hiển thị thư viện có biểu ghi trả về > 0
  connectedBriefResults = computed(() => this.briefResults().filter(b => b.connected && b.count > 0));
  totalCount = computed(() =>
    this.detailResults().reduce((sum, lib) => sum + (lib.connected ? (lib.totalCount ?? 0) : 0), 0)
  );
  totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  private systemApi = inject(SystemApiService);
  private z3950 = inject(Z3950SearchService);

  ngOnInit(): void {
    this.systemApi.getZ3950Libraries().subscribe(libs => {
      this.libraries.set(libs.map(lib => ({ id: lib.publicId, name: lib.name, selected: false })));
    });
  }

  toggleAll(ev: Event): void {
    const checked = (ev.target as HTMLInputElement).checked;
    this.libraries.update(list => list.map(l => ({ ...l, selected: checked })));
  }

  private buildFields(): Z3950Field[] {
    const rows = [
      { field: this.field1, value: this.query1 },
      { field: this.field2, value: this.query2 },
      { field: this.field3, value: this.query3 },
    ];
    return rows
      .filter(r => r.value && r.value.trim())
      .map(r => ({ field: FIELD_MAP[r.field] ?? 'Keyword', value: r.value.trim() }));
  }

  private get operator(): 'AND' | 'OR' {
    return this.bool2 === 'Hoặc' ? 'OR' : 'AND';
  }

  private selectedLibraryIds(): string[] {
    const selected = this.libraries().filter(l => l.selected).map(l => l.id);
    // Không chọn thư viện nào → tìm trên tất cả
    return selected.length ? selected : this.libraries().map(l => l.id);
  }

  onSearch(): void {
    const fields = this.buildFields();
    if (!fields.length) {
      this.errorMsg.set('Vui lòng nhập từ khóa tìm kiếm.');
      return;
    }
    const ids = this.selectedLibraryIds();
    if (!ids.length) {
      this.errorMsg.set('Không có thư viện liên kết để tìm kiếm.');
      return;
    }

    this.errorMsg.set('');
    this.searched.set(true);
    this.isSearching.set(true);
    this.pageIndex.set(1);
    this.detailResults.set([]);
    this.briefResults.set([]);

    this.z3950.searchBrief(fields, this.operator, ids).subscribe(brief => {
      this.briefResults.set(brief);
      const connectedIds = brief.filter(b => b.connected && b.count > 0).map(b => b.libraryId);
      if (!connectedIds.length) {
        this.isSearching.set(false);
        return;
      }
      this.loadDetail(connectedIds);
    });
  }

  private loadDetail(ids: string[]): void {
    const fields = this.buildFields();
    this.isSearching.set(true);
    this.expandedIndex.set(null);
    this.z3950.searchDetail(fields, this.operator, ids, this.pageIndex(), this.pageSize).subscribe(detail => {
      this.detailResults.set(detail);
      this.isSearching.set(false);
    });
  }

  toggleExpand(i: number): void {
    this.expandedIndex.set(this.expandedIndex() === i ? null : i);
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages() || p === this.pageIndex() || this.isSearching()) return;
    this.pageIndex.set(p);
    const connectedIds = this.briefResults().filter(b => b.connected && b.count > 0).map(b => b.libraryId);
    if (connectedIds.length) this.loadDetail(connectedIds);
  }
}
