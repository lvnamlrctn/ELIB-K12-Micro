import { Component, ElementRef, EventEmitter, Input, OnInit, Output, QueryList, ViewChildren, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * Ô nhập OTP dạng các ô vuông tách biệt (1 ký tự số/ô), tự nhảy focus khi gõ/xoá,
 * hỗ trợ dán (paste) cả mã cùng lúc. Hỗ trợ cả formControlName/ngModel (qua
 * ControlValueAccessor) lẫn [(value)] thuần cho nơi bind trực tiếp vào signal (giống
 * quy ước ở app-date-input).
 */
@Component({
  selector: 'app-otp-input',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './otp-input.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => OtpInput),
      multi: true,
    },
  ],
})
export class OtpInput implements ControlValueAccessor, OnInit {
  @Input() length = 6;
  @Input() disabled = false;
  @Input() autoFocus = false;
  @Input() boxClass = '';

  @Input()
  get value(): string {
    return this.digits.join('');
  }
  set value(v: string | null) {
    this.setDigitsFromString(v ?? '');
  }
  @Output() valueChange = new EventEmitter<string>();
  @Output() enter = new EventEmitter<void>();

  @ViewChildren('box') private boxes!: QueryList<ElementRef<HTMLInputElement>>;

  digits: string[] = [];

  private onChange: (value: string) => void = () => {};
  onTouched: () => void = () => {};

  ngOnInit(): void {
    if (this.digits.length !== this.length) this.digits = Array(this.length).fill('');
    if (this.autoFocus) queueMicrotask(() => this.focusBox(0));
  }

  private setDigitsFromString(v: string): void {
    const clean = v.replace(/\D/g, '').slice(0, this.length).split('');
    this.digits = Array.from({ length: this.length }, (_, i) => clean[i] ?? '');
  }

  writeValue(value: string): void {
    this.setDigitsFromString(value ?? '');
  }
  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  private emit(): void {
    const v = this.digits.join('');
    this.onChange(v);
    this.valueChange.emit(v);
  }

  private focusBox(i: number): void {
    queueMicrotask(() => this.boxes?.get(i)?.nativeElement.focus());
  }

  private distribute(startIndex: number, text: string): void {
    const clean = text.replace(/\D/g, '');
    if (!clean) return;
    let idx = startIndex;
    for (const ch of clean) {
      if (idx >= this.length) break;
      this.digits[idx] = ch;
      idx++;
    }
    this.emit();
    this.focusBox(Math.min(idx, this.length - 1));
  }

  onInput(i: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const raw = input.value.replace(/\D/g, '');
    if (raw.length > 1) { this.distribute(i, raw); return; }
    this.digits[i] = raw;
    input.value = raw;
    this.emit();
    this.onTouched();
    if (raw && i < this.length - 1) this.focusBox(i + 1);
  }

  onKeydown(i: number, event: KeyboardEvent): void {
    if (event.key === 'Enter') { this.enter.emit(); return; }
    if (event.key === 'Backspace') {
      if (!this.digits[i] && i > 0) {
        event.preventDefault();
        this.digits[i - 1] = '';
        this.emit();
        this.focusBox(i - 1);
      }
      return;
    }
    if (event.key === 'ArrowLeft' && i > 0) { event.preventDefault(); this.focusBox(i - 1); }
    if (event.key === 'ArrowRight' && i < this.length - 1) { event.preventDefault(); this.focusBox(i + 1); }
  }

  onPaste(i: number, event: ClipboardEvent): void {
    const text = event.clipboardData?.getData('text') ?? '';
    if (!text) return;
    event.preventDefault();
    this.distribute(i, text);
  }

  onFocus(event: FocusEvent): void {
    (event.target as HTMLInputElement).select();
  }
}
