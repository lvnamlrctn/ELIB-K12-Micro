// Sách ra vào kho — nguồn: Entities/PrintBook/BookOutStore.cs
export interface BookOutStoreLine {
  id:          number;
  publicId?:   string;
  barcode?:    string;
  bibTitle?:   string;
  exportDate?: string;
  importDate?: string;
  status?:     string; // 'O' = Xuất kho, 'R' = Nhập kho
  reasonName?: string;
  tenantName?: string;
}

export interface ScanOutParams {
  barcode: string;
  delivererName?: string | null;
  receiverName?:  string | null;
  reasonId?:      number | null;
  unitId?:        number | null;
  exhibitionLocationId?: number | null;
  returnBarcodeAtLibrary: boolean;
}

export interface BookOutStoreSearchParams {
  keyword?:               string | null;
  typeStatus?:            string | null; // 'O' Sách ra kho / 'R' Sách vào kho
  unitId?:                number | null;
  reasonId?:              number | null;
  exhibitionLocationId?:  number | null;
  delivererName?:         string | null;
  receiverName?:          string | null;
  barcode?:               string | null;
  returnBarcodeAtLibrary?: boolean | null;
  exportDateFrom?:        string | null;
  exportDateTo?:          string | null;
  importDateFrom?:        string | null;
  importDateTo?:          string | null;
  pageIndex?:             number;
  pageSize?:              number;
  tenantId?:              string | null;
}
