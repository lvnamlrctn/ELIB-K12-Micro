// Phiếu phạt quá hạn — nguồn: Controllers/PrintBook/Circulation/CFineTicketController.cs
export interface FineTicketLine {
  id:           number;
  barcode?:     string;
  bibTitle?:    string;
  fine_type_id?: string;
  overdueDays?: number;
  unitPrice?:   number;
  value?:       number;
}

export interface FineTicketFeeLegend {
  overdueRate?:  number;
  techFee?:      number;
  shippingFee?:  number;
}

export interface FineTicket {
  id:               number;
  publicId?:        string;
  code?:            string;
  readerId?:        number;
  cardNo?:          string;
  readerName?:      string;
  fineDate?:        string;
  status?:          number;
  lanphat?:         number;
  discountAmount?:  number;
  paidAmount?:      number;
  totalAmount?:     number;
  remaining?:       number;
  owesDocument?:    number;
  fineTypeId?:      number;
  fineTypeName?:    string;
  fineMethodId?:    number;
  fineMethodName?:  string;
  readerIssueDate?: string;
  note?:            string;
  lines:            FineTicketLine[];
  feeLegend?:       FineTicketFeeLegend;
}

export interface FineTicketListRow {
  id:           number;
  publicId:     string;
  code?:        string;
  fineDate?:    string;
  status?:      number;
  cardNo?:      string;
  readerName?:  string;
  fineTypeId?:  number;
  fineTypeName?: string;
  totalAmount?: number;
  paidAmount?:  number;
  remaining?:   number;
  tenantName?:  string;
}

export interface FineTicketTotals {
  totalReceivable: number;
  totalReceived:   number;
  remaining:       number;
}
