import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { ProfService } from '../../../services/circulation/prof.service';

@Component({
  selector: 'app-prof',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="PROF"></app-base-entity>`
})
export class ProfPage {
  service = inject(ProfService);
}
