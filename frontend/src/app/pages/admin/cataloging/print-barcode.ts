import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { BarcodeDirective } from '../../../components/barcode.directive';

@Component({
  selector: 'app-print-barcode',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule, BarcodeDirective],
  templateUrl: './print-barcode.html'
})
export class PrintBarcodePage {
  prefix = signal('');
  fromNo = signal<number | null>(null);
  toNo = signal<number | null>(null);
  pad = signal(6);
  manual = signal('');
  barcodes = signal<string[]>([]);

  generateRange(): void {
    const from = this.fromNo(); const to = this.toNo();
    if (from == null || to == null || to < from) return;
    const list: string[] = [];
    for (let i = from; i <= to && list.length < 1000; i++) {
      list.push(`${this.prefix()}${String(i).padStart(this.pad(), '0')}`);
    }
    this.barcodes.set(list);
  }

  generateManual(): void {
    const list = this.manual().split(/[\n,;]+/).map(s => s.trim()).filter(Boolean).slice(0, 1000);
    this.barcodes.set(list);
  }

  clear(): void { this.barcodes.set([]); }
  print(): void { if (typeof window !== 'undefined') window.print(); }
}
