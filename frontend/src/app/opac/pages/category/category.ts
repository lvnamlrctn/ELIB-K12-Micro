import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NewsApiService } from '../../services/news-api.service';
import { BookApiService } from '../../services/book-api.service';
import { News } from '../../services/models';

import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-category',
  imports: [RouterLink, TranslateModule],
  templateUrl: './category.html'
})
export class CategoryComponent implements OnInit {
  private newsApi = inject(NewsApiService);
  private bookApi = inject(BookApiService);
  private route = inject(ActivatedRoute);
  
  newsList = signal<News[]>([]);
  totalItems = signal(0);
  
  currentPage = signal(1);
  itemsPerPage = 3;
  currentCategoryId = signal<string>('150');
  categoryTitle = signal<string>('Danh mục');

  totalPages = computed(() => Math.max(1, Math.ceil(this.totalItems() / this.itemsPerPage)));
  paginatedNews = computed(() => this.newsList());
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id') || '150';
      this.currentCategoryId.set(id);
      this.currentPage.set(1);
      
      this.bookApi.getCategoryById(id).subscribe(cat => {
        if (cat) {
          this.categoryTitle.set(cat.name);
        } else {
          this.categoryTitle.set('Tin tức & Sự kiện');
        }
      });
      
      this.fetchData();
    });
  }

  fetchData() {
    this.newsApi.getNewsByCategory(this.currentCategoryId(), this.currentPage(), this.itemsPerPage).subscribe(res => {
      this.newsList.set(res.items);
      this.totalItems.set(res.total);
    });
  }

  goToPage(page: number) {
    if (page !== this.currentPage() && page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
      this.fetchData();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }
}
