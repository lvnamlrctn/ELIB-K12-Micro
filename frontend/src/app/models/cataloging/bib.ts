// Biểu ghi thư mục (MARC) — nguồn: Entities/PrintBook/Catalogue/Bib.cs + Bussiness Book/Marc21Ultil
export interface Bib {
  bibId:          number;
  mfn?:           number;
  bibTypeId?:     number;
  bibWorkSheetId?: number;
  collectionId?:  number;
  marcStatus?:    number;
  status?:        number;
  images?:        string;
  url?:           string;
  createdBy?:     number;
  createdTime?:   string;
  updatedBy?:     number;
  updatedTime?:   string;
  // MARC content
  fields?:        MarcField[];
  // convenience fields cho danh sách
  title?:         string;
  author?:        string;
  publisher?:     string;
  publishDate?:   string;
  isbd?:          string;
  publicId?:      string;
  hasReceipt?:    boolean; // đã có trong 1 đơn nhận nào đó — dùng cho màn Tra trùng
  hasOrder?:      boolean; // đã có trong 1 đơn đặt nào đó — dùng cho màn Tra trùng
  tenantName?:    string;
}

export interface MarcField {
  tag:        string;        // "245"
  ind1?:      string;
  ind2?:      string;
  value?:     string;        // control field value (tag < 010)
  subFields?: MarcSubField[];
}

export interface MarcSubField {
  code:  string;             // "a"
  value: string;
}
