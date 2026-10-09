export interface BookRecord {
  mfn?:        number;
  donNhan?:    string | number; // chuỗi "799, 829" khi ĐKCB cũ suy theo biểu ghi thuộc nhiều đơn
  dkcb?:       string;
  title?:      string;
  author?:     string;
  publisher?:  string;
  publishYear?: string;
  storeId?:    number;
  storeName?:  string;
  docTypeId?:  number;
  keyword?:    string;
  summary?:    string;
  status?:     string;
  statusName?: string;
  publicId?:   string;
  tenantName?: string;
}
