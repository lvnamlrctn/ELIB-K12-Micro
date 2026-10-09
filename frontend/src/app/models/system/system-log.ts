export interface SystemLog {
  id?: number;
  userId?: number;
  fullName?: string;
  actionType?: string;
  object?: string;
  action?: string;
  submited?: string;
  ip?: string;
  application?: string;
  portalId?: number | null;
  tenantId?: string;
  tenantName?: string;
}

export interface SystemUser {
  publicId?: string;
  id?: string;
  fullName?: string;
  userName?: string;
}

export interface SystemLogSearchParams {
  userId?: string;
  action?: string;
  dateFrom?: string;
  dateTo?: string;
  tenantId?: string | null;
  pageIndex: number;
  pageSize: number;
}
