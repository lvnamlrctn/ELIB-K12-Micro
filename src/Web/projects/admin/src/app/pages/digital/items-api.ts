import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CollectionPermission, EbookItem, ExportField, ItemBulkResult, ItemImportDefaults, ItemImportResult, ItemSelection, blobRequest,
} from '../../core/api';

/** Tích hợp đang bật ở đơn vị (tra ISBN, gợi ý AI, đồng bộ nội dung, vector hóa) — nút của dịch vụ chưa cấu hình được ẩn. */
export interface ItemCapabilities { isbn: boolean; ai: boolean; content: boolean; embedding: boolean; }

export interface IsbnMetadata {
  isbn: string; title: string | null; subtitle: string | null; authors: string[]; publisher: string | null; publishDate: string | null; pages: number | null;
  language: string | null; description: string | null; categories: string[]; source: string;
}

/** Đồng bộ dữ liệu dùng chung (chỉ đơn vị SHARED): lần chạy gần nhất của bộ chạy nền. */
export interface SharedSyncStatus { isShared: boolean; lastRunAt: string | null; applied: number; pending: number; failed: number; error: string | null; }

export interface ClassificationSuggestion { subjectId: number | null; subjectName: string | null; topicId: number | null; topicName: string | null; keywords: string[]; }

/** Trạng thái đồng bộ nội dung của một tài liệu: pending | running | done | no_text | failed. */
export interface ContentStatus {
  itemPublicId: string; status: string; pageCount: number; chunkCount: number; embeddedCount: number; error: string | null; syncedAt: string | null;
}

export interface ContentSyncResult { queued: number; skipped: ItemBulkResult['skipped']; }

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

  setItemHidden(publicId: string, hidden: boolean) { return this.put<EbookItem>(`/api/admin/digital/items/${publicId}/Hidden`, { hidden }); }
  /** 403 NOT_SHARED_TENANT với đơn vị thường — gọi này chỉ để biết đơn vị hiện tại có phải SHARED không. */
  sharedSyncStatus() { return this.get<SharedSyncStatus>('/api/admin/digital/shared-sync/Status'); }
  sharedSyncRun() { return this.post<SharedSyncStatus>('/api/admin/digital/shared-sync/Run'); }
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
  exportItemsWithFiles(selection: ItemSelection | null) {
    return blobRequest(firstValueFrom(this.http.post('/api/admin/digital/items/ExportWithFiles', { selection }, { responseType: 'blob' })));
  }
  capabilities() { return this.get<ItemCapabilities>('/api/admin/digital/items/Capabilities'); }
  lookupIsbn(isbn: string) { return this.get<IsbnMetadata>(`/api/admin/digital/items/LookupIsbn?isbn=${encodeURIComponent(isbn)}`); }
  suggestClassification(title: string, description: string | null) {
    return this.post<ClassificationSuggestion>('/api/admin/digital/items/SuggestClassification', { title, description });
  }
  syncContent(selection: ItemSelection) { return this.post<ContentSyncResult>('/api/admin/digital/items/SyncContent', selection); }
  contentStatus(publicIds: string[]) { return this.post<ContentStatus[]>('/api/admin/digital/items/ContentStatus', { publicIds }); }
  importItems(kind: 'excel' | 'xml' | 'dspace', files: File[], defaults: ItemImportDefaults, language: string | null) {
    const form = new FormData();
    for (const file of files) form.append('file', file, file.name);
    if (defaults.collectionId != null) form.append('collectionId', String(defaults.collectionId));
    if (defaults.subjectId != null) form.append('subjectId', String(defaults.subjectId));
    if (defaults.topicId != null) form.append('topicId', String(defaults.topicId));
    form.append('published', String(defaults.published));
    form.append('free', String(defaults.free));
    form.append('allowDownload', String(defaults.allowDownload));
    if (language) form.append('language', language);
    const endpoint = kind === 'excel' ? 'ImportExcel' : kind === 'xml' ? 'ImportXml' : 'ImportDspace';
    return this.post<ItemImportResult>(`/api/admin/digital/items/${endpoint}`, form);
  }
  collectionPermissions(publicId: string) { return this.get<CollectionPermission[]>(`/api/admin/digital/collections/${publicId}/permissions`); }
  saveCollectionPermissions(publicId: string, rows: CollectionPermission[]) { return this.put<CollectionPermission[]>(`/api/admin/digital/collections/${publicId}/permissions`, rows); }
}
