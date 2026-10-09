// Chính sách lưu thông theo loại bạn đọc — nguồn: ELIB CircPolicy (PrintBook.PolicyCirc)
export interface CircPolicy {
  id:                 number;
  readerType?:        number;
  readerTypeName?:    string;
  storeId?:           number;
  numberOfBook?:      number;   // số sách mượn tối đa
  numberOfDate?:      number;   // số ngày mượn
  numberOfRenew?:     number;   // số lần gia hạn tối đa
  numberOfRenewDays?: number;   // số ngày mỗi lần gia hạn
  finePerDay?:        number;   // tiền phạt / ngày quá hạn (chưa có cột tương ứng trên PolicyCirc)
  publicId?:          string;
  tenantName?:        string;
}
