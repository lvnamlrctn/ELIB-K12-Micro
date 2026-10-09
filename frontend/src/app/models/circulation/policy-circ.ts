// Chính sách lưu thông cho loại bạn đọc (theo đúng field backend PolicyCirc) — dùng cho màn hình
// quản lý điểm lưu thông (circ-place.ts). Tách biệt với models/circulation/circ-policy.ts (trang
// bảng phẳng cũ) để không ảnh hưởng trang đó.
export interface PolicyCirc {
  id?:                   number;
  readerType?:           number;
  circPlace?:            number;
  numberOfDate?:         number;   // Hạn mượn (ngày)
  numberOfRenew?:        number;   // Số lần gia hạn
  numberOfBook?:         number;   // SL mượn
  numberOfRequest?:      number;   // SL gửi
  muontrung?:            number;
  store?:                number;
  numberOfBookAccept?:   number;   // SL YC chấp nhận
  numberOfRenewQty?:     number;   // SL gia hạn
  numberOfRenewDays?:    number;   // Ngày gia hạn
  numberOfRequestCount?: number;   // Số lần gửi
  numberOfRequestDays?:  number;   // Ngày gửi
  allowOpacRequest?:     number;   // Được yêu cầu mượn qua opac
  publicId?:             string;
}

export interface PolicyCircTreeItem {
  id:             number;
  publicId:       string;
  readerTypeId?:  number;
  readerTypeName?: string;
  numberOfBook?:  number;
  numberOfDate?:  number;
  /** Đợt 26 — cùng 1 loại bạn đọc có thể có nhiều dòng, mỗi dòng ứng với 1 Kho riêng tại điểm lưu thông. */
  storeId?:       number;
  storeName?:     string;
}
