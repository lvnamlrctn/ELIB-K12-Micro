import { Injectable } from '@angular/core';

import { map, catchError, switchMap } from 'rxjs/operators';
import { BaseEntityService } from '../shared/base-entity.service';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';
import { BaseEntity } from '../../models/shared/base-entity';

import { Observable, of } from 'rxjs';
import { Link } from '../../models/cms/link';

@Injectable({
  providedIn: 'root'
})
export class LinkService extends BaseEntityService<Link> {
  protected override get baseUrl(): string {
    return '/api/Cms/Link';
  }
override getAll(params: DataTableParams): Observable<DataTableResponse<Link>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      portalId: '',
      language: '',
      linkGroupId: params['linkGroupId'],
      pageIndex: pageIndex,
      pageSize: params.length || 10,
      tenantId: params['tenantId'] ?? null
    };

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: Link[] = [];
        if (Array.isArray(res)) {
          data = res;
        } else if (res?.data && Array.isArray(res.data)) {
          data = res.data;
        } else if (res?.data?.items && Array.isArray(res.data.items)) {
          data = res.data.items;
        } else if (res && Array.isArray(res.items)) {
           data = res.items;
        }

        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;

        return {
          draw: params.draw,
          data: data,
          recordsTotal: total,
          recordsFiltered: total
        };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }
  override create(item: Partial<Link>, file?: File): Observable<Link> {
    const itemRecord = item as Record<string, unknown>;

    const formData = new FormData();
    formData.append('name', item.name || '');
    if (itemRecord['portalId']) formData.append('portalId', itemRecord['portalId'] as string);
    if (itemRecord['language']) formData.append('language', itemRecord['language'] as string);
    
    formData.append('linkUrl', item.linkUrl || item.link || '');
    formData.append('description', item.description || '');
    
    const statusVal = item.status !== undefined ? (item.status === true || item.status === 2 ? 2 : 1) : 1;
    formData.append('status', String(statusVal));
    
    if (item.linkGroupId) formData.append('linkGroupId', String(item.linkGroupId));
    
    if (file) {
      formData.append('imageFile', file);
    }

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Add`, formData).pipe(
      map(res => res.data || res)
    );
  }

  override update(item: Link, file?: File): Observable<Link> {
    const itemRecord = item as unknown as Record<string, unknown>;
    const id = itemRecord['publicId'] || item.id || itemRecord['Id'];
    
    return this.getById(String(id)).pipe(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      switchMap((existingRecord: any) => {
        const formData = new FormData();
        formData.append('id', String(id));
        formData.append('name', item.name || '');
        if (itemRecord['portalId']) formData.append('portalId', itemRecord['portalId'] as string);
        if (itemRecord['language']) formData.append('language', itemRecord['language'] as string);
        
        formData.append('linkUrl', item.linkUrl || item.link || '');
        formData.append('description', item.description || '');
        
        const statusVal = item.status !== undefined ? (item.status === true || item.status === 2 ? 2 : 1) : 1;
        formData.append('status', String(statusVal));
        
        if (item.linkGroupId) formData.append('linkGroupId', String(item.linkGroupId));
        
        if (file) {
          formData.append('imageFile', file);
        } else {
          const existingImages = existingRecord.images || existingRecord.Images;
          if (existingImages) {
            formData.append('images', existingImages);
          }
        }

        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return this.http.put<any>(`${this.baseUrl}/Update/${id}`, formData).pipe(
          map(res => res.data || res)
        );
      })
    );
  }
}
