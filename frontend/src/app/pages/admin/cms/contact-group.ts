import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { ContactGroupService } from '../../../services/cms/contact-group.service';

@Component({
  selector: 'app-cms-contact-group',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="CMS_CONTACT_GROUP"></app-base-entity>`
})
export class ContactGroupPage {
  service = inject(ContactGroupService);
}
