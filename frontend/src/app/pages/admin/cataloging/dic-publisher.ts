import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { DicPublisherService } from '../../../services/cataloging/dic-publisher.service';

@Component({
  selector: 'app-dic-publisher',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="DIC_PUBLISHER"></app-base-entity>`
})
export class DicPublisherPage {
  service = inject(DicPublisherService);
}
