// Đăng ký nhận ấn phẩm định kỳ — thiết kế ILS (Serials Control)
// Nguồn ELIB: DataAccess.PrintBook.Magazine.Serial.SaveSerial — mở rộng theo chuẩn ILS (Koha/MARC serials)

// Cách sinh kỳ
export type SubscriptionType = 1 | 2;        // 1 = thủ công (manual), 2 = tự động dự đoán (predicted)
// Đơn vị độ dài đăng ký
export type LengthUnit = 1 | 2 | 3;          // 1 = số kỳ, 2 = tuần, 3 = tháng
// Vòng đời đăng ký
export type SubscriptionStatus = 1 | 2 | 3;  // 1 = đang hiệu lực, 2 = hết hạn, 3 = đã hủy

export interface Subscription {
  id:               number;
  bibId?:           number;   // biểu ghi tài liệu (đầu báo/tạp chí)
  title?:           string;   // hiển thị: nhan đề biểu ghi
  issn?:            string;
  magazineTypeId?:  number;   // loại ấn phẩm
  magazineTypeName?: string;

  // lịch & mẫu đánh số
  frequencyId?:     number;   frequencyName?: string;
  patternId?:       number;   patternName?:   string;
  startTime?:       string;   // ngày bắt đầu đăng ký
  endTime?:         string;   // ngày kết thúc đăng ký
  firstTime?:       string;   // ngày phát hành kỳ đầu
  startX?:          number;   startY?: number;   startZ?: number;   // giá trị khởi đầu của mẫu đánh số
  lastX?:           number;   lastY?: number;    lastZ?: number;    // giá trị kỳ gần nhất đã sinh

  // điều khiển đăng ký theo chuẩn ILS
  subscriptionType?:   SubscriptionType;  // thủ công / tự động dự đoán
  subscriptionLength?: number;            // độ dài đăng ký
  lengthUnit?:         LengthUnit;         // đơn vị độ dài (kỳ/tuần/tháng)
  graceDays?:          number;             // số ngày chờ trước khi khiếu nại (claim grace period)
  status?:             SubscriptionStatus; // trạng thái vòng đời

  // bổ sung & lưu trữ
  storeId?:         number;   storeName?:    string;
  supplierId?:      number;   supplierName?: string;
  callNumber?:      string;   // ký hiệu xếp giá / ĐKCB
  locate?:          string;   // vị trí lưu
  publicNote?:      string;   // ghi chú công khai (OPAC)
  internalNote?:    string;   // ghi chú nội bộ
  note?:            string;

  // thống kê (chỉ đọc, backend trả kèm)
  expectedCount?:   number;
  receivedCount?:   number;
  missingCount?:    number;

  // duyệt đơn đặt — sau khi duyệt thì khóa sửa/xóa (theo cẩm nang mục V.3)
  approved?:        boolean;
  approvedDate?:    string;

  publicId?:        string;
  tenantName?:      string;
}
