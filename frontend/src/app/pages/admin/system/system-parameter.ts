import { Component, inject, OnInit, OnDestroy, signal, ViewChild, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { CKEditorModule } from '@ckeditor/ckeditor5-angular';
import { ensureCkeditorStyles } from '../../../services/shared/ckeditor-styles';
import { ToastrService } from '../../../services/shared/toastr.service';
import { SystemParameterService } from '../../../services/system/system-parameter';
import { SystemParameter } from '../../../models/system/system-parameter';
import { DataTableParams } from '../../../models/shared/datatable';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-system-parameter',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, CKEditorModule, TenantFilterSelectComponent],
  templateUrl: './system-parameter.html'
})
export class SystemParameterPage implements OnInit, OnDestroy {
  private systemParameterService = inject(SystemParameterService);
  private toastr = inject(ToastrService);
  public translate = inject(TranslateService);
  private platformId = inject(PLATFORM_ID);
  private auth = inject(Auth);
  private destroy$ = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  isBrowser = isPlatformBrowser(this.platformId);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  public Editor = signal<any>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  public editorConfig = signal<any>(null);
  public editorError = signal(false);

  displayedColumns: string[] = ['select', 'id', 'code', 'description', 'value', 'actions'];
  dataSource: SystemParameter[] = [];
  selection = new SelectionModel<SystemParameter>(true, []);
  
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];
  keyword = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  editMode = signal(false);
  currentId = signal<string | null>(null);
  isLoading = signal<boolean>(false);
  
  showConfirmDelete = signal(false);
  confirmDeleteId = signal<string | null>(null);

  dataForm = new FormGroup({
    Code: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    DescriptionVn: new FormControl<string>('', { nonNullable: true }),
    DescriptionEn: new FormControl<string>('', { nonNullable: true }),
    Value: new FormControl<string>('', { nonNullable: true }),
    IsRemoveHtml: new FormControl<boolean>(false, { nonNullable: true })
  });

  ngOnInit() {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    if (this.isBrowser) {
      this.initEditor();
    }
    this.loadData();
  }

  onTenantChange(): void { this.refreshTable(); }

  private async initEditor(): Promise<void> {
    try {
      ensureCkeditorStyles();
      const {
        ClassicEditor, Bold, Essentials, Italic, Underline, Paragraph, Heading,
        Link, List, ListProperties, Undo, BlockQuote, FontColor, FontBackgroundColor,
        FontSize, FontFamily, HorizontalLine, Table, TableToolbar, TableCaption,
        Alignment, Indent, IndentBlock, Image, ImageCaption, ImageStyle,
        ImageToolbar, ImageResizeEditing, ImageResizeHandles, MediaEmbed,
        SourceEditing, SpecialCharacters, SpecialCharactersEssentials, CodeBlock
      } = await import('ckeditor5');

      this.Editor.set(ClassicEditor);
      this.editorConfig.set({
      licenseKey: 'GPL',
      plugins: [
        Essentials, Paragraph, Bold, Italic, Underline, Heading,
        Link, List, ListProperties, Undo, BlockQuote,
        FontColor, FontBackgroundColor, FontSize, FontFamily,
        HorizontalLine, Alignment, Indent, IndentBlock,
        Table, TableToolbar, TableCaption,
        Image, ImageCaption, ImageStyle, ImageToolbar,
        ImageResizeEditing, ImageResizeHandles,
        MediaEmbed, SourceEditing, SpecialCharacters, SpecialCharactersEssentials,
        CodeBlock
      ],
      toolbar: {
        items: [
          'heading', '|',
          'bold', 'italic', 'underline', '|',
          'fontColor', 'fontBackgroundColor', 'fontSize', 'fontFamily', '|',
          'alignment', '|',
          'bulletedList', 'numberedList', 'indent', 'outdent', '|',
          'link', 'blockQuote', 'horizontalLine', '|',
          'insertTable', 'mediaEmbed', '|',
          'specialCharacters', 'codeBlock', '|',
          'sourceEditing', '|',
          'undo', 'redo'
        ],
        shouldNotGroupWhenFull: true
      },
      table: {
        contentToolbar: ['tableColumn', 'tableRow', 'mergeTableCells', 'tableCellProperties', 'tableProperties', 'toggleTableCaption']
      },
      image: {
        toolbar: ['imageStyle:block', 'imageStyle:side', '|', 'imageTextAlternative', 'toggleImageCaption']
      }
    });
    } catch (err) {
      console.error('CKEditor init failed:', err);
      this.editorError.set(true);
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onEditorReady(editor: any): void {
    editor.setData(this.dataForm.get('Value')?.value || '');
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onCkChange(event: any): void {
    this.dataForm.get('Value')?.setValue(event.editor.getData(), { emitEvent: false });
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    const params: DataTableParams = {
      draw: 1,
      start: this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.keyword, regex: false },
      tenantId: this.tenantId
    };

    this.isLoading.set(true);
    this.systemParameterService.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      next: (response: any) => {
        this.dataSource = response.data || [];
        this.totalRecords = response.recordsTotal || 0;
        this.isLoading.set(false);
      },
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      error: (err: any) => {
        console.error('Error loading data', err);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  triggerSearch() {
    const searchCodeInput = document.getElementById('searchCode') as HTMLInputElement;
    this.keyword = searchCodeInput?.value || '';
    if (this.paginator) {
      this.paginator.pageIndex = 0;
      this.pageIndex = 0;
    } else {
      this.pageIndex = 0;
    }
    this.loadData();
  }

  refreshTable() {
    if (this.paginator) {
      this.paginator.pageIndex = 0;
      this.pageIndex = 0;
    }
    this.loadData();
  }

  /** Whether the number of selected elements matches the total number of rows. */
  isAllSelected() {
    const numSelected = this.selection.selected.length;
    const numRows = this.dataSource.length;
    return numSelected === numRows;
  }

  /** Selects all rows if they are not all selected; otherwise clear selection. */
  masterToggle() {
    if (this.isAllSelected()) {
      this.selection.clear();
    } else {
      this.dataSource.forEach(row => this.selection.select(row));
    }
  }

  openAddModal() {
    this.editMode.set(false);
    this.currentId.set(null);
    this.dataForm.reset();
    this.dataForm.patchValue({ IsRemoveHtml: false });
    this.showModal.set(true);
  }

  openEditModal(id: string | number) {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    this.systemParameterService.getById(String(id)).subscribe((record: any) => {
      if (record) {
        this.editMode.set(true);
        this.currentId.set(String(id));
        
        // Handle variations of keys
        const codeVal = record.code || record.Code || '';
        const descVnVal = record.descriptionVn || record.DescriptionVn || '';
        const descEnVal = record.descriptionEn || record.DescriptionEn || '';
        const valueVal = record.value || record.Value || '';
        const isRemoveHtmlVal = record.isRemoveHtml ?? false;
        
        this.dataForm.patchValue({
          Code: codeVal,
          DescriptionVn: descVnVal,
          DescriptionEn: descEnVal,
          Value: valueVal,
          IsRemoveHtml: isRemoveHtmlVal
        });
        this.showModal.set(true);
      }
    });
  }

  closeModal() {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit() {
    if (this.dataForm.valid) {
      const rawValues = this.dataForm.getRawValue();
      let value = rawValues.Value;
      if (rawValues.IsRemoveHtml) {
        value = value.replace(/<[^>]*>?/gm, '');
      }

      const payload: Partial<SystemParameter> = {
        code: rawValues.Code,
        descriptionVn: rawValues.DescriptionVn,
        descriptionEn: rawValues.DescriptionEn,
        value: value,
        isRemoveHtml: rawValues.IsRemoveHtml
      };
      
      if (this.editMode() && this.currentId()) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const updatePayload: any = {
          id: String(this.currentId()),
          publicId: String(this.currentId()),
          ...payload
        };
        this.systemParameterService.update(updatePayload).subscribe(() => {
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.loadData(); // sửa: giữ nguyên trang hiện tại
          this.closeModal();
        });
      } else {
        this.systemParameterService.create(payload as SystemParameter).subscribe(() => {
          this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
          this.refreshTable();
          this.closeModal();
        });
      }
    }
  }

  handleDelete(id: string | number) {
    this.confirmDeleteId.set(String(id));
    this.showConfirmDelete.set(true);
  }

  deleteSelected() {
    if (this.selection.selected.length === 0) return;
    this.confirmDeleteId.set(null); // null means delete selected
    this.showConfirmDelete.set(true);
  }

  closeConfirm() {
    this.showConfirmDelete.set(false);
    this.confirmDeleteId.set(null);
  }

  confirmActionExecute() {
    const id = this.confirmDeleteId();
    
    if (id === null) {
      // Delete multiple
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const selectedIds = this.selection.selected.map((item: any) => item.publicId || item.id || item.Id);
      const requests = selectedIds.map(selectedId => this.systemParameterService.delete(String(selectedId)));
      forkJoin(requests).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.refreshTable();
          this.selection.clear();
          this.closeConfirm();
        },
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        error: (err: any) => {
          console.error(err);
          this.refreshTable();
          this.closeConfirm();
        }
      });
      return;
    }

    if (id) {
      this.systemParameterService.delete(String(id)).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.refreshTable();
          this.closeConfirm();
        },
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        error: (err: any) => {
          console.error(err);
          this.closeConfirm();
        }
      });
    }
  }
}
