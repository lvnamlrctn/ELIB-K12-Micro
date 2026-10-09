export interface Badge {
  id: number;
  publicId?: string;
  code: string;
  name: string;
  description?: string | null;
  iconName?: string | null;
  criteriaType: string;
  threshold: number;
  sortOrder?: number | null;
  status?: number | null;
  tenantId?: number | null;
  tenantName?: string | null;
}

/** Các loại tiêu chí huy hiệu hỗ trợ — khớp đúng switch trong BadgeEvaluationJob (backend). */
export const BADGE_CRITERIA_TYPES: { value: string; label: string }[] = [
  { value: 'TotalDigitalReads',     label: 'Tổng số lượt đọc tài liệu số' },
  { value: 'DistinctDigitalTitles', label: 'Số đầu tài liệu số đã đọc' },
  { value: 'TotalPagesRead',        label: 'Tổng số trang đã đọc' },
  { value: 'TotalPrintBorrows',     label: 'Tổng số lượt mượn sách in' },
];
