import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin, Subscription } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BarcodeDirective } from '../../../components/barcode.directive';
import { ReaderService } from '../../../services/reader/reader.service';
import { ReaderPhotoService, ReaderPhoto } from '../../../services/reader/reader-photo.service';
import { Reader } from '../../../models/reader/reader';
import { ReaderTypeService } from '../../../services/circulation/reader-type.service';
import { CourseService } from '../../../services/circulation/course.service';
import { AcademicClassService } from '../../../services/circulation/academic-class.service';
import { EthnicService } from '../../../services/circulation/ethnic.service';
import { DegreeService } from '../../../services/circulation/degree.service';
import { ProfService } from '../../../services/circulation/prof.service';
import { OrgService } from '../../../services/circulation/org.service';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { PaymentQrDialogComponent } from '../../../components/payment-qr-dialog/payment-qr-dialog';
import { AdminTablePreferencesComponent, TablePrefColumn, TablePrefsApply, defaultVisibleColumns, sameFilters } from '../../../shared/admin-table-preferences/admin-table-preferences';
import { AdminTableSettings } from '../../../services/system/admin-preference.service';
import { Router } from '@angular/router';
import { AdminTaskService } from '../../../services/system/admin-task.service';
import { ReaderImportMappingComponent } from './reader-import-mapping';
import { ReaderImportMapping } from '../../../models/reader/reader-import-mapping';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { UnsavedChanges, UnsavedChangesPage, watchUnsavedChanges } from '../../../guards/unsaved-changes.guard';

@Component({
  selector: 'app-reader',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, BarcodeDirective, NgSelectModule, AppDatePipe, DateInputComponent, AdminTablePreferencesComponent, ReaderImportMappingComponent, PaymentQrDialogComponent],
  templateUrl: './reader.html'
})
export class ReaderPage implements OnInit, OnDestroy, UnsavedChangesPage {
  private service              = inject(ReaderService);
  private listState = inject(ListPageStateService);
  private readerPhotoService   = inject(ReaderPhotoService);
  private readerTypeService    = inject(ReaderTypeService);
  private courseService        = inject(CourseService);
  private academicClassService = inject(AcademicClassService);
  private ethnicService        = inject(EthnicService);
  private degreeService        = inject(DegreeService);
  private profService          = inject(ProfService);
  private orgService           = inject(OrgService);
  private toastr               = inject(ToastrService);
  private auth                 = inject(Auth);
  private departmentService    = inject(DepartmentService);
  private adminTaskService     = inject(AdminTaskService);
  private router                = inject(Router);
  public  translate            = inject(TranslateService);
  private destroy$             = new Subject<void>();

  viewMode = signal<'table' | 'card'>('table');

  // Đơn vị (chỉ super-admin): lọc + hiển thị cột "Đơn vị".
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];

  // Đợt 21 — cấu hình bảng theo tài khoản (thay app-column-toggle nhớ theo trình duyệt): cột hiển thị + thứ tự,
  // bộ lọc, số dòng. Cột chọn/STT/Đơn vị/thao tác luôn cố định. Khoá cột khớp AdminPreferencePolicy phía server.
  readonly prefColumns: TablePrefColumn[] = [
    { key: 'cardno',     label: 'READER.CARD_CODE' },
    { key: 'fullName',   label: 'READER.FULL_NAME' },
    { key: 'readerType', label: 'READER.READER_TYPE' },
    { key: 'issueDate',  label: 'READER.ISSUED_DATE' },
    { key: 'expireDate', label: 'READER.EXPIRED_DATE' },
    { key: 'status',     label: 'READER.STATUS' },
    { key: 'gender',     label: 'READER.GENDER',         defaultVisible: false },
    { key: 'birthDate',  label: 'READER.BIRTH_DATE',     defaultVisible: false },
    { key: 'citizenId',  label: 'READER.CITIZEN_ID',     defaultVisible: false },
    { key: 'cardUid',    label: 'READER.CARD_UID',       defaultVisible: false },
    { key: 'email',      label: 'READER.EMAIL',          defaultVisible: false },
    { key: 'phone',      label: 'READER.PHONE',          defaultVisible: false },
    { key: 'address',    label: 'READER.ADDRESS',        defaultVisible: false },
    { key: 'class',      label: 'READER.ACADEMIC_CLASS', defaultVisible: false },
    { key: 'course',     label: 'READER.COURSE',         defaultVisible: false },
    { key: 'department', label: 'READER.DEPARTMENT',     defaultVisible: false },
  ];
  readonly defaultPageSize = 10;
  private visibleColumns = signal<string[]>(defaultVisibleColumns(this.prefColumns));
  /** true khi vừa khôi phục trang/số dòng từ ListPageStateService (quay lại từ trang khác) — cấu hình tài khoản áp
   *  tự động lúc vào trang không đè lên vị trí đang xem. */
  private restoredListState = false;

  displayedColumns = computed(() => [
    'select', 'stt', ...(this.isPrivileged ? ['tenant'] : []),
    ...this.visibleColumns(),
    'actions',
  ]);

  readTableSettings = (): AdminTableSettings => ({
    version: 1, pageSize: this.pageSize, columns: this.visibleColumns(),
    filters: this.searchForm.getRawValue() as AdminTableSettings['filters'],
  });

  applyTableSettings({ settings, automatic }: TablePrefsApply): void {
    this.visibleColumns.set(settings.columns);
    const filtersChanged = !sameFilters(settings.filters, this.searchForm.getRawValue());
    const keepPosition = automatic && this.restoredListState;
    const sizeChanged = !keepPosition && settings.pageSize !== this.pageSize;
    if (!filtersChanged && !sizeChanged) return;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    if (filtersChanged) this.searchForm.patchValue(settings.filters as any);
    if (sizeChanged) this.pageSize = settings.pageSize;
    if (!keepPosition || filtersChanged) this.pageIndex = 0;
    this.loadData();
  }
  dataSource: Reader[] = [];
  selection = new SelectionModel<Reader>(true, []);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal         = signal(false);
  editMode          = signal(false);
  currentId         = signal<string | number | null>(null);
  isLoading         = signal(false);
  isSaving          = signal(false);
  showConfirmDelete = signal(false);
  filtersCollapsed  = signal(false);
  toggleFilters(): void { this.filtersCollapsed.update(v => !v); }
  confirmDeleteId   = signal<string | number | null>(null);

  // Export modal state
  showExportModal     = signal(false);
  exportFields:       any[] = [];
  exportFieldsLoading = signal(false);
  selectedExportKeys  = signal<string[]>([]);

  // Lock card state
  showLockModal = signal(false);
  lockTargetId  = signal<string | number | null>(null);
  lockReason    = new FormControl<string>('', { nonNullable: true, validators: [Validators.required] });
  isLocking     = signal(false);

  // Reset password state
  showResetPwdModal = signal(false);
  resetPwdTargetId  = signal<string | number | null>(null);
  resetPwdControl   = new FormControl<string>('', { nonNullable: true, validators: [Validators.required, Validators.minLength(6)] });
  isResettingPwd    = signal(false);

  // Bulk reset password state (theo lựa chọn / checkbox)
  showBulkResetPwdModal = signal(false);
  bulkResetPwdControl   = new FormControl<string>('', { nonNullable: true, validators: [Validators.required, Validators.minLength(6)] });
  isBulkResettingPwd    = signal(false);

  // Batch modal state
  showBatchModal    = signal(false);
  batchAction       = signal<string>('');
  batchSelectValue  = signal<number | null>(null);
  batchDateValue    = signal<string>('');
  batchPasswordValue = signal<string>('');
  isBatching        = signal(false);

  readonly batchActions = [
    { key: 'changeClass',      label: 'Thay đổi lớp học',       type: 'class'       },
    { key: 'changeCourse',     label: 'Thay đổi khóa học',      type: 'course'      },
    { key: 'changeReaderType', label: 'Thay đổi loại độc giả',  type: 'readerType'  },
    { key: 'changeIssueDate',  label: 'Thay đổi ngày cấp',      type: 'date'        },
    { key: 'changeExpireDate', label: 'Thay đổi ngày hết hạn',  type: 'date'        },
    { key: 'changePassword',   label: 'Đặt lại mật khẩu',       type: 'password'    },
  ];

  batchActionType = computed(() => this.batchActions.find(a => a.key === this.batchAction())?.type ?? '');

  // Import modal state
  showImportModal    = signal(false);
  importFile         = signal<File | null>(null);
  isImporting        = signal(false);
  importOverwrite    = signal(false);
  importReaderTypeId = signal<number | null>(null);
  importResult       = signal<{ imported: number; failed: number; errors: string[]; message: string; createdRefs?: string[];
    photoZip?: { matched: number; notFound: number; failed: number } } | null>(null);
  // Đợt 20: ghép cột Excel (undefined = bố cục cột cố định cũ) + tự tạo Lớp/Khóa học/Đơn vị chưa có.
  importMapping      = signal<ReaderImportMapping | undefined>(undefined);
  importMappingValid = signal(true);
  importAutoCreateRefs = signal(false);

  // Tab Ảnh (Đợt 25): upload kèm 1 file .zip ảnh khớp theo Cardno ngay trong luồng import — chỉ khả dụng
  // ở chế độ đồng bộ (không "Xử lý nền", vì import nền chạy bất đồng bộ nên chưa có ngay danh sách bạn đọc
  // vừa tạo để khớp ảnh).
  importActiveTab    = signal<'data' | 'photos'>('data');
  importPhotoZipFile = signal<File | null>(null);
  // Đợt 10 — xử lý nền: chỉ hiện tuỳ chọn khi AdminTasks:Enabled=true trên hệ thống.
  importBackground      = signal(false);
  adminTasksEnabled     = signal(false);
  // Tra Lớp/Khóa học/Đơn vị theo Tên (mặc định, giữ nguyên hành vi cũ) hoặc theo Mã (Id số trực tiếp)
  importClassByCode  = signal(false);
  importCourseByCode = signal(false);
  importOrgByCode    = signal(false);

  // Upload ảnh hàng loạt theo ZIP (tên file = Số thẻ) — cập nhật ảnh đại diện chính.
  showUploadZipModal = signal(false);
  uploadZipFile      = signal<File | null>(null);
  isUploadingZip     = signal(false);
  uploadZipResult    = signal<{ matched: number; notFound: number; failed: number; results: { fileName: string; cardNo: string; matched: boolean; error?: string }[]; message: string } | null>(null);

  readerTypes:     any[] = [];
  courses:         any[] = [];
  academicClasses: any[] = [];
  ethnics:         any[] = [];
  degrees:         any[] = [];
  profs:           any[] = [];
  orgs:            any[] = [];

  cardnoExists    = signal(false);
  isCheckingCardno = signal(false);

  photoPreviewUrl = signal<string | null>(null);

  // Ảnh khuôn mặt bổ sung (ReaderPhoto) — chỉ dùng được khi bạn đọc đã có publicId (đã lưu).
  editingPublicId      = signal<string | null>(null);
  // Đợt 16 — Lý do sửa (tuỳ chọn), gửi qua header X-Change-Reason, hiện khi đang sửa hồ sơ có sẵn.
  changeReason         = signal('');

  // Đợt 21 — cảnh báo chưa lưu: so form + ảnh + lý do sửa với bản đã tải/lưu.
  private unsaved = new UnsavedChanges();
  private readingImage = false;
  private formSnapshot() { return { form: this.dataForm.getRawValue(), photo: this.photoPreviewUrl(), reason: this.changeReason() }; }
  private hasUnsavedChanges(): boolean { return this.showModal() && this.unsaved.changed(this.formSnapshot()); }
  canLeave = watchUnsavedChanges(() => this.hasUnsavedChanges(), () => this.isSaving() || this.readingImage);
  extraPhotos          = signal<ReaderPhoto[]>([]);
  isLoadingExtraPhotos = signal(false);
  isAddingExtraPhoto   = signal(false);

  // In thẻ bạn đọc (đơn + hàng loạt)
  printTarget = signal<Reader[]>([]);

  searchForm = new FormGroup({
    firstName:    new FormControl<string>('', { nonNullable: true }),
    lastName:     new FormControl<string>('', { nonNullable: true }),
    cardno:       new FormControl<string>('', { nonNullable: true }),
    classId:      new FormControl<number | null>(null),
    courseId:     new FormControl<number | null>(null),
    readerTypeId: new FormControl<number | null>(null),
    status:       new FormControl<number | null>(null),
    issuedFrom:   new FormControl<string>('', { nonNullable: true }),
    issuedTo:     new FormControl<string>('', { nonNullable: true }),
    expiredFrom:  new FormControl<string>('', { nonNullable: true }),
    expiredTo:    new FormControl<string>('', { nonNullable: true }),
    tenantId:     new FormControl<string | null>(null),
  });

  dataForm = new FormGroup({
    CardNo:          new FormControl<string>('', { nonNullable: true }),
    CitizenId:       new FormControl<string>('', { nonNullable: true }),
    // UID chip thẻ cho đầu đọc kiểm soát cửa phòng học nhóm (port ELIB-LRC 10-04) — backend chuẩn hoá (bỏ ' ', ':', '-', viết hoa).
    CardUid:         new FormControl<string>('', { nonNullable: true, validators: [Validators.maxLength(64), Validators.pattern(/^[0-9A-Za-z :\-]*$/)] }),
    FirstName:       new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    LastName:        new FormControl<string>('', { nonNullable: true }),
    Sex:             new FormControl<number>(1,  { nonNullable: true }),
    BirthDate:       new FormControl<string>('', { nonNullable: true }),
    ReaderTypeId:    new FormControl<number | null>(null),
    ClassId:         new FormControl<number | null>(null),
    CourseId:        new FormControl<number | null>(null),
    EthenicId:       new FormControl<number | null>(null),
    DegreeId:        new FormControl<number | null>(null),
    ProfId:          new FormControl<number | null>(null),
    Email:           new FormControl<string>('', { nonNullable: true }),
    Phone:           new FormControl<string>('', { nonNullable: true }),
    Address:         new FormControl<string>('', { nonNullable: true }),
    IssueDate:          new FormControl<string>('', { nonNullable: true }),
    ExpireDate:         new FormControl<string>('', { nonNullable: true }),
    Status:             new FormControl<number | null>(null),
    RequiredChangePassword: new FormControl<number>(1, { nonNullable: true }),
    Password:           new FormControl<string>('', { nonNullable: true }),
    ConfirmPassword:    new FormControl<string>('', { nonNullable: true }),
  }, { validators: this.passwordMatchValidator });

  private passwordMatchValidator(group: AbstractControl): ValidationErrors | null {
    const pw  = group.get('Password')?.value;
    const cpw = group.get('ConfirmPassword')?.value;
    if (!pw && !cpw) return null;
    return pw === cpw ? null : { passwordMismatch: true };
  }

  ngOnInit(): void {
    const saved = this.listState.recall('reader.reader'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; this.restoredListState = true; }
    const dtParams = { draw: 1, start: 0, length: 1000, search: { value: '', regex: false } };
    forkJoin({
      readerTypes:     this.readerTypeService.getAll(dtParams),
      courses:         this.courseService.getAll(dtParams),
      academicClasses: this.academicClassService.getAll(dtParams),
      ethnics:         this.ethnicService.getAll(dtParams),
      degrees:         this.degreeService.getAll(dtParams),
      profs:           this.profService.getAll(dtParams),
      orgs:            this.orgService.getAll(dtParams),
    }).pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.readerTypes     = res.readerTypes.data;
      this.courses         = res.courses.data;
      this.academicClasses = res.academicClasses.data;
      this.ethnics         = res.ethnics.data;
      this.degrees         = res.degrees.data;
      this.profs           = res.profs.data;
      this.orgs            = res.orgs.data;
    });
    // Super-admin: nạp danh sách đơn vị cho combobox lọc.
    if (this.isPrivileged) {
      this.departmentService.getAll(dtParams).pipe(takeUntil(this.destroy$)).subscribe(res => {
        this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name }));
      });
    }
    this.loadData();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /** Đợt 21 — huỷ lần tải trước: cấu hình bảng có thể kích hoạt lần tải thứ 2 ngay sau lần tải mặc định, phản hồi
   *  cũ về sau sẽ ghi đè kết quả đã lọc. */
  private loadSub?: Subscription;

  loadData(): void {
    this.loadSub?.unsubscribe();
    this.listState.remember('reader.reader', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.loadSub = this.service.search({
      firstName:    s.firstName    || null,
      lastName:     s.lastName     || null,
      cardno:       s.cardno       || null,
      classId:      s.classId      || null,
      courseId:     s.courseId     || null,
      readerTypeId: s.readerTypeId || null,
      status:       s.status       ?? null,
      issuedFrom:   s.issuedFrom   || null,
      issuedTo:     s.issuedTo     || null,
      expiredFrom:  s.expiredFrom  || null,
      expiredTo:    s.expiredTo    || null,
      tenantId:     s.tenantId     ?? null,
      pageIndex: this.pageIndex + 1,
      pageSize:  this.pageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.dataSource   = res.data;
        this.totalRecords = res.recordsTotal;
        this.isLoading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  triggerSearch(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  getRowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }

  getDisplayName(reader: Reader): string {
    return [reader.lastName, reader.firstName].filter(Boolean).join(' ');
  }

  getClassName(classId: number | null | undefined): string {
    if (!classId) return '';
    return this.academicClasses.find(c => c.id === classId)?.name ?? '';
  }

  getCourseName(courseId: number | null | undefined): string {
    if (!courseId) return '';
    return this.courses.find(c => c.id === courseId)?.name ?? '';
  }

  getOrgName(orgId: number | null | undefined): string {
    if (!orgId) return '';
    return this.orgs.find((o: any) => o.id === orgId)?.name ?? '';
  }

  isExpired(dateStr: string | null | undefined): boolean {
    if (!dateStr) return false;
    return new Date(dateStr) < new Date();
  }

  private toDateInput(isoDate: string | null | undefined): string {
    if (!isoDate) return '';
    return isoDate.split('T')[0];
  }

  isAllSelected(): boolean {
    return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length;
  }

  masterToggle(): void {
    this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r));
  }

  printSingleCard(r: Reader): void { this.printTarget.set([r]); setTimeout(() => this.doPrint()); }
  printSelectedCards(): void {
    if (!this.selection.selected.length) return;
    this.printTarget.set([...this.selection.selected]);
    setTimeout(() => this.doPrint());
  }
  doPrint(): void { if (typeof window !== 'undefined') window.print(); }

  async openAddModal(): Promise<void> {
    if (!(await this.canLeave())) return;
    this.editMode.set(false);
    this.currentId.set(null);
    this.cardnoExists.set(false);
    this.isCheckingCardno.set(false);
    this.dataForm.reset({ Sex: 1, Status: 2 });
    this.dataForm.get('ConfirmPassword')?.clearValidators();
    this.dataForm.get('ConfirmPassword')?.updateValueAndValidity();
    this.photoPreviewUrl.set(null);
    // Bạn đọc mới chưa có publicId nên chưa thể gắn ảnh khuôn mặt bổ sung.
    this.editingPublicId.set(null);
    this.extraPhotos.set([]);
    this.changeReason.set('');
    this.unsaved.capture(this.formSnapshot());
    this.showModal.set(true);
  }

  async openEditModal(item: Reader): Promise<void> {
    if (!(await this.canLeave())) return;
    this.editMode.set(true);
    this.currentId.set(item.publicId ?? item.id);
    this.cardnoExists.set(false);
    this.isCheckingCardno.set(false);
    this.editingPublicId.set(item.publicId ?? null);
    this.extraPhotos.set([]);
    this.changeReason.set('');
    if (item.publicId) {
      this.isLoadingExtraPhotos.set(true);
      this.readerPhotoService.getPhotos(item.publicId).pipe(takeUntil(this.destroy$)).subscribe({
        next: photos => {
          this.extraPhotos.set(photos);
          this.isLoadingExtraPhotos.set(false);
        },
        error: () => this.isLoadingExtraPhotos.set(false)
      });
    }
    this.service.getById(item.publicId ?? item.id).pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.dataForm.patchValue({
        CardNo:          data.cardno        ?? '',
        CitizenId:       data.citizenId     ?? '',
        CardUid:         data.cardUid       ?? '',
        FirstName:       data.firstName     ?? '',
        LastName:        data.lastName      ?? '',
        Sex:             data.sex           ?? 1,
        BirthDate:       this.toDateInput(data.birthDate),
        ReaderTypeId:    data.readerTypeId  ?? null,
        ClassId:         data.classId       || null,
        CourseId:        data.courseId      || null,
        EthenicId:       data.ethenicId     || null,
        DegreeId:        data.degreeId      || null,
        ProfId:          data.profId        || null,
        Email:           data.email         ?? '',
        Phone:           data.phone?.trim() ?? '',
        Address:         data.address       ?? '',
        IssueDate:          this.toDateInput(data.issueDate),
        ExpireDate:         this.toDateInput(data.expireDate),
        Status:                 data.status                 ?? null,
        RequiredChangePassword: data.requiredChangePassword ?? 1,
        Password:           '',
        ConfirmPassword:    '',
      });
      this.photoPreviewUrl.set(data.photo ?? null);
      this.unsaved.capture(this.formSnapshot());
      this.showModal.set(true);
    });
  }

  /** Đóng theo yêu cầu người dùng (nút X, nền, Hủy) — hỏi trước nếu còn thay đổi chưa lưu. */
  async requestClose(): Promise<void> {
    if (await this.canLeave()) this.closeModal();
  }

  closeModal(): void {
    this.showModal.set(false);
    this.dataForm.reset();
    this.photoPreviewUrl.set(null);
    this.editingPublicId.set(null);
    this.extraPhotos.set([]);
    this.changeReason.set('');
  }

  // Đợt 16 — mở trang Lịch sử thay đổi cho hồ sơ đang sửa (chỉ khi đã có publicId, tức bạn đọc đã lưu).
  openHistory(): void {
    const publicId = this.editingPublicId();
    if (!publicId) return;
    this.router.navigate(['/admin/entity-history'], { queryParams: { type: 'Reader', id: publicId } });
  }

  private readFileAsDataUrl(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const fr = new FileReader();
      fr.onload = e => resolve(e.target?.result as string);
      fr.onerror = reject;
      fr.readAsDataURL(file);
    });
  }

  onPhotoChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.readingImage = true;
    this.readFileAsDataUrl(file).then(dataUrl => this.photoPreviewUrl.set(dataUrl)).finally(() => this.readingImage = false);
  }

  clearPhoto(): void {
    this.photoPreviewUrl.set(null);
    const el = (typeof document !== 'undefined' ? document.getElementById('photoInput') : null) as HTMLInputElement | null;
    if (el) el.value = '';
  }

  triggerPhotoClick(): void {
    (typeof document !== 'undefined' ? document.getElementById('photoInput') : null)?.click();
  }

  // ===== ẢNH KHUÔN MẶT BỔ SUNG (ReaderPhoto) =====
  onAddExtraPhoto(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    const publicId = this.editingPublicId();
    if (!file || !publicId) return;

    this.readFileAsDataUrl(file).then(dataUrl => {
      this.isAddingExtraPhoto.set(true);
      this.readerPhotoService.addPhoto(publicId, dataUrl).pipe(takeUntil(this.destroy$)).subscribe({
        next: photo => {
          this.extraPhotos.update(list => [...list, photo]);
          this.isAddingExtraPhoto.set(false);
        },
        error: () => {
          this.isAddingExtraPhoto.set(false);
          this.toastr.error(this.translate.instant('COMMON.SAVE_ERROR'));
        }
      });
    });
  }

  deleteExtraPhoto(photo: ReaderPhoto): void {
    this.readerPhotoService.deletePhoto(photo.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.extraPhotos.update(list => list.filter(p => p.id !== photo.id)),
      error: () => this.toastr.error(this.translate.instant('COMMON.SAVE_ERROR'))
    });
  }

  checkCardNo(value: string): void {
    if (!value.trim()) { this.cardnoExists.set(false); return; }
    this.isCheckingCardno.set(true);
    this.cardnoExists.set(false);
    const excludeId = this.editMode() ? this.currentId() : null;
    this.service.checkCardNoExists(value.trim(), excludeId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: exists => {
          this.cardnoExists.set(exists);
          this.isCheckingCardno.set(false);
        },
        error: () => this.isCheckingCardno.set(false)
      });
  }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    if (this.cardnoExists()) {
      this.toastr.error(this.translate.instant('READER.CARDNO_EXISTS'));
      return;
    }
    const v = this.dataForm.getRawValue();
    if (v.Password && v.Password !== v.ConfirmPassword) {
      this.toastr.error(this.translate.instant('READER.PASSWORD_MISMATCH'));
      return;
    }
    this.isSaving.set(true);
    const payload = {
      CardNo:       v.CardNo       || null,
      CitizenId:    v.CitizenId    || null,
      CardUid:      v.CardUid      || null,
      FirstName:    v.FirstName,
      LastName:     v.LastName     || null,
      Sex:          v.Sex,
      BirthDate:    v.BirthDate    || null,
      ReaderTypeId: v.ReaderTypeId || null,
      ClassId:      v.ClassId      || null,
      CourseId:     v.CourseId     || null,
      EthenicId:        v.EthenicId        || null,
      DegreeId:         v.DegreeId         || null,
      ProfId:           v.ProfId           || null,
      Email:            v.Email            || null,
      Phone:            v.Phone            || null,
      Address:          v.Address          || null,
      IssueDate:        v.IssueDate        || null,
      ExpireDate:       v.ExpireDate       || null,
      Status:                 v.Status                 ?? null,
      RequiredChangePassword: v.RequiredChangePassword,
      Password:         v.Password         || null,
      Photo:            this.photoPreviewUrl()         ?? null,
    };

    const id   = this.currentId();
    const mode = this.editMode();
    const req$ = mode && id !== null
      ? this.service.update(id, payload, this.changeReason())
      : this.service.create(payload);

    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.closeModal();
        if (!mode) {
          this.pageIndex = 0;
          if (this.paginator) this.paginator.pageIndex = 0;
        }
        this.loadData();
        this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      },
      error: () => {
        this.isSaving.set(false);
      }
    });
  }

  toggleStatus(item: Reader): void {
    const newStatus = item.status === 2 ? 1 : 2;
    const id = item.publicId ?? item.id;
    this.service.changeStatus(id, newStatus).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { item.status = newStatus; },
      error: () => {}
    });
  }

  handleDelete(item: Reader): void {
    this.confirmDeleteId.set(item.publicId ?? item.id);
    this.showConfirmDelete.set(true);
  }

  deleteSelected(): void {
    if (!this.selection.selected.length) return;
    this.confirmDeleteId.set(null);
    this.showConfirmDelete.set(true);
  }

  closeConfirm(): void {
    this.showConfirmDelete.set(false);
    this.confirmDeleteId.set(null);
  }

  confirmActionExecute(): void {
    const id = this.confirmDeleteId();
    if (id === null) {
      const reqs = this.selection.selected.map(item => this.service.delete(item.publicId ?? item.id));
      forkJoin(reqs).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.selection.clear();
          this.closeConfirm();
          this.loadData();
        },
        error: () => {
          this.closeConfirm();
          this.loadData();
        }
      });
      return;
    }
    this.service.delete(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
        this.closeConfirm();
        this.loadData();
      },
      error: () => {
        this.closeConfirm();
      }
    });
  }

  // ===== EXPORT =====
  openExportModal(): void {
    this.showExportModal.set(true);
    this.exportFieldsLoading.set(true);
    this.exportFields = [];
    this.selectedExportKeys.set([]);
    this.service.getExportFields().pipe(takeUntil(this.destroy$)).subscribe({
      next: fields => {
        this.exportFields = fields;
        this.selectedExportKeys.set(fields.map((f: any) => f.code ?? f.key ?? f.name ?? '').filter(Boolean));
        this.exportFieldsLoading.set(false);
      },
      error: () => this.exportFieldsLoading.set(false)
    });
  }

  closeExportModal(): void {
    this.showExportModal.set(false);
  }

  getFieldKey(f: any): string {
    return f.code ?? f.key ?? f.name ?? '';
  }

  getFieldLabel(f: any): string {
    return f.name ?? f.label ?? f.displayName ?? f.key ?? '';
  }

  isExportFieldSelected(f: any): boolean {
    return this.selectedExportKeys().includes(this.getFieldKey(f));
  }

  toggleExportField(f: any): void {
    const key     = this.getFieldKey(f);
    const current = this.selectedExportKeys();
    this.selectedExportKeys.set(
      current.includes(key) ? current.filter(k => k !== key) : [...current, key]
    );
  }

  isAllExportSelected(): boolean {
    return this.exportFields.length > 0 && this.selectedExportKeys().length === this.exportFields.length;
  }

  toggleSelectAllExport(): void {
    const allKeys = this.exportFields.map((f: any) => this.getFieldKey(f)).filter(Boolean);
    this.selectedExportKeys.set(this.isAllExportSelected() ? [] : allKeys);
  }

  confirmExport(): void {
    const s = this.searchForm.getRawValue();
    this.service.exportExcel({
      firstName:    s.firstName    || null,
      lastName:     s.lastName     || null,
      cardno:       s.cardno       || null,
      classId:      s.classId      || null,
      courseId:     s.courseId     || null,
      readerTypeId: s.readerTypeId || null,
      status:       s.status       ?? null,
      issuedFrom:   s.issuedFrom   || null,
      issuedTo:     s.issuedTo     || null,
      expiredFrom:  s.expiredFrom  || null,
      expiredTo:    s.expiredTo    || null,
      tenantId:     s.tenantId     ?? null,
      fields:       this.selectedExportKeys(),
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a   = document.createElement('a');
        a.href = url; a.download = 'readers.xlsx'; a.click();
        URL.revokeObjectURL(url);
        this.showExportModal.set(false);
      },
      error: () => this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'))
    });
  }

  // ===== LOCK CARD =====
  // Thu phí cấp lại thẻ qua QR (port ELIB-LRC 09-25) — số tiền lấy từ tham số PHI_CAP_LAI_THE của đơn vị.
  cardReissuePaymentReaderId = signal<number | null>(null);
  openCardReissuePaymentQr(item: Reader): void { this.cardReissuePaymentReaderId.set(Number(item.id)); }
  closeCardReissuePaymentQr(): void { this.cardReissuePaymentReaderId.set(null); }

  openLockModal(item: Reader): void {
    this.lockTargetId.set(item.publicId ?? item.id);
    this.lockReason.reset('');
    this.showLockModal.set(true);
  }

  closeLockModal(): void {
    this.showLockModal.set(false);
    this.lockTargetId.set(null);
    this.lockReason.reset('');
  }

  confirmLock(): void {
    if (!this.lockReason.value?.trim()) { this.lockReason.markAsTouched(); return; }
    const id = this.lockTargetId();
    if (!id) return;
    this.isLocking.set(true);
    this.service.lockCard(id, this.lockReason.value).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isLocking.set(false);
        this.closeLockModal();
        this.loadData();
        this.toastr.success(this.translate.instant('READER.LOCK_SUCCESS'));
      },
      error: () => {
        this.isLocking.set(false);
      }
    });
  }

  // ===== RESET PASSWORD MODAL =====
  openResetPasswordModal(item: Reader): void {
    this.resetPwdTargetId.set(item.publicId ?? item.id);
    this.resetPwdControl.reset('');
    this.showResetPwdModal.set(true);
  }

  closeResetPasswordModal(): void {
    this.showResetPwdModal.set(false);
    this.resetPwdTargetId.set(null);
    this.resetPwdControl.reset('');
  }

  confirmResetPassword(): void {
    if (this.resetPwdControl.invalid) { this.resetPwdControl.markAsTouched(); return; }
    const id = this.resetPwdTargetId();
    if (!id) return;
    this.isResettingPwd.set(true);
    this.service.resetPassword(id, this.resetPwdControl.value).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.isResettingPwd.set(false);
        this.closeResetPasswordModal();
        this.toastr.success(res?.message || this.translate.instant('READER.RESET_PASSWORD_SUCCESS'));
      },
      error: () => {
        this.isResettingPwd.set(false);
      }
    });
  }

  // ===== BULK RESET PASSWORD (theo lựa chọn) =====
  openBulkResetPasswordModal(): void {
    if (this.selection.selected.length === 0) return;
    this.bulkResetPwdControl.reset('');
    this.showBulkResetPwdModal.set(true);
  }

  closeBulkResetPasswordModal(): void {
    this.showBulkResetPwdModal.set(false);
    this.bulkResetPwdControl.reset('');
  }

  confirmBulkResetPassword(): void {
    if (this.bulkResetPwdControl.invalid) { this.bulkResetPwdControl.markAsTouched(); return; }
    const ids = this.selection.selected.map(r => r.publicId ?? r.id).filter((id): id is string | number => id != null);
    if (ids.length === 0) return;
    this.isBulkResettingPwd.set(true);
    this.service.bulkResetPassword(ids, this.bulkResetPwdControl.value).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.isBulkResettingPwd.set(false);
        this.closeBulkResetPasswordModal();
        this.selection.clear();
        this.toastr.success(res?.message || this.translate.instant('READER.RESET_PASSWORD_SUCCESS'));
      },
      error: () => {
        this.isBulkResettingPwd.set(false);
      }
    });
  }

  // ===== BATCH MODAL =====
  openBatchModal(): void {
    this.batchAction.set('');
    this.batchSelectValue.set(null);
    this.batchDateValue.set('');
    this.batchPasswordValue.set('');
    this.showBatchModal.set(true);
  }

  closeBatchModal(): void {
    this.showBatchModal.set(false);
    this.batchAction.set('');
    this.batchSelectValue.set(null);
    this.batchDateValue.set('');
    this.batchPasswordValue.set('');
  }

  onBatchActionChange(key: string): void {
    this.batchAction.set(key);
    this.batchSelectValue.set(null);
    this.batchDateValue.set('');
    this.batchPasswordValue.set('');
  }

  confirmBatch(): void {
    const action = this.batchAction();
    const type   = this.batchActionType();
    const value  = type === 'date' ? this.batchDateValue() : type === 'password' ? this.batchPasswordValue() : this.batchSelectValue();
    if (!action || value === null || value === '') return;
    if (type === 'password' && this.batchPasswordValue().length < 6) return;

    this.isBatching.set(true);
    const s = this.searchForm.getRawValue();
    this.service.batchUpdate(
      {
        firstName:    s.firstName    || null,
        lastName:     s.lastName     || null,
        cardno:       s.cardno       || null,
        classId:      s.classId      || null,
        courseId:     s.courseId     || null,
        readerTypeId: s.readerTypeId || null,
        status:       s.status       ?? null,
        issuedFrom:   s.issuedFrom   || null,
        issuedTo:     s.issuedTo     || null,
        expiredFrom:  s.expiredFrom  || null,
        expiredTo:    s.expiredTo    || null,
        tenantId:     s.tenantId     ?? null,
      },
      action,
      value
    ).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isBatching.set(false);
        this.closeBatchModal();
        this.loadData();
        this.toastr.success('Xử lý theo lô thành công');
      },
      error: () => {
        this.isBatching.set(false);
        this.toastr.error('Xử lý theo lô thất bại');
      }
    });
  }

  // ===== IMPORT MODAL =====
  openImportModal(): void {
    this.importFile.set(null);
    this.importResult.set(null);
    this.importOverwrite.set(false);
    this.importReaderTypeId.set(null);
    this.importClassByCode.set(false);
    this.importCourseByCode.set(false);
    this.importOrgByCode.set(false);
    this.importBackground.set(false);
    this.importMapping.set(undefined); this.importMappingValid.set(true); this.importAutoCreateRefs.set(false);
    this.importActiveTab.set('data'); this.importPhotoZipFile.set(null);
    this.showImportModal.set(true);
    this.adminTaskService.health().pipe(takeUntil(this.destroy$)).subscribe(h => this.adminTasksEnabled.set(h.enabled));
  }

  closeImportModal(): void {
    this.showImportModal.set(false);
    this.importFile.set(null);
    this.importResult.set(null);
    this.importOverwrite.set(false);
    this.importReaderTypeId.set(null);
    this.importClassByCode.set(false);
    this.importCourseByCode.set(false);
    this.importOrgByCode.set(false);
    this.importBackground.set(false);
    this.importMapping.set(undefined); this.importMappingValid.set(true); this.importAutoCreateRefs.set(false);
    this.importActiveTab.set('data'); this.importPhotoZipFile.set(null);
  }

  onImportFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.importFile.set(file);
    input.value = '';
  }

  onImportPhotoZipFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.importPhotoZipFile.set(file);
    input.value = '';
  }

  confirmImport(): void {
    const file = this.importFile();
    if (!file || !this.importReaderTypeId() || !this.importMappingValid()) return;
    const options = { mapping: this.importMapping(), autoCreateRefs: this.importAutoCreateRefs() };

    if (this.importBackground()) {
      this.isImporting.set(true);
      this.service.importExcelBackground(
        file, this.importOverwrite(), this.importReaderTypeId(),
        this.importClassByCode(), this.importCourseByCode(), this.importOrgByCode(), options
      ).pipe(takeUntil(this.destroy$)).subscribe({
        next: task => {
          this.isImporting.set(false);
          if (task) {
            this.closeImportModal();
            this.router.navigate(['/admin/admin-tasks']);
          } else {
            this.importResult.set({ imported: 0, failed: 1, errors: ['Không tạo được tác vụ nền'], message: '' });
          }
        },
        error: () => {
          this.isImporting.set(false);
          this.importResult.set({ imported: 0, failed: 1, errors: ['Lỗi kết nối'], message: '' });
        }
      });
      return;
    }

    this.isImporting.set(true);
    this.service.importExcel(
      file,
      this.importOverwrite(),
      this.importReaderTypeId(),
      this.importClassByCode(),
      this.importCourseByCode(),
      this.importOrgByCode(),
      options
    ).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        const photoZipFile = this.importPhotoZipFile();
        if (!photoZipFile) {
          this.isImporting.set(false);
          this.importResult.set(result);
          this.loadData();
          return;
        }
        this.service.uploadPhotosZip(photoZipFile).pipe(takeUntil(this.destroy$)).subscribe({
          next: zipResult => {
            this.isImporting.set(false);
            this.importResult.set({ ...result, photoZip: { matched: zipResult.matched, notFound: zipResult.notFound, failed: zipResult.failed } });
            this.loadData();
          },
          error: () => {
            this.isImporting.set(false);
            this.importResult.set({ ...result, photoZip: { matched: 0, notFound: 0, failed: 1 } });
            this.loadData();
          }
        });
      },
      error: () => {
        this.isImporting.set(false);
        this.importResult.set({ imported: 0, failed: 1, errors: ['Lỗi kết nối'], message: '' });
      }
    });
  }

  // ===== UPLOAD ẢNH HÀNG LOẠT (ZIP) =====
  openUploadZipModal(): void {
    this.uploadZipFile.set(null);
    this.uploadZipResult.set(null);
    this.showUploadZipModal.set(true);
  }

  closeUploadZipModal(): void {
    this.showUploadZipModal.set(false);
    this.uploadZipFile.set(null);
    this.uploadZipResult.set(null);
  }

  onUploadZipFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.uploadZipFile.set(file);
    input.value = '';
  }

  confirmUploadZip(): void {
    const file = this.uploadZipFile();
    if (!file) return;
    this.isUploadingZip.set(true);
    this.service.uploadPhotosZip(file).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.isUploadingZip.set(false);
        this.uploadZipResult.set(result);
        this.loadData();
      },
      error: () => {
        this.isUploadingZip.set(false);
        this.uploadZipResult.set({ matched: 0, notFound: 0, failed: 1, results: [], message: 'Lỗi kết nối' });
      }
    });
  }
}
