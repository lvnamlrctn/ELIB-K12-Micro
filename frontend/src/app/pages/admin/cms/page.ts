import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { PageService } from '../../../services/cms/page.service';

@Component({
  selector: 'app-cms-page',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="CMS_PAGE"></app-base-entity>`
})
export class PageCmsPage {
  service = inject(PageService);
}
