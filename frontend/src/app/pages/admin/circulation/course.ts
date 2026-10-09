import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { CourseService } from '../../../services/circulation/course.service';

@Component({
  selector: 'app-course',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="COURSE"></app-base-entity>`
})
export class CoursePage {
  service = inject(CourseService);
}
