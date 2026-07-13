import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { apiKeyInterceptor } from '../interceptors/api-key.interceptor';
import { httpTimeoutInterceptor } from '../interceptors/http-timeout.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch(), withInterceptors([apiKeyInterceptor, httpTimeoutInterceptor])),
  ]
};
