/**
 * Cây Bộ sưu tập cho OPAC (port ELIB-LRC 09-22, Đợt 20). API `PublicEBookCollection/SearchAll` trả danh sách phẳng
 * {id, publicId, name, parentId, totalItems}; trước đây OPAC ELIB hiển thị phẳng nên không thấy quan hệ cha/con.
 * Nối cha/con theo `parentId` (Id số); node có cha không nằm trong danh sách (gốc ELIB dùng ParentId = 0/null, hoặc
 * cha thuộc đơn vị khác bị lọc) trở thành gốc. Giá trị lọc vẫn là `publicId` (backend tự gồm cả bộ sưu tập con).
 */
export interface RawCollection {
  id?: number | string | null;
  publicId?: string | null;
  name?: string | null;
  parentId?: number | string | null;
  totalItems?: number | null;
}

export interface CollectionTreeNode {
  key: string;        // Id số — chỉ dùng để nối cha/con
  publicId: string;   // giá trị lọc gửi lên API tìm kiếm
  name: string;
  count: number;      // số tài liệu của chính node + toàn bộ con cháu
  children: CollectionTreeNode[];
}

export interface CollectionOption { id: string; title: string; depth: number; count: number; }

export function buildCollectionTree(raw: RawCollection[]): CollectionTreeNode[] {
  const nodes = new Map<string, CollectionTreeNode & { parentKey: string }>();
  for (const c of raw) {
    const key = String(c.id ?? '');
    if (!key || !c.publicId) continue;
    nodes.set(key, { key, publicId: c.publicId, name: c.name ?? '', parentKey: String(c.parentId ?? '0'),
                     count: Number(c.totalItems ?? 0), children: [] });
  }
  const roots: CollectionTreeNode[] = [];
  for (const node of nodes.values()) {
    const parent = node.parentKey !== node.key ? nodes.get(node.parentKey) : undefined;
    if (parent) parent.children.push(node); else roots.push(node);
  }
  const total = (n: CollectionTreeNode): number => { n.count += n.children.reduce((s, c) => s + total(c), 0); return n.count; };
  roots.forEach(total);
  return roots;
}

/** Trải cây theo thứ tự cha → con (duyệt sâu) kèm cấp, để hiển thị thụt lề trong ô chọn. */
export function flattenCollectionTree(nodes: CollectionTreeNode[], depth = 0, seen = new Set<string>()): CollectionOption[] {
  const out: CollectionOption[] = [];
  for (const n of nodes) {
    if (seen.has(n.key)) continue; // phòng dữ liệu vòng lặp cha/con
    seen.add(n.key);
    out.push({ id: n.publicId, title: n.name, depth, count: n.count });
    out.push(...flattenCollectionTree(n.children, depth + 1, seen));
  }
  return out;
}
