import { Api, SPACE_CATEGORIES, SPACE_KIND_ROOM, SPACE_KIND_STUDY } from '../../core/api';
import { CrudConfig } from '../../shared/crud-page';

type Named = { publicId: string; id: number; name: string };

/** Toà nhà (monolith: map.MapBuilding, quyền SPACE_BUILDINGS). */
export const SPACE_BUILDINGS: CrudConfig = {
  title: 'SPACE_CAT.BUILDINGS_TITLE',
  resource: 'buildings',
  service: 'space',
  perm: 'SPACE_BUILDINGS',
  searchLabel: 'COMMON.SEARCH',
  searchPlaceholder: 'SPACE_CAT.BUILDING_SEARCH_PH',
  columns: [
    { key: 'code', label: 'COMMON.CODE', type: 'mono', width: '120px' },
    { key: 'name', label: 'COMMON.NAME' },
    { key: 'description', label: 'SPACE_CAT.DESCRIPTION' },
  ],
  fields: [
    { key: 'code', label: 'COMMON.CODE', type: 'text', required: true, half: true },
    { key: 'name', label: 'COMMON.NAME', type: 'text', required: true, half: true },
    { key: 'description', label: 'SPACE_CAT.DESCRIPTION', type: 'textarea' },
  ],
  defaults: { description: null },
  modalWidth: 'max-w-lg',
};

/** Tầng (monolith: map.MapFloor, quyền SPACE_FLOORS) — ảnh nền sơ đồ sửa riêng ở trang Sơ đồ tầng, không ở đây. */
export const SPACE_FLOORS: CrudConfig = {
  title: 'SPACE_CAT.FLOORS_TITLE',
  resource: 'floors',
  service: 'space',
  perm: 'SPACE_FLOORS',
  searchLabel: 'COMMON.SEARCH',
  searchPlaceholder: 'SPACE_CAT.FLOOR_SEARCH_PH',
  columns: [
    { key: 'buildingName', label: 'SPACE_CAT.BUILDING' },
    { key: 'name', label: 'COMMON.NAME' },
    { key: 'floorNumber', label: 'SPACE_CAT.FLOOR_NUMBER', type: 'number', width: '120px' },
  ],
  fields: [
    {
      key: 'buildingId', label: 'SPACE_CAT.BUILDING', type: 'select', required: true, half: true,
      optionsFrom: async (api: Api) => (await api.crud<Named>('buildings', 'space').searchAll()).map((b) => ({ value: b.id, label: b.name })),
    },
    { key: 'floorNumber', label: 'SPACE_CAT.FLOOR_NUMBER', type: 'number', half: true },
    { key: 'name', label: 'COMMON.NAME', type: 'text', required: true },
  ],
  defaults: { floorNumber: null },
  modalWidth: 'max-w-lg',
};

const KIND_OPTIONS = [
  { value: SPACE_KIND_ROOM, label: 'SPACE_CAT.KIND_ROOM' },
  { value: SPACE_KIND_STUDY, label: 'SPACE_CAT.KIND_STUDY' },
];

/**
 * Không gian/phòng đặt được (monolith: map.MapObject ROOM/STUDY_SPACE + RoomBookingConfig gộp một, quyền SPACE_ROOMS).
 * Vị trí trên sơ đồ và tạm ngưng (bảo trì) sửa riêng (trang Sơ đồ tầng / nút thao tác dòng) — không qua form này.
 */
export const SPACE_ROOMS: CrudConfig = {
  title: 'SPACE_CAT.ROOMS_TITLE',
  resource: 'spaces',
  service: 'space',
  perm: 'SPACE_ROOMS',
  searchLabel: 'COMMON.SEARCH',
  searchPlaceholder: 'SPACE_CAT.ROOM_SEARCH_PH',
  hasStatus: true,
  columns: [
    { key: 'floorName', label: 'SPACE_CAT.FLOOR' },
    { key: 'code', label: 'COMMON.CODE', type: 'mono', width: '100px' },
    { key: 'name', label: 'COMMON.NAME' },
    { key: 'capacity', label: 'SPACE_CAT.CAPACITY', type: 'number', width: '110px' },
    { key: 'autoApprove', label: 'SPACE_CAT.AUTO_APPROVE', type: 'bool', width: '120px' },
  ],
  fields: [
    {
      key: 'floorId', label: 'SPACE_CAT.FLOOR', type: 'select', required: true, half: true,
      optionsFrom: async (api: Api) => (await api.crud<Named & { buildingName?: string }>('floors', 'space').searchAll())
        .map((f) => ({ value: f.id, label: f.name })),
    },
    { key: 'code', label: 'COMMON.CODE', type: 'text', required: true, half: true },
    { key: 'name', label: 'COMMON.NAME', type: 'text', required: true },
    { key: 'kind', label: 'SPACE_CAT.KIND', type: 'select', options: KIND_OPTIONS, required: true, half: true },
    { key: 'category', label: 'SPACE_CAT.CATEGORY', type: 'select', options: SPACE_CATEGORIES, required: true, half: true },
    { key: 'capacity', label: 'SPACE_CAT.CAPACITY', type: 'number', required: true, half: true },
    { key: 'minGroupSize', label: 'SPACE_CAT.MIN_GROUP_SIZE', type: 'number', half: true, hint: 'SPACE_CAT.MIN_GROUP_SIZE_HINT' },
    { key: 'minAdvanceMinutes', label: 'SPACE_CAT.MIN_ADVANCE_MINUTES', type: 'number', half: true },
    { key: 'maxAdvanceDays', label: 'SPACE_CAT.MAX_ADVANCE_DAYS', type: 'number', half: true },
    { key: 'maxAdvanceHours', label: 'SPACE_CAT.MAX_ADVANCE_HOURS', type: 'number', half: true, hint: 'SPACE_CAT.MAX_ADVANCE_HOURS_HINT' },
    { key: 'maxBookingMinutesPerReader', label: 'SPACE_CAT.MAX_MINUTES_PER_READER', type: 'number', half: true, hint: 'SPACE_CAT.MAX_MINUTES_PER_READER_HINT' },
    { key: 'checkInGraceMinutes', label: 'SPACE_CAT.CHECK_IN_GRACE_MINUTES', type: 'number', half: true },
    { key: 'autoApprove', label: 'SPACE_CAT.AUTO_APPROVE', type: 'checkbox' },
    {
      key: 'allowedReaderTypeIds', label: 'SPACE_CAT.ALLOWED_READER_TYPES', type: 'multiselect', emptyLabel: 'SPACE_CAT.ALL_READER_TYPES',
      optionsFrom: async (api: Api) => (await api.crud<Named>('reader-types', 'patron').searchAll()).map((t) => ({ value: t.id, label: t.name })),
    },
    { key: 'colorHex', label: 'SPACE_CAT.COLOR', type: 'text', half: true, placeholder: '#2563eb' },
    { key: 'iconName', label: 'SPACE_CAT.ICON', type: 'text', half: true },
    { key: 'rules', label: 'SPACE_CAT.RULES', type: 'textarea' },
  ],
  defaults: {
    kind: SPACE_KIND_ROOM, category: 1, minGroupSize: null, minAdvanceMinutes: 0, maxAdvanceDays: 7, maxAdvanceHours: null,
    maxBookingMinutesPerReader: 0, checkInGraceMinutes: 15, autoApprove: false, allowedReaderTypeIds: [], colorHex: null, iconName: null, rules: null,
  },
  modalWidth: 'max-w-2xl',
};
