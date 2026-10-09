export interface NewsArticle {
  id?: number;
  publicId?: string;
  title?: string;
  brief?: string;
  content?: string;
  images?: string;
  thumb?: string;
  startTime?: string;
  endTime?: string;
  createdDate?: string;
  lastUpDate?: string;
  createdBy?: number;
  updateBy?: number;
  categoryId?: number;
  categoryName?: string;
  tenantName?: string;
  eventId?: number;
  portalId?: string;
  language?: string;
  keyword?: string;
  author?: string;
  source?: string;
  types?: string;
  status?: number;
  allowComment?: number;
  totalView?: number;
  metaTitle?: string;
  metaKeyword?: string;
  metaDescription?: string;
}

export interface NewsSearchParams {
  keyword?: string;
  categoryId?: number;
  status?: number;
  dateFrom?: string;
  dateTo?: string;
  tenantId?: string | null;
  pageIndex: number;
  pageSize: number;
}
