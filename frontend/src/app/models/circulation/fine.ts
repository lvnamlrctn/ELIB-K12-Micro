// Phạt — nguồn: Bussiness/PrintBook/Circulation/Loan/Fine.cs (SaveFine/SearchFine)
export interface Fine {
  id:           number;
  readerId?:    number;
  cardNo?:      string;
  readerName?:  string;
  fineDate?:    string;
  reasonFineId?: number;
  reason?:      string;
  returnDate?:  string;
  note?:        string;
  value?:       number;
  fineMethodId?: number;
  borrowId?:    number;
  borrowDate?:  string;
  lanPhat?:     number;     // lần phạt
  status?:      number;     // 1=Chưa thu, 2=Đã thu, 3=Miễn
  statusName?:  string;
  tenantName?:  string;
}
