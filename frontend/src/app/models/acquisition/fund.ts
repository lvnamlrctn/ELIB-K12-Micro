export interface Fund {
  id:        number;
  name?:     string;
  manager?:  string;
  note?:     string;
  purpose?:  string;
  blane?:    number;
  budgetId?: number;
  publicId?: string;
  tenantName?: string;
}
