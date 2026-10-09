import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: string;
  type: 'success' | 'error' | 'info' | 'warning';
  title?: string;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class ToastrService {
  toasts = signal<Toast[]>([]);

  success(message: string, title?: string) {
    this.addToast('success', message, title);
  }

  error(message: string, title?: string) {
    this.addToast('error', message, title);
  }

  info(message: string, title?: string) {
    this.addToast('info', message, title);
  }

  warning(message: string, title?: string) {
    this.addToast('warning', message, title);
  }

  private addToast(type: 'success' | 'error' | 'info' | 'warning', message: string, title?: string) {
    const id = Math.random().toString(36).substring(2, 9);
    const toast: Toast = { id, type, message, title };
    
    this.toasts.update(current => [...current, toast]);

    setTimeout(() => {
      this.remove(id);
    }, 5000);
  }

  remove(id: string) {
    this.toasts.update(current => current.filter(t => t.id !== id));
  }
}
