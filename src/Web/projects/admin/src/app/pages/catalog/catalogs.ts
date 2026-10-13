import { CrudConfig } from '../../shared/crud-page';

/** Từ điển biên mục (monolith: DicAuthor/DicPublisher/DicKeyword/DicClass) — tự học khi lưu biểu ghi, gợi ý trong trình biên mục. */
function dictionary(resource: string, perm: string, title: string, withDescription = false): CrudConfig {
  return {
    title, resource, service: 'catalog', perm, importable: true,
    searchLabel: 'DICTIONARY.SEARCH_LABEL', searchPlaceholder: 'DICTIONARY.SEARCH_PH',
    columns: [
      { key: 'value', label: 'DICTIONARY.VALUE', type: withDescription ? 'mono' : undefined, width: withDescription ? '160px' : undefined },
      ...(withDescription ? [{ key: 'description', label: 'DICTIONARY.DESCRIPTION' }] : []),
      { key: 'useCount', label: 'DICTIONARY.USE_COUNT', type: 'number', width: '120px' },
      { key: 'autoAdded', label: 'DICTIONARY.AUTO_ADDED', type: 'bool', width: '130px' },
    ],
    fields: [
      { key: 'value', label: 'DICTIONARY.VALUE', type: 'text', required: true },
      ...(withDescription ? [{ key: 'description', label: 'DICTIONARY.DESCRIPTION', type: 'text' as const }] : []),
    ],
    modalWidth: 'max-w-lg',
  };
}

export const DIC_AUTHORS = dictionary('dic-authors', 'DIC_AUTHORS', 'DICTIONARY.AUTHORS');
export const DIC_PUBLISHERS = dictionary('dic-publishers', 'DIC_PUBLISHERS', 'DICTIONARY.PUBLISHERS');
export const DIC_KEYWORDS = dictionary('dic-keywords', 'DIC_KEYWORDS', 'DICTIONARY.KEYWORDS');
export const DIC_CLASSES = dictionary('dic-classes', 'DIC_CLASSES', 'DICTIONARY.CLASSES', true);

/**
 * Cấu hình ISBD theo loại biểu ghi (monolith: ConfigIsbd + IsbdField + IsbdSubfield, quyền CONFIG_ISBD). Dòng để trống trường con =
 * quy tắc của cả trường; loại chưa cấu hình dùng quy tắc ISBD mặc định. Thêm mới điền sẵn mẫu tương đương quy tắc mặc định.
 */
export const ISBD_CONFIGS: CrudConfig = {
  title: 'ISBD_CONFIG.TITLE', resource: 'isbd-configs', service: 'catalog', perm: 'CONFIG_ISBD',
  searchLabel: 'ISBD_CONFIG.SEARCH_LABEL', searchPlaceholder: '',
  columns: [{ key: 'bibTypeId', label: 'ISBD_CONFIG.BIB_TYPE', type: 'lookup' }],
  fields: [
    {
      key: 'bibTypeId', label: 'ISBD_CONFIG.BIB_TYPE', type: 'select', required: true,
      optionsFrom: async (api) => (await api.crud<{ publicId: string; id: number; name: string }>('bib-types', 'catalog').searchAll()).map((t) => ({ value: t.id, label: t.name })),
    },
    {
      key: 'rules', label: 'ISBD_CONFIG.RULES', type: 'rows', addLabel: 'ISBD_CONFIG.ADD_RULE', hint: 'ISBD_CONFIG.RULES_HINT',
      rowFields: [
        { key: 'tag', label: 'ISBD_CONFIG.TAG', type: 'text', maxLength: 3 },
        { key: 'code', label: 'ISBD_CONFIG.CODE', type: 'text', maxLength: 1 },
        { key: 'prefix', label: 'ISBD_CONFIG.PREFIX', type: 'text', maxLength: 20 },
        { key: 'suffix', label: 'ISBD_CONFIG.SUFFIX', type: 'text', maxLength: 20 },
        { key: 'separator', label: 'ISBD_CONFIG.SEPARATOR', type: 'text', maxLength: 20 },
      ],
    },
  ],
  defaults: { bibTypeId: null, rules: [] },
  defaultsFrom: async (api) => ({ rules: await api.isbdTemplate() }),
  modalWidth: 'max-w-3xl',
};

/** Leader/06 — dạng tài liệu (MARC21). */
export const RECORD_TYPES = [
  { value: 'a', label: 'BIB_TYPES_PAGE.RECORD_TYPE.a' },
  { value: 't', label: 'BIB_TYPES_PAGE.RECORD_TYPE.t' },
  { value: 'e', label: 'BIB_TYPES_PAGE.RECORD_TYPE.e' },
  { value: 'g', label: 'BIB_TYPES_PAGE.RECORD_TYPE.g' },
  { value: 'i', label: 'BIB_TYPES_PAGE.RECORD_TYPE.i' },
  { value: 'j', label: 'BIB_TYPES_PAGE.RECORD_TYPE.j' },
  { value: 'c', label: 'BIB_TYPES_PAGE.RECORD_TYPE.c' },
  { value: 'k', label: 'BIB_TYPES_PAGE.RECORD_TYPE.k' },
  { value: 'm', label: 'BIB_TYPES_PAGE.RECORD_TYPE.m' },
  { value: 'o', label: 'BIB_TYPES_PAGE.RECORD_TYPE.o' },
  { value: 'r', label: 'BIB_TYPES_PAGE.RECORD_TYPE.r' },
];

/** Leader/07 — cấp thư mục. */
export const BIB_LEVELS = [
  { value: 'm', label: 'BIB_TYPES_PAGE.BIB_LEVEL.m' },
  { value: 's', label: 'BIB_TYPES_PAGE.BIB_LEVEL.s' },
  { value: 'a', label: 'BIB_TYPES_PAGE.BIB_LEVEL.a' },
  { value: 'b', label: 'BIB_TYPES_PAGE.BIB_LEVEL.b' },
  { value: 'c', label: 'BIB_TYPES_PAGE.BIB_LEVEL.c' },
  { value: 'd', label: 'BIB_TYPES_PAGE.BIB_LEVEL.d' },
  { value: 'i', label: 'BIB_TYPES_PAGE.BIB_LEVEL.i' },
];

/** Loại biểu ghi (monolith: /admin/bib-types, quyền BIB_TYPES). */
export const BIB_TYPES: CrudConfig = {
  title: 'BIB_TYPES_PAGE.TITLE',
  resource: 'bib-types',
  service: 'catalog',
  perm: 'BIB_TYPES',
  searchLabel: 'BIB_TYPES_PAGE.SEARCH_LABEL',
  searchPlaceholder: 'BIB_TYPES_PAGE.SEARCH_PH',
  columns: [
    { key: 'code', label: 'COMMON.CODE', type: 'mono', width: '140px' },
    { key: 'name', label: 'BIB_TYPES_PAGE.NAME' },
    { key: 'recordType', label: 'Leader/06', type: 'mono', width: '110px' },
    { key: 'bibLevel', label: 'Leader/07', type: 'mono', width: '110px' },
  ],
  fields: [
    { key: 'name', label: 'BIB_TYPES_PAGE.NAME', type: 'text', required: true, placeholder: 'BIB_TYPES_PAGE.NAME_EG' },
    { key: 'code', label: 'COMMON.CODE', type: 'text', required: true, placeholder: 'BIB_TYPES_PAGE.CODE_EG', hint: 'BIB_TYPES_PAGE.CODE_HINT' },
    { key: 'recordType', label: 'BIB_TYPES_PAGE.LEADER06', type: 'select', options: RECORD_TYPES },
    { key: 'bibLevel', label: 'BIB_TYPES_PAGE.LEADER07', type: 'select', options: BIB_LEVELS },
  ],
  defaults: { recordType: 'a', bibLevel: 'm' },
  modalWidth: 'max-w-lg',
  toolbar: [
    {
      label: 'BIB_TYPES_PAGE.RESTORE',
      icon: 'restore',
      action: 'add',
      run: async (api) => {
        const r = await api.restoreCatalogDefaults();
        return r.added || r.worksheets ? { key: 'BIB_TYPES_PAGE.RESTORED', params: { types: r.added, worksheets: r.worksheets } } : 'BIB_TYPES_PAGE.RESTORE_NOTHING';
      },
    },
  ],
};
