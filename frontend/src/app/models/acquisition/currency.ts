export interface Currency {
  id:            number;
  code?:         string;
  name?:         string;
  exchangeRate?: number;
  status?:       number;
  tenantName?:   string;
}
