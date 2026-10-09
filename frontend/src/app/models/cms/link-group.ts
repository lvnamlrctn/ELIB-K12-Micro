import { BaseEntity } from '../shared/base-entity';

export interface LinkGroup extends BaseEntity {
  tenantName?: string;
  status?: number | boolean;
  Status?: number | boolean;
}
