import { Component, ElementRef, booleanAttribute, HostListener, computed, forwardRef, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';

const pad = (n: number): string => String(n).padStart(2, '0');

/** 'yyyy-MM-dd[THH:mm…]' → các phần số; null nếu không đúng dạng hoặc ngày không có thật (vd 31/02). */
function parseIso(s: string | null | undefined): { y: number; m: number; d: number; hh: number; mm: number } | null {
  const r = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2}))?/.exec(s ?? '');
  if (!r) return null;
  const [y, m, d, hh, mm] = [+r[1], +r[2], +r[3], +(r[4] ?? 0), +(r[5] ?? 0)];
  return valid(y, m, d) ? { y, m, d, hh, mm } : null;
}

function valid(y: number, m: number, d: number): boolean {
  if (y < 1000 || m < 1 || m > 12 || d < 1) return false;
  return d <= new Date(y, m, 0).getDate();
}

/** "dd/mm/yyyy" → phần số; null nếu chưa đủ hoặc không có thật. */
function parseDisplay(s: string): { y: number; m: number; d: number } | null {
  const r = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(s.trim());
  if (!r) return null;
  const [d, m, y] = [+r[1], +r[2], +r[3]];
  return valid(y, m, d) ? { y, m, d } : null;
}

/** Tự chèn dấu "/" khi gõ số: 25102026 → 25/10/2026. */
function mask(s: string): string {
  const digits = s.replace(/\D/g, '').slice(0, 8);
  if (digits.length <= 2) return digits;
  if (digits.length <= 4) return `${digits.slice(0, 2)}/${digits.slice(2)}`;
  return `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
}

/**
 * Ô chọn ngày hiển thị dd/mm/yyyy (input type="date" gốc theo locale của hệ điều hành nên không ép được), có lịch chọn.
 * Giá trị vào/ra là chuỗi 'yyyy-MM-dd' như input gốc (hoặc 'yyyy-MM-ddTHH:mm' khi bật [withTime]) → thay thẳng vào chỗ dùng [(ngModel)].
 */
@Component({
  selector: 'app-date-input',
  imports: [TranslatePipe],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DateInput), multi: true }],
  template: `
    <div class="relative flex gap-1">
      <div class="relative flex-1 min-w-0">
        <input type="text" inputmode="numeric" autocomplete="off" class="w-full border border-slate-300 rounded-lg px-3 py-2 pr-9 text-sm disabled:opacity-50" [class.border-red-500]="bad()" [id]="inputId()" placeholder="dd/mm/yyyy" maxlength="10"
          [value]="text()" [disabled]="disabled()" [attr.aria-invalid]="bad()" [attr.aria-label]="ariaLabel() || null"
          (input)="onType($any($event.target).value, $event.target)" (blur)="commitText(); onTouched()" (keydown.enter)="commitText()" (keydown.escape)="open.set(false)" />
        <button type="button" class="absolute right-1 top-1/2 -translate-y-1/2 w-7 h-7 inline-flex items-center justify-center rounded text-slate-500 hover:bg-slate-100 disabled:opacity-50" [disabled]="disabled()" [attr.aria-label]="'DATE.PICK' | translate" [title]="'DATE.PICK' | translate" (click)="toggle()">
          <span class="material-icons text-[18px]">calendar_today</span>
        </button>
      </div>
      @if (withTime()) {
        <input type="text" inputmode="numeric" autocomplete="off" class="w-20 shrink-0 text-center border border-slate-300 rounded-lg px-3 py-2 text-sm" placeholder="hh:mm" maxlength="5" [value]="timeText()" [disabled]="disabled()" [attr.aria-label]="'DATE.TIME' | translate"
          (input)="onTime($any($event.target).value, $event.target)" (blur)="commitTime(); onTouched()" (keydown.enter)="commitTime()" />
      }
      @if (open()) {
        <div class="fixed z-[70] w-64 rounded-lg border border-gray-300 bg-white shadow-lg p-2 text-sm" role="dialog" [style.top.px]="pos().top" [style.left.px]="pos().left" [attr.aria-label]="'DATE.PICK' | translate">
          <div class="flex items-center justify-between mb-1">
            <button type="button" class="w-7 h-7 inline-flex items-center justify-center rounded hover:bg-slate-100" [attr.aria-label]="'DATE.PREV_YEAR' | translate" (click)="shift(-12)"><span class="material-icons text-[18px]">keyboard_double_arrow_left</span></button>
            <button type="button" class="w-7 h-7 inline-flex items-center justify-center rounded hover:bg-slate-100" [attr.aria-label]="'DATE.PREV' | translate" (click)="shift(-1)"><span class="material-icons text-[18px]">chevron_left</span></button>
            <span class="font-semibold">{{ 'DATE.MONTH_YEAR' | translate: { m: view().m, y: view().y } }}</span>
            <button type="button" class="w-7 h-7 inline-flex items-center justify-center rounded hover:bg-slate-100" [attr.aria-label]="'DATE.NEXT' | translate" (click)="shift(1)"><span class="material-icons text-[18px]">chevron_right</span></button>
            <button type="button" class="w-7 h-7 inline-flex items-center justify-center rounded hover:bg-slate-100" [attr.aria-label]="'DATE.NEXT_YEAR' | translate" (click)="shift(12)"><span class="material-icons text-[18px]">keyboard_double_arrow_right</span></button>
          </div>
          <div class="grid grid-cols-7 text-center text-xs text-gray-500 mb-1">
            @for (w of weekdays; track w) { <span class="py-1">{{ w | translate }}</span> }
          </div>
          <div class="grid grid-cols-7 gap-0.5 text-center">
            @for (c of cells(); track c.key) {
              <button type="button" class="h-8 rounded text-sm" [class.text-gray-400]="!c.inMonth" [class.bg-blue-600]="c.selected" [class.text-white]="c.selected"
                [class.hover:bg-blue-50]="!c.selected" [class.ring-1]="c.today && !c.selected" [class.ring-blue-500]="c.today && !c.selected" (click)="pick(c)">{{ c.d }}</button>
            }
          </div>
          <div class="flex justify-between mt-2 pt-2 border-t border-gray-100 text-xs">
            <button type="button" class="text-blue-700 hover:underline" (click)="today()">{{ 'DATE.TODAY' | translate }}</button>
            <button type="button" class="text-gray-600 hover:underline" (click)="clear()">{{ 'DATE.CLEAR' | translate }}</button>
          </div>
        </div>
      }
    </div>
  `,
})
export class DateInput implements ControlValueAccessor {
  private static seq = 0;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** Thêm ô giờ:phút; giá trị thành 'yyyy-MM-ddTHH:mm' (thay cho input datetime-local). */
  readonly withTime = input(false, { transform: booleanAttribute });
  readonly fieldId = input<string | null>(null);
  readonly ariaLabel = input<string | null>(null);
  /** Id của ô gõ — nhãn <label for> trỏ vào đây; bỏ trống thì tự sinh. */
  protected readonly inputId = computed(() => this.fieldId() ?? this.autoId);
  private readonly autoId = `di-${++DateInput.seq}`;

  protected readonly weekdays = ['DATE.WD_MON', 'DATE.WD_TUE', 'DATE.WD_WED', 'DATE.WD_THU', 'DATE.WD_FRI', 'DATE.WD_SAT', 'DATE.WD_SUN'];
  protected readonly text = signal('');
  protected readonly timeText = signal('00:00');
  protected readonly disabled = signal(false);
  protected readonly open = signal(false);
  protected readonly bad = signal(false);
  protected readonly pos = signal({ top: 0, left: 0 });
  protected readonly view = signal({ y: new Date().getFullYear(), m: new Date().getMonth() + 1 });
  private readonly model = signal<{ y: number; m: number; d: number } | null>(null);
  private time = { hh: 0, mm: 0 };

  private onChange: (v: string | null) => void = () => {};
  protected onTouched: () => void = () => {};

  protected readonly cells = computed(() => {
    const { y, m } = this.view();
    const first = new Date(y, m - 1, 1);
    const lead = (first.getDay() + 6) % 7; // tuần bắt đầu từ thứ Hai
    const now = new Date();
    const sel = this.model();
    return Array.from({ length: 42 }, (_, i) => {
      const dt = new Date(y, m - 1, 1 - lead + i);
      const key = `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}`;
      return {
        key, y: dt.getFullYear(), m: dt.getMonth() + 1, d: dt.getDate(), inMonth: dt.getMonth() === m - 1,
        selected: sel?.y === dt.getFullYear() && sel.m === dt.getMonth() + 1 && sel.d === dt.getDate(),
        today: dt.getFullYear() === now.getFullYear() && dt.getMonth() === now.getMonth() && dt.getDate() === now.getDate(),
      };
    });
  });

  writeValue(v: string | null): void {
    const p = parseIso(v);
    this.model.set(p ? { y: p.y, m: p.m, d: p.d } : null);
    this.time = p ? { hh: p.hh, mm: p.mm } : { hh: 0, mm: 0 };
    this.text.set(p ? `${pad(p.d)}/${pad(p.m)}/${p.y}` : '');
    this.timeText.set(`${pad(this.time.hh)}:${pad(this.time.mm)}`);
    this.bad.set(false);
    if (p) this.view.set({ y: p.y, m: p.m });
  }
  registerOnChange(fn: (v: string | null) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(d: boolean): void { this.disabled.set(d); if (d) this.open.set(false); }

  @HostListener('document:mousedown', ['$event'])
  protected outside(e: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(e.target as Node)) this.open.set(false);
  }

  @HostListener('window:resize')
  @HostListener('window:scroll')
  protected close(): void { this.open.set(false); }

  protected toggle(): void {
    if (this.disabled()) return;
    if (this.open()) { this.open.set(false); return; }
    const sel = this.model();
    const now = new Date();
    this.view.set(sel ? { y: sel.y, m: sel.m } : { y: now.getFullYear(), m: now.getMonth() + 1 });
    // Cố định theo cửa sổ để hộp thoại (Modal có overflow) không cắt lịch; thiếu chỗ phía dưới thì mở lên trên.
    const r = this.host.nativeElement.getBoundingClientRect();
    const h = 330;
    const top = r.bottom + 4 + h > window.innerHeight && r.top - 4 - h > 0 ? r.top - 4 - h : r.bottom + 4;
    this.pos.set({ top, left: Math.max(4, Math.min(r.left, window.innerWidth - 260)) });
    this.open.set(true);
  }

  protected shift(months: number): void {
    const { y, m } = this.view();
    const dt = new Date(y, m - 1 + months, 1);
    this.view.set({ y: dt.getFullYear(), m: dt.getMonth() + 1 });
  }

  /** Gõ tới đâu tự chèn "/" tới đó; đủ ngày hợp lệ thì cập nhật giá trị ngay. */
  protected onType(raw: string, el: EventTarget | null): void {
    const masked = mask(raw);
    this.text.set(masked);
    (el as HTMLInputElement).value = masked;
    this.bad.set(false);
    if (masked === '') this.set(null);
    else if (masked.length === 10) this.commitText();
  }

  protected commitText(): void {
    const t = this.text();
    if (t === '') { this.bad.set(false); return; }
    const p = parseDisplay(t);
    this.bad.set(!p);
    if (!p) { this.set(null); return; } // chữ chưa đủ/không có thật: không giữ ngày cũ, tránh lưu nhầm
    this.text.set(`${pad(p.d)}/${pad(p.m)}/${p.y}`);
    this.set(p);
    this.view.set({ y: p.y, m: p.m });
  }

  protected onTime(raw: string, el: EventTarget | null): void {
    const digits = raw.replace(/\D/g, '').slice(0, 4);
    const masked = digits.length > 2 ? `${digits.slice(0, 2)}:${digits.slice(2)}` : digits;
    this.timeText.set(masked);
    (el as HTMLInputElement).value = masked;
    if (masked.length === 5) this.commitTime();
  }

  protected commitTime(): void {
    const r = /^(\d{1,2}):?(\d{2})$/.exec(this.timeText());
    if (!r || +r[1] > 23 || +r[2] > 59) { this.timeText.set(`${pad(this.time.hh)}:${pad(this.time.mm)}`); return; }
    this.time = { hh: +r[1], mm: +r[2] };
    this.timeText.set(`${pad(this.time.hh)}:${pad(this.time.mm)}`);
    const cur = this.model();
    if (cur) this.set(cur);
  }

  protected pick(c: { y: number; m: number; d: number }): void {
    this.text.set(`${pad(c.d)}/${pad(c.m)}/${c.y}`);
    this.bad.set(false);
    this.set({ y: c.y, m: c.m, d: c.d });
    this.open.set(false);
  }

  protected today(): void {
    const n = new Date();
    this.pick({ y: n.getFullYear(), m: n.getMonth() + 1, d: n.getDate() });
  }

  protected clear(): void {
    this.text.set('');
    this.bad.set(false);
    this.set(null);
    this.open.set(false);
  }

  private set(p: { y: number; m: number; d: number } | null): void {
    this.model.set(p);
    if (!p) { this.onChange(null); return; }
    const date = `${p.y}-${pad(p.m)}-${pad(p.d)}`;
    this.onChange(this.withTime() ? `${date}T${pad(this.time.hh)}:${pad(this.time.mm)}` : date);
  }
}
