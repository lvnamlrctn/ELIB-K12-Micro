import { Injectable } from '@angular/core';
import { BaseEntityService } from '../shared/base-entity.service';
import { BaseEntity } from '../../models/shared/base-entity';

// Đặt tên khác `services/printbook/cfine-method.service.ts` (CFineMethodService, dùng nội bộ cho
// circ-place.ts/fine-ticket-*.ts) — cùng entity PrintBook.CFineMethod nhưng đây là màn CRUD đầy đủ
// theo khuôn BaseEntityService, tránh trùng tên class dễ nhầm lẫn khi import.
@Injectable({ providedIn: 'root' })
export class CircFineMethodService extends BaseEntityService<BaseEntity> {
  protected override get baseUrl(): string {
    return '/api/PrintBook/CFineMethod';
  }
}
