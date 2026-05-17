import { Component, ContentChild, Input, signal, TemplateRef } from "@angular/core";
import { CommonModule } from "@angular/common";
import { HttpClient } from "@angular/common/http";

@Component({
  selector: 'card-component',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './card.html',
  styleUrls: ['./card.css']
})
export class CardComponent {

  @Input() title!: string;
  @Input() startEndpoint!: string;
  @Input() stopEndpoint?: string;
  @Input() startLabel: string = "Start";
  @Input() startPayload: any = {};
  @Input() stopPayload: any = {};

  @ContentChild('inputs', { read: TemplateRef })
  inputsTpl?: TemplateRef<any>;

  statusMessage = signal('');
  response = signal<any>(null);

  constructor(private http: HttpClient) {}
    
  send(endpoint: string, payload: any = {}) {
    this.statusMessage.set('Sending request...');
    this.response.set(null);

    this.http.post(endpoint, payload).subscribe({
      next: (res) => {
        this.statusMessage.set('Success ✅');
        this.response.set(res);
      },
      error: (err) => {
        this.statusMessage.set('Error ❌');
        this.response.set(err.error || err.message);
      }
    });
  }
}