// Giao dịch thanh toán QR (VietQR/VNPAY) — nguồn: backend Controllers/Payment, Controllers/Public/PaymentReaderController.cs (port ELIB-LRC 09-25)
export type PaymentTargetType = 'FINE_TICKET' | 'PHOTOCOPY' | 'CARD_REISSUE';
export type PaymentProvider = 'VietQR' | 'VNPAY';
export type PaymentStatus = 'Pending' | 'Paid' | 'Expired' | 'Cancelled';

export interface PaymentTransaction {
  publicId:        string;
  transactionCode: string;
  targetType:      PaymentTargetType;
  targetId?:       number | null;
  amount:          number;
  provider:        PaymentProvider;
  status:          PaymentStatus;
  qrContent?:      string | null;
  /** VNPAY: link trang thanh toán (mở được trên điện thoại). */
  payUrl?:         string | null;
  /** UTC ("…Z"). */
  expiresAt:       string;
  paidAt?:         string | null;
}

/** Kết quả tạo giao dịch: transaction hoặc thông báo lỗi của backend (vd đơn vị chưa cấu hình tài khoản nhận). */
export interface PaymentCreateResult {
  txn: PaymentTransaction | null;
  error?: string | null;
}

export interface ReaderDebtTicket {
  targetType: 'FINE_TICKET';
  targetId:   number;
  publicId:   string;
  code?:      string;
  fineDate?:  string;
  remaining:  number;
}

export interface ReaderDebtPhoto {
  targetType: 'PHOTOCOPY';
  targetId:   number;
  publicId:   string;
  photoDate?: string;
  remaining:  number;
}

export interface ReaderDebts {
  tickets: ReaderDebtTicket[];
  photos:  ReaderDebtPhoto[];
}

export interface ReaderDebtsResult {
  debts: ReaderDebts;
  /** Cổng đơn vị đã cấu hình — rỗng = chưa nhận thanh toán trực tuyến. */
  providers: PaymentProvider[];
}
