import { Component } from '@angular/core';
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
  discordUsername = '';
  discordPassword = '';

  gameManagerUsername = '';
  gameManagerPassword = '';

  createLobbyName = '';
  createLobbyPassword = '';

  steamId = '';
  userId = '';

  profileLobbyName = '';
  profileLobbyPassword = '';

  playerIdsInput = ''; // for now just a simple string
}