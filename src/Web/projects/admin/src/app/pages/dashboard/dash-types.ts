/** Hình dạng JSON của GET /api/admin/<service>/dashboard (camelCase như server trả). */

export interface MonthPoint { year: number; month: number; label: string; value: number; }
export interface MonthlySeries { points: MonthPoint[]; total: number; }
export interface NamedCount { name: string; count: number; }

export interface CirculationDashboard {
  daily: { date: string; label: string; borrowed: number; returned: number }[];
  dailyBorrowed: number; dailyReturned: number;
  hourly: { hour: number; label: string; borrowed: number; returned: number }[];
  hourlyBorrowed: number; hourlyReturned: number;
  trend: MonthlySeries;
  topBorrowed: { mfn: number; title: string; author: string | null; count: number }[];
  overdueReaders: number; overdueLoans: number;
  overdueAging: { label: string; count: number }[];
  borrowerTypes: NamedCount[];
  activeReaders: number; loans12Months: number;
  storeUsage: { storeId: number | null; name: string; count: number }[];
  unused: { mfn: number; title: string; author: string | null; storeName: string | null; copies: number }[];
  unusedTotal: number;
}

export interface HoldingsDashboard {
  titles: number; copies: number; unshelved: number; onLoan: number; lost: number;
  /** name rỗng = chưa có loại; other = nhóm gộp "Khác". */
  distribution: { name: string; count: number; percent: number; other: boolean }[];
  distributionTotal: number;
  printAdded: MonthlySeries;
  stores: { storeId: number; name: string; count: number; capacity: number | null; percent: number | null }[];
}

export interface CatalogDashboard {
  bibs: number; visible: number;
  newByWeek: { date: string; label: string; value: number }[]; newTotal: number;
  missingAuthor: number; missingDdc: number; missingCover: number;
}

export interface DigitalDashboard {
  ebooks: number; published: number; totalViews: number; totalDownloads: number;
  addedTrend: MonthlySeries; readsTrend: MonthlySeries;
  topRead: { ebookPublicId: string; title: string; author: string | null; count: number }[];
  formatDistribution: { format: string; count: number; percent: number }[];
  storage: { totalBytes: number; fileCount: number };
}

export interface PortalDashboard {
  visits: { total: number; today: number; last7Days: number; last30Days: number };
  trend: MonthlySeries;
}

export interface PlatformHealth {
  checkedAt: string;
  services: { name: string; status: 'Healthy' | 'Unhealthy' | 'Unreachable'; latencyMs: number | null; detail: string | null }[];
  queues: { name: string; messages: number }[] | null;
  queuesDetail: string | null;
}

export interface AcquisitionDashboard {
  orderProgress: {
    orderTotal: number; receiptTotal: number;
    ordersByStatus: { status: number; label: string; count: number }[];
    receiptsByStatus: { status: number; label: string; count: number }[];
  };
  budgetVsSpend: { funds: { fundId: number; name: string; budget: number; spend: number }[]; totalBudget: number; totalSpend: number };
}

export interface SpaceDashboard {
  occupancyNow: { totalSpaces: number; occupied: number; free: number };
  noShow30: { total: number; noShow: number; ratePercent: number | null };
  pendingRequests: number;
}

export interface PatronDashboard {
  readers: { total: number; locked: number; expired: number; active: number };
  added12: MonthlySeries;
  byType: NamedCount[];
}
