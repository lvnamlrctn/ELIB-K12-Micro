import { News, Comment, Book, BookFile, Category, MenuItem, User, Hyperlink, EbookReview } from "./models";
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformServer } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { of, Observable, map, catchError, shareReplay, forkJoin } from 'rxjs';
import { APP_CONFIG } from '../config';
import { buildCollectionTree, flattenCollectionTree, CollectionOption, RawCollection } from '../../shared/collection-tree.util';
@Injectable({ providedIn: 'root' })
export class BookApiService {
  private mockNews: any[] = [];
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);
  
  private backendBase = APP_CONFIG.BackendBase; // Use absolute directly to bypass SSR proxy issues if possible, or relative /api
  private backendDataBase = APP_CONFIG.BackendDataBase;
  
  private menuItemsCache$?: Observable<MenuItem[]>;
  private categoriesCache$?: Observable<Category[]>;
  private ebookCollectionsCache$?: Observable<{ id: string; title: string; count: number }[]>;
  
  private get baseUrl(): string {
    return isPlatformServer(this.platformId) 
      ? `${this.backendBase}/api/public/PublicNews`
      : '/api/public/PublicNews';
  }
  
  private get backendRoot(): string {
    return isPlatformServer(this.platformId)
      ? this.backendBase
      : '';
  }

  private get backenData(): string {
    return isPlatformServer(this.platformId)
      ? this.backendDataBase
      : '/data';
  }

  private mockMenuItems: MenuItem[] = [
    { id: '1', label: 'Trang chủ', url: '/' },
    { 
      id: '2', 
      label: 'Giới thiệu', 
      children: [
        { id: '2-1', label: 'Về thư viện', url: '/category/150' },
        { id: '2-2', label: 'Cơ cấu tổ chức', url: '/category/150' },
        { id: '2-3', label: 'Nội quy thư viện', url: '/category/88' }
      ] 
    },
    { 
      id: '3', 
      label: 'Tin tức', 
      children: [
        { id: '3-1', label: 'Tin hoạt động', url: '/category/150' },
        { id: '3-2', label: 'Thông báo', url: '/category/148' }
      ] 
    },
    { 
      id: '4', 
      label: 'Tài liệu số', 
      children: [
        // Tra cứu gộp: tìm được cả tài liệu in lẫn tài liệu số trong một kết quả.
        { id: '4-0', label: 'Tra cứu tài liệu', url: '/tra-cuu' },
        { id: '4-1', label: 'Tìm kiếm tài liệu số', url: '/search' },
        { 
          id: '4-2', 
          label: 'Bộ sưu tập', 
          children: [
            { id: '4-2-1', label: 'Sách kỹ năng sống', url: '/search' },
            { id: '4-2-2', label: 'Sách Y học', url: '/search' },
            { id: '4-2-3', label: 'Sách Nông nghiệp', url: '/search' },
            { id: '4-2-4', label: 'Tài liệu Đa phương tiện', url: '/search?type=multimedia' }
          ] 
        }
      ] 
    }
  ];

  private mockUsers: User[] = [
    { id: '1', username: 'admin', fullName: 'Quản trị viên', email: 'admin@tankhanh.gov.vn', role: 'admin' },
    { id: '2', username: 'user', fullName: 'Người dùng mẫu', email: 'user@gmail.com', role: 'user' }
  ];

  // Store passwords separately for mock login
  private mockPasswords: Record<string, string> = {
    'admin': 'password123',
    'user': 'user123'
  };

  private mockBooks: Book[] = [
    {
      id: '1',
      title: 'Ăn uống theo nhu cầu dinh dưỡng của bà mẹ và trẻ em Việt Nam',
      author: 'Phạm Văn Hoan; Lê Bạch Mai',
      publisher: 'Y học',
      year: 2010,
      imageUrl: 'https://picsum.photos/seed/book1/300/400',
      description: 'Sách hướng dẫn ăn uống theo nhu cầu dinh dưỡng của bà mẹ và trẻ em Việt Nam.',
      views: 350,
      downloads: 120,
      rating: 4.5,
      ratingCount: 2,
      comments: [
        {
          id: 'c1',
          author: 'Nguyễn Văn Độc Giả',
          avatar: 'https://ui-avatars.com/api/?name=Nguyễn+Văn+Độc+Giả&background=random',
          content: 'Tài liệu rất hữu ích, cảm ơn thư viện đã cung cấp.',
          date: '15/04/2026',
          rating: 5
        },
        {
          id: 'c2',
          author: 'Trần Thị Thu',
          avatar: 'https://ui-avatars.com/api/?name=Trần+Thị+Thu&background=random',
          content: 'Nội dung chi tiết, trình bày rõ ràng dễ hiểu. Tuy nhiên một số hình ảnh hơi mờ.',
          date: '10/04/2026',
          rating: 4
        }
      ],
      files: [
        { id: 'f1', name: 'An-uong-dinh-duong.pdf', size: 67152, type: 'pdf', url: 'https://mozilla.github.io/pdf.js/web/compressed.tracemonkey-pldi-09.pdf' }
      ]
    },
    {
      id: '2',
      title: 'Cẩm nang phòng trị ung thư',
      author: 'Nhiều tác giả',
      publisher: 'Y học',
      year: 2015,
      imageUrl: 'https://picsum.photos/seed/book2/300/400',
      description: 'Cẩm nang phòng trị ung thư cung cấp kiến thức cơ bản về bệnh ung thư.',
      views: 500,
      downloads: 200,
      files: [
        { id: 'f2', name: 'Cam-nang-ung-thu.pdf', size: 102400, type: 'pdf', url: '/files/doc2.pdf' }
      ]
    },
    {
      id: '3',
      title: '365 câu hỏi - đáp về sức khỏe và phòng chữa bệnh',
      author: 'Nguyễn Văn A',
      publisher: 'Y học',
      year: 2018,
      imageUrl: 'https://picsum.photos/seed/book3/300/400',
      description: 'Giải đáp 365 câu hỏi thường gặp về sức khỏe và cách phòng chữa bệnh.',
      views: 420,
      downloads: 150,
      files: [
        { id: 'f3', name: '365-cau-hoi-suc-khoe.pdf', size: 85000, type: 'pdf', url: '/files/doc3.pdf' }
      ]
    },
    {
      id: '4',
      title: '60 lời khuyên chống nhức đầu',
      author: 'Trần Thị B',
      publisher: 'Y học',
      year: 2012,
      imageUrl: 'https://picsum.photos/seed/book4/300/400',
      description: '60 lời khuyên hữu ích giúp phòng chống và điều trị bệnh nhức đầu.',
      views: 280,
      downloads: 90,
      files: [
        { id: 'f4', name: '60-loi-khuyen-nhuc-dau.pdf', size: 45000, type: 'pdf', url: '/files/doc4.pdf' }
      ]
    },
    {
      id: '5',
      title: 'Kỹ năng giao tiếp hiệu quả',
      author: 'Lê Văn C',
      publisher: 'Giáo dục',
      year: 2020,
      imageUrl: 'https://picsum.photos/seed/book5/300/400',
      description: 'Sách hướng dẫn kỹ năng giao tiếp.',
      views: 150,
      downloads: 50,
      files: []
    },
    {
      id: '6',
      title: 'Lịch sử Việt Nam',
      author: 'Trần Trọng Kim',
      publisher: 'Văn học',
      year: 2019,
      imageUrl: 'https://picsum.photos/seed/book6/300/400',
      description: 'Cuốn sách tóm tắt lịch sử Việt Nam.',
      views: 600,
      downloads: 300,
      files: []
    },
    {
      id: '7',
      title: 'Toán học vui',
      author: 'Nguyễn Đình D',
      publisher: 'Khoa học',
      year: 2021,
      imageUrl: 'https://picsum.photos/seed/book7/300/400',
      description: 'Khám phá vẻ đẹp của toán học.',
      views: 200,
      downloads: 80,
      files: []
    },
    {
      id: '8',
      title: 'Vật lý ứng dụng',
      author: 'Phạm Thị E',
      publisher: 'Khoa học',
      year: 2022,
      imageUrl: 'https://picsum.photos/seed/book8/300/400',
      description: 'Ứng dụng vật lý vào đời sống.',
      views: 180,
      downloads: 70,
      files: []
    },
    {
      id: '9',
      title: 'Học tiếng Anh qua bài hát',
      author: 'Oxford University Press',
      publisher: 'Giáo dục',
      year: 2023,
      imageUrl: 'https://picsum.photos/seed/audiobook/300/400',
      description: 'Bộ sưu tập các bài hát tiếng Anh giúp cải thiện kỹ năng nghe và phát âm.',
      views: 450,
      downloads: 180,
      files: [
        { id: 'f9', name: 'Ambient-Rain.mp3', size: 5242880, type: 'audio', url: 'https://actions.google.com/sounds/v1/ambient/rain_heavy_loud.mp3' }
      ]
    },
    {
      id: '10',
      title: 'Kỹ thuật trồng lúa năng suất cao',
      author: 'Viện Cây lương thực',
      publisher: 'Nông nghiệp',
      year: 2024,
      imageUrl: 'https://picsum.photos/seed/videobook/300/400',
      description: 'Video hướng dẫn chi tiết quy trình trồng lúa đạt năng suất cao và phòng trừ sâu bệnh. Bộ sưu tập Sách Nông Nghiệp.',
      views: 600,
      downloads: 250,
      files: [
        { id: 'f10', name: 'Ky-thuat-trong-lua.mp4', size: 25165824, type: 'video', url: 'https://storage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4' }
      ]
    },
    {
      id: '11',
      title: 'Kỹ năng quản lý thời gian đỉnh cao',
      author: 'Brian Tracy',
      publisher: 'Hồng Đức',
      year: 2022,
      imageUrl: 'https://picsum.photos/seed/skill1/300/400',
      description: 'Làm chủ thời gian, làm chủ cuộc đời với các kỹ năng thực tế. Bộ sưu tập Sách kỹ năng sống.',
      views: 850, downloads: 420, files: []
    },
    {
      id: '12',
      title: 'Địa chí Thái Nguyên',
      author: 'Nhiều tác giả',
      publisher: 'Chính trị Quốc gia',
      year: 2018,
      imageUrl: 'https://picsum.photos/seed/tn1/300/400',
      description: 'Tìm hiểu về lịch sử, địa lý và văn hóa tỉnh Thái Nguyên. Bộ sưu tập Sách về tỉnh Thái Nguyên.',
      views: 1200, downloads: 300, files: []
    },
    {
      id: '13',
      title: 'Y học cổ truyền Việt Nam',
      author: 'Đỗ Tất Lợi',
      publisher: 'Y học',
      year: 2015,
      imageUrl: 'https://picsum.photos/seed/med1/300/400',
      description: 'Các bài thuốc và phương pháp chữa bệnh dân gian hiệu quả. Bộ sưu tập Sách Y học.',
      views: 980, downloads: 500, files: []
    },
    {
      id: '14',
      title: 'Kỹ thuật nuôi cá nước ngọt',
      author: 'Nguyễn Văn Hải',
      publisher: 'Nông nghiệp',
      year: 2023,
      imageUrl: 'https://picsum.photos/seed/agri1/300/400',
      description: 'Hướng dẫn kỹ thuật nuôi các loại cá phổ biến tại Việt Nam. Bộ sưu tập Sách Nông Nghiệp.',
      views: 450, downloads: 120, files: []
    },
    {
      id: '15',
      title: 'Vũ trụ trong vỏ hạt dẻ',
      author: 'Stephen Hawking',
      publisher: 'Trẻ',
      year: 2020,
      imageUrl: 'https://picsum.photos/seed/sci1/300/400',
      description: 'Khám phá những bí ẩn của vũ trụ và vật lý lý thuyết. Bộ sưu tập Sách Khoa học tự nhiên.',
      views: 2500, downloads: 800, files: []
    },
    {
      id: '16',
      title: 'Số đỏ',
      author: 'Vũ Trọng Phụng',
      publisher: 'Văn học',
      year: 2017,
      imageUrl: 'https://picsum.photos/seed/lit1/300/400',
      description: 'Tác phẩm văn học hiện thực phê phán kinh điển của Việt Nam. Bộ sưu tập Sách Văn học.',
      views: 3200, downloads: 1500, files: []
    },
    {
      id: '17',
      title: 'Lịch sử thế giới hiện đại',
      author: 'Nhiều tác giả',
      publisher: 'Giáo dục',
      year: 2021,
      imageUrl: 'https://picsum.photos/seed/his1/300/400',
      description: 'Tóm lược các sự kiện lịch sử quan trọng từ thế kỷ 20 đến nay. Bộ sưu tập Sách Lịch sử.',
      views: 670, downloads: 200, files: []
    },
    {
      id: '18',
      title: 'Tiếng Nhật cho người mới bắt đầu',
      author: 'Minna no Nihongo',
      publisher: 'NXB Trẻ',
      year: 2022,
      imageUrl: 'https://picsum.photos/seed/lang1/300/400',
      description: 'Giáo trình cơ bản để bắt đầu học tiếng Nhật. Bộ sưu tập Sách Ngoại ngữ.',
      views: 1500, downloads: 600, files: []
    },
    {
      id: '19',
      title: 'Kỹ năng tư duy phản biện',
      author: 'Zoe McKey',
      publisher: 'Thế giới',
      year: 2021,
      imageUrl: 'https://picsum.photos/seed/skill2/300/400',
      description: 'Rèn luyện khả năng phân tích và đánh giá thông tin. Bộ sưu tập Sách kỹ năng sống.',
      views: 560, downloads: 180, files: []
    },
    {
      id: '20',
      title: 'Thái Nguyên - Tiềm năng và cơ hội đầu tư',
      author: 'Sở Kế hoạch Đầu tư',
      publisher: 'Thái Nguyên',
      year: 2024,
      imageUrl: 'https://picsum.photos/seed/tn2/300/400',
      description: 'Thông tin về kinh tế và các chính sách thu hút đầu tư của tỉnh. Bộ sưu tập Sách về tỉnh Thái Nguyên.',
      views: 400, downloads: 50, files: []
    },
    {
      id: '21',
      title: 'Điều trị bệnh bằng thực phẩm',
      author: 'Thái Hồng',
      publisher: 'Y học',
      year: 2019,
      imageUrl: 'https://picsum.photos/seed/med2/300/400',
      description: 'Chế độ ăn uống hỗ trợ điều trị các bệnh mãn tính. Bộ sưu tập Sách Y học.',
      views: 720, downloads: 250, files: []
    },
    {
      id: '22',
      title: 'Nông nghiệp hữu cơ thực hành',
      author: 'Nguyễn Lân Hùng',
      publisher: 'Nông nghiệp',
      year: 2022,
      imageUrl: 'https://picsum.photos/seed/agri2/300/400',
      description: 'Hướng dẫn canh tác không dùng hóa chất độc hại. Bộ sưu tập Sách Nông Nghiệp.',
      views: 890, downloads: 340, files: []
    },
    {
      id: '23',
      title: 'Nguồn gốc các loài',
      author: 'Charles Darwin',
      publisher: 'Khoa học kỹ thuật',
      year: 2010,
      imageUrl: 'https://picsum.photos/seed/sci2/300/400',
      description: 'Lý thuyết tiến hóa làm thay đổi cách nhìn về thế giới sinh vật. Bộ sưu tập Sách Khoa học tự nhiên.',
      views: 1800, downloads: 400, files: []
    },
    {
      id: '24',
      title: 'Truyện Kiều',
      author: 'Nguyễn Du',
      publisher: 'Văn học',
      year: 2015,
      imageUrl: 'https://picsum.photos/seed/lit2/300/400',
      description: 'Kiệt tác thơ Nôm tiêu biểu cho nền văn học Việt Nam. Bộ sưu tập Sách Văn học.',
      views: 5000, downloads: 2000, files: []
    },
    {
      id: '25',
      title: 'Đại Việt Sử Ký Toàn Thư',
      author: 'Ngô Sĩ Liên',
      publisher: 'Văn hóa thông tin',
      year: 2012,
      imageUrl: 'https://picsum.photos/seed/his2/300/400',
      description: 'Bộ chính sử lớn của Việt Nam thời phong kiến. Bộ sưu tập Sách Lịch sử.',
      views: 2200, downloads: 900, files: []
    },
    {
      id: '26',
      title: 'Giao tiếp tiếng Trung cấp tốc',
      author: 'Nhiều tác giả',
      publisher: 'Đại Học Quốc Gia',
      year: 2023,
      imageUrl: 'https://picsum.photos/seed/lang2/300/400',
      description: 'Học tiếng Trung qua các tình huống thực tế hàng ngày. Bộ sưu tập Sách Ngoại ngữ.',
      views: 600, downloads: 150, files: []
    },
    {
      id: '27',
      title: 'Kỹ năng làm việc nhóm',
      author: 'Phan Văn Trường',
      publisher: 'Trẻ',
      year: 2021,
      imageUrl: 'https://picsum.photos/seed/skill3/300/400',
      description: 'Xây dựng đội nhóm mạnh và làm việc hiệu quả. Bộ sưu tập Sách kỹ năng sống.',
      views: 1100, downloads: 400, files: []
    },
    {
      id: '28',
      title: 'Chè Thái Nguyên - Hương vị quê hương',
      author: 'Hội Nông Dân',
      publisher: 'Thái Nguyên',
      year: 2020,
      imageUrl: 'https://picsum.photos/seed/tn3/300/400',
      description: 'Giới thiệu về các vùng chè đặc sản và cách thưởng trà. Bộ sưu tập Sách về tỉnh Thái Nguyên.',
      views: 800, downloads: 100, files: []
    },
    {
      id: '29',
      title: 'Giải phẫu người kiến thức cơ bản',
      author: 'Trịnh Bình',
      publisher: 'Y học',
      year: 2018,
      imageUrl: 'https://picsum.photos/seed/med3/300/400',
      description: 'Kiến thức cốt lõi về cấu tạo cơ thể người cho sinh viên y khoa. Bộ sưu tập Sách Y học.',
      views: 1300, downloads: 450, files: []
    },
    {
      id: '30',
      title: 'Phân bón và cách sử dụng',
      author: 'Mai Văn Quyền',
      publisher: 'Nông nghiệp',
      year: 2019,
      imageUrl: 'https://picsum.photos/seed/agri3/300/400',
      description: 'Tối ưu hóa năng suất cây trồng thông qua bón phân đúng cách. Bộ sưu tập Sách Nông Nghiệp.',
      views: 500, downloads: 150, files: []
    }
  ];

  private mockCategories: Category[] = [
    { id: '150', name: 'Tin tức' },
    { id: '88', name: 'Tin thư viện' },
    { id: '148', name: 'Thông báo' }
  ];

  private mapNews(item: any): News {
    console.log(item);
    return {
      id: item.publicId || item.id || Math.random().toString(),
      categoryId: item.categoryId?.toString() || '150',
      title: item.title,
      summary: item.brief || item.title || '',
      content: (item.content || item.brief || '').replace(/src=(["'])(\/Upload[^"']+)\1/g, `src=$1${this.backenData}$2$1`),
      imageUrl: item.images ? item.images : (item.imageUrl || 'https://picsum.photos/seed/' + Math.random() + '/400/300'),
      date: item.createdDate ? new Date(item.createdDate).toLocaleDateString('vi-VN') : 'Mới',
      views: item.totalView || 0
    };
  }

  getNews(top = 8): Observable<News[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.backendRoot}/api/public/PublicNews/GetLastedNews?Top=${top}&TenantId=${APP_CONFIG.TenantId}`).pipe(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      map((res: any) => {
        if (res?.success && res.data) {
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          return res.data.map((item: any) => this.mapNews(item));
        }
        return this.mockNews;
      }),
      catchError(err => {
        console.error('getNews API failed, falling back to mock block', err);
        return of(this.mockNews);
      })
    );
  }

  getNewsAll(): Observable<News[]> {
    const body = { TenantId: APP_CONFIG.TenantId };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, body).pipe(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      map((res: any) => {
        if (res?.success && res.data) {
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          return res.data.map((item: any) => this.mapNews(item));
        }
        return this.mockNews;
      }),
      catchError(() => of(this.mockNews))
    );
  }

  searchNews(keyword = '', pageIndex = 1, pageSize = 12): Observable<{items: News[], total: number}> {
    const body = {
      TenantId: APP_CONFIG.TenantId,
      Keyword: keyword,
      PageIndex: pageIndex,
      PageSize: pageSize
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, body).pipe(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      map((res: any) => {
        if (res?.success && res.data) {
          const itemsRaw = Array.isArray(res.data) ? res.data : (res.data.items || res.data.data || []);
          const total = res.data.total || res.total || itemsRaw.length;
          
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          const items = itemsRaw.map((item: any) => this.mapNews(item));
          return { items, total };
        }
        return { items: this.mockNews.slice(0, pageSize), total: this.mockNews.length };
      }),
      catchError(() => of({ items: this.mockNews.slice(0, pageSize), total: this.mockNews.length }))
    );
  }

  getNewsByCategory(categoryId: string, pageIndex = 1, pageSize = 10): Observable<{items: News[], total: number}> {
    const body = {
      keyword: "",
      portalId: "",
      language: "",
      tenantId: APP_CONFIG.TenantId,
      pageIndex: pageIndex,
      pageSize: pageSize,
      categoryId: categoryId,
      types: ""
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.backendRoot}/api/public/PublicNews/Search`, body).pipe(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      map((res: any) => {
        if (res?.success && res.data) {
          const itemsRaw = Array.isArray(res.data) ? res.data : (res.data.items || res.data.data || []);
          const total = res.data.totalCount || res.data.totalItems || res.data.total || res.total || itemsRaw.length;
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          const items = itemsRaw.map((item: any) => this.mapNews(item));
          return { items, total };
        }
        const filtered = this.mockNews.filter(n => n.categoryId === categoryId);
        return { 
          items: filtered.slice((pageIndex - 1) * pageSize, pageIndex * pageSize), 
          total: filtered.length 
        };
      }),
      catchError(() => {
        const filtered = this.mockNews.filter(n => n.categoryId === categoryId);
        return of({ 
          items: filtered.slice((pageIndex - 1) * pageSize, pageIndex * pageSize), 
          total: filtered.length 
        });
      })
    );
  }

  getNewsDetail(id: string): Observable<News | undefined> {
    const url = `${this.backendRoot}/api/public/PublicNews/GetNewsById?newsId=${id}`;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(url).pipe(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      map((res: any) => {
        if (res?.success && res.data) {
          const items = Array.isArray(res.data) ? res.data : [res.data];
          const mappedNews = items.map((item: any) => this.mapNews(item));
          return mappedNews.length > 0 ? mappedNews[0] : undefined;
        }
        return this.mockNews.find(n => n.id === id);
      }),
      catchError(() => of(this.mockNews.find(n => n.id === id)))
    );
  }

  private mapBook(item: any): Book {
    return {
      id: item.publicId || item.id,
      title: item.title,
      author: item.author,
      publisher: item.publisher,
      year: item.publishDate || '',
      imageUrl: item.images ? item.images : 'https://picsum.photos/seed/book/' + Math.random() + '/300/400',
      description: (item.brief || item.content || '').replace(/src=(["'])(\/Upload[^"']+)\1/g, `src=$1${this.backenData}$2$1`),
      views: item.totalView || 0,
      downloads: item.totalDownload || 0,
      free: item.isFree ?? item.IsFree ?? item.free ?? item.Free ?? null, // 2 = miễn phí (backend trả `isFree`)
      allowDownload: item.allowDownload ?? item.AllowDownload ?? null, // 2 = cho tải
      files: item.files ? item.files.map((f: any) => ({
        id: f.id,
        name: f.name,
        size: f.size,
        type: f.extension?.toLowerCase().replace('.', '') || 'pdf',
        url: f.url
      })) : []
    };
  }

  getBooks(): Observable<Book[]> {
    return this.http.get<any>(`${this.backendRoot}/api/public/PublicEbook/GetLastedEbook?Top=8&TenantId=${APP_CONFIG.TenantId}`).pipe(
      map(res => {
        if (res?.success && res.data) {
           const items = Array.isArray(res.data) ? res.data : (res.data.items || res.data.data || []);
           return items.map((item: any) => this.mapBook(item));
        }
        return this.mockBooks;
      }),
      catchError(() => {
        console.warn('getBooks API failed (404), using mock data');
        return of(this.mockBooks);
      })
    );
  }

  searchBooks(query: string, author?: string, year?: string, collection?: string, pageIndex: number = 1, pageSize: number = 8, publisher?: string, keyword?: string, order?: string): Observable<{items: Book[], totalCount: number}> {
    const body: any = {
      keyword: keyword || query || "",
      tenantId: APP_CONFIG.TenantId,
      pageIndex: pageIndex,
      pageSize: pageSize,
      title: query || "",
      author: author || "",
      publishDate: year || "",
      publisher: publisher || ""
    };
    if (collection) {
      body.collectionId = collection;
    }
    if (order) {
      body.order = order;
    }
    return this.http.post<any>(`${this.backendRoot}/api/public/PublicEbook/Search`, body).pipe(
      map(res => {
        if (res?.success && res.data) {
           const itemsRaw = Array.isArray(res.data) ? res.data : (res.data.items || res.data.data || []);
           const items = itemsRaw.map((item: any) => this.mapBook(item));
           const totalCount = res.data.totalCount || items.length;
           return { items, totalCount };
        }
        return { items: [], totalCount: 0 };
      }),
      catchError(() => {
        console.warn('searchBooks API failed, using mock data');
        const filtered = this.mockBooks.filter(b => b.title.toLowerCase().includes((query || '').toLowerCase()));
        const start = (pageIndex - 1) * pageSize;
        return of({ items: filtered.slice(start, start + pageSize), totalCount: filtered.length });
      })
    );
  }

  getEBookCollections(): Observable<{ id: string; title: string; count: number }[]> {
    if (!this.ebookCollectionsCache$) {
      const body = {
        keyword: "",
        portalId: "",
        language: "",
        tenantId: APP_CONFIG.TenantId,
        pageIndex: 0,
        pageSize: 0
      };
      this.ebookCollectionsCache$ = this.http.post<any>(`${this.backendRoot}/api/public/PublicEBookCollection/SearchAll`, body).pipe(
        map(res => {
          if (res?.success && res.data) {
            const itemsRaw = Array.isArray(res.data) ? res.data : (res.data.items || []);
            return itemsRaw.map((item: any) => ({
              id: item.publicId || item.id || '',
              title: item.name,
              count: item.totalItems || 0
            }));
          }
          return [];
        }),
        catchError((err) => {
          console.warn('getEBookCollections API failed, using mock data', err);
          return of([
            { id: '', title: 'Sách kỹ năng sống', count: 6 },
            { id: '', title: 'Sách về tỉnh Thái Nguyên', count: 8 },
            { id: '', title: 'Sách Y học', count: 34 },
            { id: '', title: 'Sách Nông Nghiệp', count: 48 },
            { id: '', title: 'Sách Khoa học tự nhiên', count: 15 },
            { id: '', title: 'Sách Văn học', count: 22 },
            { id: '', title: 'Sách Lịch sử', count: 10 },
            { id: '', title: 'Sách Ngoại ngữ', count: 5 }
          ]);
        }),
        shareReplay(1)
      );
    }
    return this.ebookCollectionsCache$;
  }

  /** Bộ sưu tập theo cây (cha → con, kèm cấp) cho bộ lọc trang Tìm kiếm — Đợt 20. Giữ nguyên
   *  getEBookCollections() (danh sách phẳng) cho trang chủ. */
  private collectionTreeCache$?: Observable<CollectionOption[]>;
  getCollectionTree(): Observable<CollectionOption[]> {
    if (!this.collectionTreeCache$) {
      const body = { keyword: '', portalId: '', language: '', tenantId: APP_CONFIG.TenantId, pageIndex: 0, pageSize: 0 };
      this.collectionTreeCache$ = this.http.post<any>(`${this.backendRoot}/api/public/PublicEBookCollection/SearchAll`, body).pipe(
        map(res => {
          const raw: RawCollection[] = res?.success && res.data ? (Array.isArray(res.data) ? res.data : (res.data.items || [])) : [];
          return flattenCollectionTree(buildCollectionTree(raw));
        }),
        catchError(() => of([] as CollectionOption[])),
        shareReplay(1)
      );
    }
    return this.collectionTreeCache$;
  }

  getFilePdfUrl(id: string): string {
    return `${this.backendRoot}/api/public/PublicEbook/GetFileMinio/${id}`;
  }

  // URL tải/đọc file kèm token (dFlip/iframe không gắn được header Authorization → truyền token qua query).
  getFileUrlWithToken(id: string, token?: string): string {
    const url = this.getFilePdfUrl(id);
    return token ? `${url}?token=${encodeURIComponent(token)}` : url;
  }

  // URL PDF chỉ chứa 1 trang cụ thể — dùng cho trang page-view (link từ chatbot).
  getPageUrl(ebookFileId: string, page: number): string {
    return `${this.backendRoot}/api/public/PublicEbook/GetPageMinio/${ebookFileId}/${page}`;
  }

  // Tải file PDF 1 trang dạng blob — component tự tạo object URL và xử lý lỗi theo status code.
  getPage(ebookFileId: string, page: number): Observable<Blob> {
    return this.http.get(this.getPageUrl(ebookFileId, page), { responseType: 'blob' });
  }

  getBookDetail(id: string, tenantId?: string): Observable<Book | undefined> {
    // tenantId tùy chọn: dùng cho tìm kiếm liên thư viện (bookz3950) — lấy đúng tài liệu theo tenant của thư viện.
    const q = tenantId ? `?tenantId=${tenantId}` : '';
    const detailUrl = `${this.backendRoot}/api/public/PublicEbook/${id}${q}`;
    const filesUrl = `${this.backendRoot}/api/public/PublicEbook/${id}/Files${q}`;
    
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return forkJoin({
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      detail: this.http.get<any>(detailUrl).pipe(catchError(() => of(null))),
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      files: this.http.get<any>(filesUrl).pipe(catchError(() => of(null)))
    }).pipe(
      map(({ detail, files }) => {
        if (detail?.success && detail.data) {
          const book = this.mapBook(detail.data);
          if (files?.success && Array.isArray(files.data)) {
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            book.files = files.data.map((f: any, index: number) => {
               let type: 'pdf' | 'audio' | 'video' | 'docx' | 'pptx' | 'xlsx' = 'pdf';
               const ext = f.fileExt?.toLowerCase() || '';
               if (ext.includes('mp3') || ext.includes('wav')) type = 'audio';
               else if (ext.includes('mp4') || ext.includes('avi')) type = 'video';
               else if (ext.includes('doc')) type = 'docx';
               else if (ext.includes('ppt')) type = 'pptx';
               else if (ext.includes('xls')) type = 'xlsx';
               else type = 'pdf';
               
               return {
                 id: f.publicId || String(Math.random()),
                 name: `${book.title} - ${index + 1}${ext}`,
                 size: f.fileSize || 0,
                 type: type,
                 url: f.url ? `${this.backenData}/${f.url.replace(/^\//, '')}` : ''
               };
            });
          }
          return book;
        }
        return this.mockBooks.find(b => b.id === id);
      }),
      catchError(() => of(this.mockBooks.find(b => b.id === id)))
    );
  }

  postComment(bookId: string, author: string, content: string, rating: number): Observable<Comment> {
    const newComment: Comment = {
      id: Math.random().toString(36).substr(2, 9),
      author,
      avatar: 'https://ui-avatars.com/api/?name=' + encodeURIComponent(author) + '&background=random',
      content,
      date: new Date().toLocaleDateString('vi-VN'),
      rating
    };

    const body = {
      PublicId: bookId,
      Author: author,
      Content: content,
      Rating: rating,
      TenantId: APP_CONFIG.TenantId
    };

    // Try posting to API, but return mock if failed
    return this.http.post<any>(`${this.backendRoot}/api/public/PublicEBook/AddComment`, body).pipe(
      map(() => {
        this.updateMockComment(bookId, newComment, rating);
        return newComment;
      }),
      catchError(() => {
        this.updateMockComment(bookId, newComment, rating);
        return of(newComment);
      })
    );
  }

  private updateMockComment(bookId: string, newComment: Comment, rating: number) {
    const book = this.mockBooks.find(b => b.id === bookId);
    if (book) {
      if (!book.comments) book.comments = [];
      book.comments.unshift(newComment);

      if (!book.ratingCount) book.ratingCount = 0;
      if (!book.rating) book.rating = 0;

      const totalRating = (book.rating * book.ratingCount) + rating;
      book.ratingCount++;
      book.rating = parseFloat((totalRating / book.ratingCount).toFixed(1));
    }
  }

  // ===== API EbookReview (công khai) — itemId = publicId của tài liệu =====
  // Lấy bình luận đã duyệt (status=2) — backend tự lọc.
  getReviews(itemId: string): Observable<EbookReview[]> {
    const body = { itemId, rating: null, keyword: '' };
    return this.http.post<any>(`${this.backendRoot}/api/public/Ebook/EbookReview/SearchAll`, body).pipe(
      map(res => Array.isArray(res?.data) ? res.data as EbookReview[] : []),
      catchError(() => of([] as EbookReview[]))
    );
  }

  // Gửi đánh giá — review mới ở status=1 (chờ admin duyệt), chưa hiển thị công khai.
  addReview(itemId: string, rating: number, displayName: string, content: string, email = ''): Observable<boolean> {
    const body = { itemId, rating, displayName, content, email };
    return this.http.post<any>(`${this.backendRoot}/api/public/Ebook/EbookReview/Add`, body).pipe(
      map(res => res?.success === true),
      catchError(() => of(false))
    );
  }

  getCategories(): Observable<Category[]> {
    if (!this.categoriesCache$) {
      const body = {
        tenantId: APP_CONFIG.TenantId,
        pageIndex: 1,
        pageSize: 100
      };
      this.categoriesCache$ = this.http.post<any>(`${this.backendRoot}/api/public/PublicCategory/SearchAll`, body).pipe(
        map(res => {
          if (res?.success && res.data) {
            const items = Array.isArray(res.data) ? res.data : (res.data.items || []);
            return items.map((c: any) => ({
               id: c.id?.toString() || c.publicId || '',
               name: c.name || c.title || ''
            }));
          }
          return this.mockCategories;
        }),
        catchError(() => of(this.mockCategories)),
        shareReplay(1)
      );
    }
    return this.categoriesCache$;
  }

  getCategoryById(id: string): Observable<Category | undefined> {
    // Falls back to fetch from categories list
    return this.getCategories().pipe(
      map(categories => categories.find(c => c.id === id))
    );
  }

  getMenuItems(): Observable<MenuItem[]> {
    if (!this.menuItemsCache$) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      this.menuItemsCache$ = this.http.get<any>(`${this.backendRoot}/api/public/PublicMenu/BuildCmsMenu?DepartmentCode=${APP_CONFIG.TenantId}`).pipe(
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        map((res: any) => {
          if (res?.success && res.data) {
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            const mapNode = (m: any): MenuItem => ({
               id: m.publicId || m.id || Math.random().toString(),
               label: m.label || m.name || m.title || 'Mục menu',
               url: m.url || '/',
               // eslint-disable-next-line @typescript-eslint/no-explicit-any
               children: m.children ? m.children.map((c: any) => mapNode(c)) : undefined
            });
            return res.data.map(mapNode);
          }
          return this.mockMenuItems;
        }),
        catchError(err => {
          console.error('getMenuItems API failed, falling back to mock block', err);
          return of(this.mockMenuItems);
        }),
        shareReplay(1)
      );
    }
    return this.menuItemsCache$;
  }

  


  


  login(username: string, password: string): Observable<User | null> {
    const body = {
      loginName: username,
      password: password
    };
    return this.http.post<any>(`${this.backendRoot}/api/public/PublicReader/Login`, body).pipe(
      map(res => {
        if (res?.success && res.data) {
          const userObj: User = {
             id: res.data.id || res.data.publicId || Math.random().toString(),
             username: res.data.loginName || username,
             fullName: res.data.fullName || res.data.name || username,
             email: res.data.email || '',
             role: 'user'
          };
          return userObj;
        }
        return null;
      }),
      catchError((err) => {
        console.warn('login API failed, falling back to mock login', err);
        const user = this.mockUsers.find(u => u.username === username && this.mockPasswords[u.username] === password);
        if (user) {
          return of(user);
        }
        return of(null);
      })
    );
  }
}
