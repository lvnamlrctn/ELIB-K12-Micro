import { TranslateModule } from '@ngx-translate/core';
import { Component, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BookApiService } from '../../services/book-api.service';
import { UnifiedSearchService, UnifiedItem } from '../../services/unified-search.service';
import { Book, EbookReview } from '../../services/models';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-book-detail',
  imports: [TranslateModule, RouterLink, FormsModule, DatePipe],
  templateUrl: './book-detail.html'
})
export class BookDetailComponent implements OnInit {
  private bookApi = inject(BookApiService);
  private unifiedSearch = inject(UnifiedSearchService);
  private route = inject(ActivatedRoute);
  public authService = inject(AuthService);
  book = signal<Book | undefined>(undefined);
  // Đợt 22.2 — "Có thể bạn quan tâm": more_like_this theo title/author/keyword của chính tài liệu.
  related = signal<UnifiedItem[]>([]);

  reviews = signal<EbookReview[]>([]);
  avgRating = signal<number>(0);
  ratingCount = signal<number>(0);

  commentAuthor = signal('');
  commentContent = signal('');
  commentEmail = signal('');
  commentRating = signal(5);
  submitting = signal(false);
  pendingNotice = signal(false);
  submitError = signal(false);

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        // tenantId (nếu có) — dùng khi mở từ tìm kiếm liên thư viện (/bookz3950/:id?tenantId=...)
        const tenantId = this.route.snapshot.queryParamMap.get('tenantId') ?? undefined;
        this.loadBook(id, tenantId);
        this.loadReviews(id);
      }
    });

    // Auto-fill author name if logged in
    const user = this.authService.currentUser();
    if (user) {
      this.commentAuthor.set(user.fullName);
    }
  }

  loadBook(id: string, tenantId?: string) {
    this.bookApi.getBookDetail(id, tenantId).subscribe(b => {
      this.book.set(b);
      if (b?.id) this.unifiedSearch.similar('digital', b.id, 6).subscribe(items => this.related.set(items));
    });
  }

  loadReviews(id: string) {
    this.bookApi.getReviews(id).subscribe(list => {
      this.reviews.set(list);
      this.ratingCount.set(list.length);
      if (list.length) {
        const avg = list.reduce((s, r) => s + (r.rating || 0), 0) / list.length;
        this.avgRating.set(parseFloat(avg.toFixed(1)));
      } else {
        this.avgRating.set(0);
      }
    });
  }

  setRating(val: number) {
    this.commentRating.set(val);
  }

  submitComment() {
    const b = this.book();
    if (!b || !this.commentAuthor() || !this.commentContent() || this.submitting()) return;

    this.submitting.set(true);
    this.pendingNotice.set(false);
    this.submitError.set(false);

    this.bookApi.addReview(b.id, this.commentRating(), this.commentAuthor(), this.commentContent(), this.commentEmail())
      .subscribe(ok => {
        this.submitting.set(false);
        if (ok) {
          // Review mới ở status=1 (chờ duyệt) → chưa thêm vào danh sách công khai.
          this.pendingNotice.set(true);
          this.commentContent.set('');
          this.commentEmail.set('');
          this.commentRating.set(5);
        } else {
          this.submitError.set(true);
        }
      });
  }

  shareContent() {
    const b = this.book();
    if (!b) return;
    
    if (navigator.share) {
      navigator.share({
        title: b.title,
        text: `Chia sẻ tài liệu: ${b.title}`,
        url: window.location.href
      }).catch(console.error);
    } else {
      navigator.clipboard.writeText(window.location.href).then(() => {
        // Can add a toast notification here if needed
        console.log('Đã sao chép đường dẫn');
      });
    }
  }
}
