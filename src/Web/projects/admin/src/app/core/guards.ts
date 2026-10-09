import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { Session } from './session';

const CHANGE_PASSWORD = '/change-password';

/** Bắt buộc đăng nhập; tài khoản bị buộc đổi mật khẩu chỉ vào được trang đổi mật khẩu. */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const session = inject(Session);
  const router = inject(Router);

  if (!(await auth.ensureUser())) {
    await auth.login(state.url);
    return false;
  }
  await session.load();
  if (session.me()?.mustChangePassword && state.url !== CHANGE_PASSWORD) return router.parseUrl(CHANGE_PASSWORD);
  return true;
};

export const systemGuard: CanActivateFn = () => inject(Session).isSystem() || inject(Router).parseUrl('/');

export const tenantGuard: CanActivateFn = () => !inject(Session).isSystem() || inject(Router).parseUrl('/');

/** Có quyền xem module (MODULE:view) — chỉ để không mở màn trống; API vẫn tự kiểm tra quyền. */
export const permissionGuard = (module: string): CanActivateFn => () =>
  inject(Session).can(`${module}:view`) || inject(Router).parseUrl('/');
