import { AdminKeyService } from './admin-key.service';

const STORAGE_KEY = 'ihl.adminApiKey';

describe('AdminKeyService', () => {
  afterEach(() => localStorage.removeItem(STORAGE_KEY));

  it('starts empty when nothing is stored', () => {
    expect(new AdminKeyService().key()).toBe('');
  });

  it('loads a previously stored key on construction', () => {
    localStorage.setItem(STORAGE_KEY, 'stored-key');
    expect(new AdminKeyService().key()).toBe('stored-key');
  });

  it('setKey trims, updates the signal, and persists', () => {
    const service = new AdminKeyService();

    service.setKey('  my-key  ');

    expect(service.key()).toBe('my-key');
    expect(localStorage.getItem(STORAGE_KEY)).toBe('my-key');
  });

  it('clearing the key removes it from storage', () => {
    const service = new AdminKeyService();
    service.setKey('my-key');

    service.setKey('');

    expect(service.key()).toBe('');
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });
});
