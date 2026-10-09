import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { BaseEntityService } from './base-entity.service';
import { BaseEntity } from '../../models/shared/base-entity';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';

export abstract class DisplayNameEntityService extends BaseEntityService<BaseEntity> {
  override getById(id: string): Observable<BaseEntity> {
    return super.getById(id).pipe(
      map(item => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const record = item as any;
        return { ...record, name: record.displayName || record.DisplayName || record.name };
      })
    );
  }

  override getAll(params: DataTableParams): Observable<DataTableResponse<BaseEntity>> {
    return super.getAll(params).pipe(
      map(res => {
        // Map displayName -> name for UI to show correctly in BaseEntityComponent
        res.data = res.data.map(item => {
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          const record = item as any;
          return { ...record, name: record.displayName || record.DisplayName || record.name };
        });
        return res;
      })
    );
  }

  override create(item: Partial<BaseEntity>): Observable<BaseEntity> {
    const itemRecord = item as unknown as Record<string, unknown>;
    const payload = {
      displayName: item.name,
      portalId: itemRecord['portalId'] || '',
      language: itemRecord['language'] || ''
    };

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Add`, payload).pipe(
      map(res => res.data || res)
    );
  }

  override update(item: BaseEntity): Observable<BaseEntity> {
    const itemRecord = item as unknown as Record<string, unknown>;
    const id = itemRecord['publicId'] || item.id || itemRecord['Id'];
    
    const payload = {
      Id: id,
      id: id,
      displayName: item.name,
      portalId: itemRecord['portalId'] || '',
      language: itemRecord['language'] || ''
    };
    
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, payload).pipe(
      map(res => res.data || res)
    );
  }
}
