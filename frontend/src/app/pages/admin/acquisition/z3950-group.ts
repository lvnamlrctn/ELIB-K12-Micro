import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { Z3950GroupService } from '../../../services/acquisition/z3950-group.service';

@Component({
  selector: 'app-z3950-group',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="Z3950_GROUP"></app-base-entity>`
})
export class Z3950GroupPage {
  service = inject(Z3950GroupService);
}
