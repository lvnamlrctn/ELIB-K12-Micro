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
    { key: 'finePerDay', label: 'Phạt / ngày quá hạn', type: 'number', width: '150px' },
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
    { key: 'finePerDay', label: 'Tiền phạt mỗi ngày quá hạn (đ)', type: 'number', half: true, hint: 'Dùng khi lập phiếu phạt quá hạn.' },
    { key: 'maxHolds', label: 'Số đặt mượn cùng lúc', type: 'number', half: true, hint: 'Để trống = không giới hạn; 0 = không cho đặt mượn.' },
    { key: 'holdDays', label: 'Số ngày giữ sách đặt mượn', type: 'number', required: true, half: true },
    { key: 'renewFromToday', label: 'Gia hạn tính từ ngày gia hạn (không cộng vào hạn cũ)', type: 'checkbox' },
  ],
  defaults: {
    readerTypeId: null, circPlaceId: null, loanDays: 14, maxLoans: null, maxRenewals: null, renewDays: 7, finePerDay: 0, renewFromToday: false,
    maxHolds: null, holdDays: 2,
  },
  modalWidth: 'max-w-lg',
};

/** Lý do phạt (monolith: /admin/cfine-types, quyền FINE_REASONS). Lý do có trạng thái "Mất" đóng lượt mượn và báo kho mất sách. */
export const FINE_REASONS: CrudConfig = {
  title: 'Lý do phạt',
  resource: 'fine-reasons',
  service: 'circulation',
  perm: 'FINE_REASONS',
  searchLabel: 'Tên / mã lý do',
  searchPlaceholder: 'VD: QUAHAN, Mất tài liệu...',
  columns: [
    { key: 'code', label: 'Mã', type: 'mono', width: '140px' },
    { key: 'name', label: 'Tên lý do phạt' },
    { key: 'amount', label: 'Số tiền gợi ý (đ)', type: 'number', width: '160px' },
    { key: 'itemStatus', label: 'Trạng thái tài liệu', type: 'lookup', width: '180px' },
  ],
  fields: [
    { key: 'code', label: 'Mã', type: 'text', required: true, placeholder: 'VD: HUHONG', half: true, lockWhen: (x) => x['isBuiltIn'] === true },
    { key: 'name', label: 'Tên lý do phạt', type: 'text', required: true, placeholder: 'VD: Hư hỏng tài liệu', half: true },
    { key: 'amount', label: 'Số tiền gợi ý (đ)', type: 'number', half: true, hint: 'Điền sẵn khi thêm dòng phạt; lý do "Quá hạn" tính theo chính sách.' },
    {
      key: 'itemStatus', label: 'Trạng thái tài liệu khi phạt', type: 'select', emptyLabel: 'Không đổi', half: true,
      options: [{ value: 'L', label: 'Mất tài liệu' }],
      hint: 'Chọn "Mất tài liệu": lưu phiếu phạt sẽ đóng lượt mượn và chuyển bản sách sang trạng thái Mất.',
    },
  ],
  defaults: { amount: 0, itemStatus: null },
  badge: (x) => (x['isBuiltIn'] ? 'Hệ thống' : null),
  toolbar: [
    {
      label: 'Thêm lý do mặc định', icon: 'playlist_add', action: 'add',
      run: async (api) => { const r = await api.addDefaultFineReasons(); return r.added ? `Đã thêm ${r.added} lý do phạt mặc định` : 'Đã có đủ lý do mặc định'; },
    },
  ],
  modalWidth: 'max-w-lg',
};

/** Photo copy tài liệu (monolith: /admin/photocopy, quyền C_PHOTO). Thành tiền = số trang × số bản × đơn giá, tính ở server. */
export const PHOTOCOPIES: CrudConfig = {
  title: 'Photo copy tài liệu',
  resource: 'photocopies',
  service: 'circulation',
  perm: 'C_PHOTO',
  searchLabel: 'Số thẻ / ĐKCB / họ tên / nhan đề',
  searchPlaceholder: 'VD: HS001, VV000123...',
  columns: [
    { key: 'cardNo', label: 'Số thẻ', type: 'mono', width: '120px' },
    { key: 'readerName', label: 'Bạn đọc' },
    { key: 'barcode', label: 'ĐKCB', type: 'mono', width: '120px' },
    { key: 'title', label: 'Nhan đề' },
    { key: 'fromPage', label: 'Từ trang', type: 'number', width: '90px' },
    { key: 'toPage', label: 'Đến trang', type: 'number', width: '90px' },
    { key: 'copies', label: 'Số bản', type: 'number', width: '80px' },
    { key: 'total', label: 'Thành tiền (đ)', type: 'number', width: '130px' },
    { key: 'paid', label: 'Đã thu', type: 'bool', width: '90px' },
    { key: 'photoDate', label: 'Ngày', width: '110px' },
  ],
  fields: [
    { key: 'cardNo', label: 'Số thẻ bạn đọc', type: 'text', required: true, half: true },
    { key: 'barcode', label: 'Số ĐKCB', type: 'text', required: true, half: true },
    { key: 'fromPage', label: 'Từ trang', type: 'number', required: true, half: true },
    { key: 'toPage', label: 'Đến trang', type: 'number', required: true, half: true },
    { key: 'copies', label: 'Số bản', type: 'number', required: true, half: true },
    { key: 'unitPrice', label: 'Đơn giá / trang (đ)', type: 'number', required: true, half: true },
    { key: 'note', label: 'Ghi chú', type: 'text' },
    { key: 'paid', label: 'Đã thu tiền', type: 'checkbox' },
  ],
  defaults: { fromPage: 1, toPage: 1, copies: 1, unitPrice: 500, paid: false },
  modalWidth: 'max-w-lg',
};
