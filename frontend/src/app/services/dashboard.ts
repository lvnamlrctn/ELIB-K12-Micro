import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface DashboardStats {
  totalDocs: number;
  digitalBooks: number;
  printTitles: number;
  printBooksInStock: number;
  digitizationRate: number;
}

export interface DashboardBorrowTrend {
  labels: string[];
  data: number[];
}

export interface DashboardDailyTrendPoint {
  label: string;
  date: string;
  borrowed: number;
  returned: number;
}

export interface DashboardTypeDistributionItem {
  label: string;
  count: number;
  percentage: number;
}

export interface DashboardTopBorrowedItem {
  bibId: number;
  title: string;
  author: string;
  ddc: string;
  count: number;
}

export interface DashboardOverdueBucket {
  label: string;
  count: number;
}

export interface DashboardFundBudgetVsSpending {
  fundName: string;
  budget: number;
  spent: number;
}

export interface DashboardData {
  stats: DashboardStats;
  borrowTrend: DashboardBorrowTrend;
  overdueReaders: number;
  overdueBuckets: DashboardOverdueBucket[];
  dailyTrend: DashboardDailyTrendPoint[];
  typeDistribution: DashboardTypeDistributionItem[];
  top5Borrowed: DashboardTopBorrowedItem[];
  fundBudgetVsSpending: DashboardFundBudgetVsSpending[];
}

/** Tab Biên mục của dashboard theo vai trò (Đợt 21) — GET api/Cms/Dashboard/Cataloging. */
export interface DashboardNeverBorrowedItem {
  bibId: number;
  mfn: number | null;
  catalogedAt: string | null;
  copies: number;
  title: string | null;
  author: string | null;
}
export interface DashboardCatalogingData {
  printTitles: number;
  weeklyAdded: { weekStart: string; count: number }[];
  neverBorrowedCopies: number;
  neverBorrowedTop: DashboardNeverBorrowedItem[];
}

// ── Đợt 22.4 ──────────────────────────────────────────────────────────────────
export interface DashboardAcquisitionData {
  ordersByStatus: { status: number | null; count: number }[];
  receiptsByStatus: { status: number | null; count: number }[];
  unregisteredLines: number;
  storeFillRate: { storeId: number; name: string | null; capacity: number | null; count: number; fillRate: number | null }[];
  budgetVsSpent: { fundId: number; name: string | null; budget: number | null; spent: number }[];
}

export interface DashboardDigitalData {
  topRead: { itemId: number; count: number; title: string | null; author: string | null }[];
  formatDistribution: { format: string; count: number; sizeBytes: number }[];
  totalSizeBytes: number;
}

export interface DashboardSpaceData {
  roomsInUse: number;
  roomsTotal: number;
  roomsFree: number;
  noShowCount: number;
  noShowSample: number;
  noShowRate: number | null;
}

export interface DashboardDevOpsData {
  rateLimitRejected24h: number;
  jobsByState: { state: string | null; count: number }[];
  digitalStorage: {
    runAt: string; totalObjects: number; totalSizeBytes: number;
    orphanCount: number; dbFileCount: number; brokenFileCount: number | null;
  } | null;
}

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private http = inject(HttpClient);

  /** Không có dữ liệu mẫu dự phòng: lỗi trả null để giao diện báo "không tải được" thay vì hiện số giả. */
  getCataloging(): Observable<DashboardCatalogingData | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${environment.baseApiUrl}/api/Cms/Dashboard/Cataloging`).pipe(
      map(res => (res?.data ?? null) as DashboardCatalogingData | null),
      catchError(() => of(null))
    );
  }

  // Đợt 22.4 — 3 tab dashboard còn lại + DevOps.
  getAcquisition(): Observable<DashboardAcquisitionData | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${environment.baseApiUrl}/api/Cms/Dashboard/Acquisition`).pipe(
      map(res => (res?.data ?? null) as DashboardAcquisitionData | null),
      catchError(() => of(null))
    );
  }

  getDigital(): Observable<DashboardDigitalData | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${environment.baseApiUrl}/api/Cms/Dashboard/Digital`).pipe(
      map(res => (res?.data ?? null) as DashboardDigitalData | null),
      catchError(() => of(null))
    );
  }

  getSpace(): Observable<DashboardSpaceData | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${environment.baseApiUrl}/api/Cms/Dashboard/Space`).pipe(
      map(res => (res?.data ?? null) as DashboardSpaceData | null),
      catchError(() => of(null))
    );
  }

  /** null = không có quyền (403, chỉ tài khoản hệ thống) hoặc lỗi — cả 2 trường hợp UI đều ẩn tab, không phân biệt. */
  getDevOps(): Observable<DashboardDevOpsData | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${environment.baseApiUrl}/api/Cms/Dashboard/DevOps`).pipe(
      map(res => (res?.data ?? null) as DashboardDevOpsData | null),
      catchError(() => of(null))
    );
  }

  // Gọi endpoint tổng hợp (xem docs/BACKEND-DASHBOARD.md). Backend chưa sẵn → fallback mock.
  getDashboardData(): Observable<DashboardData> {
    return this.http.get<any>(`${environment.baseApiUrl}/api/Cms/Dashboard/Summary`).pipe(
      map(res => this.normalize(res?.data ?? res)),
      catchError(() => of(this.mock()))
    );
  }

  // Chuẩn hóa response về DashboardData, đảm bảo không undefined để biểu đồ/thẻ không vỡ.
  private normalize(d: any): DashboardData {
    return {
      stats: {
        totalDocs:         d?.stats?.totalDocs         ?? 0,
        digitalBooks:      d?.stats?.digitalBooks      ?? 0,
        printTitles:       d?.stats?.printTitles       ?? 0,
        printBooksInStock: d?.stats?.printBooksInStock ?? 0,
        digitizationRate:  d?.stats?.digitizationRate  ?? 0,
      },
      borrowTrend: {
        labels: Array.isArray(d?.borrowTrend?.labels) ? d.borrowTrend.labels : [],
        data:   Array.isArray(d?.borrowTrend?.data)   ? d.borrowTrend.data   : [],
      },
      overdueReaders: d?.overdueReaders ?? 0,
      overdueBuckets: Array.isArray(d?.overdueBuckets) ? d.overdueBuckets : [],
      dailyTrend: Array.isArray(d?.dailyTrend) ? d.dailyTrend : [],
      typeDistribution: Array.isArray(d?.typeDistribution) ? d.typeDistribution : [],
      top5Borrowed:      Array.isArray(d?.top5Borrowed)     ? d.top5Borrowed     : [],
      fundBudgetVsSpending: Array.isArray(d?.fundBudgetVsSpending) ? d.fundBudgetVsSpending : [],
    };
  }

  // Dữ liệu mẫu khi backend chưa cung cấp endpoint Summary.
  private mock(): DashboardData {
    return {
      stats: { totalDocs: 8626, digitalBooks: 3426, printTitles: 5200, printBooksInStock: 8838, digitizationRate: 40 },
      borrowTrend: {
        labels: ['Th9', 'Th10', 'Th11', 'Th12', 'Th1', 'Th2', 'Th3', 'Th4', 'Th5', 'Th6', 'Th7', 'Th8'],
        data:   [40, 45, 60, 55, 130, 65, 70, 75, 80, 95, 65, 731]
      },
      overdueReaders: 12,
      overdueBuckets: [
        { label: '1-7 ngày', count: 5 },
        { label: '8-30 ngày', count: 4 },
        { label: 'Trên 30 ngày', count: 3 },
      ],
      dailyTrend: Array.from({ length: 14 }, (_, i) => {
        const d = new Date(); d.setDate(d.getDate() - (13 - i));
        return {
          label: `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`,
          date: d.toISOString().slice(0, 10),
          borrowed: 10 + (i % 5) * 3,
          returned: 8 + (i % 4) * 3
        };
      }),
      typeDistribution: [
        { label: 'Hoạt cảnh',        count: 57, percentage: 2 },
        { label: 'Technical Report', count: 3,  percentage: 0 },
        { label: 'Phần mềm',         count: 1,  percentage: 0 },
        { label: 'Video',            count: 1,  percentage: 0 },
        { label: 'Bản thảo',         count: 1,  percentage: 0 },
      ],
      top5Borrowed: [
        { bibId: 1, title: 'Giáo trình Pháp luật đại cương', author: 'Nguyễn Thị Phương Thuý', ddc: '349.597', count: 174 },
        { bibId: 2, title: 'Giáo trình Tài chính tiền tệ',   author: 'Vũ, Thị Hậu',             ddc: '332.4',   count: 87 },
        { bibId: 3, title: 'Giáo trình Chủ nghĩa xã hội khoa học', author: 'Hoàng Chí Bảo',      ddc: '335',     count: 65 },
        { bibId: 4, title: 'Giáo trình quản trị học',         author: 'Đoàn Thị Thu Hà',          ddc: '658',     count: 60 },
        { bibId: 5, title: 'Giáo trình Nguyên lý thống kê',   author: 'Ngô Thị Mỹ',                ddc: '519.5',   count: 53 },
      ],
      fundBudgetVsSpending: [
        { fundName: 'Quỹ bổ sung sách in', budget: 200000000, spent: 145000000 },
        { fundName: 'Quỹ tài liệu số',     budget: 100000000, spent: 62000000 },
      ],
    };
  }
}
