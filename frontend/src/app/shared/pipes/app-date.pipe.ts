import { Pipe, PipeTransform } from '@angular/core';
import { formatDate } from '@angular/common';

@Pipe({ name: 'appDate', standalone: true })
export class AppDatePipe implements PipeTransform {
  transform(value: string | Date | null | undefined, mode: 'date' | 'datetime' = 'date'): string {
    if (!value) return '—';
    try {
      return formatDate(value, mode === 'datetime' ? 'dd/MM/yyyy HH:mm' : 'dd/MM/yyyy', 'en-US');
    } catch {
      return '—';
    }
  }
}
