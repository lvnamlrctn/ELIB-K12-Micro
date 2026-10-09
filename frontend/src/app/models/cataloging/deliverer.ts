// Đơn phân bổ — nguồn: Entities/PrintBook/AbDeliverer.cs + AbDelivererDetail.cs
export interface Deliverer {
  id:                number;
  code?:             number;
  receipt_Id?:       number;
  store_Id?:         number;
  delivererName?:    string;
  delivererAddress?: string;
  receiptName?:      string;
  receiptAddress?:   string;
  delivererDate?:    string;
  receiptDate?:      string;
  status?:           number;
  sign?:             number;
  note?:             string;
  createdBy?:        number;
  publicId?:         string;
  tenantName?:       string;
}

/** 1 dòng "sách trong đơn phân bổ" — mỗi dòng ứng với 1 bản sách vật lý (ĐKCB/Barcode) cụ thể. */
export interface DelivererLine {
  id:            number;
  deliverer_Id:  number;
  barcodeId?:    number;
  store_Id?:     number;
  barcodeValue?: string;  // ĐKCB
  mfn?:          number;
  title?:        string;
  author?:       string;
  publisher?:    string;
  publishDate?:  string;  // Năm XB
  publicId?:     string;
}

/** 1 kết quả tìm ĐKCB có sẵn trong danh mục (dùng cho khung "Tìm trong" khi thêm sách vào đơn). */
export interface DelivererBarcodeSearchResult {
  barcodeId:     number;
  barcodeValue?: string;
  mfn?:          number;
  title?:        string;
  author?:       string;
  publisher?:    string;
  publishDate?:  string;
  store?:        number;
}

/** Tham số tìm sách thuộc đơn nhận (chưa phân bổ) để thêm vào đơn phân bổ. */
export interface DelivererBarcodeSearchParams {
  receiptId:     number;
  searchBy?:     'mfn' | 'barcode';
  keyword?:      string;
  barcode?:      string;
  title?:        string;
  author?:       string;
  publisher?:    string;
  publishDate?:  string;
  mfnFrom?:      number | null;
  mfnTo?:        number | null;
  pageIndex?:    number;
  pageSize?:     number;
}

export interface DelivererBarcodeSearchPage {
  items:      DelivererBarcodeSearchResult[];
  totalCount: number;
}
