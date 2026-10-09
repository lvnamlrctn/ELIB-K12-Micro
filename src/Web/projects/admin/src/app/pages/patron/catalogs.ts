import { CrudConfig } from '../../shared/crud-page';
import { named } from '../tenant/catalogs';

/** Tham số bạn đọc của service patron — đường dẫn như admin cũ (/admin/reader-types, /admin/academic-classes, /admin/courses). */
export const READER_TYPES = named('Loại bạn đọc', 'reader-types', 'READER_TYPES', 'Tên loại bạn đọc', 'VD: Học sinh, Giáo viên...', 'patron');
export const CLASSES = named('Lớp', 'classes', 'CLASSES', 'Tên lớp', 'VD: 6A1', 'patron');
export const COURSES = named('Khoá', 'courses', 'COURSES', 'Tên khoá', 'VD: 2025-2029', 'patron');

export const READER_GROUPS: CrudConfig = {
  title: 'Nhóm bạn đọc',
  resource: 'reader-groups',
  service: 'patron',
  perm: 'GROUPREADER',
  searchLabel: 'Tên / mã nhóm',
  searchPlaceholder: 'Nhập tên hoặc mã nhóm...',
  columns: [
    { key: 'code', label: 'Mã nhóm', type: 'mono', width: '160px' },
    { key: 'name', label: 'Tên nhóm' },
  ],
  fields: [
    { key: 'name', label: 'Tên nhóm', type: 'text', required: true, placeholder: 'VD: CLB Đọc sách' },
    { key: 'code', label: 'Mã nhóm', type: 'text', placeholder: 'Tuỳ chọn, không trùng', hint: 'Chữ in hoa, tối đa 50 ký tự.' },
  ],
};
