import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CrudPage, CrudSearch, blobRequest } from '../../core/api';

/** Trạng thái đăng ký (service serials: 0 nháp, 1 đang đặt, 2 đã đóng). */
export const SUBSCRIPTION_STATUS = { Draft: 0, Active: 1, Closed: 2 } as const;
/** Trạng thái kỳ: 0 dự kiến, 1 đã nhận, 2 khiếu nại, 3 thiếu. */
export const ISSUE_STATUS = { Planned: 0, Received: 1, Claimed: 2, Missing: 3 } as const;

export interface Subscription {
  id: number; publicId: string; bibPublicId: string | null; bibMfn: number | null; title: string; issn: string | null; publicationTypeId: number | null;
  publicationTypeName: string | null; frequencyId: number | null; frequencyName: string | null; patternId: number | null; patternName: string | null;
  storeId: number | null; storeName: string | null; supplierPublicId: string | null; supplierName: string | null; startsOn: string | null; endsOn: string | null;
  firstIssueDate: string | null; startX: number | null; startY: number | null; startZ: number | null; lastX: number | null; lastY: number | null; lastZ: number | null;
  callNumber: string | null; publicNote: string | null; internalNote: string | null; graceDays: number; status: number; approved: boolean; approvedAt: string | null;
  issueCount: number; receivedCount: number; missingCount: number; createdAt: string; updatedAt: string | null;
}

export interface SubscriptionSearch extends CrudSearch { publicationTypeId?: number | null; frequencyId?: number | null; approved?: boolean | null; }

export interface SubscriptionRequest {
  title: string | null; issn: string | null; bibPublicId: string | null; publicationTypeId: number | null; frequencyId: number | null; patternId: number | null;
  storeId: number | null; storeName: string | null; supplierPublicId: string | null; supplierName: string | null; startsOn: string | null; endsOn: string | null;
  firstIssueDate: string | null; startX: number | null; startY: number | null; startZ: number | null; callNumber: string | null; publicNote: string | null;
  internalNote: string | null; graceDays: number; status: number;
}

export interface BibOption { bibPublicId: string; mfn: number; title: string; author: string | null; publisher: string | null; issn: string | null; }
export interface SupplierOption { publicId: string; name: string; }

export interface SerialIssue {
  id: number; publicId: string; subscriptionId: number; subscriptionPublicId: string; subscriptionTitle: string; label: string; x: number | null; y: number | null;
  z: number | null; isSpecial: boolean; isMerged: boolean; status: number; quantity: number; plannedDate: string | null; receivedDate: string | null;
  claimDate: string | null; claimCount: number; note: string | null; bound: boolean; createdAt: string;
}

export interface IssueSearch extends CrudSearch {
  subscriptionPublicId?: string | null; from?: string | null; to?: string | null; bound?: boolean | null;
}

export interface IssueSave {
  subscriptionPublicId: string; publicId?: string | null; label?: string | null; x?: number | null; y?: number | null; z?: number | null; isSpecial?: boolean;
  quantity?: number | null; plannedDate?: string | null; receivedDate?: string | null; status?: number | null; note?: string | null;
}

export interface IssueStatistics { total: number; received: number; claimed: number; missing: number; late: number; lastReceivedDate: string | null; }
export interface PredictResult { generated: number; skipped: number; issues: SerialIssue[]; }

export interface BindingIssue { issuePublicId: string; label: string; receivedDate: string | null; quantity: number; }
export interface Binding {
  id: number; publicId: string; accessionNo: string; volumeTitle: string; subscriptionPublicId: string; subscriptionTitle: string; bindingDate: string; storeId: number | null;
  storeName: string | null; note: string | null; itemPublicId: string | null; issueCount: number; issues: BindingIssue[]; createdAt: string;
}
export interface BindingSearch extends CrudSearch { accessionNo?: string | null; volumeTitle?: string | null; storeId?: number | null; subscriptionPublicId?: string | null; }
export interface BindingSave {
  subscriptionPublicId: string; issueIds: string[]; accessionNo?: string | null; volumeTitle?: string | null; bindingDate?: string | null; storeId?: number | null;
  storeName?: string | null; note?: string | null;
}

export interface SerialReportRequest {
  subscriptionPublicId: string | null; keyword: string | null; from: string | null; to: string | null; pageIndex: number; pageSize: number;
}
export interface ReceivedSummaryRow { subscriptionPublicId: string; title: string; issn: string | null; issueCount: number; quantity: number; lastReceivedDate: string | null; }
export interface ReceivedDetailRow {
  issuePublicId: string; subscriptionPublicId: string; title: string; issn: string | null; label: string; plannedDate: string | null; receivedDate: string | null;
  quantity: number; late: boolean;
}
export interface MissingClaimRow {
  issuePublicId: string; subscriptionPublicId: string; title: string; issn: string | null; label: string; plannedDate: string | null; status: number; statusName: string;
  claimCount: number; claimDate: string | null;
}
export type SerialReportName = 'ReceivedSummary' | 'ReceivedDetail' | 'MissingClaim';

/**
 * API riêng của phân hệ Báo - Tạp chí (service serials qua gateway /api/admin/serials/**). Danh mục dùng Api.crud(resource, 'serials');
 * đây là các thao tác nghiệp vụ. Tách khỏi Api chung để nằm trong gói nạp muộn, không làm phình gói khởi động của admin.
 */
@Injectable({ providedIn: 'root' })
export class SerialsApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin/serials';

  private get<T>(path: string) { return firstValueFrom(this.http.get<T>(`${this.base}${path}`)); }
  private post<T>(path: string, body: unknown = {}) { return firstValueFrom(this.http.post<T>(`${this.base}${path}`, body)); }

  // Đăng ký
  searchSubscriptions(body: SubscriptionSearch) { return this.post<CrudPage<Subscription>>('/subscriptions/Search', body); }
  getSubscription(publicId: string) { return this.get<Subscription>(`/subscriptions/GetById/${publicId}`); }
  addSubscription(body: SubscriptionRequest) { return this.post<Subscription>('/subscriptions/Add', body); }
  updateSubscription(publicId: string, body: SubscriptionRequest) { return firstValueFrom(this.http.put<Subscription>(`${this.base}/subscriptions/Update/${publicId}`, body)); }
  deleteSubscription(publicId: string) { return firstValueFrom(this.http.delete<void>(`${this.base}/subscriptions/Delete/${publicId}`)); }
  approveSubscription(publicId: string) { return this.post<Subscription>(`/subscriptions/Approve/${publicId}`); }
  linkBib(publicId: string, bibPublicId: string) { return this.post<Subscription>(`/subscriptions/LinkBib/${publicId}/${bibPublicId}`); }
  lookupBibs(keyword: string) { return this.post<BibOption[]>('/bibs/Lookup', { keyword }); }
  suppliers() { return this.get<SupplierOption[]>('/suppliers'); }

  // Kỳ
  searchIssues(body: IssueSearch) { return this.post<CrudPage<SerialIssue>>('/issues/Search', body); }
  issueStatistics(subscriptionPublicId: string) { return this.get<IssueStatistics>(`/issues/Statistics/${subscriptionPublicId}`); }
  saveIssue(body: IssueSave) { return this.post<SerialIssue>('/issues/Save', body); }
  receiveIssue(publicId: string, receivedDate?: string | null, quantity?: number | null) { return this.post<SerialIssue>('/issues/Receive', { publicId, receivedDate, quantity }); }
  claimIssue(publicId: string) { return this.post<SerialIssue>('/issues/Claim', { publicId }); }
  predictIssues(subscriptionPublicId: string, count: number) { return this.post<PredictResult>('/issues/Predict', { subscriptionPublicId, count }); }
  mergeIssues(subscriptionPublicId: string, issueIds: string[], label?: string | null) {
    return this.post<SerialIssue>('/issues/Merge', { subscriptionPublicId, issueIds, label: label || null });
  }
  deleteIssue(publicId: string) { return firstValueFrom(this.http.delete<void>(`${this.base}/issues/Delete/${publicId}`)); }

  // Đóng tập
  searchBindings(body: BindingSearch) { return this.post<CrudPage<Binding>>('/bindings/Search', body); }
  getBinding(publicId: string) { return this.get<Binding>(`/bindings/GetById/${publicId}`); }
  addBinding(body: BindingSave) { return this.post<Binding>('/bindings/Add', body); }
  updateBinding(publicId: string, body: BindingSave) { return firstValueFrom(this.http.put<Binding>(`${this.base}/bindings/Update/${publicId}`, body)); }
  deleteBinding(publicId: string) { return firstValueFrom(this.http.delete<void>(`${this.base}/bindings/Delete/${publicId}`)); }

  // Báo cáo
  report<T>(name: SerialReportName, body: SerialReportRequest) { return this.post<CrudPage<T>>(`/reports/${name}`, body); }
  exportReport(name: SerialReportName, body: SerialReportRequest) {
    return blobRequest(firstValueFrom(this.http.post(`${this.base}/reports/${name}Export`, body, { responseType: 'blob' })));
  }
}
