export interface Budget {
  id:         number;
  name?:      string;
  blance?:    number;
  startTime?: string;
  endTime?:   string;
  note?:      string;
  status?:    number;
  publicId?:  string;
  tenantName?: string;
}
