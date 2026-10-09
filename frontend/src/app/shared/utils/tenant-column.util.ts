/** Đợt 24 — thêm cột "tenant" vào danh sách cột bảng Material nếu tài khoản đặc quyền, đặt ngay trước
 * cột cuối cùng (thường là "actions"). Idempotent — gọi nhiều lần không thêm trùng. */
export function applyTenantColumn(displayedColumns: string[], isPrivileged: boolean): void {
  if (isPrivileged && !displayedColumns.includes('tenant')) {
    displayedColumns.splice(displayedColumns.length - 1, 0, 'tenant');
  }
}
