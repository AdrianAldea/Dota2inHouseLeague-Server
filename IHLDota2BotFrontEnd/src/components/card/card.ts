import { Component, ContentChild, DestroyRef, inject, Input, signal, TemplateRef } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { CommonModule } from "@angular/common";
import { HttpClient, HttpErrorResponse } from "@angular/common/http";
import { Observable } from "rxjs";

/** Request body for a card action. Heterogeneous across cards, so keyed by string. */
export type CardPayload = Record<string, unknown>;

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
  @Input() startPayload: CardPayload = {};
  @Input() stopPayload: CardPayload = {};
  @Input() method: 'GET' | 'POST' = 'POST';

  @ContentChild('inputs', { read: TemplateRef })
  inputsTpl?: TemplateRef<unknown>;

  statusMessage = signal('');
  response = signal<unknown>(null);

  private readonly destroyRef = inject(DestroyRef);

  constructor(private http: HttpClient) {}

  send(endpoint: string, payload: CardPayload = {}) {
    this.statusMessage.set('Sending request...');
    this.response.set(null);

    let request$: Observable<unknown>;

    if (this.method == 'GET') {
      request$ = this.http.get(endpoint);
    } else if (this.method == 'POST') {
      // We pass the IDs as strings in the template, and strip the quotes in the raw JSON
      // representation right before sending so they are dispatched as raw JSON 64-bit integers.
      let json = JSON.stringify(payload);
      json = json.replace(/"(\d{15,21})"/g, '$1');

      const headers = { 'Content-Type': 'application/json' };
      request$ = this.http.post(endpoint, json, { headers });
    } else {
      throw Error("Unimplemented Request type");
    }

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.statusMessage.set('Success ✅');
        this.response.set(res);
      },
      error: (err: HttpErrorResponse) => {
        this.statusMessage.set('Error ❌');
        this.response.set(err.error || err.message);
      }
    });
  }
}
