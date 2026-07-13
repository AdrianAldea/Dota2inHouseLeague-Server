import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { apiKeyInterceptor } from '../interceptors/api-key.interceptor';
import { httpTimeoutInterceptor } from '../interceptors/http-timeout.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([apiKeyInterceptor, httpTimeoutInterceptor])),
  ]
};
