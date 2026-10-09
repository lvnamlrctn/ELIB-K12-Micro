import { BaseEntity } from '../shared/base-entity';

export interface SystemParameter extends BaseEntity {
  code?: string;
  Code?: string;
  descriptionVn?: string;
  DescriptionVn?: string;
  descriptionEn?: string;
  DescriptionEn?: string;
  value?: string;
  Value?: string;
  isRemoveHtml?: boolean;
  tenantName?: string;
}
