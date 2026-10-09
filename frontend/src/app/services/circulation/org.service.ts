import { Injectable } from '@angular/core';
import { BaseEntityService } from '../shared/base-entity.service';
import { BaseEntity } from '../../models/shared/base-entity';

@Injectable({ providedIn: 'root' })
export class OrgService extends BaseEntityService<BaseEntity> {
  protected override get baseUrl(): string {
    return '/api/Dbo/Org';
  }
}
