import { TranslateModule } from '@ngx-translate/core';
import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { RouterLink, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { BookApiService } from '../../services/book-api.service';
import { NewsApiService } from '../../services/news-api.service';
import { SystemApiService } from '../../services/system-api.service';
import { News, Book, Hyperlink } from '../../services/models';

@Component({
  selector: 'app-home',
  imports: [TranslateModule, RouterLink, FormsModule],
  templateUrl: './home.html'
})
export class HomeComponent implements OnInit {
  private bookApi = inject(BookApiService);
  private newsApi = inject(NewsApiService);
  private systemApi = inject(SystemApiService);
  private router = inject(Router);
  
  searchType = 'Title';
  searchQueryInput = '';

  latestBooks = signal<Book[]>([]);
  latestNews = signal<News[]>([]);
  collections = signal<{ id: string; title: string; count: number }[]>([]);
  hyperLinks = signal<(Hyperlink & { color: string, icon: string })[]>([]);

  // Pagination states
  itemsPerPage = 4;
  colItemsPerPage = 4;

  bookIndex = signal(0);
  newsIndex = signal(0);
  colIndex = signal(0);
  showNews = signal(false); // Tạm ẩn khối "Tin tức & Thông báo" trên trang chủ
  // Ảnh nền hero: lấy banner đầu tiên từ API public, fallback ảnh tĩnh.
  bannerUrl = signal<string>('assets/images/banner.png');
  heroBgStyle = computed(() =>
    `linear-gradient(rgba(0,0,0,0.6), rgba(0,0,0,0.6)), url('${this.bannerUrl()}')`
  );
  libraryName = signal('Thư viện Phường Phan Đình Phùng');
  address = signal('Phường Phan Đình Phùng, tỉnh Thái Nguyên');
  phone = signal('02083 532434');
  email = signal('pdp@thainguyen.gov.vn');
  openHour = signal('Thứ 2 - Thứ 6: 07:30 - 21:00\nThứ 7: 08:00 - 17:00');
  // Computed visible items
  visibleBooks = computed(() => this.latestBooks().slice(this.bookIndex(), this.bookIndex() + this.itemsPerPage));
  visibleNews = computed(() => this.latestNews().slice(this.newsIndex(), this.newsIndex() + this.itemsPerPage));
  visibleCollections = computed(() => this.collections().slice(this.colIndex(), this.colIndex() + this.colItemsPerPage));

  ngOnInit() {
    this.systemApi.trackVisit().subscribe();
    this.systemApi.getBanners().subscribe(banners => {
      const first = banners.find(b => !!b.url);
      if (first?.url) this.bannerUrl.set(first.url);
    });
    this.bookApi.getBooks().subscribe(books => this.latestBooks.set(books));
    this.newsApi.getNews(8).subscribe(news => this.latestNews.set(news));
    this.bookApi.getEBookCollections().subscribe(cols => {
      this.collections.set(cols);
    });
    this.systemApi.getSystemPara('LibraryName').subscribe(res => { if(res) this.libraryName.set(res); });
    this.systemApi.getSystemPara('LIBRARY_ADDR').subscribe(res => { if(res) this.address.set(res); });
    this.systemApi.getSystemPara('LIBRARY_TEL').subscribe(res => { if(res) this.phone.set(res); });
    this.systemApi.getSystemPara('LIBRARY_EMAIL').subscribe(res => { if(res) this.email.set(res); });
    this.systemApi.getSystemPara('OpenHour').subscribe(res => { if(res) this.openHour.set(res); });
    
    this.systemApi.getHyperlinks().subscribe(links => {
      const colors = ['#3b5b8c', '#419b45', '#e46c0a', '#b31b1b', '#6b4c9a', '#009688'];
      const icons = ['fa-university', 'fa-microchip', 'fa-database', 'fa-globe', 'fa-book', 'fa-laptop'];
      this.hyperLinks.set(links.slice(0, 3).map((l, i) => ({
        ...l,
        color: colors[i % colors.length],
        icon: icons[i % icons.length]
      })));
    });
  }

  // Trỏ sang trang tra cứu GỘP (/tra-cuu) — tìm được cả tài liệu in lẫn tài liệu số,
  // là tập cha của trang /search cũ (vốn chỉ tra tài liệu số qua SQL).
  performSearch() {
    const queryParams: Record<string, string> = {};
    const term = this.searchQueryInput.trim();
    if (term) {
      if (this.searchType === 'Author') queryParams['author'] = term;
      else                              queryParams['title']  = term;
    }
    this.router.navigate(['/tra-cuu'], { queryParams });
  }

  // Navigation methods
  nextBooks() {
    if (this.bookIndex() + this.itemsPerPage < this.latestBooks().length) {
      this.bookIndex.update(i => i + this.itemsPerPage);
    }
  }
  prevBooks() {
    if (this.bookIndex() - this.itemsPerPage >= 0) {
      this.bookIndex.update(i => i - this.itemsPerPage);
    }
  }

  nextNews() {
    if (this.newsIndex() + this.itemsPerPage < this.latestNews().length) {
      this.newsIndex.update(i => i + this.itemsPerPage);
    }
  }
  prevNews() {
    if (this.newsIndex() - this.itemsPerPage >= 0) {
      this.newsIndex.update(i => i - this.itemsPerPage);
    }
  }

  nextCollections() {
    if (this.colIndex() + this.colItemsPerPage < this.collections().length) {
      this.colIndex.update(i => i + this.colItemsPerPage);
    }
  }
  prevCollections() {
    if (this.colIndex() - this.colItemsPerPage >= 0) {
      this.colIndex.update(i => i - this.colItemsPerPage);
    }
  }
}
