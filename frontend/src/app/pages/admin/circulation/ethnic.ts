import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { EthnicService } from '../../../services/circulation/ethnic.service';

@Component({
  selector: 'app-ethnic',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="ETHNIC"></app-base-entity>`
})
export class EthnicPage {
  service = inject(EthnicService);
}
