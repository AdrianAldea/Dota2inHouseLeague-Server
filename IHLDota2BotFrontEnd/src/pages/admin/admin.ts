import { Component, signal } from '@angular/core';
import { CardComponent } from '../../components/card/card';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'admin-page',
  standalone: true,
  imports: [CardComponent, FormsModule],
  templateUrl: './admin.html',
  styleUrl: './admin.css'
})
export class AdminPage {
  discordMessage = signal('');

  gameManagerCreateAuthorId = signal('');
  gameManagerCreateLobbyName = signal('');
  gameManagerCreateLobbyPassword = signal('');

  gameManagerRegisterSteamId = signal('');
  gameManagerRegisterUserId = signal('');
  gameManagerRegisterUsername = signal('');

  gameManagerReadyId = signal('');
  gameManagerPlayerId = signal('');

  gameManagerInvite = signal('');

  // Helper passed into [startPayload] for split and trim
  readonly trim = (s: string) => s.trim();
}