// Phiếu nhập bổ sung — nguồn: Entities/PrintBook/AbReceipt.cs + AbReceiptDetail.cs
// Lưu ý casing: backend dùng System.Text.Json CamelCase mặc định, các cột toàn HOA có dấu "_"
// (BUDGET_ID, CREATED_BY) serialize thành "budgeT_ID"/"createD_BY" (đã kiểm chứng qua API thật),
// khác quy tắc camelCase thông thường — KHÔNG đổi lại các tên field dưới đây theo trực giác.
export interface AbReceipt {
  id:               number;
  code?:            number;
  receipt_Name?:    string;
  receipt_Date?:    string;
  store_Id?:        number;
  supplier_Id?:     number;
  fundId?:          number;
  budgeT_ID?:       number;
  source_Id?:       number;
  paymentMethodId?: number;
  payment_Status?:  number;
  status?:          number;
  note?:            string;
  createD_BY?:      number;
  createdDate?:     string;
  publicId?:        string;
  tenantName?:      string;
}

export interface AbReceiptLine {
  id:            number;
  receipt_Id:    number;
  bibid?:        number;
  mfn?:          number;   // MFN của biểu ghi MARC — dùng để sửa lại biểu ghi
  bibPublicId?:  string;   // Bib.PublicId — in nhãn môn loại theo biểu ghi
  title?:        string;
  author?:       string;
  publisher?:    string;
  publishDate?:  string;   // Năm XB — join từ BibXml
  amount?:       number;   // số bản
  currency?:     string;
  price?:        number;   // đơn giá
  rate?:         number;   // tỷ giá
  registerCount?: number;  // số ĐKCB đã đăng ký
  publicId?:     string;
}

/** Yêu cầu đăng ký số KCB theo lô cho 1 dòng phiếu nhập. */
export interface BarcodeRegisterRequest {
  receiptLineId: number;
  prefix:        string;
  digitLength:   number;
  quantity:      number;
  storeId:       number;
  startNumber?:  number;   // "Từ số" — nếu bỏ trống, backend tự dò số lớn nhất hiện có
}

/** Yêu cầu đăng ký 1 số KCB nhập tay (tab "Riêng lẻ"). */
export interface SingleBarcodeRequest {
  receiptLineId: number;
  barcodeValue:  string;
  storeId:       number;
}

/** Một số KCB đã đăng ký cho 1 dòng phiếu nhập. */
export interface RegisteredBarcode {
  id:        number;
  barcode:   string;
  storeId?:  number;
  status?:   string;
  publicId?: string;
}
