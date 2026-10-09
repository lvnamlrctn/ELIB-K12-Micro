import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Inventory, InventoryBarcode, InventorySummary } from '../../models/printbook/inventory';
import { AdminTaskView } from '../../models/system/admin-task';

export interface InventorySearchResult { data: Inventory[]; recordsTotal: number; }
export interface InventoryBarcodeSearchResult { data: InventoryBarcode[]; recordsTotal: number; }
export interface InventoryImportResult { successCount: number; skippedCount: number; failedCount: number; errors: string[]; message: string }

// ELIB: /api/PrintBook/Store/Inventory — nguồn Bussiness.PrintBook.Store.Inventory
@Injectable({ providedIn: 'root' })
export class InventoryService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Store/Inventory`; }

  // Backend: Keyword lọc tên, InventoryDateFrom/To lọc ngày kiểm kê (cột Submited) — rỗng gửi null (port ELIB-LRC 10-04:
  // trước đây gửi inventoryName/'' nên tìm theo tên/ngày không lọc gì).
  search(params: { keyword?: string | null; dateFrom?: string | null; dateTo?: string | null; status?: number | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<InventorySearchResult> {
    const payload = { keyword: params.keyword || null, inventoryDateFrom: params.dateFrom || null, inventoryDateTo: params.dateTo || null, status: params.status ?? 0, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        let data: any[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data: data.map(d => this.fromApi(d)), recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  // Ngày kiểm kê lưu ở cột Submited của PrintBook.Inventory; màn hình dùng tên inventoryDate (trước đây không lưu được).
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private fromApi(d: any): Inventory { return { ...d, inventoryDate: d?.inventoryDate ?? d?.submited ?? '' }; }
  private toApi(item: Partial<Inventory>) { const { inventoryDate, ...rest } = item; return { ...rest, submited: inventoryDate || null }; }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getById(publicId: string): Observable<Inventory> { return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(r => this.fromApi(r.data ?? r))); }
  // CreateInventory / SaveInventory
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Partial<Inventory>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, this.toApi(item)).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<Inventory>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, this.toApi(item)).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<void> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }

  // InsertBarcodeInventory(Barcode, StoreId, InventoryId) — quét 1 mã vạch
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  scanBarcode(payload: { inventoryId: number; barcode: string; storeId?: number }): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/ScanBarcode`, payload).pipe(map(r => r.data ?? r));
  }

  // SearchBarcodeInventory(InventoryId, CheckStoreStatus, CheckBorrow, CheckStatus, page)
  searchBarcodes(params: { inventoryId: number; checkStoreStatus?: number; checkBorrow?: number; checkStatus?: number; pageIndex?: number; pageSize?: number }): Observable<InventoryBarcodeSearchResult> {
    const payload = { inventoryId: params.inventoryId, checkStoreStatus: params.checkStoreStatus ?? -1, checkBorrow: params.checkBorrow ?? -1, checkStatus: params.checkStatus ?? -1, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 20 };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchBarcode`, payload).pipe(
      map(res => {
        let data: InventoryBarcode[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  // CountBookNotRegister + CountBookNotCorrecStore + ProcessLostBook → tổng hợp báo cáo
  getSummary(inventoryId: number): Observable<InventorySummary> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/Summary/${inventoryId}`).pipe(
      map(r => { const d = r?.data ?? r ?? {}; return { inventoryId, scanned: d.scanned ?? 0, notRegister: d.notRegister ?? 0, notCorrectStore: d.notCorrectStore ?? 0, lost: d.lost ?? 0 }; }),
      catchError(() => of({ inventoryId, scanned: 0, notRegister: 0, notCorrectStore: 0, lost: 0 }))
    );
  }

  // ProcessLostBook(InventoryId) — danh sách sách có trong kho nhưng không quét được
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  processLost(inventoryId: number): Observable<any[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/ProcessLost`, { inventoryId }).pipe(map(r => r?.data?.items ?? r?.data ?? r ?? []), catchError(() => of([])));
  }

  // ReportInventory(InventoryId) — xuất báo cáo (blob)
  exportReport(inventoryId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/Report/${inventoryId}`, { responseType: 'blob' }).pipe(catchError(() => of(new Blob())));
  }

  // Đợt 22.5 — nhập danh sách mã đã quét hàng loạt từ Excel (cột: Mã vạch, Mã kho [tuỳ chọn]), thay quét tay từng mã.
  importExcel(inventoryId: number, file: File): Observable<InventoryImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('inventoryId', String(inventoryId));
    return this.http.post<any>(`${this.baseUrl}/Import`, formData).pipe(
      map(res => {
        const d = res?.data ?? res;
        return { successCount: d?.successCount ?? 0, skippedCount: d?.skippedCount ?? 0, failedCount: d?.failedCount ?? 0,
          errors: Array.isArray(d?.errors) ? d.errors : [], message: res?.message ?? d?.message ?? '' };
      }),
      catchError(err => of({ successCount: 0, skippedCount: 0, failedCount: 1, errors: [err?.error?.message || 'Lỗi kết nối'], message: '' }))
    );
  }

  importExcelBackground(inventoryId: number, file: File): Observable<AdminTaskView | null> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('inventoryId', String(inventoryId));
    formData.append('background', 'true');
    return this.http.post<any>(`${this.baseUrl}/Import`, formData).pipe(
      map(res => (res?.data ?? res) as AdminTaskView),
      catchError(() => of(null))
    );
  }
}
