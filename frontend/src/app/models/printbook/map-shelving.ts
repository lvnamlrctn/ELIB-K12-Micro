// Bản cá biệt (số KCB) chưa xếp giá, kèm gợi ý vị trí theo DDC trên sơ đồ trực quan.
export interface MapUnshelvedBarcode {
  id:                    number;
  barcode?:              string;
  bibTitle?:             string;
  ddc?:                  string;
  receiptCode?:          string;
  storeId?:              number;
  storeName?:            string;
  tenantName?:           string;
  suggestedMapObjectId?: number;
  suggestedShelfName?:   string;
  suggestedShelfRowId?:  number;
  suggestedRowIndex?:    number;
}

export interface MapShelvingSearchParams {
  receiptCode?: string | null;
  barcodeFrom?: string | null;
  barcodeTo?:   string | null;
  storeId?:     number | null;
  pageIndex?:   number;
  pageSize?:    number;
  tenantId?:    string | null;
}

export interface ShelfRowOption {
  id:           number;
  rowIndex?:    number;
  ddcStart?:    string;
  ddcEnd?:      string;
  description?: string;
}

export interface ShelfDetailInfo {
  id:             number;
  categoryRange?: string;
  subjectName?:   string;
  capacity?:      number;
  rows:           ShelfRowOption[];
}

export interface ShelfNode {
  id:           number;
  name?:        string;
  code?:        string;
  positionX?:   number;
  positionY?:   number;
  width?:       number;
  height?:      number;
  colorHex?:    string;
  iconName?:    string;
  shelfDetail?: ShelfDetailInfo | null;
}

export interface FloorWithShelves {
  id:            number;
  name?:         string;
  floorNumber?:  number;
  width?:        number;
  height?:       number;
  buildingName?: string;
  shelves:       ShelfNode[];
}
