import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import { Auth } from '../services/auth';

export const authGuard: CanActivateFn = () => {
  const auth   = inject(Auth);
  const router = inject(Router);

  if (auth.isLoggedIn()() && !auth.isTokenExpired()) {
    return true;
  }

  auth.logout();
  router.navigate(['/admin/login']);
  return false;
};
