import { CrudConfig } from '../../shared/crud-page';

type Named = { publicId: string; id: number; name: string; code?: string };

/** Điểm lưu thông (monolith: /admin/circ-places, quyền CIRC_PLACES) — quầy mượn trả và các kho được mượn tại quầy. */
export const CIRC_PLACES: CrudConfig = {
  title: 'CIRC_CAT.PLACE_TITLE',
  resource: 'circ-places',
  service: 'circulation',
  perm: 'CIRC_PLACES',
  searchLabel: 'CIRC_CAT.PLACE_SEARCH_LABEL',
  searchPlaceholder: 'CIRC_CAT.PLACE_SEARCH_PH',
  columns: [
    { key: 'code', label: 'COMMON.CODE', type: 'mono', width: '120px' },
    { key: 'name', label: 'CIRC_CAT.PLACE_NAME' },
    { key: 'storeIds', label: 'CIRC_CAT.PLACE_STORES', type: 'lookup' },
  ],
  fields: [
    { key: 'code', label: 'COMMON.CODE', type: 'text', required: true, placeholder: 'CIRC_CAT.PLACE_CODE_EG', half: true },
    { key: 'name', label: 'CIRC_CAT.PLACE_NAME', type: 'text', required: true, placeholder: 'CIRC_CAT.PLACE_NAME_EG', half: true },
    {
      key: 'storeIds', label: 'CIRC_CAT.PLACE_STORES_FULL', type: 'multiselect', emptyLabel: 'CIRC_CAT.ALL_STORES',
      hint: 'CIRC_CAT.PLACE_STORES_HINT',
      optionsFrom: async (api) => (await api.crud<Named>('stores', 'holdings').searchAll()).map((s) => ({ value: s.id, label: `${s.code} — ${s.name}` })),
    },
  ],
  defaults: { storeIds: [] },
  modalWidth: 'max-w-lg',
};

/** Chính sách lưu thông (monolith: /admin/circ-policies, quyền CIRC_POLICIES) theo loại bạn đọc × điểm lưu thông. */
export const LOAN_POLICIES: CrudConfig = {
  title: 'CIRC_CAT.POLICY_TITLE',
  resource: 'loan-policies',
  service: 'circulation',
  perm: 'CIRC_POLICIES',
  searchLabel: 'CIRC_CAT.FIND',
  searchPlaceholder: '',
  columns: [
    { key: 'readerTypeId', label: 'CIRC_CAT.READER_TYPE', type: 'lookup' },
    { key: 'circPlaceId', label: 'CIRC_CAT.PLACE', type: 'lookup' },
    { key: 'loanDays', label: 'CIRC_CAT.LOAN_DAYS', type: 'number', width: '120px' },
    { key: 'maxLoans', label: 'CIRC_CAT.MAX_LOANS', type: 'number', width: '120px' },
    { key: 'maxRenewals', label: 'CIRC_CAT.MAX_RENEWALS', type: 'number', width: '130px' },
    { key: 'renewDays', label: 'CIRC_CAT.RENEW_DAYS', type: 'number', width: '160px' },
    { key: 'finePerDay', label: 'CIRC_CAT.FINE_PER_DAY', type: 'number', width: '150px' },
  ],
  fields: [
    {
      key: 'readerTypeId', label: 'CIRC_CAT.READER_TYPE', type: 'select', emptyLabel: 'CIRC_CAT.ALL_READER_TYPES', half: true,
      optionsFrom: async (api) => (await api.crud<Named>('reader-types', 'patron').searchAll()).map((t) => ({ value: t.id, label: t.name })),
    },
    {
      key: 'circPlaceId', label: 'CIRC_CAT.PLACE', type: 'select', emptyLabel: 'CIRC_CAT.ALL_PLACES', half: true,
      optionsFrom: async (api) => (await api.crud<Named>('circ-places', 'circulation').searchAll()).map((p) => ({ value: p.id, label: `${p.code} — ${p.name}` })),
    },
    { key: 'loanDays', label: 'CIRC_CAT.LOAN_DAYS', type: 'number', required: true, half: true },
    { key: 'maxLoans', label: 'CIRC_CAT.MAX_LOANS_FULL', type: 'number', half: true, hint: 'CIRC_CAT.NO_LIMIT' },
    { key: 'maxRenewals', label: 'CIRC_CAT.MAX_RENEWALS_FULL', type: 'number', half: true, hint: 'CIRC_CAT.RENEW_LIMIT_HINT' },
    { key: 'renewDays', label: 'CIRC_CAT.RENEW_DAYS_FULL', type: 'number', required: true, half: true },
    { key: 'finePerDay', label: 'CIRC_CAT.FINE_PER_DAY_FULL', type: 'number', half: true, hint: 'CIRC_CAT.FINE_PER_DAY_HINT' },
    { key: 'maxHolds', label: 'CIRC_CAT.MAX_HOLDS', type: 'number', half: true, hint: 'CIRC_CAT.HOLD_LIMIT_HINT' },
    { key: 'holdDays', label: 'CIRC_CAT.HOLD_DAYS', type: 'number', required: true, half: true },
    { key: 'techFee', label: 'CIRC_CAT.TECH_FEE', type: 'number', half: true },
    { key: 'shippingFee', label: 'CIRC_CAT.SHIPPING_FEE', type: 'number', half: true, hint: 'CIRC_CAT.DOC_FEES_HINT' },
    { key: 'renewFromToday', label: 'CIRC_CAT.RENEW_FROM_TODAY', type: 'checkbox' },
    {
      key: 'bibTypeRules', label: 'CIRC_CAT.BIB_TYPE_RULES', type: 'rows', addLabel: 'CIRC_CAT.BIB_TYPE_RULE_ADD', hint: 'CIRC_CAT.BIB_TYPE_RULES_HINT',
      rowFields: [
        {
          key: 'bibTypeId', label: 'CIRC_CAT.BIB_TYPE', type: 'select',
          optionsFrom: async (api) => (await api.crud<Named>('bib-types', 'catalog').searchAll()).map((t) => ({ value: t.id, label: t.name })),
        },
        { key: 'loanDays', label: 'CIRC_CAT.LOAN_DAYS', type: 'number' },
        { key: 'maxLoans', label: 'CIRC_CAT.MAX_LOANS', type: 'number' },
        { key: 'maxRenewals', label: 'CIRC_CAT.MAX_RENEWALS', type: 'number' },
        { key: 'maxHolds', label: 'CIRC_CAT.MAX_HOLDS_SHORT', type: 'number' },
      ],
    },
  ],
  defaults: {
    readerTypeId: null, circPlaceId: null, loanDays: 14, maxLoans: null, maxRenewals: null, renewDays: 7, finePerDay: 0, renewFromToday: false,
    maxHolds: null, holdDays: 2, techFee: 0, shippingFee: 0, bibTypeRules: [],
  },
  modalWidth: 'max-w-2xl',
};

/** Lý do phạt (monolith: /admin/cfine-types, quyền FINE_REASONS). Lý do có trạng thái "Mất" đóng lượt mượn và báo kho mất sách. */
export const FINE_REASONS: CrudConfig = {
  title: 'CIRC_CAT.REASON_TITLE',
  resource: 'fine-reasons',
  service: 'circulation',
  perm: 'FINE_REASONS',
  searchLabel: 'CIRC_CAT.REASON_SEARCH_LABEL',
  searchPlaceholder: 'CIRC_CAT.REASON_SEARCH_PH',
  columns: [
    { key: 'code', label: 'COMMON.CODE', type: 'mono', width: '140px' },
    { key: 'name', label: 'CIRC_CAT.REASON_NAME' },
    { key: 'amount', label: 'CIRC_CAT.AMOUNT', type: 'number', width: '160px' },
    { key: 'itemStatus', label: 'CIRC_CAT.ITEM_STATUS', type: 'lookup', width: '180px' },
  ],
  fields: [
    { key: 'code', label: 'COMMON.CODE', type: 'text', required: true, placeholder: 'CIRC_CAT.REASON_CODE_EG', half: true, lockWhen: (x) => x['isBuiltIn'] === true },
    { key: 'name', label: 'CIRC_CAT.REASON_NAME', type: 'text', required: true, placeholder: 'CIRC_CAT.REASON_NAME_EG', half: true },
    { key: 'amount', label: 'CIRC_CAT.AMOUNT', type: 'number', half: true, hint: 'CIRC_CAT.AMOUNT_HINT' },
    {
      key: 'itemStatus', label: 'CIRC_CAT.ITEM_STATUS_FULL', type: 'select', emptyLabel: 'CIRC_CAT.NO_CHANGE', half: true,
      options: [{ value: 'L', label: 'CIRC_CAT.LOST' }],
      hint: 'CIRC_CAT.LOST_HINT',
    },
  ],
  defaults: { amount: 0, itemStatus: null },
  badge: (x) => (x['isBuiltIn'] ? 'CIRC_CAT.BUILT_IN' : null),
  toolbar: [
    {
      label: 'CIRC_CAT.ADD_DEFAULT_REASONS', icon: 'playlist_add', action: 'add',
      run: async (api) => { const r = await api.addDefaultFineReasons(); return r.added ? { key: 'CIRC_CAT.REASONS_ADDED', params: { count: r.added } } : 'CIRC_CAT.REASONS_COMPLETE'; },
    },
  ],
  modalWidth: 'max-w-lg',
};

/** Photo copy tài liệu (monolith: /admin/photocopy, quyền C_PHOTO). Thành tiền = số trang × số bản × đơn giá, tính ở server. */
export const PHOTOCOPIES: CrudConfig = {
  title: 'CIRC_CAT.PHOTO_TITLE',
  resource: 'photocopies',
  service: 'circulation',
  perm: 'C_PHOTO',
  searchLabel: 'CIRC_CAT.PHOTO_SEARCH_LABEL',
  searchPlaceholder: 'VD: HS001, VV000123...',
  columns: [
    { key: 'cardNo', label: 'CIRC_CAT.CARD_NO', type: 'mono', width: '110px' },
    { key: 'readerName', label: 'CIRC_CAT.READER', width: '160px' },
    { key: 'barcode', label: 'CIRC_CAT.BARCODE_SHORT', type: 'mono', width: '120px' },
    { key: 'title', label: 'CIRC_CAT.TITLE_COL' },
    { key: 'pages', label: 'Trang', width: '90px' },
    { key: 'copies', label: 'CIRC_CAT.COPIES', type: 'number', width: '70px' },
    { key: 'total', label: 'CIRC_CAT.TOTAL', type: 'number', width: '120px' },
    { key: 'paid', label: 'CIRC_CAT.PAID', type: 'bool', width: '80px' },
    { key: 'photoDate', label: 'CIRC_CAT.DATE', type: 'date', width: '110px' },
  ],
  fields: [
    { key: 'cardNo', label: 'CIRC_CAT.CARD_NO_FULL', type: 'text', required: true, half: true },
    { key: 'barcode', label: 'CIRC_CAT.BARCODE', type: 'text', required: true, half: true },
    { key: 'fromPage', label: 'CIRC_CAT.FROM_PAGE', type: 'number', required: true, half: true },
    { key: 'toPage', label: 'CIRC_CAT.TO_PAGE', type: 'number', required: true, half: true },
    { key: 'copies', label: 'CIRC_CAT.COPIES', type: 'number', required: true, half: true },
    { key: 'unitPrice', label: 'CIRC_CAT.UNIT_PRICE', type: 'number', required: true, half: true },
    { key: 'note', label: 'COMMON.NOTE', type: 'text' },
    { key: 'paid', label: 'CIRC_CAT.PAID_FULL', type: 'checkbox' },
  ],
  defaults: { fromPage: 1, toPage: 1, copies: 1, unitPrice: 500, paid: false },
  modalWidth: 'max-w-lg',
};
