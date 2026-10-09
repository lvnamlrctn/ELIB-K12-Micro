export interface PolicyCircFine {
  id?:           number;
  policyCircId:  number;
  fineTypeId?:   number;
  fineMethodId?: number;
  holdCardDays?: number;   // Số ngày giữ thẻ
  fineAmount?:   number;   // Số tiền phạt
  publicId?:     string;
}
