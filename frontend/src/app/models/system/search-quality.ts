export interface SearchQueryCount { query: string; count: number; }
export interface SearchQualityDaily { date: string; count: number; empty: number; failed: number; }

export interface SearchQualityReport {
  since: string;
  until: string;
  total: number;
  failures: number;
  empty: number;
  averageMs: number;
  emptyRate: number;
  fallbackCount: number;
  fallbackRate: number;
  topEmpty: SearchQueryCount[];
  topQueries: SearchQueryCount[];
  daily: SearchQualityDaily[];
}
