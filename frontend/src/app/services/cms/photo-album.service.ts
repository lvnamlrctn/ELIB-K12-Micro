import { Injectable } from '@angular/core';

import { map, catchError, switchMap } from 'rxjs/operators';
import { BaseEntityService } from '../shared/base-entity.service';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';
import { BaseEntity } from '../../models/shared/base-entity';

import { Observable, of } from 'rxjs';
import { PhotoAlbum } from '../../models/cms/photo-album';

@Injectable({
  providedIn: 'root'
})
export class PhotoAlbumService extends BaseEntityService<PhotoAlbum> {
  protected override get baseUrl(): string {
    return '/api/Cms/PhotoAlbum';
  }

  override getAll(params: DataTableParams): Observable<DataTableResponse<PhotoAlbum>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      portalId: '',
      language: '',
      pageIndex: pageIndex,
      pageSize: params.length || 10,
      tenantId: params['tenantId'] ?? null
    };

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: PhotoAlbum[] = [];
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

  override create(item: Partial<PhotoAlbum>, file?: File): Observable<PhotoAlbum> {
    const formData = new FormData();
    formData.append('name', item.name || '');
    formData.append('code', item.code || '');
    formData.append('description', item.description || '');
    
    const sortOrder = item.sortOrder ?? 0;
    formData.append('sortOrder', String(sortOrder));

    const statusVal = item.status !== undefined ? (item.status === true || item.status === 2 ? 2 : 1) : 1;
    formData.append('status', String(statusVal));

    const isSpecialVal = item.isSpecial === true || String(item.isSpecial) === 'true';
    formData.append('isSpecial', String(isSpecialVal));
    
    if (file) {
      formData.append('imageFile', file);
    }

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Add`, formData).pipe(
      map(res => res.data || res)
    );
  }

  override update(item: PhotoAlbum, file?: File): Observable<PhotoAlbum> {
    const itemRecord = item as unknown as Record<string, unknown>;
    const id = itemRecord['publicId'] || item.id || itemRecord['Id'];
    
    return this.getById(String(id)).pipe(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      switchMap((existingRecord: any) => {
        const formData = new FormData();
        formData.append('id', String(id));
        formData.append('name', item.name || '');
        formData.append('code', item.code || '');
        formData.append('description', item.description || '');
        
        const sortOrder = item.sortOrder ?? 0;
        formData.append('sortOrder', String(sortOrder));

        const statusVal = item.status !== undefined ? (item.status === true || item.status === 2 ? 2 : 1) : 1;
        formData.append('status', String(statusVal));
        
        const isSpecialVal = item.isSpecial === true || String(item.isSpecial) === 'true';
        formData.append('isSpecial', String(isSpecialVal));
        
        if (file) {
          formData.append('imageFile', file);
        } else {
          const existingImage = existingRecord.image || existingRecord.Image;
          if (existingImage) {
            formData.append('image', existingImage);
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
