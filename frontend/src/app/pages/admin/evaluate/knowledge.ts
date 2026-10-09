import { Component, inject } from '@angular/core';
import { BaseEntityComponent } from '../shared/base-entity';
import { KnowledgeService } from '../../../services/evaluate/knowledge.service';

@Component({
  selector: 'app-knowledge',
  standalone: true,
  imports: [BaseEntityComponent],
  template: `<app-base-entity [service]="service" translationKeyPrefix="KNOWLEDGE"></app-base-entity>`
})
export class KnowledgePage {
  service = inject(KnowledgeService);
}
