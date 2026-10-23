import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CollectionPermission, EbookItem, ExportField, ItemBulkResult, ItemImportDefaults, ItemImportResult, ItemSelection, blobRequest,
} from '../../core/api';

/**
 * API riêng của màn Tài liệu số (hàng loạt, xuất/nhập, ảnh bìa, phân quyền bộ sưu tập). Tách khỏi Api chung để nằm trong gói nạp lười của trang,
 * không làm phình gói khởi động của admin.
 */
@Injectable({ providedIn: 'root' })
export class ItemsApi {
  private readonly http = inject(HttpClient);

  private get<T>(url: string): Promise<T> { return firstValueFrom(this.http.get<T>(url)); }
  private post<T>(url: string, body: unknown = {}): Promise<T> { return firstValueFrom(this.http.post<T>(url, body)); }
  private put<T>(url: string, body: unknown): Promise<T> { return firstValueFrom(this.http.put<T>(url, body)); }

  setItemCover(publicId: string, coverUrl: string | null) { return this.put<EbookItem>(`/api/admin/digital/items/${publicId}/cover`, { coverUrl }); }
  itemSelectionCount(selection: ItemSelection) { return this.post<{ count: number }>('/api/admin/digital/items/Selection/Preview', selection); }
  itemsMoveCollection(selection: ItemSelection, collectionId: number) { return this.put<ItemBulkResult>('/api/admin/digital/items/BulkMoveCollection', { selection, collectionId }); }
  itemsBulkStatus(selection: ItemSelection, status: number) { return this.put<ItemBulkResult>('/api/admin/digital/items/BulkStatus', { selection, status }); }
  itemsBulkDelete(selection: ItemSelection) { return this.post<ItemBulkResult>('/api/admin/digital/items/BulkDelete', selection); }
  itemExportFields() { return this.get<ExportField[]>('/api/admin/digital/items/GetExportFields'); }
  exportItems(selection: ItemSelection | null, fields: string[]) {
    return blobRequest(firstValueFrom(this.http.post('/api/admin/digital/items/Export', { selection, fields }, { responseType: 'blob' })));
  }
  exportItemsMetadata(selection: ItemSelection | null) {
    return blobRequest(firstValueFrom(this.http.post('/api/admin/digital/items/ExportMetadata', { selection }, { responseType: 'blob' })));
  }
  importItems(kind: 'excel' | 'xml', files: File[], defaults: ItemImportDefaults, language: string | null) {
    const form = new FormData();
    for (const file of files) form.append('file', file, file.name);
    if (defaults.collectionId != null) form.append('collectionId', String(defaults.collectionId));
    if (defaults.subjectId != null) form.append('subjectId', String(defaults.subjectId));
    if (defaults.topicId != null) form.append('topicId', String(defaults.topicId));
    form.append('published', String(defaults.published));
    form.append('free', String(defaults.free));
    form.append('allowDownload', String(defaults.allowDownload));
    if (language) form.append('language', language);
    return this.post<ItemImportResult>(kind === 'excel' ? '/api/admin/digital/items/ImportExcel' : '/api/admin/digital/items/ImportXml', form);
  }
  collectionPermissions(publicId: string) { return this.get<CollectionPermission[]>(`/api/admin/digital/collections/${publicId}/permissions`); }
  saveCollectionPermissions(publicId: string, rows: CollectionPermission[]) { return this.put<CollectionPermission[]>(`/api/admin/digital/collections/${publicId}/permissions`, rows); }
}
