import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'ihl.adminApiKey';

function readStoredKey(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
}

/**
 * Holds the admin API key the operator entered for the Game Manager's
 * `X-Api-Key`-gated admin endpoints. Persisted to localStorage so a page
 * reload doesn't prompt again; per-browser only. An empty value means
 * "send no header" — matching a server with no key configured.
 */
@Injectable({ providedIn: 'root' })
export class AdminKeyService {
  private readonly keySignal = signal<string>(readStoredKey());

  /** The current key (`''` = unset). */
  readonly key = this.keySignal.asReadonly();

  setKey(value: string): void {
    const key = value.trim();
    this.keySignal.set(key);
    try {
      if (key) {
        localStorage.setItem(STORAGE_KEY, key);
      } else {
        localStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      // Storage unavailable (private browsing etc.) — the key still applies
      // for this session via the signal; it just won't survive a reload.
    }
  }
}
