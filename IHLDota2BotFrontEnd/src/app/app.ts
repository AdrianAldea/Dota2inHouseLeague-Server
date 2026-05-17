import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

interface ActionGroup {
  label: string;
  icon: string;
  actions: string[];
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    * { box-sizing: border-box; margin: 0; padding: 0; }

    :host {
      display: block;
      min-height: 100vh;
      background: #0d1117;
      color: #e6edf3;
      font-family: 'Segoe UI', sans-serif;
      padding: 2rem;
    }

    h1 {
      font-size: 1.4rem;
      font-weight: 700;
      letter-spacing: 0.05em;
      color: #f0a500;
      margin-bottom: 0.25rem;
    }

    .subtitle { font-size: 0.8rem; color: #8b949e; margin-bottom: 2rem; }

    /* ── Server Start ── */
    .server-card {
      background: #161b22;
      border: 1px solid #30363d;
      border-radius: 10px;
      padding: 1.25rem 1.5rem;
      margin-bottom: 2rem;
      display: flex;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .server-card input {
      background: #0d1117;
      border: 1px solid #30363d;
      border-radius: 6px;
      color: #e6edf3;
      padding: 0.45rem 0.75rem;
      font-size: 0.85rem;
      outline: none;
      transition: border-color 150ms;
    }
    .server-card input:focus { border-color: #f0a500; }

    .status {
      margin-left: auto;
      font-size: 0.82rem;
      color: #8b949e;
      font-style: italic;
      min-width: 100px;
      text-align: right;
    }

    /* ── Action Groups ── */
    .groups { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 1.25rem; }

    .group {
      background: #161b22;
      border: 1px solid #30363d;
      border-radius: 10px;
      overflow: hidden;
    }
    .group-header {
      padding: 0.65rem 1rem;
      background: #1c2128;
      border-bottom: 1px solid #30363d;
      font-size: 0.78rem;
      font-weight: 600;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: #8b949e;
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .group-body { padding: 0.75rem; display: flex; flex-direction: column; gap: 0.5rem; }

    /* ── Buttons ── */
    button {
      background: #21262d;
      border: 1px solid #30363d;
      border-radius: 6px;
      color: #e6edf3;
      padding: 0.5rem 0.9rem;
      font-size: 0.82rem;
      cursor: pointer;
      text-align: left;
      transition: background 120ms, border-color 120ms, color 120ms;
      width: 100%;
    }
    button:hover { background: #2d333b; border-color: #f0a500; color: #f0a500; }
    button:active { background: #f0a50015; }
    button.primary {
      background: #f0a500;
      color: #0d1117;
      font-weight: 700;
      border-color: #f0a500;
      padding: 0.5rem 1.25rem;
      width: auto;
    }
    button.primary:hover { background: #e09400; }
  `],
  template: `
    <h1>⚔️ IHL Dota 2 Bot</h1>
    <p class="subtitle">Admin Panel — Dev Mode</p>

    <!-- Server Start -->
    <div class="server-card">
      <input [(ngModel)]="username" placeholder="Username" />
      <input [(ngModel)]="password" placeholder="Password" type="password" />
      <button class="primary" (click)="startServer()">Start Server</button>
      <span class="status">{{ message() }}</span>
    </div>

    <!-- Action Groups -->
    <div class="groups">
      @for (group of actionGroups; track group.label) {
        <div class="group">
          <div class="group-header">{{ group.icon }} {{ group.label }}</div>
          <div class="group-body">
            @for (action of group.actions; track action) {
              <button (click)="triggerAction(action)">{{ action }}</button>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class App {
  username = '';
  password = '';
  message = signal('');

  actionGroups: ActionGroup[] = [
    {
      label: 'Player',
      icon: '👤',
      actions: ['REGISTER', 'PROFILE', 'MYSTATS'],
    },
    {
      label: 'Match',
      icon: '⚔️',
      actions: ['MATCH', 'MATCHES', 'CHALLENGE', 'ACCEPT_MATCH', 'DECLINE_MATCH', 'REPORT', 'REPORT_MATCH'],
    },
    {
      label: 'Lobby',
      icon: '🏟️',
      actions: ['CREATE_LOBBY', 'INVITE_LOBBY', 'END_LOBBY', 'CREATE_GAME'],
    },
    {
      label: 'Info',
      icon: 'ℹ️',
      actions: ['LEADERBOARD', 'COMMANDS'],
    },
  ];

  constructor(private http: HttpClient) {}

  startServer() {
    this.message.set('Starting...');
    this.http.post<boolean>('/api/server/start', {
      username: this.username,
      password: this.password,
    }, { responseType: 'json' }).subscribe({
      next: (res) => this.message.set(res ? 'Started ✅' : 'Failed to start'),
      error: (err) => this.message.set('Error: ' + err.message),
    });
  }

  triggerAction(action: string) {
    this.message.set(`api/discord/${action}...`);
    this.http.post<string>(`/api/actions/${action}`, {}, { responseType: 'json' }).subscribe({
      next: () => this.message.set(`${action} ✅`),
      error: (err) => this.message.set(`${action} ❌ ` + err.message),
    });
  }
}