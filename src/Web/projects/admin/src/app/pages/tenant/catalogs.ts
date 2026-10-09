import { CrudConfig } from '../../shared/crud-page';

/** Danh mục chỉ có tên — như các trang dùng app-base-entity của admin cũ (nationality.ts, chuc-vu.ts…). */
function named(title: string, resource: string, perm: string, nameLabel: string, placeholder: string): CrudConfig {
  return {
    title,
    resource,
    perm,
    searchLabel: nameLabel,
    searchPlaceholder: placeholder,
    columns: [{ key: 'name', label: nameLabel }],
    fields: [{ key: 'name', label: nameLabel, type: 'text', required: true, placeholder }],
    importable: true,
  };
}

export const NATIONALITIES = named('Quốc tịch', 'nationalities', 'NATIONALITIES', 'Tên quốc tịch', 'Nhập tên quốc tịch...');
export const ACADEMIC_TITLES = named('Học hàm học vị', 'academic-titles', 'PROFS', 'Tên học hàm học vị', 'Nhập tên học hàm học vị...');
export const ETHNICITIES = named('Dân tộc', 'ethnicities', 'ETHNICS', 'Tên dân tộc', 'Nhập tên dân tộc...');
export const DEGREES = named('Trình độ', 'degrees', 'DEGREES', 'Tên trình độ', 'Nhập tên trình độ...');
export const POSITIONS = named('Chức vụ', 'positions', 'POSITIONS', 'Tên chức vụ', 'Nhập tên chức vụ...');

export const CURRENCIES: CrudConfig = {
  title: 'Tiền tệ',
  resource: 'currencies',
  perm: 'CURRENCIES',
  searchLabel: 'Mã / tên tiền tệ',
  searchPlaceholder: 'Nhập mã hoặc tên tiền tệ...',
  hasStatus: true,
  columns: [
    { key: 'code', label: 'Mã tiền tệ', type: 'mono', width: '140px' },
    { key: 'name', label: 'Tên tiền tệ' },
    { key: 'exchangeRate', label: 'Tỷ giá (VND)', type: 'number', width: '160px' },
  ],
  fields: [
    { key: 'code', label: 'Mã tiền tệ', type: 'text', required: true, placeholder: 'VD: USD', half: true },
    { key: 'exchangeRate', label: 'Tỷ giá quy đổi (VND)', type: 'number', required: true, placeholder: 'VD: 25000', half: true },
    { key: 'name', label: 'Tên tiền tệ', type: 'text', required: true, placeholder: 'VD: Đô la Mỹ' },
    { key: 'status', label: 'Trạng thái', type: 'status' },
  ],
  defaults: { exchangeRate: 1 },
};

export const SYSTEM_PARAMETERS: CrudConfig = {
  title: 'Tham số hệ thống',
  resource: 'system-parameters',
  perm: 'SYSTEM_PARAMS',
  searchLabel: 'Mã / mô tả tham số',
  searchPlaceholder: 'Nhập mã hoặc mô tả...',
  modalWidth: 'max-w-2xl',
  columns: [
    { key: 'code', label: 'Mã tham số', type: 'mono', width: '260px' },
    { key: 'description', label: 'Mô tả' },
    { key: 'value', label: 'Giá trị', width: '240px' },
    { key: 'isPublic', label: 'Công khai OPAC', type: 'bool', width: '130px' },
  ],
  fields: [
    { key: 'code', label: 'Mã tham số', type: 'text', required: true, lockOnEdit: true, placeholder: 'VD: LIBRARY_ADDR', hint: 'Chữ in hoa, số, dấu gạch dưới. Không đổi được sau khi tạo.' },
    { key: 'description', label: 'Mô tả (tiếng Việt)', type: 'text' },
    { key: 'descriptionEn', label: 'Mô tả (tiếng Anh)', type: 'text' },
    { key: 'value', label: 'Giá trị', type: 'textarea' },
    { key: 'type', label: 'Kiểu giá trị', type: 'text', placeholder: 'text, bool, number, json', half: true },
    { key: 'service', label: 'Phân hệ sử dụng', type: 'text', placeholder: 'VD: circulation', half: true, lockWhen: (p) => !!p['isBuiltIn'] },
    {
      key: 'isPublic', label: 'Cho phép OPAC đọc khi chưa đăng nhập', type: 'checkbox', lockWhen: (p) => !!p['isBuiltIn'],
      hint: 'Tham số có sẵn của hệ thống: phạm vi công khai do hệ thống quy định. Không bật cho tham số chứa mật khẩu/khoá.',
    },
  ],
  defaults: { type: 'text', isPublic: false },
  badge: (p) => (p['isBuiltIn'] ? 'Hệ thống' : null),
};
