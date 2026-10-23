import { CrudConfig } from '../../shared/crud-page';
import { TreeCatalogConfig } from '../../shared/tree-catalog-page';

const LANGUAGE_OPTIONS = [
  { value: '', label: 'PORTAL_CAT.ALL_LANGUAGES' },
  { value: 'vi', label: 'Tiếng Việt' }, // i18n-data: tên ngôn ngữ hiện đúng như vậy ở mọi ngôn ngữ giao diện
  { value: 'en', label: 'English' },
];

const sortField = { key: 'sortOrder', label: 'PORTAL_CAT.SORT_ORDER', type: 'number', half: true } as const;
const languageField = { key: 'language', label: 'PORTAL_CAT.LANGUAGE', type: 'select', options: LANGUAGE_OPTIONS, half: true } as const;

/** Chuyên mục tin (LRC: Category, quyền PORTAL_CATEGORIES) — cây. */
export const PORTAL_NEWS_CATEGORIES: TreeCatalogConfig = {
  title: 'PORTAL_CAT.CATEGORIES_TITLE',
  resource: 'news-categories',
  service: 'portal',
  perm: 'PORTAL_CATEGORIES',
  extraFields: [
    { key: 'language', label: 'PORTAL_CAT.LANGUAGE', type: 'select', options: LANGUAGE_OPTIONS, half: true },
    { key: 'link', label: 'PORTAL_CAT.LINK', type: 'text', half: true },
    { key: 'description', label: 'PORTAL_CAT.DESCRIPTION', type: 'textarea' },
    { key: 'keyword', label: 'PORTAL_CAT.KEYWORD', type: 'text', half: true },
    { key: 'pageTitle', label: 'PORTAL_CAT.PAGE_TITLE', type: 'text', half: true },
    { key: 'metaDescription', label: 'PORTAL_CAT.META_DESCRIPTION', type: 'text' },
    { key: 'requiresLogin', label: 'PORTAL_CAT.REQUIRES_LOGIN', type: 'checkbox' },
  ],
  defaults: { language: '', link: null, description: null, keyword: null, pageTitle: null, metaDescription: null, requiresLogin: false },
  modalWidth: 'max-w-xl',
};

export const PORTAL_NEWS_EVENTS: CrudConfig = {
  title: 'PORTAL_CAT.EVENTS_TITLE', resource: 'news-events', service: 'portal', perm: 'PORTAL_CATEGORIES', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '',
  columns: [{ key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'language', label: 'PORTAL_CAT.LANGUAGE', width: '120px' }],
  fields: [{ key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true }, { ...languageField, half: false }],
  defaults: { name: '', language: '' }, modalWidth: 'max-w-md',
};

/** Bình luận tin (LRC: NewsComment, quyền PORTAL_COMMENTS): bạn đọc gửi từ OPAC, thủ thư duyệt bằng cột trạng thái. */
export const PORTAL_NEWS_COMMENTS: CrudConfig = {
  title: 'PORTAL_CAT.COMMENTS_TITLE', resource: 'news-comments', service: 'portal', perm: 'PORTAL_COMMENTS', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true, hideAdd: true,
  columns: [
    { key: 'newsTitle', label: 'PORTAL_CAT.NEWS' }, { key: 'name', label: 'PORTAL_CAT.SENDER', width: '160px' }, { key: 'content', label: 'PORTAL_CAT.CONTENT' },
    { key: 'createdAt', label: 'PORTAL_CAT.SENT_AT', type: 'date', width: '150px' },
  ],
  fields: [
    { key: 'name', label: 'PORTAL_CAT.SENDER', type: 'text', required: true, half: true }, { key: 'email', label: 'PORTAL_CAT.EMAIL', type: 'text', half: true },
    { key: 'title', label: 'PORTAL_CAT.TITLE', type: 'text' }, { key: 'content', label: 'PORTAL_CAT.CONTENT', type: 'textarea', required: true },
  ],
  defaults: {}, modalWidth: 'max-w-lg',
};

export const PORTAL_MENU_TYPES: CrudConfig = {
  title: 'PORTAL_CAT.MENU_TYPES_TITLE', resource: 'menu-types', service: 'portal', perm: 'PORTAL_MENUS', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '',
  columns: [{ key: 'code', label: 'PORTAL_CAT.CODE', type: 'mono', width: '160px' }, { key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'description', label: 'PORTAL_CAT.DESCRIPTION' }],
  fields: [
    { key: 'code', label: 'PORTAL_CAT.CODE', type: 'text', required: true, half: true, hint: 'PORTAL_CAT.CODE_HINT' }, { key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true, half: true },
    { key: 'description', label: 'PORTAL_CAT.DESCRIPTION', type: 'text' }, { ...languageField, half: false },
  ],
  defaults: { code: '', name: '', description: null, language: '' }, modalWidth: 'max-w-md',
};

export const PORTAL_LINK_GROUPS: CrudConfig = {
  title: 'PORTAL_CAT.LINK_GROUPS_TITLE', resource: 'link-groups', service: 'portal', perm: 'PORTAL_LINKS', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true,
  columns: [{ key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'sortOrder', label: 'PORTAL_CAT.SORT_ORDER', type: 'number', width: '110px' }, { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' }],
  fields: [{ key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true }, sortField, languageField, { key: 'status', label: 'COMMON.STATUS', type: 'status' }],
  defaults: { name: '', sortOrder: 0, language: '', status: 2 }, modalWidth: 'max-w-md',
};

export const PORTAL_LINKS: CrudConfig = {
  title: 'PORTAL_CAT.LINKS_TITLE', resource: 'links', service: 'portal', perm: 'PORTAL_LINKS', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true,
  columns: [{ key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'linkUrl', label: 'PORTAL_CAT.LINK' }, { key: 'sortOrder', label: 'PORTAL_CAT.SORT_ORDER', type: 'number', width: '110px' }, { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' }],
  fields: [
    {
      key: 'linkGroupId', label: 'PORTAL_CAT.GROUP', type: 'select', required: true,
      optionsFrom: async (api) => (await api.crud<{ id: number; publicId: string; name: string }>('link-groups', 'portal').searchAll()).map((g) => ({ value: g.id, label: g.name })),
    },
    { key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true }, { key: 'linkUrl', label: 'PORTAL_CAT.LINK', type: 'text', required: true, placeholder: 'https://' },
    { key: 'description', label: 'PORTAL_CAT.DESCRIPTION', type: 'text' }, { key: 'images', label: 'PORTAL_CAT.LOGO', type: 'image' }, sortField, { key: 'status', label: 'COMMON.STATUS', type: 'status', half: true },
  ],
  defaults: { linkGroupId: null, name: '', linkUrl: '', description: null, images: null, sortOrder: 0, status: 2 }, modalWidth: 'max-w-lg',
};

/** Banner trang chủ (LRC: Banner, quyền PORTAL_BANNERS) — ảnh trượt, có thể hẹn khoảng ngày hiển thị. */
export const PORTAL_BANNERS: CrudConfig = {
  title: 'PORTAL_CAT.BANNERS_TITLE', resource: 'banners', service: 'portal', perm: 'PORTAL_BANNERS', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true,
  columns: [
    { key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'position', label: 'PORTAL_CAT.POSITION', width: '100px' }, { key: 'sortOrder', label: 'PORTAL_CAT.SORT_ORDER', type: 'number', width: '110px' },
    { key: 'showFrom', label: 'PORTAL_CAT.SHOW_FROM', type: 'date', width: '120px' }, { key: 'showTo', label: 'PORTAL_CAT.SHOW_TO', type: 'date', width: '120px' }, { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' },
  ],
  fields: [
    { key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true }, { key: 'image', label: 'PORTAL_CAT.IMAGE', type: 'image', required: true },
    { key: 'position', label: 'PORTAL_CAT.POSITION', type: 'select', options: [{ value: 'home', label: 'PORTAL_CAT.POSITION_HOME' }, { value: 'top', label: 'PORTAL_CAT.POSITION_TOP' }], half: true },
    sortField, { key: 'link', label: 'PORTAL_CAT.LINK', type: 'text', placeholder: 'https://' }, { key: 'showFrom', label: 'PORTAL_CAT.SHOW_FROM', type: 'date', half: true },
    { key: 'showTo', label: 'PORTAL_CAT.SHOW_TO', type: 'date', half: true }, languageField, { key: 'status', label: 'COMMON.STATUS', type: 'status', half: true },
  ],
  defaults: { name: '', image: null, position: 'home', sortOrder: 0, link: null, showFrom: null, showTo: null, language: '', status: 2 }, modalWidth: 'max-w-xl',
};

export const PORTAL_ADS_GROUPS: CrudConfig = {
  title: 'PORTAL_CAT.ADS_GROUPS_TITLE', resource: 'ads-groups', service: 'portal', perm: 'PORTAL_ADS', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true,
  columns: [{ key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'width', label: 'PORTAL_CAT.WIDTH', type: 'number', width: '100px' }, { key: 'height', label: 'PORTAL_CAT.HEIGHT', type: 'number', width: '100px' }, { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' }],
  fields: [
    { key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true }, { key: 'width', label: 'PORTAL_CAT.WIDTH', type: 'number', half: true }, { key: 'height', label: 'PORTAL_CAT.HEIGHT', type: 'number', half: true },
    languageField, { key: 'status', label: 'COMMON.STATUS', type: 'status', half: true },
  ],
  defaults: { name: '', width: null, height: null, language: '', status: 2 }, modalWidth: 'max-w-md',
};

export const PORTAL_ADS: CrudConfig = {
  title: 'PORTAL_CAT.ADS_TITLE', resource: 'ads', service: 'portal', perm: 'PORTAL_ADS', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true,
  columns: [{ key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'sortOrder', label: 'PORTAL_CAT.SORT_ORDER', type: 'number', width: '110px' }, { key: 'showTo', label: 'PORTAL_CAT.SHOW_TO', type: 'date', width: '120px' }, { key: 'clicks', label: 'PORTAL_CAT.CLICKS', type: 'number', width: '100px' }, { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' }],
  fields: [
    {
      key: 'adsGroupId', label: 'PORTAL_CAT.GROUP', type: 'select', required: true,
      optionsFrom: async (api) => (await api.crud<{ id: number; publicId: string; name: string }>('ads-groups', 'portal').searchAll()).map((g) => ({ value: g.id, label: g.name })),
    },
    { key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true }, { key: 'image', label: 'PORTAL_CAT.IMAGE', type: 'image', required: true },
    { key: 'link', label: 'PORTAL_CAT.LINK', type: 'text', placeholder: 'https://' }, { key: 'showFrom', label: 'PORTAL_CAT.SHOW_FROM', type: 'date', half: true }, { key: 'showTo', label: 'PORTAL_CAT.SHOW_TO', type: 'date', half: true },
    sortField, { key: 'status', label: 'COMMON.STATUS', type: 'status', half: true },
  ],
  defaults: { adsGroupId: null, name: '', image: null, link: null, showFrom: null, showTo: null, sortOrder: 0, status: 2 }, modalWidth: 'max-w-lg',
};

export const PORTAL_PHOTO_ALBUMS: CrudConfig = {
  title: 'PORTAL_CAT.ALBUMS_TITLE', resource: 'photo-albums', service: 'portal', perm: 'PORTAL_GALLERY', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true,
  columns: [{ key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'photoCount', label: 'PORTAL_CAT.PHOTO_COUNT', type: 'number', width: '110px' }, { key: 'sortOrder', label: 'PORTAL_CAT.SORT_ORDER', type: 'number', width: '110px' }, { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' }],
  fields: [
    { key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true }, { key: 'description', label: 'PORTAL_CAT.DESCRIPTION', type: 'textarea' }, { key: 'image', label: 'PORTAL_CAT.IMAGE', type: 'image' },
    sortField, languageField, { key: 'status', label: 'COMMON.STATUS', type: 'status' },
  ],
  defaults: { name: '', description: null, image: null, sortOrder: 0, language: '', status: 2 }, modalWidth: 'max-w-lg',
};

export const PORTAL_PHOTOS: CrudConfig = {
  title: 'PORTAL_CAT.PHOTOS_TITLE', resource: 'photos', service: 'portal', perm: 'PORTAL_GALLERY', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true,
  columns: [{ key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'sortOrder', label: 'PORTAL_CAT.SORT_ORDER', type: 'number', width: '110px' }, { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' }],
  fields: [
    {
      key: 'photoAlbumId', label: 'PORTAL_CAT.ALBUM', type: 'select', required: true,
      optionsFrom: async (api) => (await api.crud<{ id: number; publicId: string; name: string }>('photo-albums', 'portal').searchAll()).map((a) => ({ value: a.id, label: a.name })),
    },
    { key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true }, { key: 'image', label: 'PORTAL_CAT.IMAGE', type: 'image', required: true }, { key: 'brief', label: 'PORTAL_CAT.DESCRIPTION', type: 'text' },
    { key: 'link', label: 'PORTAL_CAT.LINK', type: 'text' }, sortField, { key: 'status', label: 'COMMON.STATUS', type: 'status', half: true },
  ],
  defaults: { photoAlbumId: null, name: '', image: null, brief: null, link: null, sortOrder: 0, status: 2 }, modalWidth: 'max-w-lg',
};

export const PORTAL_VIDEOS: CrudConfig = {
  title: 'PORTAL_CAT.VIDEOS_TITLE', resource: 'videos', service: 'portal', perm: 'PORTAL_GALLERY', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true,
  columns: [{ key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'provider', label: 'PORTAL_CAT.PROVIDER', width: '110px' }, { key: 'sortOrder', label: 'PORTAL_CAT.SORT_ORDER', type: 'number', width: '110px' }, { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' }],
  fields: [
    { key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true },
    { key: 'link', label: 'PORTAL_CAT.VIDEO_LINK', type: 'text', required: true, placeholder: 'https://www.youtube.com/watch?v=…', hint: 'PORTAL_CAT.VIDEO_HINT' },
    { key: 'description', label: 'PORTAL_CAT.DESCRIPTION', type: 'text' }, { key: 'images', label: 'PORTAL_CAT.IMAGE', type: 'image' }, sortField, languageField,
    { key: 'status', label: 'COMMON.STATUS', type: 'status' },
  ],
  defaults: { name: '', link: '', description: null, images: null, sortOrder: 0, language: '', status: 2 }, modalWidth: 'max-w-lg',
};

export const PORTAL_CONTACT_GROUPS: CrudConfig = {
  title: 'PORTAL_CAT.CONTACT_GROUPS_TITLE', resource: 'contact-groups', service: 'portal', perm: 'PORTAL_CONTACTS', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hasStatus: true,
  columns: [{ key: 'name', label: 'PORTAL_CAT.NAME' }, { key: 'email', label: 'PORTAL_CAT.RECEIVER_EMAIL' }, { key: 'status', label: 'COMMON.STATUS', type: 'status', width: '130px' }],
  fields: [
    { key: 'name', label: 'PORTAL_CAT.NAME', type: 'text', required: true }, { key: 'email', label: 'PORTAL_CAT.RECEIVER_EMAIL', type: 'text', hint: 'PORTAL_CAT.RECEIVER_HINT' },
    { key: 'description', label: 'PORTAL_CAT.DESCRIPTION', type: 'text' }, sortField, languageField, { key: 'status', label: 'COMMON.STATUS', type: 'status' },
  ],
  defaults: { name: '', email: null, description: null, sortOrder: 0, language: '', status: 2 }, modalWidth: 'max-w-md',
};

/** Liên hệ từ OPAC (LRC: Contact, quyền PORTAL_CONTACTS): chỉ xem, đánh dấu đã xử lý kèm ghi chú, xoá. Trạng thái: Mới / Đã xử lý. */
export const PORTAL_CONTACTS: CrudConfig = {
  title: 'PORTAL_CAT.CONTACTS_TITLE', resource: 'contacts', service: 'portal', perm: 'PORTAL_CONTACTS', searchLabel: 'COMMON.SEARCH', searchPlaceholder: '', hideAdd: true, hideEdit: true,
  columns: [
    { key: 'name', label: 'PORTAL_CAT.SENDER', width: '160px' }, { key: 'email', label: 'PORTAL_CAT.EMAIL', width: '200px' }, { key: 'title', label: 'PORTAL_CAT.TITLE' }, { key: 'content', label: 'PORTAL_CAT.CONTENT' },
    { key: 'contactGroupName', label: 'PORTAL_CAT.GROUP', width: '140px' }, { key: 'createdAt', label: 'PORTAL_CAT.SENT_AT', type: 'date', width: '150px' },
    { key: 'handledAt', label: 'PORTAL_CAT.HANDLED_AT', type: 'date', width: '150px' }, { key: 'handleNote', label: 'PORTAL_CAT.HANDLE_NOTE' },
  ],
  fields: [],
  rowActions: [{
    icon: 'task_alt', title: 'PORTAL_CAT.HANDLE', action: 'edit', show: (row) => row['status'] !== 2,
    inputs: [{ key: 'note', label: 'PORTAL_CAT.HANDLE_NOTE' }],
    run: async (api, row, v) => { await api.handlePortalContact(row.publicId, v['note'] ?? ''); return { key: 'PORTAL_CAT.HANDLED' }; },
  }],
  defaults: {}, modalWidth: 'max-w-md',
};
