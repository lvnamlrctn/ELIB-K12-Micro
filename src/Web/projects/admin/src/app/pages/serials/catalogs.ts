import { CrudConfig } from '../../shared/crud-page';

const UNIT_OPTIONS = [
  { value: 1, label: 'SERIALS.UNIT_DAY' },
  { value: 2, label: 'SERIALS.UNIT_WEEK' },
  { value: 3, label: 'SERIALS.UNIT_MONTH' },
  { value: 4, label: 'SERIALS.UNIT_QUARTER' },
  { value: 5, label: 'SERIALS.UNIT_YEAR' },
];

/** Loại ấn phẩm: Báo, Tạp chí… (monolith: /admin/magazine-types, quyền MAGAZINE_TYPES). */
export const SERIAL_TYPES: CrudConfig = {
  title: 'SERIALS.TYPES_TITLE',
  resource: 'types',
  service: 'serials',
  perm: 'MAGAZINE_TYPES',
  searchLabel: 'SERIALS.NAME',
  searchPlaceholder: 'SERIALS.NAME_SEARCH_PH',
  columns: [{ key: 'name', label: 'SERIALS.NAME' }],
  fields: [{ key: 'name', label: 'SERIALS.NAME', type: 'text', required: true, placeholder: 'SERIALS.TYPE_EG' }],
};

/** Tần suất xuất bản (monolith: /admin/magazine-frequencies, quyền FREQUENCIES). */
export const SERIAL_FREQUENCIES: CrudConfig = {
  title: 'SERIALS.FREQUENCIES_TITLE',
  resource: 'frequencies',
  service: 'serials',
  perm: 'FREQUENCIES',
  searchLabel: 'SERIALS.NAME',
  searchPlaceholder: 'SERIALS.NAME_SEARCH_PH',
  columns: [
    { key: 'name', label: 'SERIALS.NAME' },
    { key: 'unitName', label: 'SERIALS.UNIT', width: '120px' },
    { key: 'issuesPerUnit', label: 'SERIALS.ISSUES_PER_UNIT', type: 'number', width: '140px' },
    { key: 'unitsPerIssue', label: 'SERIALS.UNITS_PER_ISSUE', type: 'number', width: '140px' },
    { key: 'sortOrder', label: 'SERIALS.SORT_ORDER', type: 'number', width: '100px' },
  ],
  fields: [
    { key: 'name', label: 'SERIALS.NAME', type: 'text', required: true, placeholder: 'SERIALS.FREQUENCY_EG' },
    { key: 'unit', label: 'SERIALS.UNIT', type: 'select', required: true, options: UNIT_OPTIONS, half: true },
    { key: 'sortOrder', label: 'SERIALS.SORT_ORDER', type: 'number', half: true },
    { key: 'issuesPerUnit', label: 'SERIALS.ISSUES_PER_UNIT', type: 'number', half: true, hint: 'SERIALS.ISSUES_PER_UNIT_HINT' },
    { key: 'unitsPerIssue', label: 'SERIALS.UNITS_PER_ISSUE', type: 'number', half: true, hint: 'SERIALS.UNITS_PER_ISSUE_HINT' },
    { key: 'releaseDay', label: 'SERIALS.RELEASE_DAY', type: 'number', half: true },
  ],
  defaults: { unit: 3, sortOrder: 0 },
};

/** Mẫu đánh số kỳ (monolith: /admin/magazine-patterns, quyền PATTERNS) — chi tiết X/Y/Z nằm ngay trong bản ghi. */
export const SERIAL_PATTERNS: CrudConfig = {
  title: 'SERIALS.PATTERNS_TITLE',
  resource: 'patterns',
  service: 'serials',
  perm: 'PATTERNS',
  searchLabel: 'SERIALS.NAME',
  searchPlaceholder: 'SERIALS.NAME_SEARCH_PH',
  columns: [
    { key: 'name', label: 'SERIALS.NAME' },
    { key: 'labelTemplate', label: 'SERIALS.LABEL_TEMPLATE', type: 'mono', width: '200px' },
    { key: 'sortOrder', label: 'SERIALS.SORT_ORDER', type: 'number', width: '100px' },
  ],
  fields: [
    { key: 'name', label: 'SERIALS.NAME', type: 'text', required: true },
    { key: 'description', label: 'SERIALS.DESCRIPTION', type: 'text' },
    { key: 'labelTemplate', label: 'SERIALS.LABEL_TEMPLATE', type: 'text', placeholder: 'SERIALS.LABEL_TEMPLATE_EG', hint: 'SERIALS.LABEL_TEMPLATE_HINT', half: true },
    { key: 'sortOrder', label: 'SERIALS.SORT_ORDER', type: 'number', half: true },
    { key: 'nameX', label: 'SERIALS.NAME_X', type: 'text', half: true },
    { key: 'stepX', label: 'SERIALS.STEP_X', type: 'number', half: true },
    { key: 'maxX', label: 'SERIALS.MAX_X', type: 'number', half: true, hint: 'SERIALS.MAX_HINT' },
    { key: 'resetX', label: 'SERIALS.RESET_X', type: 'number', half: true },
    { key: 'nameY', label: 'SERIALS.NAME_Y', type: 'text', half: true },
    { key: 'stepY', label: 'SERIALS.STEP_Y', type: 'number', half: true },
    { key: 'maxY', label: 'SERIALS.MAX_Y', type: 'number', half: true },
    { key: 'resetY', label: 'SERIALS.RESET_Y', type: 'number', half: true },
    { key: 'nameZ', label: 'SERIALS.NAME_Z', type: 'text', half: true },
    { key: 'stepZ', label: 'SERIALS.STEP_Z', type: 'number', half: true },
    { key: 'maxZ', label: 'SERIALS.MAX_Z', type: 'number', half: true },
    { key: 'resetZ', label: 'SERIALS.RESET_Z', type: 'number', half: true },
  ],
  defaults: { labelTemplate: 'Số {X}/{Y}', stepX: 1, stepY: 1, stepZ: 1, resetX: 1, resetY: 1, resetZ: 1, sortOrder: 0 }, // i18n-data: mẫu nhãn kỳ báo
  modalWidth: 'max-w-2xl',
};
