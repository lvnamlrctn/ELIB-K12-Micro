import { Injectable } from '@angular/core';
import { MarcField } from './api';

/** Bản ghi MARC chờ nạp vào màn biên mục mới (sao từ Z39.50…) — lấy một lần. */
export interface MarcDraft { leader: string; fields: MarcField[]; source: string; }

@Injectable({ providedIn: 'root' })
export class MarcDrafts {
  private draft: MarcDraft | null = null;

  set(draft: MarcDraft): void {
    this.draft = draft;
  }

  take(): MarcDraft | null {
    const draft = this.draft;
    this.draft = null;
    return draft;
  }
}
