import { HttpInterceptorFn } from '@angular/common/http';
import { catchError } from 'rxjs/operators';
import { throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('srs-liba-token');
  let newReq = req;

  if (token) {
    newReq = req.clone({
      headers: req.headers.set('Authorization', `Bearer ${token}`)
    });
  }

  return next(newReq).pipe(
    catchError((err) => {
      if (err.status === 401 || err.status === 403) {
        localStorage.removeItem('srs-liba-token');
        localStorage.removeItem('srs-liba-role');
        localStorage.removeItem('srs-liba-user');
        if (typeof window !== 'undefined') {
          window.location.reload();
        }
      }
      return throwError(() => err);
    })
  );
};
