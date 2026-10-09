import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { EntityHistoryService, EntityHistoryEvent, EntityHistoryActor, EntityHistoryFilters } from '../../../services/system/entity-history.service';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

/** Lịch sử thay đổi theo hồ sơ (Đợt 16 — port từ ELIB-LRC, bản đầu chỉ phủ Reader + LoanTransaction).
 * Đọc query param type/id, tải cursor 25 sự kiện/lần qua "Xem thêm". Không gọi thêm API lấy tên/hồ sơ
 * hiện tại (khác LRC có thẻ thông tin riêng) — đơn giản hoá có chủ đích, trang chỉ hiện danh sách sự
 * kiện. Nội dung render qua interpolation `{{ }}` (Angular tự escape) — không dùng innerHTML. */
@Component({
  selector: 'app-entity-history',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, AppDatePipe],
  templateUrl: './entity-history.html'
})
export class EntityHistoryPage implements OnInit, OnDestroy {
  private service   = inject(EntityHistoryService);
  private route      = inject(ActivatedRoute);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  static readonly ACTION_LABELS: Record<string, string> = {
    Add: 'ENTITY_HISTORY.ACTION_ADD',
    Update: 'ENTITY_HISTORY.ACTION_UPDATE',
    Delete: 'ENTITY_HISTORY.ACTION_DELETE',
    Checkout: 'ENTITY_HISTORY.ACTION_CHECKOUT',
    Renew: 'ENTITY_HISTORY.ACTION_RENEW',
    Return: 'ENTITY_HISTORY.ACTION_RETURN',
    NoteEdit: 'ENTITY_HISTORY.ACTION_NOTE_EDIT',
    // Đợt 17
    Shelve: 'ENTITY_HISTORY.ACTION_SHELVE',
    SignConfirm: 'ENTITY_HISTORY.ACTION_SIGN_CONFIRM',
    Place: 'ENTITY_HISTORY.ACTION_PLACE',
    ReRegister: 'ENTITY_HISTORY.ACTION_RE_REGISTER',
  };

  private static readonly FIELD_LABELS: Record<string, string> = {
    FirstName: 'ENTITY_HISTORY.FIELD_FIRST_NAME', LastName: 'ENTITY_HISTORY.FIELD_LAST_NAME',
    Cardno: 'ENTITY_HISTORY.FIELD_CARDNO', CitizenId: 'ENTITY_HISTORY.FIELD_CITIZEN_ID',
    Sex: 'ENTITY_HISTORY.FIELD_SEX', BirthDate: 'ENTITY_HISTORY.FIELD_BIRTH_DATE',
    Email: 'ENTITY_HISTORY.FIELD_EMAIL', Phone: 'ENTITY_HISTORY.FIELD_PHONE',
    Address: 'ENTITY_HISTORY.FIELD_ADDRESS', IssueDate: 'ENTITY_HISTORY.FIELD_ISSUE_DATE',
    ExpireDate: 'ENTITY_HISTORY.FIELD_EXPIRE_DATE', ClassId: 'ENTITY_HISTORY.FIELD_CLASS',
    CourseId: 'ENTITY_HISTORY.FIELD_COURSE', OrgId: 'ENTITY_HISTORY.FIELD_ORG',
    ReaderTypeId: 'ENTITY_HISTORY.FIELD_READER_TYPE', DegreeId: 'ENTITY_HISTORY.FIELD_DEGREE',
    EthenicId: 'ENTITY_HISTORY.FIELD_ETHNIC', ProfId: 'ENTITY_HISTORY.FIELD_PROF',
    Status: 'ENTITY_HISTORY.FIELD_STATUS', DueDate: 'ENTITY_HISTORY.FIELD_DUE_DATE',
    BorrowDate: 'ENTITY_HISTORY.FIELD_BORROW_DATE', ReturnDate: 'ENTITY_HISTORY.FIELD_RETURN_DATE',
    Note: 'ENTITY_HISTORY.FIELD_NOTE',
    // Đợt 17 — Barcode (PrintCopy)
    BarcodeValue: 'ENTITY_HISTORY.FIELD_BARCODE_VALUE', BarcodeNumber: 'ENTITY_HISTORY.FIELD_BARCODE_NUMBER',
    Store: 'ENTITY_HISTORY.FIELD_STORE', BibId: 'ENTITY_HISTORY.FIELD_BIB_ID',
    MapObjectId: 'ENTITY_HISTORY.FIELD_MAP_OBJECT', MapShelfRowId: 'ENTITY_HISTORY.FIELD_MAP_SHELF_ROW',
    // Đợt 17 — DigitalDocument (EbookItem + metadata)
    CollectionId: 'ENTITY_HISTORY.FIELD_COLLECTION', AllowDownload: 'ENTITY_HISTORY.FIELD_ALLOW_DOWNLOAD',
    Free: 'ENTITY_HISTORY.FIELD_FREE', SubjectId: 'ENTITY_HISTORY.FIELD_SUBJECT',
    TypeId: 'ENTITY_HISTORY.FIELD_DOC_TYPE', TopicId: 'ENTITY_HISTORY.FIELD_TOPIC',
    Show: 'ENTITY_HISTORY.FIELD_SHOW', IndexContent: 'ENTITY_HISTORY.FIELD_INDEX_CONTENT',
    Share: 'ENTITY_HISTORY.FIELD_SHARE', PrintCopies: 'ENTITY_HISTORY.FIELD_PRINT_COPIES',
    OfflineDays: 'ENTITY_HISTORY.FIELD_OFFLINE_DAYS', IsDelete: 'ENTITY_HISTORY.FIELD_IS_DELETE',
    Title: 'ENTITY_HISTORY.FIELD_TITLE', OtherTitle: 'ENTITY_HISTORY.FIELD_OTHER_TITLE',
    Author: 'ENTITY_HISTORY.FIELD_AUTHOR', OldAuthor: 'ENTITY_HISTORY.FIELD_OLD_AUTHOR',
    Publisher: 'ENTITY_HISTORY.FIELD_PUBLISHER', PublishDate: 'ENTITY_HISTORY.FIELD_PUBLISH_DATE',
    Keyword: 'ENTITY_HISTORY.FIELD_KEYWORD',
    // Đợt 17 — PrintBib (Bib)
    Bib_type_id: 'ENTITY_HISTORY.FIELD_BIB_TYPE', Images: 'ENTITY_HISTORY.FIELD_IMAGES',
  };

  // Đợt 17 — nhãn tag MARC phổ biến (PrintBib). Tag không có trong bảng vẫn hiện đúng số tag gốc.
  private static readonly MARC_TAG_LABELS: Record<string, string> = {
    '000': 'ENTITY_HISTORY.MARC_000', '001': 'ENTITY_HISTORY.MARC_001', '003': 'ENTITY_HISTORY.MARC_003',
    '005': 'ENTITY_HISTORY.MARC_005', '008': 'ENTITY_HISTORY.MARC_008',
    '020': 'ENTITY_HISTORY.MARC_020', '082': 'ENTITY_HISTORY.MARC_082', '100': 'ENTITY_HISTORY.MARC_100',
    '245': 'ENTITY_HISTORY.MARC_245', '260': 'ENTITY_HISTORY.MARC_260', '264': 'ENTITY_HISTORY.MARC_264',
    '650': 'ENTITY_HISTORY.MARC_650', '700': 'ENTITY_HISTORY.MARC_700',
  };

  type = signal('');
  id = signal('');
  events = signal<EntityHistoryEvent[]>([]);
  next = signal<number | null>(null);
  isLoading = signal(false);
  isLoadingMore = signal(false);
  loadError = signal(false);

  // Đợt 21 — lọc theo ngày / người thao tác / loại thao tác (lọc phía server, cursor "Xem thêm" giữ nguyên bộ lọc).
  fromDate = '';
  toDate = '';
  actorId: number | null = null;
  action = '';
  actors = signal<EntityHistoryActor[]>([]);
  /** Bộ lọc đang áp dụng cho danh sách hiện tại (khác các ô đang gõ dở). */
  private applied: EntityHistoryFilters = {};
  readonly actionOptions = Object.keys(EntityHistoryPage.ACTION_LABELS);
  get hasFilters(): boolean { return !!(this.applied.from || this.applied.to || this.applied.actorId != null || this.applied.action); }

  applyFilters(): void {
    this.applied = { from: this.fromDate || undefined, to: this.toDate || undefined, actorId: this.actorId, action: this.action || undefined };
    this.load(false);
  }
  clearFilters(): void {
    this.fromDate = ''; this.toDate = ''; this.actorId = null; this.action = '';
    this.applyFilters();
  }

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.type.set(qp.get('type') || '');
    this.id.set(qp.get('id') || '');
    if (this.type() && this.id()) this.load(false);
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  private load(more: boolean): void {
    if (more) this.isLoadingMore.set(true); else this.isLoading.set(true);
    this.loadError.set(false);
    this.service.getHistory(this.type(), this.id(), more ? this.next() : null, 25, this.applied).pipe(takeUntil(this.destroy$)).subscribe(page => {
      this.isLoading.set(false);
      this.isLoadingMore.set(false);
      if (!page) { this.loadError.set(true); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
      this.events.set(more ? [...this.events(), ...page.items] : page.items);
      this.next.set(page.next);
      if (!more && page.actors) this.actors.set(page.actors);
    });
  }

  loadMore(): void { this.load(true); }

  actionLabel(action: string): string {
    const key = EntityHistoryPage.ACTION_LABELS[action];
    return key ? this.translate.instant(key) : action;
  }
  fieldLabel(field: string): string {
    const key = EntityHistoryPage.FIELD_LABELS[field] ?? EntityHistoryPage.MARC_TAG_LABELS[field];
    return key ? this.translate.instant(key) : field;
  }
  /** Phân biệt null (∅) và chuỗi rỗng ("") — đúng LRC. */
  displayValue(v: string | null | undefined): string {
    if (v === null || v === undefined) return '∅';
    if (v === '') return '""';
    return v;
  }
}
