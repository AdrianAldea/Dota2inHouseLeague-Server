/// Here in app.ts it would "hook" things up accordingly
import { Component } from "@angular/core";
import { AdminPage } from "../pages/admin/admin";

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [AdminPage],
    template: `<admin-page></admin-page>`, // @todo Inlined until we have a router
})

export class App {

}


// import { Component, signal, ChangeDetectionStrategy, Input, OnInit } from '@angular/core';
// import { CommonModule } from '@angular/common';
// import { HttpClient } from '@angular/common/http';
// import { FormsModule } from '@angular/forms';

// // ─── REUSABLE SERVER CARD COMPONENT ─────────────────────────────────────────────
// @Component({
//   selector: 'app-server-card',
//   standalone: true,
//   imports: [CommonModule, FormsModule],
//   changeDetection: ChangeDetectionStrategy.OnPush,
//   styles: [`
//     .card { background: #161b22; border: 1px solid #30363d; border-radius: 10px; overflow: hidden; display: flex; flex-direction: column; height: 100%; }
//     .card-header { padding: 1rem 1.25rem; background: #1c2128; border-bottom: 1px solid #30363d; font-weight: 600; font-size: 1.05rem; color: #f0a500; display: flex; justify-content: space-between; align-items: center; }
//     .card-body { padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; flex-grow: 1; }
//     .inputs { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
//     input { background: #0d1117; border: 1px solid #30363d; border-radius: 6px; color: #e6edf3; padding: 0.5rem 0.75rem; font-size: 0.85rem; outline: none; width: 100%; box-sizing: border-box; transition: border-color 150ms;}
//     input:focus { border-color: #f0a500; }
//     .actions { display: flex; align-items: center; gap: 0.75rem; }
//     button { padding: 0.5rem 1.25rem; font-size: 0.85rem; font-weight: 600; border-radius: 6px; cursor: pointer; border: none; transition: background 150ms; }
//     button.primary { background: #238636; color: #ffffff; }
//     button.primary:hover { background: #2ea043; }
//     button.danger { background: #da3633; color: #ffffff; }
//     button.danger:hover { background: #f85149; }
//     .status { margin-left: auto; font-size: 0.8rem; color: #8b949e; font-style: italic; }
//     .response-box { background: #0d1117; border: 1px solid #30363d; border-radius: 6px; margin-top: auto; overflow: hidden; }
//     .response-header { padding: 0.4rem 0.75rem; background: #21262d; font-size: 0.75rem; color: #8b949e; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #30363d; }
//     .pretty-json { margin: 0; padding: 1rem; font-size: 0.8rem; color: #79c0ff; max-height: 200px; overflow-y: auto; white-space: pre-wrap; word-wrap: break-word; font-family: ui-monospace, Consolas, monospace; }
//     .btn-clear { background: transparent; border: none; color: #ff7b72; padding: 0; cursor: pointer; font-size: 0.75rem; font-weight: normal; }
//     .btn-clear:hover { text-decoration: underline; background: transparent; }
//   `],
//   template: `
//     <div class="card">
//       <div class="card-header">{{ title }}</div>
//       <div class="card-body">
//         <div class="inputs" *ngIf="needsAuth">
//           <input [(ngModel)]="username" placeholder="Admin Username" />
//           <input [(ngModel)]="password" placeholder="Admin Password" type="password" />
//         </div>
//         <div class="actions">
//           <button class="primary" *ngIf="startEndpoint" (click)="send(startEndpoint)">Start</button>
//           <button class="danger" *ngIf="stopEndpoint" (click)="send(stopEndpoint)">Stop</button>
//           <span class="status">{{ statusMessage() }}</span>
//         </div>
        
//         <div class="response-box" *ngIf="response()">
//           <div class="response-header">
//             SERVER RESPONSE <button class="btn-clear" (click)="response.set(null)">Clear</button>
//           </div>
//           <pre class="pretty-json">{{ response() | json }}</pre>
//         </div>
//       </div>
//     </div>
//   `
// })
// export class ServerCardComponent {
//   @Input() title = '';
//   @Input() startEndpoint = '';
//   @Input() stopEndpoint = '';
//   @Input() needsAuth = false;

//   username = '';
//   password = '';
//   statusMessage = signal('');
//   response = signal<any>(null);

//   constructor(private http: HttpClient) {}

//   send(endpoint: string) {
//     this.statusMessage.set('Sending request...');
//     this.response.set(null);
//     const body = this.needsAuth ? { username: this.username, password: this.password } : {};

//     this.http.post(endpoint, body).subscribe({
//       next: (res) => {
//         this.statusMessage.set('Success ✅');
//         this.response.set(res);
//       },
//       error: (err) => {
//         this.statusMessage.set('Error ❌');
//         this.response.set(err.error || err.message);
//       }
//     });
//   }
// }

// // ─── REUSABLE ACTION COMPONENT ──────────────────────────────────────────────────
// interface ActionParam {
//   key: string;
//   label: string;
//   type?: string;
// }

// @Component({
//   selector: 'app-action-card',
//   standalone: true,
//   imports: [CommonModule, FormsModule],
//   changeDetection: ChangeDetectionStrategy.OnPush,
//   styles: [`
//     .card { background: #161b22; border: 1px solid #30363d; border-radius: 10px; overflow: hidden; display: flex; flex-direction: column; height: 100%; }
//     .card-header { padding: 0.75rem 1rem; background: #1c2128; border-bottom: 1px solid #30363d; font-weight: 600; font-size: 0.95rem; color: #e6edf3; display: flex; justify-content: space-between; align-items: center; }
//     .endpoint { font-size: 0.75rem; color: #8b949e; font-family: monospace; background: #0d1117; padding: 0.2rem 0.5rem; border-radius: 4px; border: 1px solid #30363d; }
//     .card-body { padding: 1rem; display: flex; flex-direction: column; gap: 0.75rem; flex-grow: 1; }
//     .param-group { display: flex; flex-direction: column; gap: 0.3rem; }
//     .param-group label { font-size: 0.75rem; color: #8b949e; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; }
//     input { background: #0d1117; border: 1px solid #30363d; border-radius: 6px; color: #e6edf3; padding: 0.5rem; font-size: 0.85rem; outline: none; transition: border-color 150ms; }
//     input:focus { border-color: #58a6ff; }
//     .actions { display: flex; justify-content: space-between; align-items: center; margin-top: 0.5rem; }
//     button { background: #21262d; border: 1px solid #30363d; color: #e6edf3; padding: 0.5rem 1.25rem; font-size: 0.85rem; border-radius: 6px; cursor: pointer; transition: 150ms; font-weight: 600;}
//     button:hover { background: #30363d; border-color: #8b949e; }
//     .status { font-size: 0.75rem; color: #8b949e; }
//     .response-box { background: #0d1117; border: 1px solid #30363d; border-radius: 6px; margin-top: auto; overflow: hidden; }
//     .response-header { padding: 0.35rem 0.75rem; background: #21262d; font-size: 0.7rem; color: #8b949e; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #30363d; text-transform: uppercase; font-weight: 600;}
//     .pretty-json { margin: 0; padding: 1rem; font-size: 0.8rem; color: #a5d6ff; max-height: 180px; overflow-y: auto; white-space: pre-wrap; word-wrap: break-word; font-family: ui-monospace, Consolas, monospace; }
//     .btn-clear { background: transparent; border: none; color: #ff7b72; padding: 0; cursor: pointer; font-size: 0.75rem; font-weight: normal; }
//     .btn-clear:hover { text-decoration: underline; background: transparent; border-color: transparent;}
//   `],
//   template: `
//     <div class="card">
//       <div class="card-header">
//         {{ title }} <span class="endpoint">{{ method }} {{ endpoint }}</span>
//       </div>
//       <div class="card-body">
        
//         <div *ngIf="params.length" class="params-container">
//           @for (p of params; track p.key) {
//             <div class="param-group mb-1">
//               <label>{{ p.label }}</label>
//               <input [(ngModel)]="payload[p.key]" [placeholder]="'Enter ' + p.label" [type]="p.type || 'text'" />
//             </div>
//           }
//         </div>

//         <div class="actions">
//           <button (click)="execute()">Execute</button>
//           <span class="status">{{ status() }}</span>
//         </div>

//         <div class="response-box" *ngIf="response()">
//           <div class="response-header">
//             Response <button class="btn-clear" (click)="response.set(null)">Clear</button>
//           </div>
//           <pre class="pretty-json">{{ response() | json }}</pre>
//         </div>
//       </div>
//     </div>
//   `
// })
// export class ActionCardComponent implements OnInit {
//   @Input() title = '';
//   @Input() endpoint = '';
//   @Input() method: 'GET' | 'POST' = 'POST';
//   @Input() params: ActionParam[] = [];

//   payload: any = {};
//   status = signal<string>('');
//   response = signal<any>(null);

//   constructor(private http: HttpClient) {}

//   ngOnInit() {
//     this.params.forEach(p => this.payload[p.key] = '');
//   }

//   execute() {
//     this.status.set('Loading...');
//     this.response.set(null);

//     const req = this.method === 'GET' 
//       ? this.http.get(this.endpoint, { params: this.payload })
//       : this.http.post(this.endpoint, this.payload);

//     req.subscribe({
//       next: (res) => {
//         this.status.set('Success ✅');
//         this.response.set(res);
//       },
//       error: (err) => {
//         this.status.set('Error ❌');
//         this.response.set(err.error || err.message);
//       }
//     });
//   }
// }

// // ─── MAIN APP COMPONENT ─────────────────────────────────────────────────────────
// @Component({
//   selector: 'app-root',
//   standalone: true,
//   imports: [CommonModule, FormsModule, ServerCardComponent, ActionCardComponent],
//   changeDetection: ChangeDetectionStrategy.OnPush,
//   styles: [`
//     * { box-sizing: border-box; margin: 0; padding: 0; }
//     :host {
//       display: block; min-height: 100vh; background: #0d1117; color: #e6edf3;
//       font-family: 'Segoe UI', system-ui, sans-serif; padding: 2.5rem;
//     }
//     h1 { font-size: 1.6rem; font-weight: 700; color: #f0a500; margin-bottom: 0.25rem; }
//     .subtitle { font-size: 0.9rem; color: #8b949e; margin-bottom: 2.5rem; }
//     .section-title { font-size: 1.15rem; font-weight: 600; color: #e6edf3; margin: 2rem 0 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid #21262d; }
//     .grid-2 { display: grid; grid-template-columns: repeat(auto-fit, minmax(400px, 1fr)); gap: 1.5rem; }
//   `],
//   template: `
//     <h1>⚔️ IHL Dota 2 Bot</h1>
//     <p class="subtitle">Admin Dashboard — Control Panel</p>

//     <!-- SERVER CONTROLS -->
//     <h2 class="section-title">Core Servers</h2>
//     <div class="grid-2">
//       <!-- You can map these exactly to your real endpoint URLs -->
//       <app-server-card 
//         title="Discord Bot Server"
//         startEndpoint="/api/discord/start" 
//         stopEndpoint="/api/discord/stop"
//         [needsAuth]="true">
//       </app-server-card>

//       <app-server-card 
//         title="Game Manager Server"
//         startEndpoint="/api/server/start"
//         [needsAuth]="true">
//       </app-server-card>
//     </div>

//     <!-- GAME MANAGER ENDPOINTS -->
//     <h2 class="section-title">Game Manager Tools</h2>
//     <div class="grid-2">
      
//       <app-action-card 
//         title="Register Player"
//         endpoint="/api/gamemanager/register"
//         [params]="[
//           { key: 'discordId', label: 'Discord ID' },
//           { key: 'steamId', label: 'Steam 64 ID' }
//         ]">
//       </app-action-card>

//       <app-action-card 
//         title="View Player Profile"
//         endpoint="/api/gamemanager/profile"
//         method="GET"
//         [params]="[
//           { key: 'discordId', label: 'Discord ID' }
//         ]">
//       </app-action-card>

//       <app-action-card 
//         title="Create Match / Lobby"
//         endpoint="/api/gamemanager/create"
//         [params]="[
//           { key: 'lobbyName', label: 'Lobby Name' },
//           { key: 'gameMode', label: 'Game Mode (e.g. CM, AP)' }
//         ]">
//       </app-action-card>

//       <app-action-card 
//         title="Invite To Match"
//         endpoint="/api/gamemanager/invite"
//         [params]="[
//           { key: 'steamId', label: 'Player Steam ID' },
//           { key: 'lobbyId', label: 'Lobby ID' }
//         ]">
//       </app-action-card>

//     </div>
//   `
// })
// export class App {}