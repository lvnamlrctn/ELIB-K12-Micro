import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { BaseEntityService } from '../shared/base-entity.service';
import { MenuType } from '../../models/cms/menu-type';

@Injectable({ providedIn: 'root' })
export class MenuTypeService extends BaseEntityService<MenuType> {
  protected override get baseUrl(): string {
    return '/api/Cms/MenuType';
  }

  override create(item: Partial<MenuType>): Observable<MenuType> {
    const payload = {
      name:        item.name        || '',
      code:        item.code        || '',
      description: item.description || '',
      status:      item.status      ?? 2
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.apiBase}/Add`, payload).pipe(
      map(res => res.data || res)
    );
  }

  checkCodeExists(code: string): Observable<boolean> {
    return this.getAll({ draw: 1, start: 0, length: 1000, search: { value: code }, order: [] }).pipe(
      map(res => res.data.some((item: unknown) => {
        const r = item as Record<string, unknown>;
        return ((r['code'] || r['Code']) as string || '').toLowerCase() === code.trim().toLowerCase();
      })),
      catchError(() => of(false))
    );
  }

  override update(item: MenuType): Observable<MenuType> {
    const record = item as unknown as Record<string, unknown>;
    const id = record['publicId'] || item.id;
    const payload = {
      name:        item.name        || '',
      code:        item.code        || '',
      description: item.description || '',
      status:      item.status      ?? 2
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.apiBase}/Update/${id}`, payload).pipe(
      map(res => res.data || res)
    );
  }
}
