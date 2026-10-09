import { Directive, ElementRef, Input, OnChanges, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

@Directive({
  selector: '[appBarcode]',
  standalone: true,
})
export class BarcodeDirective implements OnChanges {
  @Input('appBarcode') value = '';
  private el = inject(ElementRef);
  private platformId = inject(PLATFORM_ID);

  ngOnChanges(): void {
    if (!isPlatformBrowser(this.platformId) || !this.value?.trim()) return;
    import('jsbarcode').then(mod => {
      const JsBarcode = (mod as any).default ?? mod;
      try {
        JsBarcode(this.el.nativeElement, this.value, {
          format: 'CODE128',
          displayValue: false,
          margin: 0,
          width: 1.5,
          height: 44,
          background: '#ffffff',
          lineColor: '#1a1a1a',
        });
      } catch {
        // skip invalid barcode values
      }
    });
  }
}
