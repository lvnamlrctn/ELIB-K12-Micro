// Kỳ ấn phẩm (số) — thiết kế ILS. Nguồn: ELIB Serial.SaveSerialItem, bảng PrintBook.SERIALITEM
// status: 0 dự kiến (expected) · 1 đã nhận (received) · 2 khiếu nại (claimed) · 3 thiếu (missing) · 4 trễ (late)
export type SerialIssueStatus = 0 | 1 | 2 | 3 | 4;

export interface SerialIssue {
  id:             number;
  subscriptionId: number;
  serialSeq?:     string;   // chuỗi định danh kỳ (vd "Tập 5, Số 12") — sinh từ pattern
  serialSeqX?:    number;   serialSeqY?: number;   serialSeqZ?: number;
  status?:        SerialIssueStatus;
  isSpecial?:     number;   // kỳ đặc biệt (0/1)
  quantity?:      number;
  plannedDate?:   string;   // ngày dự kiến phát hành (expected date)
  publishedDate?: string;   // ngày nhận thực tế
  claimDate?:     string;   // ngày khiếu nại gần nhất
  claimCount?:    number;   // số lần khiếu nại
  barcode?:       string;   // ĐKCB khi nhập kho (nếu lập bản cá biệt)
  note?:          string;

  // ghép số (cẩm nang mục VII.4) — bản ghi gộp không set serialSeqX/Y/Z (không phải số),
  // chỉ set serialSeq (vd "Số 6+7") + sortOrder cho vị trí hiển thị mong muốn
  isMerged?:      boolean;
  sortOrder?:     number;

  publicId?:      string;
}

// thống kê nhận kỳ của một đăng ký (ILS serials statistics)
export interface SerialStatistics {
  expected: number;
  received: number;
  claimed:  number;
  missing:  number;
  late:     number;
}
