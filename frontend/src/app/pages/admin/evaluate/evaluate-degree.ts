import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { EvaluateDegreeService } from '../../../services/evaluate/evaluate-degree.service';

@Component({
  selector: 'app-evaluate-degree',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="EVALUATE_DEGREE"></app-base-entity>`
})
export class EvaluateDegreePage {
  service = inject(EvaluateDegreeService);
}
