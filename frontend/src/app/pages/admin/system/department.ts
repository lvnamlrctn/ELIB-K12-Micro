import { Component, inject } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { BaseEntityComponent } from '../shared/base-entity';
import { DepartmentService } from '../../../services/system/department.service';
import { CanDirective } from '../../../directives/can.directive';

@Component({
  selector: 'app-department',
  standalone: true,
  imports: [CanDirective, BaseEntityComponent, NgSelectModule],
  template: `<app-base-entity [service]="service" translationKeyPrefix="DEPARTMENT"></app-base-entity>`
})
export class DepartmentPage {
  service = inject(DepartmentService);
}
