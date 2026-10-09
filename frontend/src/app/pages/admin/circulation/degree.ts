import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { DegreeService } from '../../../services/circulation/degree.service';

@Component({
  selector: 'app-degree',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="DEGREE"></app-base-entity>`
})
export class DegreePage {
  service = inject(DegreeService);
}
