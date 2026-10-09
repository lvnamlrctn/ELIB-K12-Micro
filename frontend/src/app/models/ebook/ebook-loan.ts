export interface EbookLoan {
  id:             number;
  publicId?:      string;
  ebookItemId?:   number;
  readerId?:      number;
  checkedOutAt?:  string | null;
  expiresAt?:     string | null;
  lastAccessAt?:  string | null;
  status?:        number | null;   // 1 = Active, 2 = Recalled
  recalledAt?:    string | null;
  recalledBy?:    number | null;
  recallReason?:  string | null;
  readerName?:    string | null;
  readerCardNo?:  string | null;
  ebookTitle?:    string | null;
  tenantName?:    string | null;
}
