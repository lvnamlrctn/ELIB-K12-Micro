export interface News {
  id: string;
  categoryId: string;
  title: string;
  summary: string;
  content: string;
  imageUrl: string;
  date: string;
  views: number;
}

export interface Comment {
  id: string;
  author: string;
  avatar: string;
  content: string;
  date: string;
  rating?: number;
}

export interface Book {
  id: string;
  title: string;
  author: string;
  publisher: string;
  year: number;
  imageUrl: string;
  description: string;
  views: number;
  downloads: number;
  files: BookFile[];
  rating?: number;
  ratingCount?: number;
  comments?: Comment[];
}

export interface BookFile {
  id: string;
  name: string;
  size: number;
  type: 'pdf' | 'audio' | 'video' | 'docx' | 'pptx' | 'xlsx';
  url: string;
}

export interface Category {
  id: string;
  name: string;
}

export interface MenuItem {
  id: string;
  label: string;
  url?: string;
  children?: MenuItem[];
}

export interface User {
  id: string;
  username: string;
  fullName: string;
  email: string;
  role: 'admin' | 'user';
}

export interface Hyperlink {
  id: string;
  name: string;
  url: string;
  imageUrl?: string;
}
