// Tài liệu (Barcode) gắn vào phiếu điều chuyển — nguồn: Entities/PrintBook/AbMoveDetail.cs

export interface AbMoveDetailLine {
  id:         number;
  publicId?:  string;
  barcode?:   string;
  bibTitle?:  string;
  bibAuthor?: string;
}

export interface MoveDocumentCandidate {
  id:          number;
  barcode?:    string;
  mfn?:        number;
  title?:      string;
  author?:     string;
  publisher?:  string;
  publishYear?: string;
}

export interface MoveDocumentSearchParams {
  moveId:       number;
  mfnFrom?:     number | null;
  mfnTo?:       number | null;
  title?:       string | null;
  author?:      string | null;
  publisher?:   string | null;
  publishYear?: string | null;
  barcodeFrom?: string | null;
  barcodeTo?:   string | null;
  pageIndex?:   number;
  pageSize?:    number;
}
