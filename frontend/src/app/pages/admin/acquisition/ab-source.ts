import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { AbSourceService } from '../../../services/acquisition/ab-source.service';

@Component({
  selector: 'app-ab-source',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="AB_SOURCE"></app-base-entity>`
})
export class AbSourcePage {
  service = inject(AbSourceService);
}
