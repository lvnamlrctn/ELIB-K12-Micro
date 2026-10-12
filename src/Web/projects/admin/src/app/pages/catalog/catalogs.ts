import { CrudConfig } from '../../shared/crud-page';

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
