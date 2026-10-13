import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'acts-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, RouterLink],
  template: `
    <div class="status-page">
      <h1>Page not found</h1>
      <p>There is no page at this address.</p>
      <a mat-flat-button routerLink="/people">Go to people</a>
    </div>
  `,
  styleUrl: './status-page.scss',
})
export class NotFoundPage {}
