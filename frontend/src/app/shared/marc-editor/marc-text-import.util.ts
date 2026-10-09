import { MarcField } from '../../models/cataloging/bib';

/** Phân tích dữ liệu MARC dạng text 3-dòng/trường (Tag / Chỉ thị / Giá trị, không có dòng trống ngăn
 * cách) — định dạng xuất từ hệ thống cũ (vd file "rc_20260923151948.txt"), khác hẳn ISO2709 nhị phân
 * (.mrc) đã có sẵn parser riêng ở backend (MarcConvertController). Chạy hoàn toàn phía client, không gọi
 * API — cùng tinh thần parseWorksheetUsmarc() ở marc-defaults.util.ts.
 *
 * Quy tắc đã chốt với người dùng:
 * - Bỏ hẳn "Ldr" (Leader/000) và "001" — không hiển thị, giống luồng nhập tay (backend luôn tự sinh lại
 *   2 trường này bất kể client gửi gì, xem CatalogueBookController.ControlFieldTags).
 * - Trường dữ liệu (tag >= '010') mà dòng giá trị KHÔNG bắt đầu bằng "$" (trường cục bộ hệ cũ như
 *   900/907/911/914/925/926/927 trong mẫu thật) — bỏ qua hoàn toàn, không tạo $a tạm.
 */
export function parseMarcText(text: string): MarcField[] {
  const normalized = text.replace(/\r\n?/g, '\n').replace(/^﻿/, '');
  const lines = normalized.split('\n');
  const result: MarcField[] = [];

  for (let i = 0; i + 2 < lines.length; i += 3) {
    const tag = (lines[i] || '').trim();
    const indLine = lines[i + 1] || '';
    const valLine = lines[i + 2] || '';
    if (!tag) continue;
    if (tag.toLowerCase() === 'ldr' || tag === '001') continue;

    if (tag < '010') {
      // Trường điều khiển (002-008) — giá trị thô, không tách subfield.
      result.push({ tag, value: valLine });
      continue;
    }

    const trimmed = valLine.trimStart();
    if (!trimmed.startsWith('$')) continue; // trường cục bộ không theo chuẩn $subfield — bỏ qua

    const normalizeInd = (ch: string | undefined): string => (ch && /[0-9A-Za-z]/.test(ch)) ? ch : ' ';
    const ind1 = normalizeInd(indLine[0]);
    const ind2 = normalizeInd(indLine[1]);

    const subFields: { code: string; value: string }[] = [];
    const re = /\$(.)([^$]*)/g;
    let m: RegExpExecArray | null;
    while ((m = re.exec(trimmed)) !== null) {
      subFields.push({ code: m[1], value: m[2] });
    }
    if (!subFields.length) continue;

    result.push({ tag, ind1, ind2, subFields });
  }

  return result;
}
