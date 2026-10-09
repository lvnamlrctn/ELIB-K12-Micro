import { BaseEntity } from '../shared/base-entity';

export interface Role extends BaseEntity {
  code?: string;
  Code?: string;
  app?: string;
  App?: string;
  tenantName?: string;
}
