import { CrudConfig } from '../../shared/crud-page';
import { TreeCatalogConfig } from '../../shared/tree-catalog-page';

type Named = { publicId: string; id: number; name: string };

const LANGUAGE_OPTIONS = [
  { value: 'vi', label: 'Tiếng Việt' }, // i18n-data: tên ngôn ngữ hiện đúng như vậy ở mọi ngôn ngữ giao diện
  { value: 'en', label: 'English' },
];

/** Bộ sưu tập tài liệu số (LRC: EbookCollection, quyền DIGITAL_COLLECTIONS) — cây, có ảnh bìa công khai. */
export const DIGITAL_COLLECTIONS: TreeCatalogConfig = {
  title: 'DIGITAL_CAT.COLLECTIONS_TITLE',
  resource: 'collections',
  service: 'digital',
  perm: 'DIGITAL_COLLECTIONS',
  extraFields: [
    { key: 'language', label: 'DIGITAL_CAT.LANGUAGE', type: 'select', options: LANGUAGE_OPTIONS, half: true },
    { key: 'link', label: 'DIGITAL_CAT.LINK', type: 'text', half: true },
    { key: 'allowDownload', label: 'DIGITAL_CAT.ALLOW_DOWNLOAD', type: 'checkbox' },
    { key: 'share', label: 'DIGITAL_CAT.SHARE', type: 'checkbox' },
  ],
  defaults: { language: 'vi', link: null },
  coverUpload: true,
  modalWidth: 'max-w-xl',
};

/** Môn loại tài liệu số (LRC: EbookSubject, quyền DIGITAL_SUBJECTS) — cây. */
export const DIGITAL_SUBJECTS: TreeCatalogConfig = {
  title: 'DIGITAL_CAT.SUBJECTS_TITLE',
  resource: 'subjects',
  service: 'digital',
  perm: 'DIGITAL_SUBJECTS',
  extraFields: [
    { key: 'ddc', label: 'DIGITAL_CAT.DDC', type: 'text', half: true },
    { key: 'language', label: 'DIGITAL_CAT.LANGUAGE', type: 'select', options: LANGUAGE_OPTIONS, half: true },
    { key: 'link', label: 'DIGITAL_CAT.LINK', type: 'text' },
  ],
  defaults: { ddc: null, language: 'vi', link: null },
};

/** Chủ đề tài liệu số (LRC: EbookTopic, quyền DIGITAL_TOPICS) — cây; RequiresLogin ẩn với bạn đọc chưa đăng nhập trên OPAC. */
export const DIGITAL_TOPICS: TreeCatalogConfig = {
  title: 'DIGITAL_CAT.TOPICS_TITLE',
  resource: 'topics',
  service: 'digital',
  perm: 'DIGITAL_TOPICS',
  extraFields: [
    { key: 'requiresLogin', label: 'DIGITAL_CAT.REQUIRES_LOGIN', type: 'checkbox' },
    { key: 'description', label: 'DIGITAL_CAT.DESCRIPTION', type: 'textarea' },
    { key: 'keyword', label: 'DIGITAL_CAT.KEYWORD', type: 'text', half: true },
    { key: 'ddc', label: 'DIGITAL_CAT.DDC', type: 'text', half: true },
    { key: 'pageTitle', label: 'DIGITAL_CAT.PAGE_TITLE', type: 'text', half: true },
    { key: 'language', label: 'DIGITAL_CAT.LANGUAGE', type: 'select', options: LANGUAGE_OPTIONS, half: true },
    { key: 'metaDescription', label: 'DIGITAL_CAT.META_DESCRIPTION', type: 'textarea' },
  ],
  defaults: { requiresLogin: false, description: null, keyword: null, ddc: null, pageTitle: null, language: 'vi', metaDescription: null },
  modalWidth: 'max-w-xl',
};

/** Loại tài liệu số (LRC: DigType, quyền DIGITAL_DIG_TYPES) — danh mục phẳng, mã không trùng. */
export const DIGITAL_DIG_TYPES: CrudConfig = {
  title: 'DIGITAL_CAT.DIG_TYPES_TITLE',
  resource: 'dig-types',
  service: 'digital',
  perm: 'DIGITAL_DIG_TYPES',
  searchLabel: 'COMMON.SEARCH',
  searchPlaceholder: 'DIGITAL_CAT.DIG_TYPE_SEARCH_PH',
  columns: [
    { key: 'code', label: 'COMMON.CODE', type: 'mono', width: '120px' },
    { key: 'descriptionVn', label: 'DIGITAL_CAT.DESCRIPTION_VN' },
    { key: 'descriptionEn', label: 'DIGITAL_CAT.DESCRIPTION_EN' },
    { key: 'sortOrder', label: 'DIGITAL_CAT.SORT_ORDER', type: 'number', width: '110px' },
  ],
  fields: [
    { key: 'code', label: 'COMMON.CODE', type: 'text', required: true, half: true },
    { key: 'sortOrder', label: 'DIGITAL_CAT.SORT_ORDER', type: 'number', half: true },
    { key: 'descriptionVn', label: 'DIGITAL_CAT.DESCRIPTION_VN', type: 'text' },
    { key: 'descriptionEn', label: 'DIGITAL_CAT.DESCRIPTION_EN', type: 'text' },
    { key: 'language', label: 'DIGITAL_CAT.LANGUAGE', type: 'select', options: LANGUAGE_OPTIONS, emptyLabel: 'COMMON.ALL' },
  ],
  defaults: { sortOrder: 0, descriptionVn: null, descriptionEn: null, language: null },
  modalWidth: 'max-w-lg',
};

/** Chính sách tài liệu số theo loại bạn đọc (LRC: PolicyDigital, quyền DIGITAL_POLICIES) — kiểm trước PolicyDigitalByCollection. */
export const DIGITAL_POLICIES: CrudConfig = {
  title: 'DIGITAL_CAT.POLICIES_TITLE',
  resource: 'policies',
  service: 'digital',
  perm: 'DIGITAL_POLICIES',
  searchLabel: 'COMMON.SEARCH',
  searchPlaceholder: '',
  columns: [
    { key: 'readerTypeId', label: 'DIGITAL_CAT.READER_TYPE', type: 'lookup' },
    { key: 'maxDocument', label: 'DIGITAL_CAT.MAX_DOCUMENT', type: 'number', width: '160px' },
    { key: 'reservationExpireHours', label: 'DIGITAL_CAT.RESERVATION_EXPIRE_HOURS', type: 'number', width: '180px' },
  ],
  fields: [
    {
      key: 'readerTypeId', label: 'DIGITAL_CAT.READER_TYPE', type: 'select', emptyLabel: 'DIGITAL_CAT.ALL_READER_TYPES', half: true,
      optionsFrom: async (api) => (await api.crud<Named>('reader-types', 'patron').searchAll()).map((t) => ({ value: t.id, label: t.name })),
    },
    { key: 'maxDocument', label: 'DIGITAL_CAT.MAX_DOCUMENT', type: 'number', half: true, hint: 'DIGITAL_CAT.MAX_DOCUMENT_HINT' },
    { key: 'maxPage', label: 'DIGITAL_CAT.MAX_PAGE', type: 'number', half: true },
    { key: 'maxSize', label: 'DIGITAL_CAT.MAX_SIZE', type: 'number', half: true, hint: 'DIGITAL_CAT.MAX_SIZE_HINT' },
    {
      key: 'reservationExpireHours', label: 'DIGITAL_CAT.RESERVATION_EXPIRE_HOURS', type: 'number', half: true,
      hint: 'DIGITAL_CAT.RESERVATION_EXPIRE_HOURS_HINT',
    },
  ],
  defaults: { readerTypeId: null, maxDocument: null, maxPage: null, maxSize: null, reservationExpireHours: null },
  modalWidth: 'max-w-lg',
};

const METADATA_INPUTS = [
  { value: 'text', label: 'DIGITAL_METADATA.INPUT_TEXT' },
  { value: 'textarea', label: 'DIGITAL_METADATA.INPUT_TEXTAREA' },
  { value: 'date', label: 'DIGITAL_METADATA.INPUT_DATE' },
  { value: 'number', label: 'DIGITAL_METADATA.INPUT_NUMBER' },
];

/** Lược đồ metadata (LRC: MetadataSchemaRegistry, quyền DIGITAL_METADATA) — ví dụ Dublin Core. */
export const DIGITAL_METADATA_SCHEMAS: CrudConfig = {
  title: 'DIGITAL_METADATA.SCHEMAS_TITLE',
  resource: 'metadata-schemas',
  service: 'digital',
  perm: 'DIGITAL_METADATA',
  searchLabel: 'COMMON.SEARCH',
  searchPlaceholder: '',
  hasStatus: true,
  columns: [
    { key: 'shortId', label: 'DIGITAL_METADATA.SHORT_ID', type: 'mono', width: '140px' },
    { key: 'nameSpace', label: 'DIGITAL_METADATA.NAMESPACE' },
    { key: 'language', label: 'DIGITAL_CAT.LANGUAGE', width: '100px' },
    { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' },
  ],
  fields: [
    { key: 'shortId', label: 'DIGITAL_METADATA.SHORT_ID', type: 'text', required: true, half: true },
    { key: 'language', label: 'DIGITAL_CAT.LANGUAGE', type: 'select', options: LANGUAGE_OPTIONS, emptyLabel: 'COMMON.ALL', half: true },
    { key: 'nameSpace', label: 'DIGITAL_METADATA.NAMESPACE', type: 'text', required: true },
  ],
  defaults: { shortId: '', nameSpace: '', language: null },
  modalWidth: 'max-w-lg',
};

/** Trường metadata (LRC: MetaDataFieldRegistery, quyền DIGITAL_METADATA) — Field/Subfield, mô tả vi/en, kiểu nhập. */
export const DIGITAL_METADATA_FIELDS: CrudConfig = {
  title: 'DIGITAL_METADATA.FIELDS_TITLE',
  resource: 'metadata-fields',
  service: 'digital',
  perm: 'DIGITAL_METADATA',
  searchLabel: 'COMMON.SEARCH',
  searchPlaceholder: '',
  hasStatus: true,
  columns: [
    { key: 'schemaShortId', label: 'DIGITAL_METADATA.SCHEMA', type: 'mono', width: '110px' },
    { key: 'field', label: 'DIGITAL_METADATA.FIELD', type: 'mono', width: '150px' },
    { key: 'subfield', label: 'DIGITAL_METADATA.SUBFIELD', type: 'mono', width: '130px' },
    { key: 'descriptionVn', label: 'DIGITAL_CAT.DESCRIPTION_VN' },
    { key: 'input', label: 'DIGITAL_METADATA.INPUT', width: '100px' },
    { key: 'sortOrder', label: 'DIGITAL_CAT.SORT_ORDER', type: 'number', width: '90px' },
    { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' },
  ],
  fields: [
    {
      key: 'schemaPublicId', label: 'DIGITAL_METADATA.SCHEMA', type: 'select', required: true,
      optionsFrom: async (api) =>
        (await api.crud<{ publicId: string; shortId: string }>('metadata-schemas', 'digital').searchAll()).map((s) => ({ value: s.publicId, label: s.shortId })),
    },
    { key: 'sortOrder', label: 'DIGITAL_CAT.SORT_ORDER', type: 'number', half: true },
    { key: 'field', label: 'DIGITAL_METADATA.FIELD', type: 'text', required: true, half: true },
    { key: 'subfield', label: 'DIGITAL_METADATA.SUBFIELD', type: 'text', half: true },
    { key: 'input', label: 'DIGITAL_METADATA.INPUT', type: 'select', options: METADATA_INPUTS, half: true },
    { key: 'descriptionVn', label: 'DIGITAL_CAT.DESCRIPTION_VN', type: 'text', required: true },
    { key: 'descriptionEn', label: 'DIGITAL_CAT.DESCRIPTION_EN', type: 'text' },
  ],
  defaults: { schemaPublicId: null, field: '', subfield: null, descriptionVn: '', descriptionEn: null, sortOrder: 0, input: 'text', exportField: null },
  modalWidth: 'max-w-lg',
};

/** Đánh giá tài liệu số (LRC: EbookReview, quyền DIGITAL_REVIEWS) — bạn đọc gửi ẩn danh qua OPAC, thủ thư duyệt bằng cột trạng thái. */
export const DIGITAL_REVIEWS: CrudConfig = {
  title: 'DIGITAL_REVIEWS_PAGE.TITLE',
  resource: 'reviews',
  service: 'digital',
  perm: 'DIGITAL_REVIEWS',
  searchLabel: 'COMMON.SEARCH',
  searchPlaceholder: '',
  columns: [
    { key: 'itemTitle', label: 'DIGITAL_REVIEWS_PAGE.ITEM' },
    { key: 'rating', label: 'DIGITAL_REVIEWS_PAGE.RATING', width: '90px' },
    { key: 'displayName', label: 'DIGITAL_REVIEWS_PAGE.DISPLAY_NAME' },
    { key: 'rejectReason', label: 'DIGITAL_REVIEWS_PAGE.REJECT_REASON' },
    { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' },
  ],
  rowActions: [
    {
      icon: 'check_circle', title: 'DIGITAL_REVIEWS_PAGE.APPROVE', action: 'approve', show: (r) => r['status'] !== 2,
      run: async (api, row) => { await api.approveEbookReview(row.publicId); return { key: 'DIGITAL_REVIEWS_PAGE.APPROVED' }; },
    },
    {
      icon: 'block', title: 'DIGITAL_REVIEWS_PAGE.REJECT', action: 'approve', show: (r) => !r['rejectedAt'],
      inputs: [{ key: 'reason', label: 'DIGITAL_REVIEWS_PAGE.REASON' }],
      run: async (api, row, v) => { await api.rejectEbookReview(row.publicId, v['reason'] ?? ''); return { key: 'DIGITAL_REVIEWS_PAGE.REJECTED' }; },
    },
  ],
  fields: [
    {
      key: 'ebookItemPublicId', label: 'DIGITAL_REVIEWS_PAGE.ITEM', type: 'select', required: true,
      optionsFrom: async (api) =>
        (await api.crud<{ publicId: string; title: string }>('items', 'digital').searchAll()).map((i) => ({ value: i.publicId, label: i.title })),
    },
    { key: 'rating', label: 'DIGITAL_REVIEWS_PAGE.RATING', type: 'number', half: true },
    { key: 'displayName', label: 'DIGITAL_REVIEWS_PAGE.DISPLAY_NAME', type: 'text', half: true },
    { key: 'email', label: 'DIGITAL_REVIEWS_PAGE.EMAIL', type: 'text', half: true },
    { key: 'content', label: 'DIGITAL_REVIEWS_PAGE.CONTENT', type: 'textarea' },
  ],
  defaults: { ebookItemPublicId: null, rating: null, displayName: null, email: null, content: null },
  modalWidth: 'max-w-xl',
};

/** Chính sách tài liệu số theo loại bạn đọc × bộ sưu tập (LRC: PolicyDigitalByCollection, quyền DIGITAL_POLICIES_BY_COLLECTION). */
export const DIGITAL_POLICIES_BY_COLLECTION: CrudConfig = {
  title: 'DIGITAL_CAT.POLICIES_BY_COLLECTION_TITLE',
  resource: 'policies-by-collection',
  service: 'digital',
  perm: 'DIGITAL_POLICIES_BY_COLLECTION',
  searchLabel: 'COMMON.SEARCH',
  searchPlaceholder: '',
  columns: [
    { key: 'collectionName', label: 'DIGITAL_CAT.COLLECTION' },
    { key: 'readerTypeId', label: 'DIGITAL_CAT.READER_TYPE', type: 'lookup' },
    { key: 'read', label: 'DIGITAL_CAT.READ_ALLOWED', type: 'bool', width: '110px' },
    { key: 'download', label: 'DIGITAL_CAT.DOWNLOAD_ALLOWED', type: 'bool', width: '130px' },
    { key: 'offlineDays', label: 'DIGITAL_CAT.OFFLINE_DAYS', type: 'number', width: '140px' },
  ],
  fields: [
    {
      key: 'collectionId', label: 'DIGITAL_CAT.COLLECTION', type: 'select', required: true, half: true,
      optionsFrom: async (api) => (await api.crud<Named>('collections', 'digital').searchAll()).map((c) => ({ value: c.id, label: c.name })),
    },
    {
      key: 'readerTypeId', label: 'DIGITAL_CAT.READER_TYPE', type: 'select', emptyLabel: 'DIGITAL_CAT.ALL_READER_TYPES', half: true,
      optionsFrom: async (api) => (await api.crud<Named>('reader-types', 'patron').searchAll()).map((t) => ({ value: t.id, label: t.name })),
    },
    { key: 'read', label: 'DIGITAL_CAT.READ_ALLOWED', type: 'checkbox' },
    { key: 'download', label: 'DIGITAL_CAT.DOWNLOAD_ALLOWED', type: 'checkbox' },
    { key: 'comment', label: 'DIGITAL_CAT.COMMENT_ALLOWED', type: 'checkbox' },
    { key: 'offlineDays', label: 'DIGITAL_CAT.OFFLINE_DAYS', type: 'number', half: true, hint: 'DIGITAL_CAT.OFFLINE_DAYS_HINT' },
    { key: 'maxDocument', label: 'DIGITAL_CAT.MAX_DOCUMENT', type: 'number', half: true },
    { key: 'maxPage', label: 'DIGITAL_CAT.MAX_PAGE', type: 'number', half: true },
    { key: 'maxSize', label: 'DIGITAL_CAT.MAX_SIZE', type: 'number', half: true },
    {
      key: 'reservationExpireHours', label: 'DIGITAL_CAT.RESERVATION_EXPIRE_HOURS', type: 'number', half: true,
      hint: 'DIGITAL_CAT.RESERVATION_EXPIRE_HOURS_UNUSED_HINT',
    },
  ],
  defaults: { readerTypeId: null, read: true, comment: false, download: false, offlineDays: null, maxDocument: null, maxPage: null, maxSize: null, reservationExpireHours: null },
  modalWidth: 'max-w-2xl',
};
