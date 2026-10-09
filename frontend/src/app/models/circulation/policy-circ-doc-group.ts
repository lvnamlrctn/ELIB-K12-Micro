export interface PolicyCircDocGroup {
  id?:                 number;
  policyCircId:        number;
  docGroupId:          number;
  docGroupName?:       string;
  numberOfBook?:       number;   // SL mượn
  numberOfRequest?:    number;   // SL gửi
  numberOfRenewQty?:   number;   // SL gia hạn
  numberOfBookAccept?: number;   // SL YC chấp nhận
  publicId?:           string;
}
