export interface EbookReservation {
  id:              number;
  publicId?:       string;
  ebookItemId?:    number;
  readerId?:       number;
  requestedAt?:    string | null;
  status?:         number | null;   // 1=Pending, 2=Ready, 3=Fulfilled, 4=Cancelled, 5=Expired
  readyAt?:        string | null;
  readyExpiresAt?: string | null;
  fulfilledAt?:    string | null;
  cancelledAt?:    string | null;
  readerName?:     string | null;
  readerCardNo?:   string | null;
  ebookTitle?:     string | null;
  tenantName?:     string | null;
}
