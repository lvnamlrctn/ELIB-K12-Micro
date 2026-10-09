import { Injectable, inject } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { TranslateService } from '@ngx-translate/core';

@Injectable()
export class CustomPaginatorIntl extends MatPaginatorIntl {
  private translateService = inject(TranslateService);

  constructor() {
    super();
    this.translateService.onLangChange.subscribe(() => {
      this.getTranslations();
    });
    this.getTranslations();
  }

  getTranslations() {
    this.itemsPerPageLabel = this.translateService.instant('DATATABLE.LENGTH_MENU').replace('_MENU_', '');
    this.nextPageLabel = this.translateService.instant('DATATABLE.PAGINATE.NEXT');
    this.previousPageLabel = this.translateService.instant('DATATABLE.PAGINATE.PREVIOUS');
    this.firstPageLabel = this.translateService.instant('DATATABLE.PAGINATE.FIRST');
    this.lastPageLabel = this.translateService.instant('DATATABLE.PAGINATE.LAST');
    this.changes.next();
  }

  override getRangeLabel = (page: number, pageSize: number, length: number) => {
    if (length === 0 || pageSize === 0) {
      return `0 / ${length}`;
    }
    length = Math.max(length, 0);
    const startIndex = page * pageSize;
    const endIndex = startIndex < length ? Math.min(startIndex + pageSize, length) : startIndex + pageSize;
    
    let info = this.translateService.instant('DATATABLE.INFO');
    info = info.replace('_START_', (startIndex + 1).toString());
    info = info.replace('_END_', endIndex.toString());
    info = info.replace('_TOTAL_', length.toString());
    
    return info;
  };
}
