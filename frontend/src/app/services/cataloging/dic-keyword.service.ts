import { Injectable } from '@angular/core';
import { DisplayNameEntityService } from '../shared/display-name-entity.service';

@Injectable({ providedIn: 'root' })
export class DicKeywordService extends DisplayNameEntityService {
  protected override get baseUrl(): string {
    return '/api/PrintBook/DicKeyword';
  }
}
