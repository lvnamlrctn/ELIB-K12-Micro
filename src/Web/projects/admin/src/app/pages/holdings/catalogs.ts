import { CrudConfig } from '../../shared/crud-page';

/** Loại kho (monolith: /admin/store-types, quyền STORE_TYPES). */
export const STORE_TYPES: CrudConfig = {
  title: 'Loại kho',
  resource: 'store-types',
  service: 'holdings',
  perm: 'STORE_TYPES',
  searchLabel: 'Tên loại kho',
  searchPlaceholder: 'VD: Kho mở...',
  columns: [{ key: 'name', label: 'Tên loại kho' }],
  fields: [{ key: 'name', label: 'Tên loại kho', type: 'text', required: true, placeholder: 'VD: Kho tham khảo' }],
  importable: true,
};

/** Kho (monolith: /admin/stores, quyền STORES). */
export const STORES: CrudConfig = {
  title: 'Kho',
  resource: 'stores',
  service: 'holdings',
  perm: 'STORES',
  searchLabel: 'Tên / mã kho',
  searchPlaceholder: 'VD: KM, Kho mở...',
  columns: [
    { key: 'code', label: 'Mã kho', type: 'mono', width: '120px' },
    { key: 'name', label: 'Tên kho' },
    { key: 'storeTypeId', label: 'Loại kho', type: 'lookup', width: '160px' },
    { key: 'position', label: 'Vị trí', width: '220px' },
    { key: 'itemCount', label: 'Số bản', type: 'number', width: '100px' },
  ],
  fields: [
    { key: 'code', label: 'Mã kho', type: 'text', required: true, placeholder: 'VD: KM', hint: 'In trên nhãn gáy và phiếu; tối đa 20 ký tự.', half: true },
    {
      key: 'storeTypeId', label: 'Loại kho', type: 'select', half: true,
      optionsFrom: async (api) => (await api.crud<{ publicId: string; id: number; name: string }>('store-types', 'holdings').searchAll())
        .map((t) => ({ value: t.id, label: t.name })),
    },
    { key: 'name', label: 'Tên kho', type: 'text', required: true, placeholder: 'VD: Kho mở tầng 1' },
    { key: 'position', label: 'Vị trí', type: 'text', placeholder: 'VD: Tầng 1, phòng 101', half: true },
    { key: 'capacity', label: 'Sức chứa (bản)', type: 'number', half: true },
  ],
  defaults: { storeTypeId: null },
  modalWidth: 'max-w-lg',
};
