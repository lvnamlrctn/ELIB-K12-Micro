import { HttpInterceptorFn } from '@angular/common/http';
import { APP_CONFIG } from './config';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  // Check if we are calling the CMS API
  if (req.url.includes('/api/public/cms/')) {
    // Clone and append the TenantId parameter
    const modifiedReq = req.clone({
      params: req.params.set('TenantId', APP_CONFIG.TenantId)
    });
    return next(modifiedReq);
  }
  
  return next(req);
};
