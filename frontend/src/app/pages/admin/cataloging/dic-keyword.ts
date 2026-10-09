import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { DicKeywordService } from '../../../services/cataloging/dic-keyword.service';

@Component({
  selector: 'app-dic-keyword',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="DIC_KEYWORD"></app-base-entity>`
})
export class DicKeywordPage {
  service = inject(DicKeywordService);
}
