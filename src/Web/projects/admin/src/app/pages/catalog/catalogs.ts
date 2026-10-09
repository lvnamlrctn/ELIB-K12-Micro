import { CrudConfig } from '../../shared/crud-page';

/** Leader/06 — dạng tài liệu (MARC21). */
export const RECORD_TYPES = [
  { value: 'a', label: 'a — Tài liệu ngôn ngữ (sách, báo)' },
  { value: 't', label: 't — Tài liệu ngôn ngữ dạng bản thảo' },
  { value: 'e', label: 'e — Bản đồ' },
  { value: 'g', label: 'g — Tài liệu chiếu, nghe nhìn' },
  { value: 'i', label: 'i — Ghi âm không phải nhạc' },
  { value: 'j', label: 'j — Ghi âm âm nhạc' },
  { value: 'c', label: 'c — Bản nhạc in' },
  { value: 'k', label: 'k — Tài liệu đồ hoạ hai chiều' },
  { value: 'm', label: 'm — Tệp máy tính' },
  { value: 'o', label: 'o — Bộ tài liệu' },
  { value: 'r', label: 'r — Đồ vật ba chiều' },
];

/** Leader/07 — cấp thư mục. */
export const BIB_LEVELS = [
  { value: 'm', label: 'm — Chuyên khảo (sách, tài liệu trọn bộ)' },
  { value: 's', label: 's — Ấn phẩm tiếp tục (báo, tạp chí)' },
  { value: 'a', label: 'a — Phần của chuyên khảo (bài trích)' },
  { value: 'b', label: 'b — Phần của ấn phẩm tiếp tục (bài báo)' },
  { value: 'c', label: 'c — Sưu tập' },
  { value: 'd', label: 'd — Phần của sưu tập' },
  { value: 'i', label: 'i — Tài liệu tích hợp' },
];

/** Loại biểu ghi (monolith: /admin/bib-types, quyền BIB_TYPES). */
export const BIB_TYPES: CrudConfig = {
  title: 'Loại biểu ghi',
  resource: 'bib-types',
  service: 'catalog',
  perm: 'BIB_TYPES',
  searchLabel: 'Tên / mã loại',
  searchPlaceholder: 'VD: Sách, SGK...',
  columns: [
    { key: 'code', label: 'Mã', type: 'mono', width: '140px' },
    { key: 'name', label: 'Tên loại biểu ghi' },
    { key: 'recordType', label: 'Leader/06', type: 'mono', width: '110px' },
    { key: 'bibLevel', label: 'Leader/07', type: 'mono', width: '110px' },
  ],
  fields: [
    { key: 'name', label: 'Tên loại biểu ghi', type: 'text', required: true, placeholder: 'VD: Sách tham khảo' },
    { key: 'code', label: 'Mã', type: 'text', required: true, placeholder: 'VD: STK', hint: 'Chữ in hoa, số, "-", "_"; tối đa 20 ký tự.' },
    { key: 'recordType', label: 'Dạng tài liệu (Leader/06)', type: 'select', options: RECORD_TYPES },
    { key: 'bibLevel', label: 'Cấp thư mục (Leader/07)', type: 'select', options: BIB_LEVELS },
  ],
  defaults: { recordType: 'a', bibLevel: 'm' },
  modalWidth: 'max-w-lg',
  toolbar: [
    {
      label: 'Khôi phục loại & biểu mẫu mặc định',
      icon: 'restore',
      action: 'add',
      run: async (api) => {
        const r = await api.restoreCatalogDefaults();
        return r.added || r.worksheets ? `Đã thêm ${r.added} loại biểu ghi, ${r.worksheets} biểu mẫu.` : 'Đã đủ loại biểu ghi và biểu mẫu mặc định.';
      },
    },
  ],
};
