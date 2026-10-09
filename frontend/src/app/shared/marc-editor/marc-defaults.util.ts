import { MarcField } from '../../models/cataloging/bib';
import { BookMetadataResult } from '../../services/ebook/ebook-document.service';

/** Bộ trường MARC mặc định khi tạo biểu ghi mới chưa chọn Worksheet. */
export function defaultMarcFields(): MarcField[] {
  return [
    ...['001', '002', '003', '004', '005', '006', '007', '008'].map(tag => ({ tag, value: '' }) as MarcField),
    { tag: '245', ind1: '0', ind2: '0', subFields: [{ code: 'a', value: '' }] },
    { tag: '100', ind1: '1', ind2: ' ', subFields: [{ code: 'a', value: '' }] },
    { tag: '260', ind1: ' ', ind2: ' ', subFields: [{ code: 'a', value: '' }, { code: 'b', value: '' }, { code: 'c', value: '' }] },
  ];
}

/** Parse chuỗi usmarc dạng "245|a,b,c#260|a,b,c" (từ Worksheet) thành danh sách MarcField mẫu. */
export function parseWorksheetUsmarc(usmarc: string): MarcField[] {
  return usmarc.split('#').filter(Boolean).map(seg => {
    const [tag, subs] = seg.split('|');
    return { tag: (tag || '').trim(), ind1: ' ', ind2: ' ', subFields: (subs || 'a').split(',').map(c => ({ code: c.trim(), value: '' })) };
  });
}

/** Ghi đè (tạo mới nếu chưa có) giá trị 1 subfield trong mảng MarcField — dùng để đồng bộ 1 giá trị từ
 * ngoài MARC (VD Đơn giá) vào đúng 1 subfield cụ thể, không đụng các subfield/field khác. */
export function setMarcSubfieldValue(fields: MarcField[], tag: string, code: string, value: string): MarcField[] {
  const idx = fields.findIndex(f => f.tag === tag);
  if (idx === -1) return [...fields, { tag, ind1: ' ', ind2: ' ', subFields: [{ code, value }] }];
  const subs = fields[idx].subFields || [];
  const subIdx = subs.findIndex(s => s.code === code);
  const newSubs = subIdx === -1 ? [...subs, { code, value }] : subs.map((s, i) => i === subIdx ? { ...s, value } : s);
  return fields.map((f, i) => i === idx ? { ...f, subFields: newSubs } : f);
}

/** Định dạng Đơn giá + Loại tiền thành chuỗi ghi vào MARC 020$c, VD "150000 VND". */
export function formatPriceForMarc(price: number, currency: string): string {
  return `${price} ${currency}`;
}

/** Điền metadata AI phân tích được từ ảnh bìa vào các trường MARC tương ứng — ghi đè không điều kiện
 * (khác copyFieldsFromEbook chỉ điền trường trống), vì đây là hành động người dùng chủ động bấm. */
export function applyBookMetadataToFields(fields: MarcField[], meta: BookMetadataResult): MarcField[] {
  let result = fields;
  if (meta.title)       result = setMarcSubfieldValue(result, '245', 'a', meta.title);
  if (meta.author)      result = setMarcSubfieldValue(result, '100', 'a', meta.author);
  if (meta.publisher)   result = setMarcSubfieldValue(result, '260', 'b', meta.publisher);
  if (meta.publishYear) result = setMarcSubfieldValue(result, '260', 'c', meta.publishYear);
  if (meta.isbn)        result = setMarcSubfieldValue(result, '020', 'a', meta.isbn);
  return result;
}
