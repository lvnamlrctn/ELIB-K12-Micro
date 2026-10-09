import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { AcademicClassService } from '../../../services/circulation/academic-class.service';

@Component({
  selector: 'app-academic-class',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="ACADEMIC_CLASS"></app-base-entity>`
})
export class AcademicClassPage {
  service = inject(AcademicClassService);
}
