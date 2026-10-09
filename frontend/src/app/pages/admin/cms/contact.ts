import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { ContactService } from '../../../services/cms/contact.service';

@Component({
  selector: 'app-cms-contact',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="CMS_CONTACT"></app-base-entity>`
})
export class ContactPage {
  service = inject(ContactService);
}
