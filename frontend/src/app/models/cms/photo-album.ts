import { BaseEntity } from '../shared/base-entity';

export interface PhotoAlbum extends BaseEntity {
  Name?: string;
  tenantName?: string;
  code?: string;
  Code?: string;
  description?: string;
  Description?: string;
  sortOrder?: number;
  SortOrder?: number;
  status?: number | boolean;
  Status?: number | boolean;
  isSpecial?: number | boolean;
  IsSpecial?: number | boolean;
  image?: string;
  Image?: string;
}
