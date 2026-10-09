import { BaseEntity } from '../shared/base-entity';

export interface Link extends BaseEntity {
  tenantName?: string;
  linkUrl?: string;
  LinkUrl?: string;
  link?: string;
  Link?: string;
  description?: string;
  Description?: string;
  status?: number | boolean;
  Status?: number | boolean;
  images?: string;
  Images?: string;
  linkGroupId?: string | number;
  LinkGroupId?: string | number;
}
