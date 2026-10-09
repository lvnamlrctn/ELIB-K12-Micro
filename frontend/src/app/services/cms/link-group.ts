import { Injectable } from '@angular/core';
import { BaseEntityService } from '../shared/base-entity.service';
import { BaseEntity } from '../../models/shared/base-entity';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { LinkGroup } from '../../models/cms/link-group';

@Injectable({
  providedIn: 'root'
})
export class LinkGroupService extends BaseEntityService<LinkGroup> {
  protected override get baseUrl(): string {
    return '/api/Cms/LinkGroup';
  }

  

  override update(item: LinkGroup): Observable<LinkGroup> {
    const itemRecord = item as unknown as Record<string, unknown>;
    const id = itemRecord['publicId'] || item.id || itemRecord['Id'];
    
    const payload = {
      name: item.name,
      portalId: itemRecord['portalId'] || '',
      language: itemRecord['language'] || '',
      status: item.status !== undefined ? (item.status === true || item.status === 2 ? 2 : 1) : undefined
    };
    
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, payload).pipe(
      map(res => res.data || res)
    );
  }
}
