import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { DicAuthorService } from '../../../services/cataloging/dic-author.service';

@Component({
  selector: 'app-dic-author',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="DIC_AUTHOR"></app-base-entity>`
})
export class DicAuthorPage {
  service = inject(DicAuthorService);
}
