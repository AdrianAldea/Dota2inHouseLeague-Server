import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { App } from './app';
import { routes } from './app.routes';
import { AdminPage } from '../pages/admin/admin';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes),
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('redirects the root path to the admin page', async () => {
    const harness = await RouterTestingHarness.create('/');
    expect(harness.routeDebugElement?.componentInstance).toBeInstanceOf(AdminPage);
  });

  it('redirects unknown paths to the admin page', async () => {
    const harness = await RouterTestingHarness.create('/no-such-page');
    expect(harness.routeDebugElement?.componentInstance).toBeInstanceOf(AdminPage);
  });
});
