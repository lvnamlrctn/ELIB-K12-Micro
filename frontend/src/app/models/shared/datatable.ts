export interface DataTableParams {
  draw: number;
  start: number;
  length: number;
  search: { value: string; regex?: boolean };
  order?: { column: number; dir: 'asc' | 'desc' }[];
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  [key: string]: any;
}

export interface DataTableResponse<T> {
  draw: number;
  recordsTotal: number;
  recordsFiltered: number;
  data: T[];
}
