export interface MapObject {
  id:          number;
  floorId:     number;
  name?:       string;
  code?:       string;
  objectType?: string; // ROOM, SHELF, PC, STUDY_SPACE, COUNTER, SERVER_ROOM, OFFICE, DOOR, ELEVATOR, RESTROOM, OTHER
  category?:   number; // 1=Không gian học tập, 2=Phòng học nhóm thảo luận, 3=Công nghệ và Stem, 4=Khác
  positionX?:  number; // % 0-100
  positionY?:  number; // % 0-100
  width?:      number; // % 0-100
  height?:     number; // % 0-100
  colorHex?:   string;
  iconName?:   string;
  storeId?:    number; // Kho (PrintBook.Store) gắn với giá này — chỉ có ý nghĩa khi objectType = SHELF
  status?:     number;
  publicId?:   string;
  tenantName?: string;
}

export const MAP_OBJECT_TYPES: { value: string; label: string; icon: string }[] = [
  { value: 'ROOM',        label: 'Phòng',                icon: 'meeting_room' },
  { value: 'SHELF',       label: 'Kệ sách',               icon: 'menu_book' },
  { value: 'PC',          label: 'Trạm máy tính',         icon: 'computer' },
  { value: 'STUDY_SPACE', label: 'Không gian học tập',    icon: 'chair' },
  { value: 'COUNTER',     label: 'Bàn quầy',              icon: 'storefront' },
  { value: 'SERVER_ROOM', label: 'Phòng máy chủ',         icon: 'dns' },
  { value: 'OFFICE',      label: 'Phòng làm việc',        icon: 'business_center' },
  { value: 'DOOR',        label: 'Cửa ra vào',            icon: 'sensor_door' },
  { value: 'ELEVATOR',    label: 'Thang máy',             icon: 'elevator' },
  { value: 'RESTROOM',    label: 'Nhà vệ sinh',           icon: 'wc' },
  { value: 'OTHER',       label: 'Khác',                  icon: 'category' },
];

export const MAP_OBJECT_CATEGORIES: { value: number; label: string }[] = [
  { value: 1, label: 'Không gian học tập' },
  { value: 2, label: 'Phòng học nhóm thảo luận' },
  { value: 3, label: 'Công nghệ và Stem' },
  { value: 4, label: 'Khác' },
];
