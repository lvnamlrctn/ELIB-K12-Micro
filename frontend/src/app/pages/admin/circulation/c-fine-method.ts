import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { CircFineMethodService } from '../../../services/circulation/c-fine-method.service';

@Component({
  selector: 'app-c-fine-method',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="C_FINE_METHOD"></app-base-entity>`
})
export class CFineMethodPage {
  service = inject(CircFineMethodService);
}
