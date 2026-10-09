import { BaseEntity } from '../shared/base-entity';

export interface Banner extends BaseEntity {
  name: string;
  tenantName?: string;
  link?: string;
  url?: string;
  status?: number | boolean;
  sortOrder?: number;
  width?: number;
  height?: number;
}
