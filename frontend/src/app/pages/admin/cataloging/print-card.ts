import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { PrintQueueService } from '../../../services/cataloging/print-queue.service';
import { Bib, MarcField } from '../../../models/cataloging/bib';

interface CatalogCard {
  bibId?:      number;
  classSymbol: string;
  title:       string;
  author:      string;
  publisher:   string;
}

function subfield(fields: MarcField[] | undefined, tag: string, code: string): string {
  const field = fields?.find(f => f.tag === tag);
  return field?.subFields?.find(s => s.code === code)?.value || '';
}

@Component({
  selector: 'app-print-card',
  standalone: true,
  imports: [CommonModule, TranslateModule, MatIconModule],
  templateUrl: './print-card.html'
})
export class PrintCardPage {
  printQueue = inject(PrintQueueService);

  toCard(bib: Bib): CatalogCard {
    const f = bib.fields;
    const classSymbol = subfield(f, '082', 'a') || subfield(f, '090', 'a');
    const title = subfield(f, '245', 'a') || bib.title || '';
    const author = subfield(f, '100', 'a') || bib.author || '';
    const place = subfield(f, '260', 'a');
    const publisher = subfield(f, '260', 'b') || bib.publisher || '';
    const year = subfield(f, '260', 'c') || bib.publishDate || '';
    const publisherLine = [place, publisher, year].filter(Boolean).join(' : ').replace(' : ', ' : ').trim();
    return { bibId: bib.bibId, classSymbol, title, author, publisher: publisherLine || [publisher, year].filter(Boolean).join(', ') };
  }

  remove(bibId: number | undefined): void { if (bibId != null) this.printQueue.remove(bibId); }
  clear(): void { this.printQueue.clear(); }
  print(): void { if (typeof window !== 'undefined') window.print(); }
}
