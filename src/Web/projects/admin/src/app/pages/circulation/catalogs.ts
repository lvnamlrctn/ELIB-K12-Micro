import { CrudConfig } from '../../shared/crud-page';

type Named = { publicId: string; id: number; name: string; code?: string };

/** Điểm lưu thông (monolith: /admin/circ-places, quyền CIRC_PLACES) — quầy mượn trả và các kho được mượn tại quầy. */
export const CIRC_PLACES: CrudConfig = {
  title: 'Điểm lưu thông',
  resource: 'circ-places',
  service: 'circulation',
  perm: 'CIRC_PLACES',
  searchLabel: 'Tên / mã điểm',
  searchPlaceholder: 'VD: QUAY, Quầy tầng 1...',
  columns: [
    { key: 'code', label: 'Mã', type: 'mono', width: '120px' },
    { key: 'name', label: 'Tên điểm lưu thông' },
    { key: 'storeIds', label: 'Kho được mượn', type: 'lookup' },
  ],
  fields: [
    { key: 'code', label: 'Mã', type: 'text', required: true, placeholder: 'VD: QUAY', half: true },
    { key: 'name', label: 'Tên điểm lưu thông', type: 'text', required: true, placeholder: 'VD: Quầy mượn trả tầng 1', half: true },
    {
      key: 'storeIds', label: 'Kho được mượn tại điểm này', type: 'multiselect', emptyLabel: 'Mọi kho',
      hint: 'Không chọn kho nào = mượn được tài liệu của mọi kho.',
      optionsFrom: async (api) => (await api.crud<Named>('stores', 'holdings').searchAll()).map((s) => ({ value: s.id, label: `${s.code} — ${s.name}` })),
    },
  ],
  defaults: { storeIds: [] },
  modalWidth: 'max-w-lg',
};

/** Chính sách lưu thông (monolith: /admin/circ-policies, quyền CIRC_POLICIES) theo loại bạn đọc × điểm lưu thông. */
export const LOAN_POLICIES: CrudConfig = {
  title: 'Chính sách lưu thông',
  resource: 'loan-policies',
  service: 'circulation',
  perm: 'CIRC_POLICIES',
  searchLabel: 'Tìm',
  searchPlaceholder: '',
  columns: [
    { key: 'readerTypeId', label: 'Loại bạn đọc', type: 'lookup' },
    { key: 'circPlaceId', label: 'Điểm lưu thông', type: 'lookup' },
    { key: 'loanDays', label: 'Số ngày mượn', type: 'number', width: '120px' },
    { key: 'maxLoans', label: 'Mượn tối đa', type: 'number', width: '120px' },
    { key: 'maxRenewals', label: 'Số lần gia hạn', type: 'number', width: '130px' },
    { key: 'renewDays', label: 'Ngày mỗi lần gia hạn', type: 'number', width: '160px' },
  ],
  fields: [
    {
      key: 'readerTypeId', label: 'Loại bạn đọc', type: 'select', emptyLabel: 'Mọi loại bạn đọc', half: true,
      optionsFrom: async (api) => (await api.crud<Named>('reader-types', 'patron').searchAll()).map((t) => ({ value: t.id, label: t.name })),
    },
    {
      key: 'circPlaceId', label: 'Điểm lưu thông', type: 'select', emptyLabel: 'Mọi điểm lưu thông', half: true,
      optionsFrom: async (api) => (await api.crud<Named>('circ-places', 'circulation').searchAll()).map((p) => ({ value: p.id, label: `${p.code} — ${p.name}` })),
    },
    { key: 'loanDays', label: 'Số ngày mượn', type: 'number', required: true, half: true },
    { key: 'maxLoans', label: 'Số tài liệu mượn cùng lúc', type: 'number', half: true, hint: 'Để trống = không giới hạn.' },
    { key: 'maxRenewals', label: 'Số lần gia hạn tối đa', type: 'number', half: true, hint: 'Để trống = không giới hạn; 0 = không cho gia hạn.' },
    { key: 'renewDays', label: 'Số ngày mỗi lần gia hạn', type: 'number', required: true, half: true },
  ],
  defaults: { readerTypeId: null, circPlaceId: null, loanDays: 14, maxLoans: null, maxRenewals: null, renewDays: 7 },
  modalWidth: 'max-w-lg',
};
