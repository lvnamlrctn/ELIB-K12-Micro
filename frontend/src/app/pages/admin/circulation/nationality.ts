import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { NationalityService } from '../../../services/circulation/nationality.service';

@Component({
  selector: 'app-nationality',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="NATIONALITY"></app-base-entity>`
})
export class NationalityPage {
  service = inject(NationalityService);
}
