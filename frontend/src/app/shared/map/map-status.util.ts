const STATUS_LABELS: Record<string, string> = { HOAT_DONG: 'Hoạt động', BAO_TRI: 'Bảo trì', HU_HONG: 'Hư hỏng' };

export function statusColor(status?: string): string {
  const s = (status || '').toUpperCase();
  if (s === 'HOAT_DONG' || s.includes('HOẠT ĐỘNG') || s.includes('SẴN SÀNG')) return 'bg-emerald-100 text-emerald-700';
  if (s === 'BAO_TRI' || s.includes('BẢO TRÌ')) return 'bg-amber-100 text-amber-700';
  if (s === 'HU_HONG' || s.includes('HƯ HỎNG')) return 'bg-red-100 text-red-700';
  return 'bg-gray-100 text-gray-600';
}

export function statusLabel(status?: string): string {
  return STATUS_LABELS[(status || '').toUpperCase()] || status || '—';
}
