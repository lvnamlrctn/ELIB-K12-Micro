import { CrudConfig } from '../../shared/crud-page';
import { named } from '../tenant/catalogs';

/** Tham số bạn đọc của service patron — đường dẫn như admin cũ (/admin/reader-types, /admin/academic-classes, /admin/courses). */
export const READER_TYPES = named('READER_TYPE', 'reader-types', 'READER_TYPES', 'patron');
export const CLASSES = named('ACADEMIC_CLASS', 'classes', 'CLASSES', 'patron');
export const COURSES = named('COURSE', 'courses', 'COURSES', 'patron');

export const READER_GROUPS: CrudConfig = {
  title: 'READER_GROUP_CAT.TITLE',
  resource: 'reader-groups',
  service: 'patron',
  perm: 'GROUPREADER',
  searchLabel: 'READER_GROUP_CAT.SEARCH_LABEL',
  searchPlaceholder: 'READER_GROUP_CAT.SEARCH_PH',
  columns: [
    { key: 'code', label: 'READER_GROUP_CAT.CODE', type: 'mono', width: '160px' },
    { key: 'name', label: 'READER_GROUP_CAT.NAME' },
  ],
  fields: [
    { key: 'name', label: 'READER_GROUP_CAT.NAME', type: 'text', required: true, placeholder: 'READER_GROUP_CAT.NAME_EG' },
    { key: 'code', label: 'READER_GROUP_CAT.CODE', type: 'text', placeholder: 'READER_GROUP_CAT.CODE_PH', hint: 'READER_GROUP_CAT.CODE_HINT' },
  ],
};
