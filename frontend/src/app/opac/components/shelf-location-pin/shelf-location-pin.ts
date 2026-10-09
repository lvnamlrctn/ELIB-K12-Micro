import { Component, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BookLocation } from '../../services/library-map-api.service';

/**
 * Ghim vị trí kệ thật của 1 bản sách in, vẽ trên khung lưới % dựa vào
 * BookLocation.positionX/positionY (cùng quy ước phần trăm với trang sơ đồ đầy đủ
 * /so-do-thu-vien — xem geom() trong library-map.ts) -- không vẽ gì khi bản chưa xác định
 * được vị trí (found=false hoặc thiếu toạ độ), chỉ hiện cảnh báo trung thực.
 *
 * Chủ động không có prop "floorObjects" (ngữ cảnh các đối tượng khác cùng tầng, có ở bản
 * ELIB-LRC gốc) -- chỉ vẽ đúng 1 ghim, tránh thêm 1 lệnh gọi getFloor() không cần thiết
 * cho phạm vi "chỉ xây dựng pin vị trí kệ sách" đã chốt.
 */
@Component({
  selector: 'app-shelf-location-pin',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (hasPosition()) {
      <div class="space-y-2">
        <div class="flex items-center justify-between text-xs font-bold text-gray-600">
          <span class="flex items-center gap-1.5">
            <i class="fas fa-building text-main"></i>
            {{ [loc()!.buildingName, loc()!.floorName].filter(v => !!v).join(' - ') }}
          </span>
          @if (loc()!.rowIndex != null) {
            <span class="text-gray-400 font-mono">Ngăn {{ loc()!.rowIndex }}</span>
          }
        </div>

        <div class="relative w-full bg-gray-50 border-2 border-gray-200 rounded-2xl overflow-hidden" style="aspect-ratio: 3 / 2;">
          <div class="absolute inset-0 opacity-40 pointer-events-none"
               style="background-image: linear-gradient(#cbd5e1 1px, transparent 1px), linear-gradient(90deg, #cbd5e1 1px, transparent 1px); background-size: 24px 24px;">
          </div>

          <div class="absolute z-10 -translate-x-1/2 -translate-y-1/2 flex flex-col items-center pointer-events-none"
               [style.left.%]="loc()!.positionX"
               [style.top.%]="loc()!.positionY">
            <div class="px-3 py-1.5 rounded-xl bg-amber-500 text-gray-950 font-black text-sm shadow-lg border-2 border-white flex items-center gap-1 animate-pulse whitespace-nowrap">
              <i class="fas fa-location-dot"></i>
              {{ loc()!.shelfName || loc()!.shelfCode || 'Kệ sách' }}
            </div>
            <div class="w-3.5 h-3.5 rounded-full bg-amber-500 border-2 border-white shadow-md"></div>
          </div>
        </div>

        @if (loc()!.source === 'ddc-range') {
          <p class="text-[11px] text-amber-700 italic">Khu vực ước tính theo DDC — sách này chưa được xếp giá cụ thể.</p>
        }
      </div>
    } @else {
      <div class="p-4 bg-amber-50 border border-amber-200 rounded-xl text-xs text-amber-900 flex items-start gap-2">
        <i class="fas fa-circle-info text-amber-600 mt-0.5"></i>
        <span>Chưa xác định được vị trí kệ của bản sách này.</span>
      </div>
    }
  `
})
export class ShelfLocationPinComponent {
  loc = input<BookLocation | null>(null);

  hasPosition = computed(() => {
    const l = this.loc();
    return !!l?.found && l.positionX != null && l.positionY != null;
  });
}
