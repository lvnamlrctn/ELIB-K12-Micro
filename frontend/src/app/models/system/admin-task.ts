// Đợt 10 — nền tảng tác vụ nền (AdminTask v2). Hiện chỉ có kind "reader-import".

export type AdminTaskState = 'Queued' | 'Running' | 'Paused' | 'Completed' | 'Cancelled' | 'NeedsReview' | 'Failed';

export interface ReaderFieldChange {
  field: string;
  before: string | null;
  after: string | null;
}

export interface ReaderChangePreview {
  cardno: string | null;
  isNew: boolean;
  fields: ReaderFieldChange[];
}

export interface AdminTaskView {
  id: string;
  kind: string;
  state: AdminTaskState;
  preview: boolean;
  totalChunks: number;
  completedChunks: number;
  totalItems: number;
  completedItems: number;
  error: string | null;
  reviewToken: string | null;
  createdAt: string;
  updatedAt: string;
  result: any;
}

// Đợt 13 — giám sát tác vụ của người khác + dọn payload/result cũ.

export interface AdminTaskMonitorItem {
  id: string;
  actorId: number;
  ownerName: string;
  tenantId: number | null;
  kind: string;
  state: AdminTaskState;
  preview: boolean;
  totalChunks: number;
  completedChunks: number;
  totalItems: number;
  completedItems: number;
  createdAt: string;
  updatedAt: string;
  error: string | null;
  needsAttention: boolean;
}

export interface AdminTaskControlEventView {
  id: string;
  operatorId: number;
  operatorName: string;
  action: 'Pause' | 'Resume';
  reason: string;
  stateBefore: string;
  stateAfter: string;
  createdAt: string;
}

export interface AdminTaskMonitorDetail extends AdminTaskMonitorItem {
  events: AdminTaskControlEventView[];
}

export interface AdminTaskMonitorSummary {
  counts: Record<string, number>;
}

export interface RetentionPreview {
  enabled: boolean;
  dryRun: boolean;
  payloadDays: number;
  batchSize: number;
  candidateCount: number;
  estimatedBytes: number;
  checkedAt: string;
}

export interface RetentionRun {
  id: string;
  startedAt: string;
  finishedAt: string;
  dryRun: boolean;
  taskCount: number;
  chunkCount: number;
  estimatedBytes: number;
}
