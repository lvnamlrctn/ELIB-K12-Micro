import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { BaseEntityService } from '../shared/base-entity.service';
import { BaseEntity } from '../../models/shared/base-entity';

export interface ChucVu {
  id: number;
  name?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ChucVuService extends BaseEntityService<BaseEntity> {
  protected override get baseUrl(): string {
    return '/api/Dbo/ChucVu';
  }

  getAllForCombobox(): Observable<ChucVu[]> {
    return this.getAll({ draw: 1, start: 0, length: 999, search: { value: '' } }).pipe(
      map(res => res.data.map(d => ({ id: Number(d.id), name: d.name || '' } as ChucVu)))
    );
  }
}
