// Thanh lý tài liệu — nguồn ELIB: Book.LiquidateBook / ReLiquidateBook / SearchLiquidate
export interface LiquidateItem {
  barcodeId:     number;
  barcode:       string;
  mfn?:          number;
  bibTitle?:     string;
  author?:       string;
  publisher?:    string;
  storeId?:      number;
  storeName?:    string;
  liquidateDate?: string;   // ngày thanh lý
  reason?:       string;
  userName?:     string;
  publicId?:     string;
  tenantName?:   string;
}
