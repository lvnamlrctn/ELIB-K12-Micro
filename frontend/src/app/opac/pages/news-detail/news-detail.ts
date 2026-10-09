import { TranslateModule } from '@ngx-translate/core';
import { Component, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NewsApiService, NewsAttachment } from '../../services/news-api.service';
import { News } from '../../services/models';
import { AuthService } from '../../services/auth.service';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-news-detail',
  imports: [TranslateModule, RouterLink, FormsModule],
  templateUrl: './news-detail.html'
})
export class NewsDetailComponent implements OnInit {
  private newsApi = inject(NewsApiService);
  private route = inject(ActivatedRoute);
  public authService = inject(AuthService);
  news = signal<News | undefined>(undefined);
  attachments = signal<NewsAttachment[]>([]);

  firebaseComments = signal<any[]>([]);
  avgRating = signal<number>(0);
  ratingCount = signal<number>(0);

  commentAuthor = signal('');
  commentContent = signal('');
  commentRating = signal(5);

  formatSize(kb: number | null): string {
    if (kb == null) return '';
    return kb >= 1024 ? `${(kb / 1024).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} MB` : `${Math.ceil(kb).toLocaleString('vi-VN')} KB`;
  }

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.newsApi.getNewsById(id).subscribe(n => {
          this.news.set(n);
        });
        this.newsApi.getAttachments(id).subscribe(files => this.attachments.set(files));
      }
    });

    const user = this.authService.currentUser();
    if (user) {
      this.commentAuthor.set(user.fullName);
    }
  }

  setRating(val: number) {
    this.commentRating.set(val);
  }

  submitComment() {
    const n = this.news();
    if (!n || !this.commentAuthor() || !this.commentContent()) return;

    // Optimistically show the new comment locally
    this.firebaseComments.update(list => [
      {
        id: Math.random().toString(36).slice(2),
        userName: this.commentAuthor(),
        comment: this.commentContent(),
        rating: this.commentRating(),
        date: new Date().toLocaleDateString('vi-VN')
      },
      ...list
    ]);

    this.commentContent.set('');
    this.commentRating.set(5);
  }

  shareContent() {
    const n = this.news();
    if (!n) return;
    
    if (navigator.share) {
      navigator.share({
        title: n.title,
        text: `Đọc tin tức: ${n.title}`,
        url: window.location.href
      }).catch(console.error);
    } else {
      navigator.clipboard.writeText(window.location.href).then(() => {
        console.log('Đã sao chép đường dẫn');
      });
    }
  }

  printContent() {
    window.print();
  }
}
