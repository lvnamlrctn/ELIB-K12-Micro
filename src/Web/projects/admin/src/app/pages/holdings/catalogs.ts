import { CrudConfig } from '../../shared/crud-page';

/** Loại kho (monolith: /admin/store-types, quyền STORE_TYPES). */
export const STORE_TYPES: CrudConfig = {
  title: 'STORE_CAT.TYPE_TITLE',
  resource: 'store-types',
  service: 'holdings',
  perm: 'STORE_TYPES',
  searchLabel: 'STORE_CAT.TYPE_NAME',
  searchPlaceholder: 'STORE_CAT.TYPE_SEARCH_PH',
  columns: [{ key: 'name', label: 'STORE_CAT.TYPE_NAME' }],
  fields: [{ key: 'name', label: 'STORE_CAT.TYPE_NAME', type: 'text', required: true, placeholder: 'STORE_CAT.TYPE_NAME_EG' }],
  importable: true,
};

/** Kho (monolith: /admin/stores, quyền STORES). */
export const STORES: CrudConfig = {
  title: 'STORE_CAT.TITLE',
  resource: 'stores',
  service: 'holdings',
  perm: 'STORES',
  searchLabel: 'STORE_CAT.SEARCH_LABEL',
  searchPlaceholder: 'STORE_CAT.SEARCH_PH',
  columns: [
    { key: 'code', label: 'STORE_CAT.CODE', type: 'mono', width: '120px' },
    { key: 'name', label: 'STORE_CAT.NAME' },
    { key: 'storeTypeId', label: 'STORE_CAT.TYPE_TITLE', type: 'lookup', width: '160px' },
    { key: 'position', label: 'STORE_CAT.POSITION', width: '220px' },
    { key: 'itemCount', label: 'STORE_CAT.ITEM_COUNT', type: 'number', width: '100px' },
  ],
  fields: [
    { key: 'code', label: 'STORE_CAT.CODE', type: 'text', required: true, placeholder: 'STORE_CAT.CODE_EG', hint: 'STORE_CAT.CODE_HINT', half: true },
    {
      key: 'storeTypeId', label: 'STORE_CAT.TYPE_TITLE', type: 'select', half: true,
      optionsFrom: async (api) => (await api.crud<{ publicId: string; id: number; name: string }>('store-types', 'holdings').searchAll())
        .map((t) => ({ value: t.id, label: t.name })),
    },
    { key: 'name', label: 'STORE_CAT.NAME', type: 'text', required: true, placeholder: 'STORE_CAT.NAME_EG' },
    { key: 'position', label: 'STORE_CAT.POSITION', type: 'text', placeholder: 'STORE_CAT.POSITION_EG', half: true },
    { key: 'capacity', label: 'STORE_CAT.CAPACITY', type: 'number', half: true },
  ],
  defaults: { storeTypeId: null },
  modalWidth: 'max-w-lg',
};
