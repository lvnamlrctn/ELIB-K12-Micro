// Phiếu photo copy tài liệu — nguồn: Entities/PrintBook/CPhoto.cs (bảng PrintBook.C_photo)
export interface PhotoCopyRow {
  id:           number;
  publicId?:    string;
  readerId?:    number;
  cardNo?:      string;
  readerName?:  string;
  unitName?:    string;   // Đơn vị (lớp) của bạn đọc
  barcode?:     string;   // Số ĐKCB
  bibTitle?:    string;   // Nhan đề tài liệu
  photoDate?:   string;
  frompage?:    number;   // giữ đúng chữ 'p' thường như entity/DTO backend
  toPage?:      number;
  pages?:       number;
  copy?:        number;
  price?:       number;
  totalAmount?: number;
  isPaid?:      number;   // 2 = đã thanh toán, 1/null = chưa
  tenantName?:  string;
}

export interface PhotoCopyReaderLookup {
  readerId:  number;
  cardNo?:   string;
  fullName?: string;
  unitName?: string;
}

export interface PhotoCopyDocLookup {
  barcode?:  string;
  bibId?:    number;
  bibTitle?: string;
}

export interface PhotoCopyTotals {
  totalAmountSum: number;
  totalPaid:      number;
  totalUnpaid:    number;
  count:          number;
}

export interface PhotoCopySearchParams {
  cardNo?:        string | null;
  lastName?:      string | null;
  firstName?:     string | null;
  bibTitle?:      string | null;
  photoDateFrom?: string | null;
  photoDateTo?:   string | null;
  isPaid?:        number | null;
  tenantId?:      string | null;
  pageIndex?:     number;
  pageSize?:      number;
}
