import { BaseEntity } from '../shared/base-entity';

export interface Photo extends BaseEntity {
  name: string;
  tenantName?: string;
  brief?: string;
  types?: string;
  link?: string;
  position?: string;
  photoAlbumId?: string | number;
  status?: number | boolean;
  sortOrder?: number;
  width?: number;
  height?: number;
  image?: string;
}
