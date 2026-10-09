import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { VideoService } from '../../../services/cms/video.service';

@Component({
  selector: 'app-cms-video',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="CMS_VIDEO"></app-base-entity>`
})
export class VideoPage {
  service = inject(VideoService);
}
