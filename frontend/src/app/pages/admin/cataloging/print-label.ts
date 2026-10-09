import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';

interface SpineLabel { line1: string; line2: string; line3: string; }

@Component({
  selector: 'app-print-label',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule],
  templateUrl: './print-label.html'
})
export class PrintLabelPage {
  manual = signal('');
  labels = signal<SpineLabel[]>([]);

  generate(): void {
    // Mỗi dòng = 1 nhãn; phân tách bằng dấu | thành tối đa 3 dòng (ví dụ DDC | Cutter | Năm)
    const list = this.manual().split(/\n+/).map(s => s.trim()).filter(Boolean).slice(0, 1000).map(s => {
      const parts = s.split('|').map(p => p.trim());
      return { line1: parts[0] || '', line2: parts[1] || '', line3: parts[2] || '' } as SpineLabel;
    });
    this.labels.set(list);
  }
  clear(): void { this.labels.set([]); }
  print(): void { if (typeof window !== 'undefined') window.print(); }
}
