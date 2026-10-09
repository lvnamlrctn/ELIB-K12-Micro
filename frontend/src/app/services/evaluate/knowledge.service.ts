import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { BaseEntityService } from '../shared/base-entity.service';
import { BaseEntity } from '../../models/shared/base-entity';

@Injectable({ providedIn: 'root' })
export class KnowledgeService extends BaseEntityService<BaseEntity> {
  protected override get baseUrl(): string {
    return '/api/Evaluate/Knowledge';
  }

  searchAll(): Observable<BaseEntity[]> {
    return this.http.post<any>(`${this.apiBase}/SearchAll`, {}).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res?.data?.items) return res.data.items;
        if (res?.data && Array.isArray(res.data)) return res.data;
        return [];
      }),
      catchError(() => of([]))
    );
  }
}
