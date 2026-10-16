import { CrudConfig } from '../../shared/crud-page';

/** Nguồn bổ sung (monolith: /admin/ab-sources, quyền AB_SOURCES). */
export const ACQ_SOURCES: CrudConfig = {
  title: 'ACQ_CAT.SOURCE_TITLE',
  resource: 'sources',
  service: 'acquisition',
  perm: 'AB_SOURCES',
  searchLabel: 'ACQ_CAT.NAME',
  searchPlaceholder: 'ACQ_CAT.NAME_SEARCH_PH',
  columns: [{ key: 'name', label: 'ACQ_CAT.NAME' }],
  fields: [{ key: 'name', label: 'ACQ_CAT.NAME', type: 'text', required: true, placeholder: 'ACQ_CAT.SOURCE_EG' }],
  importable: true,
};

/** Nhà cung cấp (monolith: /admin/suppliers, quyền SUPPLIERS). */
export const SUPPLIERS: CrudConfig = {
  title: 'ACQ_CAT.SUPPLIER_TITLE',
  resource: 'suppliers',
  service: 'acquisition',
  perm: 'SUPPLIERS',
  searchLabel: 'ACQ_CAT.SUPPLIER_SEARCH',
  searchPlaceholder: 'ACQ_CAT.SUPPLIER_SEARCH_PH',
  columns: [
    { key: 'name', label: 'ACQ_CAT.NAME' },
    { key: 'phone', label: 'ACQ_CAT.PHONE', width: '140px' },
    { key: 'email', label: 'ACQ_CAT.EMAIL', width: '200px' },
    { key: 'taxCode', label: 'ACQ_CAT.TAX_CODE', type: 'mono', width: '140px' },
    { key: 'representative', label: 'ACQ_CAT.REPRESENTATIVE', width: '180px' },
  ],
  fields: [
    { key: 'name', label: 'ACQ_CAT.NAME', type: 'text', required: true },
    { key: 'address', label: 'ACQ_CAT.ADDRESS', type: 'text' },
    { key: 'phone', label: 'ACQ_CAT.PHONE', type: 'text', half: true },
    { key: 'fax', label: 'ACQ_CAT.FAX', type: 'text', half: true },
    { key: 'email', label: 'ACQ_CAT.EMAIL', type: 'text', half: true },
    { key: 'website', label: 'ACQ_CAT.WEBSITE', type: 'text', half: true },
    { key: 'taxCode', label: 'ACQ_CAT.TAX_CODE', type: 'text', half: true },
    { key: 'bankAccount', label: 'ACQ_CAT.BANK_ACCOUNT', type: 'text', half: true },
    { key: 'bankName', label: 'ACQ_CAT.BANK_NAME', type: 'text' },
    { key: 'representative', label: 'ACQ_CAT.REPRESENTATIVE', type: 'text', half: true },
    { key: 'position', label: 'ACQ_CAT.POSITION', type: 'text', half: true },
  ],
  modalWidth: 'max-w-2xl',
};

/** Kinh phí (monolith: /admin/budgets, quyền BUDGETS). Ngừng hoạt động thì không chọn được cho đơn mới. */
export const BUDGETS: CrudConfig = {
  title: 'ACQ_CAT.BUDGET_TITLE',
  resource: 'budgets',
  service: 'acquisition',
  perm: 'BUDGETS',
  searchLabel: 'ACQ_CAT.NAME',
  searchPlaceholder: 'ACQ_CAT.NAME_SEARCH_PH',
  hasStatus: true,
  columns: [
    { key: 'name', label: 'ACQ_CAT.NAME' },
    { key: 'amount', label: 'ACQ_CAT.AMOUNT', type: 'number', width: '160px' },
    { key: 'startsOn', label: 'ACQ_CAT.STARTS_ON', type: 'date', width: '120px' },
    { key: 'endsOn', label: 'ACQ_CAT.ENDS_ON', type: 'date', width: '120px' },
    { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' },
  ],
  fields: [
    { key: 'name', label: 'ACQ_CAT.NAME', type: 'text', required: true },
    { key: 'amount', label: 'ACQ_CAT.AMOUNT', type: 'number' },
    { key: 'startsOn', label: 'ACQ_CAT.STARTS_ON', type: 'date', half: true },
    { key: 'endsOn', label: 'ACQ_CAT.ENDS_ON', type: 'date', half: true },
    { key: 'note', label: 'COMMON.NOTE', type: 'text' },
  ],
  modalWidth: 'max-w-lg',
};

/** Quỹ (monolith: /admin/funds, quyền FUNDS) — thuộc một kinh phí (tuỳ chọn). */
export const FUNDS: CrudConfig = {
  title: 'ACQ_CAT.FUND_TITLE',
  resource: 'funds',
  service: 'acquisition',
  perm: 'FUNDS',
  searchLabel: 'ACQ_CAT.NAME',
  searchPlaceholder: 'ACQ_CAT.NAME_SEARCH_PH',
  columns: [
    { key: 'name', label: 'ACQ_CAT.NAME' },
    { key: 'budgetName', label: 'ACQ_CAT.BUDGET_TITLE', width: '200px' },
    { key: 'amount', label: 'ACQ_CAT.AMOUNT', type: 'number', width: '160px' },
    { key: 'manager', label: 'ACQ_CAT.MANAGER', width: '180px' },
  ],
  fields: [
    { key: 'name', label: 'ACQ_CAT.NAME', type: 'text', required: true },
    {
      key: 'budgetId', label: 'ACQ_CAT.BUDGET_TITLE', type: 'select', emptyLabel: 'ACQ_CAT.NO_BUDGET',
      optionsFrom: async (api) => (await api.crud<{ publicId: string; id: number; name: string }>('budgets', 'acquisition').searchAll())
        .map((b) => ({ value: b.id, label: b.name })),
    },
    { key: 'amount', label: 'ACQ_CAT.AMOUNT', type: 'number', half: true },
    { key: 'manager', label: 'ACQ_CAT.MANAGER', type: 'text', half: true },
    { key: 'purpose', label: 'ACQ_CAT.PURPOSE', type: 'text' },
    { key: 'note', label: 'COMMON.NOTE', type: 'text' },
  ],
  defaults: { budgetId: null },
  modalWidth: 'max-w-lg',
};
