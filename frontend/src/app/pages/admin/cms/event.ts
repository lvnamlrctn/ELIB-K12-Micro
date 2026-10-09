import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { EventService } from '../../../services/cms/event.service';

@Component({
  selector: 'app-cms-event',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="CMS_EVENT"></app-base-entity>`
})
export class EventPage {
  service = inject(EventService);
}
