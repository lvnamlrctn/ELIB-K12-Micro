import { TranslateModule } from '@ngx-translate/core';
import { DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { BookApiService } from '../services/book-api.service';
import { SystemApiService } from '../services/system-api.service';
import { VisitStats } from '../services/models';

@Component({
  imports: [TranslateModule, DecimalPipe],
  selector: 'opac-footer',
  templateUrl: './footer.html'
})
export class FooterComponent implements OnInit {
  private bookApi = inject(BookApiService);
  private systemApi = inject(SystemApiService);

  libraryName = signal('Thư viện Phường Phan Đình Phùng');
  address = signal('Phường Phan Đình Phùng, tỉnh Thái Nguyên');
  phone = signal('02083 532434');
  email = signal('pdp@thainguyen.gov.vn');

  quickLinks = signal<{name: string, url: string}[]>([]);
  visitStats = signal<VisitStats>({ total: 0, today: 0, lastWeek: 0, lastMonth: 0 });

  ngOnInit() {
    this.systemApi.getVisitStats().subscribe(stats => this.visitStats.set(stats));

    this.systemApi.getHyperlinks().subscribe(links => {
      if(links && links.length > 0) {
        this.quickLinks.set(links.map(l => ({ name: l.name, url: l.linkUrl })));
      }
    });

    this.systemApi.getSystemPara('LibraryName').subscribe(res => { if(res) this.libraryName.set(res); });
    this.systemApi.getSystemPara('LIBRARY_ADDR').subscribe(res => { if(res) this.address.set(res); });
    this.systemApi.getSystemPara('LIBRARY_TEL').subscribe(res => { if(res) this.phone.set(res); });
    this.systemApi.getSystemPara('LIBRARY_EMAIL').subscribe(res => { if(res) this.email.set(res); });
  }
}
